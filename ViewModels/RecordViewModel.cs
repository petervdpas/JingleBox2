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

/// <summary>
/// RECORD: what the machine is listening to, what it has taken down, and the shelf of takes.
/// </summary>
/// <remarks>
/// The shelf is the app's own, in the application folder, and it is where every other page gets
/// its audio from: a pad plays a take off it, and so does a sampler, a kit and a map. That is
/// the whole reason this page owns the files rather than pointing at wherever somebody happened
/// to leave a wav.
///
/// Deleting a take does not delete it. It moves into <c>deleted/</c> beside the recordings, so
/// undo on this page fetches the last one back and the confirmation stopped having to say "this
/// cannot be undone". A move rather than a copy, because a take is the one thing here that can
/// be a hundred megabytes and paying for the undo up front would be paying whether or not
/// anybody wanted it. Only this session's deletions are offered back: putting back a take from
/// last week is a filing cabinet, not undo.
/// </remarks>
public sealed partial class RecordViewModel : ObservableObject, ITransportDeck, Shortcuts.IShortcutContext
{
    /// <summary>What actually opens the input and writes the file.</summary>
    private readonly IRecordingService _recordingService;

    /// <summary>Where the meter's numbers come from while nothing is being recorded.</summary>
    private readonly ILevelMeterService _levelMeter;

    /// <summary>Reduces a finished take to peaks, for the picture under the list.</summary>
    private readonly IWaveformService _waveformService;

    /// <summary>Where the input device and the gain are written down, which is the settings file.</summary>
    private readonly ConfigStore _configStore;

    /// <summary>The settings themselves, held so a change can be written without reading first.</summary>
    private readonly AppConfig _cfg;

    /// <summary>How long the take has been running, for the clock in the bar.</summary>
    /// <remarks>
    /// A stopwatch rather than a count of buffers, because what this shows is how long somebody
    /// has been talking and not how much audio has been written; the two differ when the input
    /// drops out, and the honest answer for a person watching a clock is wall time.
    /// </remarks>
    private Stopwatch _recordingTimer = new();

    /// <summary>Reads the meter while the page is up, and only while it is.</summary>
    private System.Timers.Timer? _levelUpdateTimer;

    /// <summary>What the system has wired to the input, which is not what somebody chose.</summary>
    private readonly IAudioRouting _routing;

    /// <summary>Who to ask whether a recording is spoken for. Null before the rack exists.</summary>
    private ISampleUsage? _sampleUsage;

    /// <summary>
    /// Auditions a recording from the list. One at a time on purpose: this is for hearing
    /// what a take is, and two of them at once tells you nothing.
    /// </summary>
    private readonly Waveform.WaveformPlayer _preview = new();

    /// <summary>The take the preview is on, so its row can be put back to idle when it stops.</summary>
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
    /// How often the input's level is read, in milliseconds.
    /// </summary>
    /// <remarks>
    /// Twenty a second: faster than an eye can follow a meter, and slow enough that reading the
    /// last moment of audio costs nothing worth counting.
    /// </remarks>
    private const int LevelPollMs = 50;

    /// <summary>
    /// How long the gain sits still before it is written down.
    /// </summary>
    /// <remarks>
    /// Half a second is longer than the pause inside a drag and shorter than the pause before
    /// somebody closes the program, which is the only thing this has to get right.
    /// </remarks>
    private static readonly TimeSpan GainSaveDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// What was picked, as opposed to what happens to be wired up. The input is reopened every
    /// time this page comes back, and the system wires the new stream to its own default, so
    /// without this a choice would last until the next tab switch.
    /// </summary>
    private AudioRoute? _preferredRoute;

    /// <summary>
    /// Holds the gain back from the settings file while it is being dragged.
    /// </summary>
    /// <remarks>
    /// The slider fires on every pixel, and each of those would otherwise be a write of the
    /// whole settings file. The value reaches the recorder at once either way: only the writing
    /// down waits.
    /// </remarks>
    private readonly DispatcherTimer _gainSaveTimer;

    /// <summary>
    /// False until the stored gain has been put on the slider.
    /// </summary>
    /// <remarks>
    /// Setting the slider raises a change like any other, and answering that one would write the
    /// settings file back with the value it was just read from, on every start.
    /// </remarks>
    private bool _gainLoaded;

    /// <summary>The same guard for the input device, and for the same reason.</summary>
    private bool _deviceLoaded;

    /// <summary>Every input the machine offers, in the order the system lists them.</summary>
    public ObservableCollection<string> InputDevices { get; } = new();

    /// <summary>Everything on the shelf, whatever the list is showing at the moment.</summary>
    public ObservableCollection<Recording> Recordings { get; } = new();

    /// <summary>What a take is filed under, written down beside the takes.</summary>
    private readonly IRecordingCategories _filing = new RecordingCategories();

    /// <summary>
    /// The shelf as this page shows it, narrowed to a category or not.
    /// </summary>
    /// <remarks>
    /// The same kind of filter the machines put in front of their take pickers, so a category
    /// made here is one they can hunt by. Everything else that asks this page for the
    /// recordings still gets all of them: the name check, the count in the bar along the
    /// bottom. Hiding a take from the list is not taking it off the shelf.
    /// </remarks>
    public TakeFilter Shelf { get; }

    /// <summary>What is in the category box, which is not yet what the take is filed under.</summary>
    /// <remarks>
    /// Typed a letter at a time, and a category is made by typing one, so committing on every
    /// keystroke would leave "S", "Sp" and "Spe" behind on the way to "Speaking". The box says
    /// so when the field is left or Enter is pressed, and nothing before that.
    /// </remarks>
    [ObservableProperty] private string takeCategory = "";

