using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Audio.Routing;
using JingleBox2.Config;
using JingleBox2.Models;
using JingleBox2.Tracker;
using JingleBox2.Views;
using System;
using System.Collections.Generic;
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
    private readonly AppConfig _cfg;
    private Stopwatch _recordingTimer = new();
    private System.Timers.Timer? _levelUpdateTimer;
    private readonly IAudioRouting _routing;

    /// <summary>Who to ask whether a recording is spoken for. Null before the library exists.</summary>
    private ISampleUsage? _sampleUsage;

    /// <summary>
    /// Auditions a recording from the list. One at a time on purpose: this is for hearing
    /// what a take is, and two of them at once tells you nothing.
    /// </summary>
    private readonly Waveform.WaveformPlayer _preview = new();

    private Recording? _playing;

    /// <summary>Set while a route is being read back, so showing it does not re-apply it.</summary>
    private bool _readingRoute;

    /// <summary>Set while one is being applied, so reading it back does not start another.</summary>
    private bool _applyingRoute;

    /// <summary>Set while the graph is being read, so ticks do not pile up on each other.</summary>
    private bool _refreshingRoutes;

    /// <summary>Watches the graph while the page is open, so a source that appears is used.</summary>
    private DispatcherTimer? _routeWatch;

    /// <summary>Two seconds is quick enough to feel automatic and slow enough to be cheap.</summary>
    private static readonly TimeSpan RouteWatchInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// What was picked, as opposed to what happens to be wired up. The input is reopened every
    /// time this page comes back, and the system wires the new stream to its own default, so
    /// without this a choice would last until the next tab switch.
    /// </summary>
    private AudioRoute? _preferredRoute;
    private readonly DispatcherTimer _gainSaveTimer;
    private bool _gainLoaded;
    private bool _deviceLoaded;

    public ObservableCollection<string> InputDevices { get; } = new();
    public ObservableCollection<Recording> Recordings { get; } = new();

    [ObservableProperty] private string? selectedDevice;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRecord))]
    private bool isRecording;
    [ObservableProperty] private string recordingTime = "00:00:00";
    [ObservableProperty] private float level;

    /// <summary>True when the input is captured in stereo, so the meter shows two bars.</summary>
    public bool IsStereoInput => _recordingService.Channels >= 2;

    /// <summary>The two sides on their own, for the meter. Mono input reports the same twice.</summary>
    [ObservableProperty] private float levelLeft;
    [ObservableProperty] private float levelRight;
    [ObservableProperty] private WaveformData? currentWaveform;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRecord))]
    private string recordingName = RecordingNameValidator.DefaultBaseName;

    /// <summary>Null when the name is usable, otherwise why it is not.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRecord))]
    [NotifyPropertyChangedFor(nameof(HasNameError))]
    private string? nameError;
    [ObservableProperty] private string status = "Ready";
    [ObservableProperty] private Recording? selectedRecordingForEdit;
    [ObservableProperty] private double recordGainDb;
    [ObservableProperty] private bool isClipping;

    public double MinGainDb => Audio.RecordingService.MinGainDb;
    public double MaxGainDb => Audio.RecordingService.MaxGainDb;

    public bool HasNameError => NameError != null;
    public bool CanRecord => !IsRecording && NameError == null;

    public RecordViewModel(IRecordingService recordingService, ILevelMeterService levelMeter, IWaveformService waveformService, ConfigStore configStore, AppConfig cfg, IAudioRouting routing)
    {
        _routing = routing;

        _cfg = cfg;
        _recordingService = recordingService;
        _levelMeter = levelMeter;
        _waveformService = waveformService;
        _configStore = configStore;

        // Dragging the slider fires on every pixel, so coalesce the writes to config.json.
        _gainSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _gainSaveTimer.Tick += (_, _) =>
        {
            _gainSaveTimer.Stop();
            _cfg.RecordGainDb = _recordingService.GainDb;
            _configStore.Save(_cfg);
        };

        RecordGainDb = cfg.RecordGainDb; // also pushes it into the service via the partial hook
        _recordingService.GainDb = cfg.RecordGainDb; // covers the case where the value was already 0
        _gainLoaded = true;

        RefreshDevices();
        _deviceLoaded = true;

        LoadRecordings();

        // Deleting a recording frees its name again, so the check has to follow the list.
        Recordings.CollectionChanged += (_, _) => ValidateName();

        // Whether it ran out or was stopped, the row it was playing goes back to idle.
        _preview.Stopped += () =>
        {
            if (_playing != null) _playing.IsPlaying = false;
            _playing = null;
        };

        RecordingName = NextRecordingName(RecordingNameValidator.DefaultBaseName);
        ValidateName();
    }

    public IAsyncRelayCommand StartRecordingCommand => new AsyncRelayCommand(StartRecording);
    public IAsyncRelayCommand StopRecordingCommand => new AsyncRelayCommand(StopRecording);
    public IRelayCommand RefreshDevicesCommand => new RelayCommand(RefreshDevices);
    public IRelayCommand<Recording> EditRecordingCommand => new RelayCommand<Recording>(EditRecording);
    public IAsyncRelayCommand<Recording> DeleteRecordingCommand => new AsyncRelayCommand<Recording>(DeleteRecording);

    public IRelayCommand<Recording> PlayRecordingCommand => new RelayCommand<Recording>(PlayRecording);

    public IRelayCommand<Recording> StopRecordingPlaybackCommand => new RelayCommand<Recording>(_ => StopPreview());

    /// <summary>
    /// Raised with the path of a recording whose audio has changed, so anything playing it
    /// from memory can read it again.
    /// </summary>
    public event EventHandler<string>? RecordingChanged;

    /// <summary>
    /// Raised when a recording has moved, with where it was and where it is now, so anything
    /// holding the old path can follow it.
    /// </summary>
    public event EventHandler<(string From, string To)>? RecordingRenamed;

    /// <summary>The name in the edit dialog's box, which is what a rename would call it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RenameError))]
    private string editName = "";

    partial void OnEditNameChanged(string value) => OnPropertyChanged(nameof(CanRename));

    /// <summary>Why that name cannot be used, or null when it can.</summary>
    public string? RenameError
    {
        get
        {
            var recording = SelectedRecordingForEdit;

            if (recording == null) return null;

            string wanted = (EditName ?? "").Trim();

            if (string.Equals(wanted, recording.Name, StringComparison.Ordinal)) return null;

            return RecordingNameValidator.Validate(
                wanted,
                Recordings.Where(r => !ReferenceEquals(r, recording)).Select(r => r.Name));
        }
    }

    public bool CanRename => RenameError == null && SelectedRecordingForEdit != null;

    /// <summary>
    /// Gives the recording another name, which for a recording means another file name.
    /// </summary>
    /// <remarks>
    /// The name shown is read off the file when the list is built, so there is nowhere else to
    /// put it: renaming is moving. Which is why the instruments that play it are repointed in
    /// the same breath, on the shelf and in whatever song is open, rather than being left to
    /// find out at the next note.
    /// </remarks>
    public async Task<bool> RenameAsync(string? newName)
    {
        var recording = SelectedRecordingForEdit;

        if (recording == null) return false;

        string wanted = (newName ?? "").Trim();

        if (string.Equals(wanted, recording.Name, StringComparison.Ordinal)) return true;

        string? problem = RecordingNameValidator.Validate(
            wanted, Recordings.Where(r => !ReferenceEquals(r, recording)).Select(r => r.Name));

        if (problem != null)
        {
            Status = problem;
            return false;
        }

        string from = recording.FilePath;
        string? folder = Path.GetDirectoryName(from);

        if (folder == null) return false;

        string to = Path.Combine(folder, wanted + Path.GetExtension(from));

        if (File.Exists(to))
        {
            Status = "There is already a file by that name.";
            return false;
        }

        try
        {
            // A file being played is a file that is open, which on Windows is a file that will
            // not move.
            if (ReferenceEquals(_playing, recording)) StopPreview();

            await Task.Run(() => File.Move(from, to));

            recording.FilePath = to;
            recording.Name = wanted;

            int moved = _sampleUsage?.Repoint(from, to) ?? 0;

            RecordingRenamed?.Invoke(this, (from, to));

            Status = moved == 0
                ? $"Renamed to '{wanted}'"
                : $"Renamed to '{wanted}', and {moved} instrument{(moved == 1 ? "" : "s")} followed it";

            return true;
        }
        catch (Exception ex)
        {
            Status = $"Rename failed: {ex.Message}";
            return false;
        }
    }

    private void RefreshDevices()
    {
        string? previous = SelectedDevice ?? _cfg.RecordInputDevice;

        InputDevices.Clear();
        foreach (var device in _recordingService.GetInputDevices())
            InputDevices.Add(device);

        // Keep the current pick if it is still plugged in, otherwise fall back to the first one.
        SelectedDevice = InputDeviceSelector.Pick(InputDevices, previous);
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

    partial void OnRecordingNameChanged(string value) => ValidateName();

    private void ValidateName() =>
        NameError = RecordingNameValidator.Validate(RecordingName, Recordings.Select(r => r.Name));

    /// <summary>Next free name in the same series as <paramref name="basedOn"/>.</summary>
    private string NextRecordingName(string basedOn) =>
        RecordingNameValidator.NextName(basedOn, Recordings.Select(r => r.Name));

    partial void OnRecordGainDbChanged(double value)
    {
        _recordingService.GainDb = value;

        if (!_gainLoaded) return; // do not rewrite the file just for loading it

        _gainSaveTimer.Stop();
        _gainSaveTimer.Start();
    }

    partial void OnSelectedDeviceChanged(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;

        _recordingService.SelectedDevice = value;

        if (!_deviceLoaded) return; // do not rewrite the file just for loading it
        if (_cfg.RecordInputDevice == value) return;

        _cfg.RecordInputDevice = value;
        _configStore.Save(_cfg);
    }

    private void EditRecording(Recording? recording)
    {
        if (recording == null) return;

        // The dialog has a player of its own, and the list's would go on underneath it.
        StopPreview();

        try
        {
            SelectedRecordingForEdit = recording;
            EditName = recording.Name;

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
    /// <summary>Returns true when the file was rewritten, so callers can reset their view.</summary>
    public async Task<bool> ApplyTrimAsync(double startFraction, double endFraction)
    {
        var recording = SelectedRecordingForEdit;
        var waveform = CurrentWaveform;
        if (recording == null || waveform == null) return false;

        try
        {
            long totalFrames = waveform.TotalSamples;
            long startFrame = (long)(Math.Clamp(startFraction, 0, 1) * totalFrames);
            long endFrame = (long)(Math.Clamp(endFraction, 0, 1) * totalFrames);

            Status = "Trimming...";
            await Task.Run(() => _waveformService.TrimFile(recording.FilePath, startFrame, endFrame));

            CurrentWaveform = await Task.Run(() => _waveformService.AnalyzeFile(recording.FilePath));
            recording.DurationMs = ReadDurationMs(recording.FilePath);

            // An instrument built on this file is holding the old audio in memory, so say so.
            RecordingChanged?.Invoke(this, recording.FilePath);

            Status = $"Trimmed '{recording.Name}' to {TimeSpan.FromMilliseconds(recording.DurationMs):mm\\:ss\\.fff}";
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Trim failed: {ex.Message}";
            return false;
        }
    }

    /// <summary>Where a normalize puts the loudest moment, in dBFS.</summary>
    [ObservableProperty] private double normalizeTargetDb = Normalization.DefaultTargetDecibels;

    public double MinNormalizeDb => Normalization.MinTargetDecibels;
    public double MaxNormalizeDb => Normalization.MaxTargetDecibels;

    /// <summary>
    /// Lifts the whole recording so its loudest moment sits on the target. The trim region is
    /// not involved: this is about the level of the file, not about part of it.
    /// </summary>
    /// <summary>Returns true when the file was rewritten, so callers can redraw.</summary>
    public async Task<bool> NormalizeAsync()
    {
        var recording = SelectedRecordingForEdit;
        if (recording == null) return false;

        try
        {
            Status = "Normalizing...";

            double target = NormalizeTargetDb;
            double moved = await Task.Run(() => _waveformService.NormalizeFile(recording.FilePath, target));

            if (Math.Abs(moved) < 0.001)
            {
                Status = $"'{recording.Name}' is already at {target:0.0} dB";
                return false;
            }

            CurrentWaveform = await Task.Run(() => _waveformService.AnalyzeFile(recording.FilePath));

            // The audio has changed under any instrument built on it.
            RecordingChanged?.Invoke(this, recording.FilePath);

            Status = $"Normalized '{recording.Name}' by {moved:+0.0;-0.0} dB";
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Normalize failed: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// The instrument library, set once it has been built. Recordings are its raw material,
    /// so the page has to be able to ask what is still in use before it removes anything.
    /// </summary>
    public ISampleUsage? SampleUsage
    {
        get => _sampleUsage;
        set
        {
            _sampleUsage = value;
            RefreshUsage();
        }
    }

    /// <summary>
    /// Marks each recording with the instruments that play it. Called whenever the library
    /// changes, so a recording becomes free again the moment its last instrument goes.
    /// </summary>
    public void RefreshUsage()
    {
        foreach (var recording in Recordings)
            recording.UsedBy = Tracker.SampleUsage.Describe(UsersOf(recording));
    }

    /// <summary>The instruments playing a recording, right now rather than as last stamped.</summary>
    private IReadOnlyList<string> UsersOf(Recording recording)
    {
        if (_sampleUsage == null) return Array.Empty<string>();

        try
        {
            return _sampleUsage.InstrumentsUsing(recording.FilePath);
        }
        catch (Exception)
        {
            // An unreadable library is no reason to start deleting things, so this reads as
            // "nothing known" and the delete still asks before it acts.
            return Array.Empty<string>();
        }
    }

    /// <summary>Plays a recording whole, from the list, so a take can be heard without opening it.</summary>
    private void PlayRecording(Recording? recording)
    {
        if (recording == null) return;

        StopPreview();

        long frames;
        try
        {
            frames = _waveformService.GetFrameCount(recording.FilePath);
        }
        catch (Exception)
        {
            frames = 0;
        }

        if (frames <= 0)
        {
            Status = $"'{recording.Name}' could not be read.";
            return;
        }

        _preview.Play(recording.FilePath, 0, 1, frames);

        // The stream can refuse to open, and a row that says it is playing when nothing is
        // would leave its stop button as the only way out.
        if (!_preview.IsPlaying)
        {
            Status = $"'{recording.Name}' could not be played.";
            return;
        }

        _playing = recording;
        recording.IsPlaying = true;

        Status = $"Playing '{recording.Name}'";
    }

    /// <summary>Silence, whichever recording it was. Safe to call when nothing is playing.</summary>
    public void StopPreview() => _preview.Stop();

    private async Task DeleteRecording(Recording? recording)
    {
        if (recording == null) return;

        // Asked again here rather than trusting the stamp: the library may have gained an
        // instrument since the list was last marked up.
        var used = UsersOf(recording);
        recording.UsedBy = Tracker.SampleUsage.Describe(used);

        if (used.Count > 0)
        {
            Status = $"'{recording.Name}' is the sound of {recording.UsedBy} and was not deleted";

            await ConfirmDialog.NoteAsync(
                "Recording in use",
                $"'{recording.Name}' is the sound of {recording.UsedBy}.\n\n"
                + "A sample instrument plays the file itself, so deleting this recording would "
                + "silence it in every song that uses it. Delete the instrument first, or point "
                + "it at another recording.");

            return;
        }

        bool confirmed = await ConfirmDialog.AskAsync(
            "Delete recording",
            $"Delete '{recording.Name}' permanently? This cannot be undone.",
            "Delete");

        if (!confirmed) return;

        try
        {
            // A file that is being played is a file that is open, which on Windows is a file
            // that will not delete.
            if (ReferenceEquals(_playing, recording)) StopPreview();

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

    /// <summary>What the input can be taken from, where the system lets that be chosen.</summary>
    public ObservableCollection<AudioRoute> Routes { get; } = new();

    [ObservableProperty] private AudioRoute? selectedRoute;

    /// <summary>False on a system with no graph to patch, and the picker stays hidden.</summary>
    public bool IsRoutingAvailable => _routing.IsAvailable;

    public IRelayCommand RefreshRoutesCommand => new RelayCommand(RefreshRoutes);

    /// <summary>
    /// Reads the graph and shows what is feeding the recorder. The tools take a moment, so
    /// this happens off the UI thread.
    /// </summary>
    private async void RefreshRoutes()
    {
        if (!_routing.IsAvailable || _refreshingRoutes) return;

        try
        {
            _refreshingRoutes = true;

            var routes = await Task.Run(() => _routing.GetRoutes());
            var current = await Task.Run(() => _routing.GetCurrentRoute());

            _readingRoute = true;

            Merge(routes);

            // Match by node: the list is read afresh, so the object from before is not in it.
            var showing = current == null ? null : Routes.FirstOrDefault(r => r.Node == current.Node);
            if (!ReferenceEquals(showing, SelectedRoute)) SelectedRoute = showing;

            _readingRoute = false;
            RestorePreferred(current);
        }
        catch (Exception ex)
        {
            Status = $"Could not read the audio routing: {ex.Message}";
        }
        finally
        {
            _readingRoute = false;
            _refreshingRoutes = false;
        }
    }

    /// <summary>
    /// Keeps an eye on the graph while the page is up. A program appears in it only while it
    /// is playing, so a source picked before it started, or restarted since, would otherwise
    /// sit there unconnected until someone pressed Refresh.
    /// </summary>
    private void StartRouteWatch()
    {
        if (!_routing.IsAvailable || _routeWatch != null) return;

        _routeWatch = new DispatcherTimer { Interval = RouteWatchInterval };
        _routeWatch.Tick += (_, _) => RefreshRoutes();
        _routeWatch.Start();
    }

    private void StopRouteWatch()
    {
        _routeWatch?.Stop();
        _routeWatch = null;
    }

    /// <summary>
    /// Brings the list up to date without rebuilding it. Clearing and refilling would drop the
    /// selection and shut a dropdown that is open at the time, which is exactly when this runs.
    /// </summary>
    private void Merge(IReadOnlyList<AudioRoute> routes)
    {
        for (int i = Routes.Count - 1; i >= 0; i--)
        {
            if (!routes.Any(r => r.Node == Routes[i].Node)) Routes.RemoveAt(i);
        }

        for (int i = 0; i < routes.Count; i++)
        {
            var route = routes[i];
            int existing = IndexOfRoute(route.Node);

            if (existing < 0) Routes.Insert(Math.Min(i, Routes.Count), route);
            else if (Routes[existing] != route) Routes[existing] = route;
        }
    }

    private int IndexOfRoute(string node)
    {
        for (int i = 0; i < Routes.Count; i++)
        {
            if (Routes[i].Node == node) return i;
        }

        return -1;
    }

    partial void OnSelectedRouteChanged(AudioRoute? value)
    {
        if (_readingRoute || value == null) return;

        // Picked, rather than merely being shown: this is the one to put back later.
        _preferredRoute = value;
        ApplyRoute(value, announce: true);
    }

    /// <summary>
    /// Puts the chosen source back after the input has been reopened. Silent when the choice is
    /// already in place, and gives up when whatever was chosen has since stopped playing.
    /// </summary>
    private void RestorePreferred(AudioRoute? current)
    {
        if (_applyingRoute || _preferredRoute == null) return;
        if (current != null && current.Node == _preferredRoute.Node) return;

        var still = Routes.FirstOrDefault(r => r.Node == _preferredRoute.Node);
        if (still == null) return;

        // A retry, not a request: it says nothing unless it works, since the source coming and
        // going is normal and there is nothing for anyone to do about it.
        ApplyRoute(still, announce: false);
    }

    /// <summary>
    /// Rewires the input. Off the UI thread: connecting runs a handful of command line tools,
    /// and half a second of frozen window is not something a dropdown should cost.
    /// </summary>
    private async void ApplyRoute(AudioRoute route, bool announce)
    {
        if (_applyingRoute) return;

        try
        {
            _applyingRoute = true;
            if (announce) Status = $"Taking audio from {route.Name}...";

            // Applying it replaces whatever the system wired up, which is the whole point.
            bool connected = await Task.Run(() => _routing.Connect(route));

            // Show what was applied, without that showing counting as a new choice.
            _readingRoute = true;
            var showing = Routes.FirstOrDefault(r => r.Node == route.Node);
            if (connected && showing != null) SelectedRoute = showing;
            _readingRoute = false;

            if (connected) Status = $"Recording from {route.Display}";
            else if (announce) Status = $"{route.Name} is not giving anything to record yet. It will be picked up as soon as it does.";
        }
        catch (Exception ex)
        {
            Status = $"Could not change the input: {ex.Message}";
        }
        finally
        {
            _applyingRoute = false;
        }
    }

    /// <summary>
    /// Watches the input's level without keeping any of it, so the meter is live while a gain
    /// is being set. Called when the RECORD page comes up.
    /// </summary>
    public void StartInputMonitoring()
    {
        try
        {
            _recordingService.StartMonitoring();
            StartLevelPolling();

            // The recorder only appears in the graph once it is listening, so the routes are
            // read after the input is open, not before.
            RefreshRoutes();
            StartRouteWatch();
        }
        catch (Exception ex)
        {
            Status = $"Could not open the input: {ex.Message}";
        }
    }

    /// <summary>Stops watching, unless a take is running, which keeps the input open anyway.</summary>
    public void StopInputMonitoring()
    {
        _recordingService.StopMonitoring();

        if (_recordingService.IsRecording) return;

        StopRouteWatch();
        StopLevelPolling();

        Level = 0;
        LevelLeft = 0;
        LevelRight = 0;
        IsClipping = false;
    }

    /// <summary>
    /// One poll for both jobs. It runs while the input is open, for a take or for the meter,
    /// and reads the last moment of audio rather than being pushed at from the audio thread.
    /// </summary>
    private void StartLevelPolling()
    {
        if (_levelUpdateTimer != null) return;

        _levelUpdateTimer = new System.Timers.Timer(50);
        _levelUpdateTimer.Elapsed += (_, _) =>
        {
            var recentData = _recordingService.GetRecentRecordingData(4410);
            var stereo = _levelMeter.GetStereoFromBytes(recentData, _recordingService.Channels);

            bool clipping = _recordingService.IsClipping;
            bool recording = _recordingService.IsRecording;

            Dispatcher.UIThread.Invoke(() =>
            {
                Level = stereo.Peak;
                LevelLeft = stereo.Left;
                LevelRight = stereo.Right;
                IsClipping = clipping;

                if (recording) RecordingTime = _recordingTimer.Elapsed.ToString(@"hh\:mm\:ss");
            });
        };

        _levelUpdateTimer.Start();
    }

    private void StopLevelPolling()
    {
        _levelUpdateTimer?.Stop();
        _levelUpdateTimer?.Dispose();
        _levelUpdateTimer = null;
    }

    private async Task StartRecording()
    {
        ValidateName();
        if (NameError != null)
        {
            Status = NameError;
            return;
        }

        // Auditioning an old take while capturing a new one would put the first one into the
        // second, on any source that carries what the machine is playing.
        StopPreview();

        try
        {
            _recordingService.StartRecording();
            IsRecording = true;
            Status = _recordingService.LastStartWarning ?? "Recording...";

            _recordingTimer.Restart();
            StartLevelPolling();
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
            _recordingTimer.Stop();
            _recordingService.StopRecording();

            // The meter keeps reading if the page is still watching the input; if it is not,
            // the poll goes with the take.
            if (!_recordingService.IsMonitoring) StopLevelPolling();

            IsRecording = false;
            IsClipping = false;

            bool clipped = _recordingService.ClippedDuringTake;

            // The name check trims, so save under the trimmed name too or the two disagree.
            string savedName = RecordingName.Trim();
            string filePath = await _recordingService.SaveRecordingAsync(savedName);
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
                Name = savedName,
                FilePath = filePath,
                DurationMs = ReadDurationMs(filePath),
                CreatedAt = DateTime.Now
            };

            Recordings.Add(recording);

            if (clipped)
                Status = "Saved, but the input clipped. Lower the input gain or the source level.";

            Level = 0;
            LevelLeft = 0;
            LevelRight = 0;
            RecordingTime = "00:00:00";
            RecordingName = NextRecordingName(savedName);
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
    }
}
