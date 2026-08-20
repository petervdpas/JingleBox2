using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Config;
using JingleBox2.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace JingleBox2.ViewModels;

public sealed partial class RecordViewModel : ObservableObject
{
    private readonly IRecordingService _recordingService;
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

    public RecordViewModel(IRecordingService recordingService, IWaveformService waveformService, ConfigStore configStore)
    {
        _recordingService = recordingService;
        _waveformService = waveformService;
        _configStore = configStore;

        RefreshDevices();
    }

    public IAsyncRelayCommand StartRecordingCommand => new AsyncRelayCommand(StartRecording);
    public IAsyncRelayCommand StopRecordingCommand => new AsyncRelayCommand(StopRecording);
    public IRelayCommand RefreshDevicesCommand => new RelayCommand(RefreshDevices);

    private void RefreshDevices()
    {
        InputDevices.Clear();
        foreach (var device in _recordingService.GetInputDevices())
            InputDevices.Add(device);

        SelectedDevice = InputDevices.FirstOrDefault() ?? "Default";
    }

    partial void OnSelectedDeviceChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
            _recordingService.SelectedDevice = value;
    }

    private async Task StartRecording()
    {
        try
        {
            _recordingService.StartRecording();
            IsRecording = true;
            Status = "Recording...";

            _recordingTimer.Restart();
            _levelUpdateTimer = new System.Timers.Timer(100);
            _levelUpdateTimer.Elapsed += (s, e) =>
            {
                Level = _recordingService.GetLevel();
                RecordingTime = _recordingTimer.Elapsed.ToString(@"hh\:mm\:ss");
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
                CurrentWaveform = _waveformService.AnalyzeFile(filePath);
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
                DurationMs = (long)_recordingTimer.Elapsed.TotalMilliseconds,
                CreatedAt = DateTime.Now
            };

            Recordings.Add(recording);
            Status = "Ready";
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