    /// <summary>Which input is being listened to, by the name the system gives it.</summary>
    /// <remarks>
    /// A name rather than a number, because a device's number moves when something else is
    /// plugged in and a name is what somebody recognises when they come back tomorrow.
    /// </remarks>
    [ObservableProperty] private string? selectedDevice;

    /// <summary>Where you are, for the bar along the bottom: what it is listening to, and how many takes.</summary>
    public string Context
    {
        get
        {
            string input = string.IsNullOrWhiteSpace(SelectedDevice) ? "no input" : SelectedDevice!;
            int held = Recordings.Count;

            return input + "  ·  " + held + (held == 1 ? " recording" : " recordings") +
                   (IsRecording ? "  ·  recording" : "");
        }
    }
    /// <summary>Whether a take is being written right now.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRecord))]
    private bool isRecording;

    /// <summary>How long it has been running, written out for the clock.</summary>
    [ObservableProperty] private string recordingTime = "00:00:00";

    /// <summary>The loudest of the two sides, for a meter with one bar.</summary>
    [ObservableProperty] private float level;

    /// <summary>True when the input is captured in stereo, so the meter shows two bars.</summary>
    public bool IsStereoInput => _recordingService.Channels >= 2;

    /// <summary>The two sides on their own, for the meter. Mono input reports the same twice.</summary>
    [ObservableProperty] private float levelLeft;

    /// <summary>The right side, which reads the same as the left on a mono input.</summary>
    [ObservableProperty] private float levelRight;

    /// <summary>The picture of the take that is picked, or null while there is none to show.</summary>
    [ObservableProperty] private WaveformData? currentWaveform;

    /// <summary>What the next take will be called.</summary>
    /// <remarks>
    /// Filled in with the next unused name so that pressing record twice does not stop to ask
    /// anything, and checked as it is typed, since a name that cannot be a file name has to be
    /// refused before the recording rather than after it.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRecord))]
    private string recordingName = RecordingNameValidator.DefaultBaseName;

    /// <summary>Null when the name is usable, otherwise why it is not.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRecord))]
    [NotifyPropertyChangedFor(nameof(HasNameError))]
    private string? nameError;

    /// <summary>What the page has to say for itself, in the bar under the buttons.</summary>
    [ObservableProperty] private string status = "Ready";

    /// <summary>
    /// The take the page is pointed at: the one whose picture is up and whose buttons are the
    /// ones under it.
    /// </summary>
    /// <remarks>
    /// One take at a time, chosen by clicking its row. The buttons used to sit on every row,
    /// four to a line, which made the list a wall of controls to read past when all anybody
    /// wanted was to find a take by name.
    /// </remarks>
    [ObservableProperty] private Recording? selectedRecording;

    /// <summary>The take whose name is being typed over, or null when none is being renamed.</summary>
    [ObservableProperty] private Recording? selectedRecordingForEdit;

    /// <summary>How much the input is turned up, in decibels, before anything is written.</summary>
    /// <remarks>
    /// Applied to the incoming audio rather than to the file afterwards, which is the point: a
    /// take recorded too quietly cannot be repaired later without bringing the noise up with it.
    /// </remarks>
    [ObservableProperty] private double recordGainDb;

    /// <summary>True when the input has hit the ceiling, so the meter can say so in red.</summary>
    [ObservableProperty] private bool isClipping;

    /// <summary>The two ends of the gain slider, taken from the recorder so they cannot drift.</summary>
    public double MinGainDb => Audio.RecordingService.MinGainDb;

    /// <inheritdoc cref="MinGainDb"/>
    public double MaxGainDb => Audio.RecordingService.MaxGainDb;

    /// <summary>Whether there is a reason to show, which is what puts the message on the page.</summary>
    public bool HasNameError => NameError != null;

    /// <summary>Whether the record button does anything: not already running, and a usable name.</summary>
    public bool CanRecord => !IsRecording && NameError == null;

    /// <summary>
    /// Builds the page, reads the shelf, and puts the stored gain and input back where they were.
    /// </summary>
    /// <remarks>
    /// The order matters in two places. The filter is built after the takes are read, so it
    /// starts stocked rather than filling itself a moment later and flickering. And the gain is
    /// pushed into the recorder as well as onto the slider, since a stored value that happens to
    /// equal the slider's own starting value changes nothing and would never reach the recorder
    /// at all.
    ///
    /// The name check follows the shelf rather than being run once, because deleting a recording
    /// frees its name again and a name refused as taken would go on being refused.
    ///
    /// The preview's row goes back to idle when it stops, whether it ran out on its own or
    /// somebody stopped it, since those are the same thing to whoever is looking at the list.
    /// </remarks>
    public RecordViewModel(IRecordingService recordingService, ILevelMeterService levelMeter, IWaveformService waveformService, ConfigStore configStore, AppConfig cfg, IAudioRouting routing)
    {
        _routing = routing;

        _cfg = cfg;
        _recordingService = recordingService;
        _levelMeter = levelMeter;
        _waveformService = waveformService;
        _configStore = configStore;

        _gainSaveTimer = new DispatcherTimer { Interval = GainSaveDelay };
        _gainSaveTimer.Tick += (_, _) =>
        {
            _gainSaveTimer.Stop();
            _cfg.RecordGainDb = _recordingService.GainDb;
            _configStore.Save(_cfg);
        };

        RecordGainDb = cfg.RecordGainDb;
        _recordingService.GainDb = cfg.RecordGainDb;
        _gainLoaded = true;

        RefreshDevices();
        _deviceLoaded = true;

        LoadRecordings();

        Shelf = new TakeFilter(Recordings);

        Recordings.CollectionChanged += (_, _) => ValidateName();

        _preview.Stopped += () =>
        {
            if (_playing != null) _playing.IsPlaying = false;
            _playing = null;
            IsPreviewing = false;
        };

        RecordingName = NextRecordingName(RecordingNameValidator.DefaultBaseName);
        ValidateName();
    }

