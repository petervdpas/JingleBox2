using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Config;
using JingleBox2.Models;
using JingleBox2.Views;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JingleBox2.ViewModels;

public sealed partial class RecordViewModel : ObservableObject
{
    private readonly IRecordingService _recordingService;
    private readonly ILevelMeterService _levelMeter;
    private readonly IWaveformService _waveformService;
    private readonly ConfigStore _configStore;
    private Stopwatch _recordingTimer = new();
    private System.Timers.Timer? _levelUpdateTimer;

    public ObservableCollection<string> InputDevices { get; } = new();
    public ObservableCollection<Recording> Recordings { get; } = new();

    [ObservableProperty] private string? selectedDevice;
    [ObservableProperty] private bool isRecording;
    [ObservableProperty] private string recordingTime = "00:00:00";
    [ObservableProperty] private float level;
    [ObservableProperty] private WaveformData? currentWaveform;
    [ObservableProperty] private string recordingName = "Recording";
    [ObservableProperty] private string status = "Ready";
    [ObservableProperty] private Recording? selectedRecordingForEdit;

    public RecordViewModel(IRecordingService recordingService, ILevelMeterService levelMeter, IWaveformService waveformService, ConfigStore configStore)
    {
        _recordingService = recordingService;
        _levelMeter = levelMeter;
        _waveformService = waveformService;
        _configStore = configStore;

        RefreshDevices();
        LoadRecordings();
    }

    public IAsyncRelayCommand StartRecordingCommand => new AsyncRelayCommand(StartRecording);
    public IAsyncRelayCommand StopRecordingCommand => new AsyncRelayCommand(StopRecording);
    public IRelayCommand RefreshDevicesCommand => new RelayCommand(RefreshDevices);
    public IRelayCommand<Recording> EditRecordingCommand => new RelayCommand<Recording>(EditRecording);
    public IAsyncRelayCommand<Recording> DeleteRecordingCommand => new AsyncRelayCommand<Recording>(DeleteRecording);

    private void RefreshDevices()
    {
        InputDevices.Clear();
        foreach (var device in _recordingService.GetInputDevices())
            InputDevices.Add(device);

        SelectedDevice = InputDevices.FirstOrDefault() ?? "Default";
    }

    private void LoadRecordings()
    {
        try
        {
            string recordingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JingleBox2", "recordings");
            if (!Directory.Exists(recordingsDir))
                return;

            Recordings.Clear();
            foreach (var file in Directory.GetFiles(recordingsDir, "*.wav"))
            {
                var info = new FileInfo(file);
                var recording = new Recording
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = Path.GetFileNameWithoutExtension(file),
                    FilePath = file,
                    DurationMs = ReadDurationMs(file),
                    CreatedAt = info.CreationTime
                };
                Recordings.Add(recording);
            }
        }
        catch (Exception ex)
        {
            Status = $"Failed to load recordings: {ex.Message}";
        }
    }

    partial void OnSelectedDeviceChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
            _recordingService.SelectedDevice = value;
    }

    private void EditRecording(Recording? recording)
    {
        if (recording == null) return;

        try
        {
            SelectedRecordingForEdit = recording;
            var waveform = _waveformService.AnalyzeFile(recording.FilePath);
            CurrentWaveform = waveform;

            // Open edit dialog
            var dialog = new RecordingEditDialog
            {
                DataContext = this
            };

            if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is not null)
            {
                _ = dialog.ShowDialog(desktop.MainWindow);
            }
        }
        catch (Exception ex)
        {
            Status = $"Failed to load recording: {ex.Message}";
        }
    }

    /// <summary>Duration in ms, or 0 for a file we cannot read.</summary>
    private long ReadDurationMs(string filePath)
    {
        try { return (long)_waveformService.GetDuration(filePath).TotalMilliseconds; }
        catch { return 0; }
    }

    /// <summary>
    /// Cuts the recording down to the selected region. Start and end are fractions of the
    /// whole file, matching the trim handles in the editor.
    /// </summary>
    public async Task ApplyTrimAsync(double startFraction, double endFraction)
    {
        var recording = SelectedRecordingForEdit;
        var waveform = CurrentWaveform;
        if (recording == null || waveform == null) return;

        try
        {
            long totalFrames = waveform.TotalSamples;
            long startFrame = (long)(Math.Clamp(startFraction, 0, 1) * totalFrames);
            long endFrame = (long)(Math.Clamp(endFraction, 0, 1) * totalFrames);

            Status = "Trimming...";
            await Task.Run(() => _waveformService.TrimFile(recording.FilePath, startFrame, endFrame));

            CurrentWaveform = await Task.Run(() => _waveformService.AnalyzeFile(recording.FilePath));
            recording.DurationMs = ReadDurationMs(recording.FilePath);

            Status = $"Trimmed '{recording.Name}' to {TimeSpan.FromMilliseconds(recording.DurationMs):mm\\:ss\\.fff}";
        }
        catch (Exception ex)
        {
            Status = $"Trim failed: {ex.Message}";
        }
    }

    private async Task DeleteRecording(Recording? recording)
    {
        if (recording == null) return;

        if (App.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
            return;

        bool confirmed = await ConfirmDialog.ShowAsync(
            desktop.MainWindow,
            "Delete recording",
            $"Delete '{recording.Name}' permanently? This cannot be undone.",
            "Delete");

        if (!confirmed) return;

        try
        {
            if (File.Exists(recording.FilePath))
                File.Delete(recording.FilePath);

            Recordings.Remove(recording);

            if (ReferenceEquals(SelectedRecordingForEdit, recording))
            {
                SelectedRecordingForEdit = null;
                CurrentWaveform = null;
            }

            Status = $"Deleted '{recording.Name}'";
        }
        catch (Exception ex)
        {
            Status = $"Delete failed: {ex.Message}";
        }
    }

    private async Task StartRecording()
    {
        try
        {
            _recordingService.StartRecording();
            IsRecording = true;
            Status = _recordingService.LastStartWarning ?? "Recording...";

            _recordingTimer.Restart();
            _levelUpdateTimer = new System.Timers.Timer(50);
            _levelUpdateTimer.Elapsed += (s, e) =>
            {
                var recentData = _recordingService.GetRecentRecordingData(4410);
                float level = _levelMeter.GetLevelFromBytes(recentData);

                Dispatcher.UIThread.Invoke(() =>
                {
                    Level = level;
                    RecordingTime = _recordingTimer.Elapsed.ToString(@"hh\:mm\:ss");
                });
            };
            _levelUpdateTimer.Start();
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task StopRecording()
    {
        try
        {
            _levelUpdateTimer?.Stop();
            _levelUpdateTimer?.Dispose();
            _recordingTimer.Stop();

            _recordingService.StopRecording();
            IsRecording = false;

            string filePath = await _recordingService.SaveRecordingAsync(RecordingName);
            Status = "Saved recording";

            try
            {
                Status = "Processing waveform...";
                var waveform = await Task.Run(() => _waveformService.AnalyzeFile(filePath));
                CurrentWaveform = waveform;
                Status = "Ready";
            }
            catch (Exception wfEx)
            {
                Status = $"Waveform analysis failed: {wfEx.Message}";
            }

            var recording = new Recording
            {
                Id = Guid.NewGuid().ToString(),
                Name = RecordingName,
                FilePath = filePath,
                DurationMs = ReadDurationMs(filePath),
                CreatedAt = DateTime.Now
            };

            Recordings.Add(recording);
            Level = 0;
            RecordingTime = "00:00:00";
            RecordingName = "Recording";
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
    }
}