    /// <summary>
    /// Opens the input and starts writing a take under the name in the box.
    /// </summary>
    /// <remarks>
    /// Always enabled; what stops it is <see cref="CanRecord"/> on the button, since a page that
    /// silently ignored the record key would be worse than one that says why it will not.
    /// </remarks>
    public IAsyncRelayCommand StartRecordingCommand => new AsyncRelayCommand(StartRecording);

    /// <summary>Closes the take, reads its shape, and puts it on the shelf.</summary>
    public IAsyncRelayCommand StopRecordingCommand => new AsyncRelayCommand(StopRecording);

    /// <summary>Asks the system for its inputs again, for a microphone plugged in just now.</summary>
    public IRelayCommand RefreshDevicesCommand => new RelayCommand(RefreshDevices);

    /// <summary>Opens the edit dialog on that take, which is where a rename is typed.</summary>
    public IRelayCommand<Recording> EditRecordingCommand => new RelayCommand<Recording>(EditRecording);

    /// <summary>
    /// Puts that take in the bin, having first asked what else is playing it.
    /// </summary>
    /// <remarks>
    /// The bin is a folder beside the recordings rather than a delete, so undo can fetch it
    /// back. What is asked is the rack and the songs both: a song owns its instruments, so a
    /// recording nothing on the rack plays can still be the sound of three songs.
    /// </remarks>
    public IAsyncRelayCommand<Recording> DeleteRecordingCommand => new AsyncRelayCommand<Recording>(DeleteRecording);

    /// <summary>True while a take is being auditioned from the list.</summary>
    [ObservableProperty] private bool isPreviewing;

    /// <inheritdoc/>
    /// <remarks>
    /// Two things count as running here, taking a take and auditioning one, because either of
    /// them is this page making a sound and the transport wants to know which page owns it.
    /// </remarks>
    bool ITransportDeck.IsRunning => IsRecording || IsPreviewing;

    /// <inheritdoc/>
    /// <remarks>Playing on RECORD means auditioning a take off the shelf, nothing else.</remarks>
    bool ITransportDeck.IsPlaying => IsPreviewing;

    /// <inheritdoc/>
    /// <remarks>
    /// Never. A take is either being made or it is not, and half a recording paused in the
    /// middle is not a thing a tape machine ever offered either.
    /// </remarks>
    bool ITransportDeck.IsPaused => false;

    /// <inheritdoc/>
    /// <remarks>There has to be a take picked to play, and it must not already be playing.</remarks>
    bool ITransportDeck.CanPlay => SelectedRecording != null && !IsPreviewing;

    /// <inheritdoc/>
    /// <remarks>The pause cap is greyed on this page, so nothing ever calls Pause.</remarks>
    bool ITransportDeck.CanPause => false;

    /// <inheritdoc/>
    void ITransportDeck.Record() => StartRecordingCommand.Execute(null);

    /// <inheritdoc/>
    /// <remarks>The take whose picture is up, which is the one the buttons underneath are about.</remarks>
    void ITransportDeck.Play() => PlayRecording(SelectedRecording);

    /// <inheritdoc/>
    /// <remarks>Nothing to do, and the cap that would call it is greyed.</remarks>
    void ITransportDeck.Pause() { }

    /// <inheritdoc/>
    /// <remarks>
    /// Stops whichever of the two is happening. Recording wins, since it is the one where
    /// pressing stop a second late costs something.
    /// </remarks>
    void ITransportDeck.Stop()
    {
        if (IsRecording) StopRecordingCommand.Execute(null);
        else StopPreview();
    }

    /// <summary>
    /// Auditions that take, stopping whatever was being auditioned.
    /// </summary>
    /// <remarks>
    /// One at a time on purpose: this is for hearing what a take is, and two at once tells you
    /// nothing.
    /// </remarks>
    public IRelayCommand<Recording> PlayRecordingCommand => new RelayCommand<Recording>(PlayRecording);

    /// <summary>Stops the audition, whichever take it is on: the argument is ignored.</summary>
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

    /// <summary>Typing in the box moves the dialog's button, which is what says the name is usable.</summary>
    partial void OnEditNameChanged(string value) => OnPropertyChanged(nameof(CanRename));

    /// <summary>Why that name cannot be used, or null when it can.</summary>
    /// <remarks>
    /// The take's own name is left out of what is checked against, or a take renamed to what it
    /// is already called would be refused as taken. Its name unchanged is allowed outright,
    /// since pressing Rename on a name nobody edited is not an error to report.
    /// </remarks>
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

    /// <summary>Whether the dialog's Rename button does anything: a take open and a usable name.</summary>
    public bool CanRename => RenameError == null && SelectedRecordingForEdit != null;

    /// <summary>
    /// Gives the recording another name, which for a recording means another file name.
    /// </summary>
    /// <remarks>
    /// The name shown is read off the file when the list is built, so there is nowhere else to
    /// put it: renaming is moving. Which is why the instruments that play it are repointed in
    /// the same breath, on the shelf and in whatever song is open, rather than being left to
    /// find out at the next note.
    ///
    /// The audition is stopped first, because a file being played is a file that is open, and on
    /// Windows a file that is open is a file that will not move.
    ///
    /// The filing is kept by name rather than by path, so a take called something else has to be
    /// written down again under the new one or it loses its category on the way past.
    /// </remarks>
    /// <returns>
    /// True when the take really moved. False leaves <see cref="Status"/> saying why, since a
    /// rename that failed is something the dialog has to stay open about.
    /// </returns>
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
            if (ReferenceEquals(_playing, recording)) StopPreview();

            await Task.Run(() => File.Move(from, to));

            string was = recording.Name;

            recording.FilePath = to;
            recording.Name = wanted;

            _filing.Renamed(was, wanted);

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

    /// <summary>
    /// Asks the system what inputs there are and keeps the one that was chosen if it is still
    /// there.
    /// </summary>
    /// <remarks>
    /// The pick falls back to the settings file rather than to nothing, so a page built before
    /// anybody has touched the picker still comes up on the input somebody chose last time. What
    /// is not there any more falls back to the first input, since a page pointed at a microphone
    /// that has been unplugged records silence and says nothing about why.
    /// </remarks>
    private void RefreshDevices()
    {
        string? previous = SelectedDevice ?? _cfg.RecordInputDevice;

        InputDevices.Clear();
        foreach (var device in _recordingService.GetInputDevices())
            InputDevices.Add(device);

        SelectedDevice = InputDeviceSelector.Pick(InputDevices, previous);
    }

    /// <summary>
    /// Reads the shelf off the disc, which is the folder and nothing else.
    /// </summary>
    /// <remarks>
    /// There is no index: what is on the shelf is what wav files are in the folder, so a take
    /// copied in by hand is on the shelf and one deleted by hand is off it, with nothing to
    /// repair. The category is the one thing that cannot be read off the audio, so it is looked
    /// up by name in the filing beside the takes.
    ///
    /// A folder that is not there yet is not a fault: it is a first run, and the folder appears
    /// when the first take is written.
    /// </remarks>
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
                string name = Path.GetFileNameWithoutExtension(file);

                var recording = new Recording
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    FilePath = file,
                    DurationMs = ReadDurationMs(file),
                    CreatedAt = info.CreationTime,
                    Category = _filing.Of(name)
                };
                Recordings.Add(recording);
            }
        }
        catch (Exception ex)
        {
            Status = $"Failed to load recordings: {ex.Message}";
        }
    }

    /// <summary>
    /// Files the take that is picked under whatever has been typed in the box.
    /// </summary>
    /// <remarks>
    /// A category is made by naming one: there is no list to add to first, and no list to tidy
    /// up afterwards either, since a category is only ever the takes filed under it. Empty the
    /// box and the take is uncategorized again.
    ///
    /// A take filed into a category the list is not showing leaves the list, which is the
    /// point of working through the uncategorized ones.
    /// </remarks>
    public void FileTake() => FileUnder(SelectedRecording, TakeCategory);

    /// <summary>Files the take under a category that is already in use.</summary>
    public void FileTakeUnder(string? category)
    {
        TakeCategory = category ?? "";

        FileTake();
    }

    /// <summary>
    /// Files one take under one category, whichever take the page is showing by now.
    /// </summary>
    /// <remarks>
    /// Named rather than implied, because the box can be left by clicking on another take, and
    /// what was typed belongs to the take it was typed for.
    ///
    /// The box on the page is only written when the take being filed is the one the page is
    /// showing, since the box says what the picked take is filed under and nothing else.
    /// </remarks>
    public void FileUnder(Recording? recording, string? category)
    {
        if (recording == null) return;

        string wanted = (category ?? "").Trim();

        if (ReferenceEquals(recording, SelectedRecording) &&
            !string.Equals(TakeCategory, wanted, StringComparison.Ordinal))
        {
            TakeCategory = wanted;
        }

        if (string.Equals(recording.Category, wanted, StringComparison.Ordinal)) return;

        recording.Category = wanted;
        _filing.Put(recording.Name, wanted);

        Status = wanted.Length == 0
            ? $"'{recording.Name}' is uncategorized"
            : $"'{recording.Name}' filed under '{wanted}'";
    }

    /// <summary>
    /// Puts the picture of whichever take was picked up on the page, and takes it down again
    /// when nothing is picked.
    /// </summary>
    /// <remarks>
    /// Whatever is sounding stops first: the picture and the play button now belong to the
    /// take that is picked, and leaving the last one running underneath a different waveform
    /// is a lie about what you are hearing.
    ///
    /// Three other things follow the pick. The transport's play cap is lit by there being
    /// something to play. The trim, the normalise and the edit dialog all work on this take. And
    /// the category box shows what it is filed under, since that box is about whichever take is
    /// picked.
    ///
    /// A take that cannot be read leaves the picture empty and says so rather than throwing: a
    /// damaged or half-copied wav is an ordinary thing to find on a shelf.
    /// </remarks>
    partial void OnSelectedRecordingChanged(Recording? value)
    {
        StopPreview();

        OnPropertyChanged(nameof(ITransportDeck.CanPlay));

        SelectedRecordingForEdit = value;

        TakeCategory = value?.Category ?? "";

        if (value == null)
        {
            CurrentWaveform = null;
            return;
        }

        try
        {
            CurrentWaveform = _waveformService.AnalyzeFile(value.FilePath);
            Status = $"'{value.Name}', {TimeSpan.FromMilliseconds(value.DurationMs):mm\\:ss\\.fff}";
        }
        catch (Exception ex)
        {
            CurrentWaveform = null;
            Status = $"'{value.Name}' could not be read: {ex.Message}";
        }
    }

    /// <summary>Every keystroke in the name box is checked, so the button moves as it is typed.</summary>
    partial void OnRecordingNameChanged(string value) => ValidateName();

    /// <summary>
    /// Works out whether the name in the box can be used, and why not when it cannot.
    /// </summary>
    /// <remarks>
    /// Checked against the shelf rather than against the disc, because the shelf is what is
    /// really there and a check that opened the folder would run on every letter typed.
    /// </remarks>
    private void ValidateName() =>
        NameError = RecordingNameValidator.Validate(RecordingName, Recordings.Select(r => r.Name));

    /// <summary>Next free name in the same series as <paramref name="basedOn"/>.</summary>
    private string NextRecordingName(string basedOn) =>
        RecordingNameValidator.NextName(basedOn, Recordings.Select(r => r.Name));

    /// <summary>
    /// The gain reaches the recorder at once and the settings file half a second later.
    /// </summary>
    /// <remarks>
    /// Nothing is written while the stored value is being put on the slider, or every start
    /// would rewrite the settings file with the value it had just read out of it.
    /// </remarks>
    partial void OnRecordGainDbChanged(double value)
    {
        _recordingService.GainDb = value;

        if (!_gainLoaded) return;

        _gainSaveTimer.Stop();
        _gainSaveTimer.Start();
    }

    /// <summary>
    /// Points the recorder at that input and remembers it for the next session.
    /// </summary>
    /// <remarks>
    /// Written down straight away rather than coalesced, unlike the gain: picking an input is
    /// one act somebody performed, not a hundred values from a drag. The same guard applies
    /// while the stored device is being put on the picker, and a device that is already the one
    /// stored writes nothing.
    /// </remarks>
    partial void OnSelectedDeviceChanged(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;

        _recordingService.SelectedDevice = value;

        if (!_deviceLoaded) return;
        if (_cfg.RecordInputDevice == value) return;

        _cfg.RecordInputDevice = value;
        _configStore.Save(_cfg);
    }

    /// <summary>
    /// Opens the edit dialog on a take: its name, its picture, and the two things that rewrite it.
    /// </summary>
    /// <remarks>
    /// The audition is stopped first, because the dialog has a player of its own and the page's
    /// would go on sounding underneath it.
    ///
    /// The picture is read again rather than the page's being handed over, since the dialog is
    /// the one that rewrites the file and has to start from what is on disc now.
    /// </remarks>
    private void EditRecording(Recording? recording)
    {
        if (recording == null) return;

        StopPreview();

        try
        {
            SelectedRecordingForEdit = recording;
            EditName = recording.Name;

            CurrentWaveform = _waveformService.AnalyzeFile(recording.FilePath);

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

    /// <summary>
    /// Brings WAV files in from the disc onto the shelf, as the 16-bit files everything here
    /// works in.
    /// </summary>
    /// <remarks>
    /// Copied in rather than pointed at, for the same reason the machines' own importer does
    /// it: a take that lives in somebody's downloads folder is a song waiting to go silent the
    /// next time that folder is tidied.
    ///
    /// Anything wider than sixteen bits, or written as floats, is rewritten on the way in.
    /// That happens at the door and is said out loud in the status line, rather than quietly
    /// the first time the file is trimmed.
    ///
    /// One file at a time, so what came back can be matched to what went in and the line at
    /// the end can count honestly.
    ///
    /// A file picked out of the recordings folder itself is already on the shelf, and importing
    /// it would hand it straight back and give the list a second row for one file. Those are
    /// counted as held rather than refused, since dragging a folder in wholesale is the ordinary
    /// way to meet this.
    /// </remarks>
    public void Import(IReadOnlyList<string> paths)
    {
        if (paths == null || paths.Count == 0) return;

        var landed = new List<Recording>();
        int converted = 0, held = 0;

        foreach (string path in paths)
        {
            if (Recordings.Any(r => string.Equals(r.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            {
                held++;
                continue;
            }

            bool converts = RecordingImport.Converts(path);

            foreach (var recording in RecordingImport.Take(new[] { path }))
            {
                recording.DurationMs = ReadDurationMs(recording.FilePath);
                recording.Category = _filing.Of(recording.Name);

                Recordings.Add(recording);
                landed.Add(recording);

                if (converts) converted++;
            }
        }

        Status = ImportReport(landed, converted, held, paths.Count - landed.Count - held);
    }

    /// <summary>What the import did, in one line, without a tally nobody asked for.</summary>
    private static string ImportReport(IReadOnlyList<Recording> landed, int converted, int held, int failed)
    {
        if (landed.Count == 0)
            return held > 0 ? "Already on the shelf; nothing imported." : "Nothing imported.";

        string said = landed.Count == 1
            ? $"Imported '{landed[0].Name}'"
            : $"Imported {landed.Count} recordings";

        if (converted > 0)
            said += converted == landed.Count ? ", converted to 16-bit" : $", {converted} converted to 16-bit";

        if (held > 0) said += $", {held} already on the shelf";
        if (failed > 0) said += failed == 1 ? ", one could not be read" : $", {failed} could not be read";

        return said + ".";
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
    /// <remarks>
    /// The file itself is rewritten, so anything built on it is holding audio that no longer
    /// exists: <see cref="RecordingChanged"/> says the path, and whoever is playing it from
    /// memory reads it again.
    ///
    /// The work is done off the drawing thread, since a long take takes a moment and a page that
    /// stopped while it did would read as a program that had hung.
    /// </remarks>
    /// <returns>True when the file was rewritten, so callers can reset their view.</returns>
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

    /// <summary>The two ends of the target slider, taken from the rule so they cannot drift.</summary>
    public double MinNormalizeDb => Normalization.MinTargetDecibels;

    /// <inheritdoc cref="MinNormalizeDb"/>
    public double MaxNormalizeDb => Normalization.MaxTargetDecibels;

    /// <summary>
    /// Lifts the whole recording so its loudest moment sits on the target. The trim region is
    /// not involved: this is about the level of the file, not about part of it.
    /// </summary>
    /// <remarks>
    /// A take already at the target is left alone and says so, rather than being rewritten to
    /// the same audio: every rewrite is a file written and a picture redrawn, and doing that for
    /// no change reads as work having happened when none did.
    ///
    /// The audio changes under anything built on this file, so <see cref="RecordingChanged"/>
    /// carries the path.
    /// </remarks>
    /// <returns>True when the file was rewritten, so callers can redraw.</returns>
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
    /// The rack, set once it has been built. Recordings are its raw material,
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
    /// Reads the shelf again, for takes that arrived without this page putting them there.
    /// </summary>
    /// <remarks>
    /// A packed song puts its recordings on the shelf as it opens, through the same door as
    /// anything imported, but nothing on this page did it and nothing on this page knows. Read
    /// again rather than told what to add, so what turns up in the list is built exactly the
    /// way every other row was.
    /// </remarks>
    public void Rescan()
    {
        LoadRecordings();
        RefreshUsage();
    }

    /// <summary>
    /// Marks each recording with the instruments that play it. Called whenever the rack
    /// changes, so a recording becomes free again the moment its last instrument goes.
    /// </summary>
    public void RefreshUsage()
    {
        foreach (var recording in Recordings)
            recording.UsedBy = Tracker.SampleUsage.Describe(UsersOf(recording));
    }

    /// <summary>
    /// The instruments playing a recording, right now rather than as last stamped.
    /// </summary>
    /// <remarks>
    /// A rack that cannot be read answers as "nothing known" rather than throwing: an unreadable
    /// rack is no reason to start deleting things, and the delete still asks before it acts.
    /// </remarks>
    private IReadOnlyList<string> UsersOf(Recording recording)
    {
        if (_sampleUsage == null) return Array.Empty<string>();

        try
        {
            return _sampleUsage.InstrumentsUsing(recording.FilePath);
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Plays a recording whole, from the list, so a take can be heard without opening it.
    /// </summary>
    /// <remarks>
    /// The stream can refuse to open even after the file has been read, and a row that said it
    /// was playing when nothing was would leave its stop button as the only way out of a state
    /// nobody is in. So the player is asked whether it really started before the row is lit.
    /// </remarks>
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

        if (!_preview.IsPlaying)
        {
            Status = $"'{recording.Name}' could not be played.";
            return;
        }

        _playing = recording;
        recording.IsPlaying = true;
        IsPreviewing = true;

        Status = $"Playing '{recording.Name}'";
    }

    /// <summary>Silence, whichever recording it was. Safe to call when nothing is playing.</summary>
    public void StopPreview()
    {
        _preview.Stop();
        IsPreviewing = false;
    }

    /// <summary>
    /// Puts a take in the bin, having first found out what would go silent without it.
    /// </summary>
    /// <remarks>
    /// What is playing it is asked again here rather than trusting the stamp on the row: the
    /// rack may have gained an instrument since the list was last marked up, and this is the one
    /// moment where being out of date costs somebody a song.
    ///
    /// A take that is in use is refused outright rather than warned about, because an instrument
    /// plays the file itself: deleting the recording would silence it in every song that uses
    /// it, and there is nothing the deletion could do about that afterwards.
    ///
    /// The audition is stopped first. A file that is being played is a file that is open, which
    /// on Windows is a file that will not delete.
    /// </remarks>
    private async Task DeleteRecording(Recording? recording)
    {
        if (recording == null) return;

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
            $"Delete '{recording.Name}'? It goes into the bin beside the recordings and can be "
                + "put back until you empty it.",
            "Delete");

        if (!confirmed) return;

        try
        {
            if (ReferenceEquals(_playing, recording)) StopPreview();

            Binned(recording);

            Recordings.Remove(recording);
            _filing.Forget(recording.Name);

            if (ReferenceEquals(SelectedRecording, recording)) SelectedRecording = null;

            if (ReferenceEquals(SelectedRecordingForEdit, recording))
            {
                SelectedRecordingForEdit = null;
                CurrentWaveform = null;
            }

            Status = $"'{recording.Name}' is in the bin. Press undo, or SETTINGS to empty it.";
        }
        catch (Exception ex)
        {
            Status = $"Delete failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Undo on the RECORD page fetches back the last take you deleted.
    /// </summary>
    /// <remarks>
    /// Not a history of edits, because there is only one edit here that is worth taking back and
    /// it is the destructive one. Renaming a take or moving it between categories is a thing you
    /// can simply do again; deleting one used to be a thing you could not.
    ///
    /// Redo is deliberately not answered. Redoing a deletion is deleting, and asking somebody
    /// once is the point.
    /// </remarks>
    /// <remarks>
    /// This page is asked before the window is, because the shortcut dispatcher walks outwards
    /// from whatever has the keyboard and, when nothing has it, that walk only reaches the
    /// window: a page with no focused control inside it was never asked at all. Pressing undo on
    /// RECORD straight after clicking a button in a dialog is exactly that, and it silently did
    /// nothing.
    /// </remarks>
    bool Shortcuts.IShortcutContext.Can(Shortcuts.ShortcutAction action) =>
        action == Shortcuts.ShortcutAction.Undo && CanUnbin;

    /// <inheritdoc/>
    /// <remarks>Only undo is answered, and only while there is something in the bin.</remarks>
    void Shortcuts.IShortcutContext.Do(Shortcuts.ShortcutAction action)
    {
        if (action == Shortcuts.ShortcutAction.Undo) Unbin();
    }

    /// <summary>Where a deleted take waits, beside the recordings rather than inside them.</summary>
    /// <remarks>
    /// Beside, so that everything reading the shelf sees a folder of recordings and nothing
    /// else. Inside, every reader would have to learn to skip a folder, and one of them would
    /// not.
    /// </remarks>
    public static string Bin => Path.Combine(Config.AppFolder.Path(), "recordings", "..", "deleted");

    /// <summary>
    /// What has been thrown away this session and could still be fetched back.
    /// </summary>
    /// <remarks>
    /// The session, and not the folder. Anything in the bin from a previous run stays there and
    /// is emptied deliberately; undo is about what you have just done, and offering to put back
    /// a take you deleted last Tuesday is not undo, it is a filing cabinet.
    /// </remarks>
    private readonly Stack<(string Name, string Was, string Now)> _binned = new();

    /// <summary>True when the last thing deleted can be fetched back.</summary>
    public bool CanUnbin => _binned.Count > 0;

    /// <summary>
    /// Moves a take into the bin rather than deleting it.
    /// </summary>
    /// <remarks>
    /// A move and not a copy, so it costs nothing whatever the take's length: a recording is
    /// the one thing here that can be a hundred megabytes, and copying one to make a deletion
    /// reversible would be paying for the undo whether or not anybody wanted it.
    ///
    /// A name already in the bin is given a number rather than being written over, since a
    /// second take of the same name deleted later must not land on the first one.
    /// </remarks>
    private void Binned(Recording recording)
    {
        string from = recording.FilePath;

        if (string.IsNullOrWhiteSpace(from) || !File.Exists(from)) return;

        string folder = Path.GetFullPath(Bin);

        Directory.CreateDirectory(folder);

        string to = Path.Combine(folder, Path.GetFileName(from));

        for (int at = 2; File.Exists(to); at++)
            to = Path.Combine(folder,
                Path.GetFileNameWithoutExtension(from) + " (" + at + ")" + Path.GetExtension(from));

        File.Move(from, to);

        _binned.Push((recording.Name, from, to));

        Diagnostics.Log.Write(Diagnostics.LogArea.App, () => "recordings: '" + recording.Name + "' went into the bin");
    }

    /// <summary>
    /// Fetches the last thing deleted back out of the bin.
    /// </summary>
    /// <remarks>
    /// Back to where it came from, and only if nothing has taken that name in the meantime: a
    /// take recorded into the gap since is somebody's work and is not something an undo may
    /// write over.
    /// </remarks>
    public bool Unbin()
    {
        while (_binned.Count > 0)
        {
            var (name, was, now) = _binned.Pop();

            if (!File.Exists(now)) continue;

            if (File.Exists(was))
            {
                Status = $"'{name}' cannot come back: something else is called that now.";

                return false;
            }

            try
            {
                File.Move(now, was);
            }
            catch (Exception bad)
            {
                Status = $"'{name}' could not be fetched back: {bad.Message}";

                return false;
            }

            Rescan();

            Status = $"'{name}' is back.";

            Diagnostics.Log.Write(Diagnostics.LogArea.App, () => "recordings: '" + name + "' came back out of the bin");

            return true;
        }

        return false;
    }

    /// <summary>What the input can be taken from, where the system lets that be chosen.</summary>
    public ObservableCollection<AudioRoute> Routes { get; } = new();

    /// <summary>Which of those the input is being taken from, or null while none is chosen.</summary>
    /// <remarks>
    /// Written both by somebody picking one and by the graph being read back, which is why the
    /// reading is guarded: a route shown would otherwise be applied again the moment it appeared
    /// in the picker.
    /// </remarks>
    [ObservableProperty] private AudioRoute? selectedRoute;

    /// <summary>False on a system with no graph to patch, and the picker stays hidden.</summary>
    public bool IsRoutingAvailable => _routing.IsAvailable;

    /// <summary>Reads the graph again, for a program that has started playing since.</summary>
    /// <remarks>
    /// Always enabled, and the button is only on the page at all where there is a graph to
    /// read. It is also called on a timer while the page is up, so pressing it is a way to be
    /// sure rather than the only way to be told.
    /// </remarks>
    public IRelayCommand RefreshRoutesCommand => new RelayCommand(RefreshRoutes);

    /// <summary>
    /// Reads the graph and shows what is feeding the recorder. The tools take a moment, so
    /// this happens off the UI thread.
    /// </summary>
    /// <remarks>
    /// The route on show is matched to the current one by node rather than by object, because
    /// the list is read afresh every time and the object from before is not in it.
    ///
    /// One reading at a time: the timer fires every two seconds and the tools can take longer
    /// than that, so without the guard the readings would pile up on each other.
    /// </remarks>
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

    /// <summary>Stops watching, for a page that has gone away or an input that has closed.</summary>
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

    /// <summary>Where a route with that node sits in the list, or -1 when none does.</summary>
    /// <remarks>
    /// By node and not by the whole route, since a route's name and its display can change under
    /// it while it stays the same thing to connect to.
    /// </remarks>
    private int IndexOfRoute(string node)
    {
        for (int i = 0; i < Routes.Count; i++)
        {
            if (Routes[i].Node == node) return i;
        }

        return -1;
    }

    /// <summary>
    /// Somebody picked a source, so it is wired up and remembered as what they want.
    /// </summary>
    /// <remarks>
    /// Nothing happens while the graph is being read back, which is what tells a choice apart
    /// from a reading: only a choice is worth putting back after the input has been reopened.
    /// </remarks>
    partial void OnSelectedRouteChanged(AudioRoute? value)
    {
        if (_readingRoute || value == null) return;

        _preferredRoute = value;
        ApplyRoute(value, announce: true);
    }

    /// <summary>
    /// Puts the chosen source back after the input has been reopened. Silent when the choice is
    /// already in place, and gives up when whatever was chosen has since stopped playing.
    /// </summary>
    /// <remarks>
    /// A retry rather than a request, so it says nothing unless it works: a source coming and
    /// going is normal, and there is nothing anybody could do about it if it were announced.
    /// </remarks>
    private void RestorePreferred(AudioRoute? current)
    {
        if (_applyingRoute || _preferredRoute == null) return;
        if (current != null && current.Node == _preferredRoute.Node) return;

        var still = Routes.FirstOrDefault(r => r.Node == _preferredRoute.Node);
        if (still == null) return;

        ApplyRoute(still, announce: false);
    }

    /// <summary>
    /// Rewires the input. Off the UI thread: connecting runs a handful of command line tools,
    /// and half a second of frozen window is not something a dropdown should cost.
    /// </summary>
    /// <remarks>
    /// Connecting replaces whatever the system wired up, which is the whole point of the
    /// picker: the system's own choice is a default, not a decision.
    ///
    /// What was applied is then shown, with the reading guard up so that showing it does not
    /// count as a fresh choice and start the whole thing again.
    /// </remarks>
    /// <param name="route">The input to wire up, taken from the picker or from what was preferred last.</param>
    /// <param name="announce">
    /// False for a retry, which must stay quiet: see <see cref="RestorePreferred"/>.
    /// </param>
    private async void ApplyRoute(AudioRoute route, bool announce)
    {
        if (_applyingRoute) return;

        try
        {
            _applyingRoute = true;
            if (announce) Status = $"Taking audio from {route.Name}...";

            bool connected = await Task.Run(() => _routing.Connect(route));

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
    /// <remarks>
    /// The routes are read after the input is open and not before, because the recorder only
    /// appears in the graph once it is listening: reading first would show a graph with nothing
    /// to connect to.
    /// </remarks>
    public void StartInputMonitoring()
    {
        try
        {
            _recordingService.StartMonitoring();
            StartLevelPolling();

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
    /// <remarks>
    /// Twenty readings a second, which is faster than an eye can follow a bar and slow enough
    /// that the reading costs nothing. The audio is read on the timer's own thread and only the
    /// writing is handed to the drawing one, since the meter is four values and the clock is a
    /// string.
    ///
    /// The clock is only written while a take is running: the meter runs whenever the input is
    /// open, and a clock that ticked while nothing was being recorded would be counting
    /// something nobody could keep.
    /// </remarks>
    private void StartLevelPolling()
    {
        if (_levelUpdateTimer != null) return;

        _levelUpdateTimer = new System.Timers.Timer(LevelPollMs);
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

    /// <summary>Stops the poll and lets its timer go, for a page that is no longer listening.</summary>
    private void StopLevelPolling()
    {
        _levelUpdateTimer?.Stop();
        _levelUpdateTimer?.Dispose();
        _levelUpdateTimer = null;
    }

    /// <summary>
    /// Opens the take: checks the name, silences the audition, and starts the clock.
    /// </summary>
    /// <remarks>
    /// The audition is stopped first because auditioning an old take while capturing a new one
    /// would put the first one into the second, on any source that carries what the machine is
    /// playing.
    ///
    /// The name is checked again here rather than trusted from the box, since the record cap on
    /// the transport reaches this without going past the button that is greyed out.
    /// </remarks>
    private async Task StartRecording()
    {
        ValidateName();
        if (NameError != null)
        {
            Status = NameError;
            return;
        }

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

    /// <summary>
    /// Closes the take, writes it, and puts it on the shelf as the one being looked at.
    /// </summary>
    /// <remarks>
    /// The meter goes on reading if the page is still watching the input; if it is not, the poll
    /// goes with the take, since nothing would be reading it.
    ///
    /// The file is written under the trimmed name, because the name check trims and the two
    /// would otherwise disagree about whether a name is taken.
    ///
    /// The take just made is picked, so its picture is up and the buttons under it are to hand;
    /// reading it back is what puts the waveform on the page. And the box is filled with the
    /// next name in the same series, so pressing record again stops to ask nothing.
    ///
    /// A take that clipped is still saved and says so. Refusing to keep it would throw away
    /// audio somebody cannot record again.
    /// </remarks>
    private async Task StopRecording()
    {
        try
        {
            _recordingTimer.Stop();
            _recordingService.StopRecording();

            if (!_recordingService.IsMonitoring) StopLevelPolling();

            IsRecording = false;
            IsClipping = false;

            bool clipped = _recordingService.ClippedDuringTake;

            string savedName = RecordingName.Trim();
            string filePath = await _recordingService.SaveRecordingAsync(savedName);
            Status = "Saved recording";

            var recording = new Recording
            {
                Id = Guid.NewGuid().ToString(),
                Name = savedName,
                FilePath = filePath,
                DurationMs = ReadDurationMs(filePath),
                CreatedAt = DateTime.Now
            };

            Recordings.Add(recording);

            SelectedRecording = recording;

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
