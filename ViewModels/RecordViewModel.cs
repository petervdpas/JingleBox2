using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Config;
using JingleBox2.Audio.Records;
using JingleBox2.Tracker;
using JingleBox2.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JingleBox2.Audio.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.ViewModels.Interfaces;
using JingleBox2.Audio.Routing.Records;

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
public sealed partial class RecordViewModel : ObservableObject, ITransportDeck, IInputWatch, IInputSource, Shortcuts.Interfaces.IShortcutContext
{
    /// <summary>The one door recordings come in through. Holds nothing, so one is enough.</summary>
    private readonly IRecordingImport _import = new RecordingImport();

    /// <summary>How a take is named and what makes a name unusable. Holds nothing of its own.</summary>
    private readonly IRecordingNames _names = new RecordingNames();

    /// <summary>Which instruments play a given recording, and how to say so.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly ISampleUsers Usage = new SampleUsers();

    /// <summary>What actually opens the input and writes the file.</summary>
    private readonly IRecordingService _recordingService;

    /// <summary>Where the meter's numbers come from while nothing is being recorded.</summary>
    private readonly ILevelMeterService _levelMeter;

    /// <summary>Reduces a finished take to peaks, for the picture under the list.</summary>
    private readonly IWaveformService _waveformService;

    /// <summary>The copy of the take the editor works on, so the shelf's own file is untouched.</summary>
    private readonly IWorkingCopy _copy;

    /// <summary>What turns one written-down step into the edit it asks for.</summary>
    private readonly ITakeSteps _steps;

    /// <summary>What has been done to the take that is open, and where in it you are standing.</summary>
    private readonly ITakeHistory _history = new TakeHistory();

    /// <summary>What each step is called where somebody reads a list of them.</summary>
    private readonly ITakeStepWords _stepWords = new TakeStepWords();

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

    /// <summary>
    /// What the IN strip does to the machine, which is the source and the tick as one thing.
    /// </summary>
    /// <remarks>
    /// Built here from the same route this page was handed, so there is one of it and it is the
    /// only thing that reaches out and moves somebody else's audio about. The page keeps the two
    /// facts because it draws them; what they come to is not its business.
    /// </remarks>
    private readonly Audio.Routing.Interfaces.IInputPath _input;

    /// <summary>Who to ask whether a recording is spoken for. Null before the rack exists.</summary>
    private ISampleUsage? _sampleUsage;

    /// <summary>
    /// Auditions a recording from the list. One at a time on purpose: this is for hearing
    /// what a take is, and two of them at once tells you nothing.
    /// </summary>
    private readonly Waveform.WaveformPlayer _preview;

    /// <summary>The bus a take goes onto, kept so the editing dialog is given the same one.</summary>
    private readonly JingleBox2.Audio.Interfaces.IOutputBus? _takes;

    /// <summary>How a recording is made to sound, kept so the editing dialog is given the same one.</summary>
    private readonly JingleBox2.Audio.Interfaces.IRecordingSource? _recordings;

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

    /// <summary>The chain a take is run through, and where it is held.</summary>
    private readonly RecordPluginTarget _chain;

    /// <summary>Where a take lives before somebody gives it a name.</summary>
    private readonly ITakeScratch _scratch = new TakeScratch();

    /// <summary>What a take is called on the scratchpad, where it has no name of its own.</summary>
    /// <remarks>
    /// Fixed rather than named after the box, since the scratchpad holds one take and a name
    /// somebody is still typing is not something to build a path out of. What is in the box is
    /// what the card shows and what the file is called once it is saved.
    /// </remarks>
    private const string ScratchName = "take";

    /// <summary>What the untouched twin is called on the scratchpad.</summary>
    private const string ScratchCleanName = "take (clean)";

    /// <summary>Where the shelf keeps its takes.</summary>
    private static string ShelfFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JingleBox2", "recordings");

    /// <summary>
    /// The take that has just been made, through whatever was on the chain, or none.
    /// </summary>
    /// <remarks>
    /// **Deliberately not in <see cref="Recordings"/>.** A take nobody has named is not on the
    /// shelf, so it is not in the list, not under the search box and not filed under a category:
    /// it is one card of its own with a name box and two buttons. That is what the scratchpad
    /// is, and the shelf is what is left once somebody has said a take was worth keeping.
    ///
    /// One at a time. Recording again is starting again, and what was on the scratchpad goes
    /// with it, which is the whole meaning of the word.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScratch))]
    [NotifyPropertyChangedFor(nameof(ScratchShown))]
    [NotifyPropertyChangedFor(nameof(CanSaveTake))]
    private Recording? scratchTake;

    /// <summary>The same take as it arrived, where a chain means there are two of it.</summary>
    /// <remarks>
    /// Both are on the scratchpad and both are saved under the name, because an effect cannot be
    /// taken off a take afterwards and the moment to decide you wanted the plain one is after
    /// you have heard the other.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScratchHasClean))]
    [NotifyPropertyChangedFor(nameof(ScratchShown))]
    private Recording? scratchClean;

    /// <summary>Which of the two the card is drawing and would play.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScratchShown))]
    private bool scratchShowsClean;

    /// <summary>The picture of whichever one is being shown.</summary>
    [ObservableProperty]
    private WaveformData? scratchWaveform;

    /// <summary>Where the scratchpad's own playback has got to, and -1 for nothing playing.</summary>
    [ObservableProperty]
    private double scratchPlayhead = -1;

    /// <summary>Whether there is a take waiting to be kept or thrown away.</summary>
    public bool HasScratch => ScratchTake != null;

    /// <summary>Whether this take has an untouched twin, which it has where a chain was on.</summary>
    public bool ScratchHasClean => ScratchClean != null;

    /// <summary>The one the card is about: the clean twin where that is what is picked.</summary>
    public Recording? ScratchShown =>
        ScratchShowsClean && ScratchClean != null ? ScratchClean : ScratchTake;

    /// <summary>Whether the take can be put on the shelf under what is in the name box.</summary>
    public bool CanSaveTake => HasScratch && NameError == null;

    /// <summary>Puts the take on the shelf under the name in the box.</summary>
    public IRelayCommand SaveTakeCommand => new RelayCommand(SaveTake);

    /// <summary>Throws the take away without keeping it.</summary>
    public IRelayCommand DiscardTakeCommand => new RelayCommand(DiscardTake);

    /// <summary>Reads the picture of whichever of the two is being shown.</summary>
    private void ReadScratchWaveform()
    {
        ScratchPlayhead = -1;

        if (ScratchShown is not { } shown)
        {
            ScratchWaveform = null;
            return;
        }

        try { ScratchWaveform = _waveformService.AnalyzeFile(shown.FilePath); }
        catch (Exception) { ScratchWaveform = null; }
    }

    /// <summary>Switching between the two draws the other one and stops what was playing.</summary>
    partial void OnScratchShowsCleanChanged(bool value)
    {
        StopPreview();
        ReadScratchWaveform();
    }

    /// <summary>
    /// Moves the take onto the shelf under the name in the box, its clean twin with it.
    /// </summary>
    /// <remarks>
    /// The name is checked here as well as on every keystroke, because the shelf can have
    /// gained a take of that name since the box was last typed in: importing one, or another
    /// take saved in between.
    ///
    /// The audition is stopped first. A file that is being played is a file that is open, which
    /// on Windows is a file that will not move.
    ///
    /// The clean twin is moved first and the take second, so a failure part way leaves the take
    /// still on the scratchpad rather than half of it on the shelf.
    /// </remarks>
    private void SaveTake()
    {
        if (ScratchTake is not { } take) return;

        string name = RecordingName.Trim();

        if (_names.Validate(name, Recordings.Select(one => one.Name)) is { } why)
        {
            Status = why;
            return;
        }

        StopPreview();

        string folder = ShelfFolder;
        string? clean = null;

        if (ScratchClean is { } twin)
        {
            string cleanName = CleanName(name);

            if (_scratch.Keep(twin.FilePath, folder, cleanName) is { } wentTo)
            {
                clean = cleanName;
                Shelve(cleanName, wentTo);
            }
        }

        if (_scratch.Keep(take.FilePath, folder, name) is not { } kept)
        {
            Status = $"'{name}' could not be saved.";
            return;
        }

        var row = Shelve(name, kept);

        ClearScratch(drop: false);

        SelectedRecording = row;
        RecordingName = NextRecordingName(name);

        Status = clean == null ? $"Saved '{name}'" : $"Saved '{name}', and '{clean}' beside it";
    }

    /// <summary>Throws the take away, both of it where there are two.</summary>
    private void DiscardTake()
    {
        StopPreview();
        ClearScratch(drop: true);

        Status = "Take thrown away.";
    }

    /// <summary>Empties the scratchpad, deleting what was on it where nothing else has it.</summary>
    /// <param name="drop">
    /// True to delete the files, which is throwing a take away and is also what starting another
    /// one does. False where they have just been moved onto the shelf and are somebody's now.
    /// </param>
    private void ClearScratch(bool drop)
    {
        if (drop)
        {
            _scratch.Drop(ScratchTake?.FilePath);
            _scratch.Drop(ScratchClean?.FilePath);
        }

        ScratchTake = null;
        ScratchClean = null;
        ScratchShowsClean = false;
        ScratchWaveform = null;
        ScratchPlayhead = -1;
    }

    /// <summary>A take on the scratchpad, which is a recording that is on no shelf.</summary>
    /// <param name="name">What to call it while it is unnamed, which is what the box says.</param>
    /// <param name="path">The scratch file.</param>
    private Recording Scratched(string name, string path) =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            FilePath = path,
            DurationMs = ReadDurationMs(path),
            CreatedAt = DateTime.Now
        };

    /// <summary>
    /// Says the application is closing, so nothing unnamed is left lying about.
    /// </summary>
    /// <remarks>
    /// What is on the scratchpad was never asked to be kept, which is the whole of what the
    /// scratchpad means, so it goes. Swept rather than dropped one file at a time, since a run
    /// that ended badly can have left more than this one behind.
    /// </remarks>
    public void Finished()
    {
        StopPreview();
        GiveRoutesBack();
        _scratch.Sweep();
    }

    /// <summary>What the plugins on the chain are holding inside themselves, in chain order.</summary>
    /// <remarks>
    /// A preset is not a set of knob positions, so a chain saved as its parameters alone comes
    /// back sounding roughly right and calling itself untitled. Read when the chain settles
    /// rather than on every move of a knob, since asking a plugin for its patch is a round trip
    /// to another process.
    /// </remarks>
    private IReadOnlyList<byte[]> _patches = Array.Empty<byte[]>();

    /// <summary>What is on the chain, or null until the page has been given the plugin list.</summary>
    /// <remarks>
    /// Set once, from <see cref="UsePlugins"/>, the same as a pad's: a page built without a
    /// plugin library simply has no strip rather than a broken one.
    /// </remarks>
    public PluginChainViewModel? Effect { get; private set; }

    /// <summary>How long the chain has to be still before it is written down, in milliseconds.</summary>
    /// <remarks>
    /// Long enough that a knob dragged across its travel is one save rather than a hundred,
    /// short enough that letting go and closing the application keeps the change. The pads keep
    /// the same rule for the same reason.
    /// </remarks>
    private const int ChainSettleMs = 600;

    /// <summary>Restarted by every change to the chain, so it fires once the hand has stopped.</summary>
    private readonly DispatcherTimer _chainSave =
        new() { Interval = TimeSpan.FromMilliseconds(ChainSettleMs) };

    /// <summary>
    /// Gives the page its effect chain and puts back whatever was on it last time.
    /// </summary>
    /// <remarks>
    /// Told rather than asked for, because the plugin library is scanned in SETTINGS and belongs
    /// to the application rather than to this page.
    /// </remarks>
    /// <param name="plugins">Everything installed, as scanned in SETTINGS.</param>
    /// <param name="effects">What effects of ours this installation has, which the plus offers first.</param>
    /// <param name="front">Where a face opened off this chain says it is in front.</param>
    public void UsePlugins(
        PluginLibraryViewModel plugins,
        SoundDevices.SoundEffects.Interfaces.ISoundEffectProjects? effects = null,
        ISoundEffectInFront? front = null)
    {
        _chains = new Audio.Plugins.PluginChainState(
            new SoundDevices.SoundEffects.SoundEffectEngines(effects));

        Effect = new PluginChainViewModel(plugins, effects, front: front)
        {
            Target = _chain,
            Nothing = "Nothing yet, so a take is kept exactly as it arrives."
        };

        Effect.Changed += () =>
        {
            _chainSave.Stop();
            _chainSave.Start();
        };

        _chainSave.Tick += (_, _) =>
        {
            _chainSave.Stop();

            _patches = _chains.Patches(_chain.Chain);

            _cfg.RecordEffects = _chains.Capture(_chain.Chain, patches: true);
            _configStore.Save(_cfg);
        };

        if (_cfg.RecordEffects is { IsEmpty: false } saved)
        {
            var missing = _chains.Restore(
                _chain.Chain, saved, _chain.SampleRate, PluginChainViewModel.MaxFrames);

            Effect.Reload();

            _patches = saved.Devices.Select(one => one.State).ToList();

            if (missing.Count > 0) Effect.Status = "Missing: " + string.Join(", ", missing);
        }

        OnPropertyChanged(nameof(Effect));
    }

    /// <summary>Builds and reads the chain's plugins, which is arithmetic and a round trip.</summary>
    private Audio.Plugins.Interfaces.IPluginChainState _chains = new Audio.Plugins.PluginChainState();

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

    /// <summary>How long it has been running, for the clock on the page.</summary>
    /// <remarks>
    /// A span rather than a string, since the wording belongs to the control that draws it: two
    /// pages show one of these and a format written out here would be the second spelling of it.
    /// Read off the level poll, which already runs while a take is being made, so the clock costs
    /// no timer of its own.
    /// </remarks>
    [ObservableProperty] private TimeSpan recordingTime;

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

    /// <summary>
    /// How far through the take being previewed, from 0 to 1, and minus one when none is.
    /// </summary>
    /// <remarks>
    /// The picture on this page had no play cursor at all, which is the one thing somebody
    /// watching a take play is looking for: the position was arriving here from the player all
    /// along and nothing was drawing it. Minus one rather than nought when nothing is playing,
    /// since nought is the start of the take and is a place the cursor really can be.
    /// </remarks>
    [ObservableProperty] private double playhead = -1;

    /// <summary>What the next take will be called.</summary>
    /// <remarks>
    /// Filled in with the next unused name so that pressing record twice does not stop to ask
    /// anything, and checked as it is typed, since a name that cannot be a file name has to be
    /// refused before the recording rather than after it.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRecord))]
    private string recordingName = RecordingNames.DefaultBaseName;

    /// <summary>Null when the name is usable, otherwise why it is not.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRecord))]
    [NotifyPropertyChangedFor(nameof(HasNameError))]
    [NotifyPropertyChangedFor(nameof(CanSaveTake))]
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
    public RecordViewModel(IRecordingService recordingService, ILevelMeterService levelMeter, IWaveformService waveformService, ConfigStore configStore, AppConfig cfg, IAudioRouting routing, JingleBox2.Audio.Interfaces.IOutputBus? takes = null, JingleBox2.Audio.Interfaces.IRecordingSource? recordings = null, IWorkingCopy? copy = null, ITakeSteps? steps = null)
    {
        _copy = copy ?? new WorkingCopy();
        _steps = steps ?? new TakeSteps(waveformService);

        _takes = takes;
        _recordings = recordings;
        _preview = Playing(recordings, takes);

        _routing = routing;
        _input = new Audio.Routing.InputPath(routing);

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

        _recordingService.Rang += Ringing;

        RefreshDevices();
        _deviceLoaded = true;

        LoadRecordings();

        Shelf = new TakeFilter(Recordings);

        _chain = new RecordPluginTarget(_recordingService);

        _scratch.Sweep();

        Recordings.CollectionChanged += (_, _) => ValidateName();

        _preview.PositionChanged += at =>
        {
            if (ScratchTake != null && ReferenceEquals(_playing, ScratchShown)) ScratchPlayhead = at;
            else Playhead = at;
        };

        _preview.Stopped += () =>
        {
            if (_playing != null) _playing.IsPlaying = false;
            _playing = null;
            IsPreviewing = false;
            Playhead = -1;
        };

        RecordingName = NextRecordingName(RecordingNames.DefaultBaseName);
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
    /// <remarks>
    /// The whole gesture, both presses: starting where nothing is being recorded and stopping
    /// where something is. Record is one button and one key, and what a second press of it means
    /// is this page's business rather than the key handler's, which cannot know that arming a
    /// track and starting a take are different things.
    /// </remarks>
    void ITransportDeck.Record()
    {
        if (IsRecording) StopRecordingCommand.Execute(null);
        else StartRecordingCommand.Execute(null);
    }

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

            return _names.Validate(
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

        string? problem = _names.Validate(
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

        SelectedDevice = new AudioInputSelector().Pick(InputDevices, previous);
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
        NameError = _names.Validate(RecordingName, Recordings.Select(r => r.Name));

    /// <summary>Next free name in the same series as <paramref name="basedOn"/>.</summary>
    private string NextRecordingName(string basedOn) =>
        _names.NextName(basedOn, Recordings.Select(r => r.Name));

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
    /// Opens the editor on a take: its name, its picture, and the tools that work on it.
    /// </summary>
    /// <remarks>
    /// **A copy of the take is taken first and everything the editor does happens to the copy**,
    /// so the file on the shelf is exactly as it was until somebody saves. The picture is read
    /// off the copy for the same reason: what it has to show is the take with the unsaved work
    /// on it.
    ///
    /// The audition is stopped first, because the dialog has a player of its own and the page's
    /// would go on sounding underneath it.
    /// </remarks>
    private void EditRecording(Recording? recording)
    {
        if (recording == null) return;

        StopPreview();

        try
        {
            SelectedRecordingForEdit = recording;
            EditName = recording.Name;

            _history.Clear();

            string path = _copy.Open(recording.FilePath) ?? recording.FilePath;

            CurrentWaveform = _waveformService.AnalyzeFile(path);

            Shown();

            var dialog = Editor();

            dialog.DataContext = this;

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

            bool converts = _import.Converts(path);

            foreach (var recording in _import.Take(new[] { path }))
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

    /// <summary>Where a normalize puts the loudest moment, in dBFS.</summary>
    [ObservableProperty] private double normalizeTargetDb = Normalization.Target;

    /// <summary>The two ends of the target slider, taken from the rule so they cannot drift.</summary>
    public double MinNormalizeDb => Normalization.Quietest;

    /// <inheritdoc cref="MinNormalizeDb"/>
    public double MaxNormalizeDb => Normalization.Loudest;

    /// <summary>
    /// The copy of the take the editor is working on, which is what it draws, plays and edits.
    /// </summary>
    /// <remarks>
    /// **Nothing the editor does reaches the take on the shelf until Save.** So the picture, the
    /// preview and every tool are pointed here, and the shelf's own file is what a replay comes
    /// off and what Save writes to.
    /// </remarks>
    public string? EditingPath => _copy.Path;

    /// <summary>True while a take is open in the editor.</summary>
    public bool IsEditing => _copy.IsOpen;

    /// <summary>True while something has been done that the take on the shelf has not got.</summary>
    public bool HasEdits => _history.Done > 0;

    /// <summary>True when there is a step to go back past.</summary>
    public bool CanUndoEdit => _history.CanUndo;

    /// <summary>And one in front to do again.</summary>
    public bool CanRedoEdit => _history.CanRedo;

    /// <summary>
    /// The history as a list somebody can read and point at, the take itself at the top of it.
    /// </summary>
    /// <remarks>
    /// The first line is the take as it was found, so standing on it is standing on the file the
    /// shelf holds. Every line under it is one step, and the ones past where you are standing
    /// are still there until something new is done.
    ///
    /// The rows outlive what happens to them, which is why they are told rather than made again:
    /// see <see cref="TakeStepRow"/>.
    /// </remarks>
    public ObservableCollection<TakeStepRow> EditSteps { get; } = new();

    /// <summary>
    /// Which line of the history is the take you are looking at.
    /// </summary>
    /// <remarks>
    /// Written to as well as read, since picking a line in the list is how somebody walks the
    /// history with a pointer rather than a key. The walk is a file being rebuilt, so it is
    /// started and not waited for, and the guard is what keeps the list agreeing with the
    /// history rather than starting a second walk on the way back.
    ///
    /// Nothing picked at all is ignored rather than read as the top of the list. A list says
    /// minus one whenever it is holding nothing, and walking the take back to how it was found
    /// because a row went away for a moment is not what anybody meant.
    /// </remarks>
    public int EditAt
    {
        get => _history.Done;
        set
        {
            if (!_walking && value >= 0 && value != _history.Done) _ = GoToStepAsync(value);
        }
    }

    /// <summary>True while the history is being wound, so the list writing back is ignored.</summary>
    private bool _walking;

    /// <summary>
    /// The working copy is about to be written over, so anything holding it open should let go.
    /// </summary>
    /// <remarks>
    /// The editor plays its preview off the working copy, and a file that is being played is a
    /// file that is open, which on Windows is a file that will not be rewritten. Every tool the
    /// window presses already stops the preview on the way past; the ones that do not go through
    /// a button are undo, redo and picking a line of the history, which are a keystroke and a
    /// list, and this is what reaches them.
    /// </remarks>
    public event Action? TakeRewriting;

    /// <summary>
    /// Does one of the editor's tools to the working copy and writes it down as a step.
    /// </summary>
    /// <remarks>
    /// The region arrives as fractions, which is what the picture deals in, and is written down
    /// in frames against the take **as it is now**: a trim changes the length underneath, and a
    /// step recorded as a fraction would mean somewhere else the moment one happened.
    ///
    /// The work is off the drawing thread, since a long take takes a moment and a page that
    /// stopped while it did would read as a program that had hung. Nothing is announced to the
    /// rest of the application: what changed is a copy, and the shelf's take is still what
    /// everything else is playing until Save.
    /// </remarks>
    /// <param name="kind">Which edit.</param>
    /// <param name="startFraction">Where the selection starts, nought to one.</param>
    /// <param name="endFraction">Where it ends.</param>
    /// <returns>True when the working copy changed, so the editor can redraw.</returns>
    public async Task<bool> EditAsync(TakeEditKind kind, double startFraction, double endFraction)
    {
        if (_copy.Path is not { } path || CurrentWaveform is not { } waveform) return false;

        long frames = waveform.TotalSamples;
        long from = (long)(Math.Clamp(startFraction, 0, 1) * frames);
        long to = (long)(Math.Clamp(endFraction, 0, 1) * frames);

        string word = _stepWords.For(kind);

        if (kind != TakeEditKind.Normalize && to <= from)
        {
            Status = "Select a part of the take first.";
            return false;
        }

        if (kind == TakeEditKind.Trim && from == 0 && to >= frames)
        {
            Status = "The whole take is selected, so there is nothing to trim.";
            return false;
        }

        var step = new TakeStep(kind, from, to, NormalizeTargetDb);

        try
        {
            TakeRewriting?.Invoke();

            Status = word + "...";

            if (!await Task.Run(() => _steps.Run(step, path)))
            {
                Status = $"Already at {NormalizeTargetDb:0.0} dB, so nothing was done";
                return false;
            }

            _history.Add(step);

            await Drawn();

            Status = $"{word}. {Outstanding()}";
            return true;
        }
        catch (Exception ex)
        {
            Status = $"{word} failed: {ex.Message}";
            return false;
        }
    }

    /// <summary>Stands one step further back.</summary>
    public Task UndoEditAsync() => GoToStepAsync(_history.Done - 1);

    /// <summary>And one step further forward.</summary>
    public Task RedoEditAsync() => GoToStepAsync(_history.Done + 1);

    /// <summary>
    /// Walks the history to a place in it, rebuilding the working copy to match.
    /// </summary>
    /// <remarks>
    /// **Going back is a fresh copy of the take with the steps before that point done again.**
    /// No edit here has an undo of its own and none needs one, which is the whole reason the
    /// history is a list of what was asked for rather than a pile of copies of the audio: a take
    /// is up to a hundred megabytes, so twenty steps kept as audio is two gigabytes.
    ///
    /// What it costs is the replay, which is a copy and a pass per step: on the takes anybody
    /// records that is a fraction of a second, and on a very long one with a long history it is
    /// not. Going forward by one is the exception and is the common case, since that is what
    /// redo is: the step is simply done to what is already there.
    /// </remarks>
    /// <param name="done">How many steps should have been done.</param>
    public async Task GoToStepAsync(int done)
    {
        if (_copy.Path is not { } path) return;

        done = Math.Clamp(done, 0, _history.Steps.Count);

        if (done == _history.Done) return;

        var walk = _history.Toward(done);

        if (walk.Idle && done == _history.Done) return;

        try
        {
            TakeRewriting?.Invoke();

            Status = walk.Fresh ? "Going back..." : "Doing it again...";

            await Task.Run(() =>
            {
                if (walk.Fresh) _copy.Fresh();

                foreach (var step in walk.Steps) _steps.Run(step, path);
            });

            _history.GoTo(done);

            await Drawn();

            Status = done == 0
                ? $"'{Name(SelectedRecordingForEdit)}' as it was found"
                : Outstanding();
        }
        catch (Exception ex)
        {
            Status = $"Could not go back: {ex.Message}";
        }
    }

    /// <summary>
    /// Puts the working copy over the take on the shelf, which is the only thing here that does.
    /// </summary>
    /// <remarks>
    /// The audio under everything that plays this file has changed, so
    /// <see cref="RecordingChanged"/> carries the path and whoever is holding it in memory reads
    /// it again. The history is emptied rather than kept: what is on the shelf is now what is on
    /// the screen, so there is nothing outstanding to go back past, and a step that claimed to
    /// undo a saved edit would be undoing it against the wrong original.
    /// </remarks>
    /// <returns>True when the take was replaced.</returns>
    public async Task<bool> SaveEditAsync()
    {
        if (SelectedRecordingForEdit is not { } recording || !HasEdits) return false;

        try
        {
            TakeRewriting?.Invoke();

            Status = "Saving...";

            if (!await Task.Run(() => _copy.Keep()))
            {
                Status = $"'{recording.Name}' could not be saved";
                return false;
            }

            recording.DurationMs = ReadDurationMs(recording.FilePath);

            RecordingChanged?.Invoke(this, recording.FilePath);

            _history.Clear();
            Shown();

            Status = $"Saved '{recording.Name}'";
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Save failed: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Throws every step away and puts the working copy back to what the shelf holds.
    /// </summary>
    /// <remarks>
    /// The same act as walking the history to the top of it, and then the way forward goes as
    /// well, which is the difference between this and an undo: this says the whole session was
    /// not wanted.
    /// </remarks>
    public async Task RevertEditAsync()
    {
        if (_history.Steps.Count == 0) return;

        await GoToStepAsync(0);

        _history.Clear();
        Shown();

        Status = $"'{Name(SelectedRecordingForEdit)}' as it was found";
    }

    /// <summary>
    /// Lets the working copy go, which is what closing the editor does.
    /// </summary>
    /// <remarks>
    /// The copy is deleted rather than kept for next time. Keeping it would mean a take that
    /// opens holding somebody's abandoned edits with nothing on the screen saying so, and the
    /// file is the size of the take.
    /// </remarks>
    public void EndEdit()
    {
        _copy.Close();
        _history.Clear();

        EditSteps.Clear();
        Moved();
    }

    /// <summary>How many steps are waiting to be saved, in words.</summary>
    /// <returns>The sentence, which is never blank.</returns>
    private string Outstanding() =>
        _history.Done == 1 ? "1 change to save" : $"{_history.Done} changes to save";

    /// <summary>The take's name, or a word for the one that is not there.</summary>
    /// <param name="recording">The take, or nothing.</param>
    /// <returns>What to call it in a sentence.</returns>
    private static string Name(Recording? recording) => recording?.Name ?? "the take";

    /// <summary>
    /// Reads the working copy again and says everything about the history moved.
    /// </summary>
    /// <remarks>
    /// The picture comes off the copy rather than off the shelf, which is the whole of what
    /// makes an unsaved edit visible: the take itself has not changed and must not be read here
    /// or the screen would show the edit undone the moment it was made.
    /// </remarks>
    private async Task Drawn()
    {
        if (_copy.Path is not { } path) return;

        CurrentWaveform = await Task.Run(() => _waveformService.AnalyzeFile(path));

        Shown();
    }

    /// <summary>Builds the history list again and says what moved with it.</summary>
    private void Shown()
    {
        _walking = true;

        while (EditSteps.Count > _history.Steps.Count + 1) EditSteps.RemoveAt(EditSteps.Count - 1);

        if (EditSteps.Count == 0) EditSteps.Add(new TakeStepRow("Original", true));

        for (int at = 0; at < _history.Steps.Count; at++)
        {
            string said = _stepWords.For(_history.Steps[at].Kind);
            bool done = at < _history.Done;

            if (at + 1 < EditSteps.Count) EditSteps[at + 1].Say(said, done);
            else EditSteps.Add(new TakeStepRow(said, done));
        }

        _walking = false;

        Moved();
    }

    /// <summary>Says that everything about the editor's state may have moved.</summary>
    private void Moved()
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(EditingPath));
        OnPropertyChanged(nameof(HasEdits));
        OnPropertyChanged(nameof(CanUndoEdit));
        OnPropertyChanged(nameof(CanRedoEdit));
        OnPropertyChanged(nameof(EditAt));
    }

    /// <summary>Goes back a step, for the button on the editor.</summary>
    public IAsyncRelayCommand UndoEditCommand => new AsyncRelayCommand(UndoEditAsync);

    /// <summary>And forward again.</summary>
    public IAsyncRelayCommand RedoEditCommand => new AsyncRelayCommand(RedoEditAsync);

    /// <summary>Puts the working copy over the take.</summary>
    public IAsyncRelayCommand SaveEditCommand => new AsyncRelayCommand(async () => await SaveEditAsync());

    /// <summary>Throws every step away.</summary>
    public IAsyncRelayCommand RevertEditCommand => new AsyncRelayCommand(RevertEditAsync);

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
            recording.UsedBy = Usage.Describe(UsersOf(recording));
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

    /// <summary>
    /// What plays a take here, which is a real player or one that cannot sound anything.
    /// </summary>
    /// <remarks>
    /// **A page built with no audio under it is a real thing**, which is what the bench that
    /// measures this page is, so the answer is said out loud rather than left as the shape two
    /// arguments happen to take when they are missing. A player is never half wired.
    /// </remarks>
    /// <param name="recordings">How a recording is made to sound, or nothing.</param>
    /// <param name="takes">The bus a take goes onto, or nothing.</param>
    private static Waveform.WaveformPlayer Playing(
        JingleBox2.Audio.Interfaces.IRecordingSource? recordings,
        JingleBox2.Audio.Interfaces.IOutputBus? takes) =>
        recordings is { } source && takes is { } bus
            ? new Waveform.WaveformPlayer(source, bus)
            : Waveform.WaveformPlayer.Silent();

    /// <summary>The editing window, over this page's own audio where there is any.</summary>
    /// <remarks>
    /// The same answer as <see cref="Playing"/> one layer up: the window is handed both halves or
    /// neither, so it cannot be built holding one of them.
    /// </remarks>
    private RecordingEditDialog Editor() =>
        _recordings is { } source && _takes is { } bus
            ? new RecordingEditDialog(source, bus)
            : new RecordingEditDialog();

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
        recording.UsedBy = Usage.Describe(used);

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
    bool Shortcuts.Interfaces.IShortcutContext.Can(Shortcuts.Enums.ShortcutAction action) =>
        action switch
        {
            Shortcuts.Enums.ShortcutAction.Undo => IsEditing ? CanUndoEdit : CanUnbin,
            Shortcuts.Enums.ShortcutAction.Redo => IsEditing && CanRedoEdit,
            Shortcuts.Enums.ShortcutAction.Save => IsEditing && HasEdits,
            _ => false
        };

    /// <inheritdoc/>
    /// <remarks>
    /// **Undo means the editor while the editor is open and the bin otherwise**, which is the
    /// rule this whole mechanism exists for: the keystroke belongs to whatever you are looking
    /// at. The editor's window answers with this page, since the window's own settings are what
    /// the walk outwards from the keyboard reaches.
    ///
    /// Each one is started rather than waited for. Going back a step is a file being rebuilt,
    /// and a keystroke that held the drawing thread while it happened would read as a program
    /// that had hung.
    /// </remarks>
    void Shortcuts.Interfaces.IShortcutContext.Do(Shortcuts.Enums.ShortcutAction action)
    {
        switch (action)
        {
            case Shortcuts.Enums.ShortcutAction.Undo when IsEditing:
                _ = UndoEditAsync();
                break;

            case Shortcuts.Enums.ShortcutAction.Undo:
                Unbin();
                break;

            case Shortcuts.Enums.ShortcutAction.Redo:
                _ = RedoEditAsync();
                break;

            case Shortcuts.Enums.ShortcutAction.Save:
                _ = SaveEditAsync();
                break;
        }
    }

    /// <summary>Where a deleted take waits, beside the recordings rather than inside them.</summary>
    /// <remarks>
    /// Beside, so that everything reading the shelf sees a folder of recordings and nothing
    /// else. Inside, every reader would have to learn to skip a folder, and one of them would
    /// not.
    /// </remarks>
    public static string Bin => Path.Combine(new Files.AppFolder().Path(), "recordings", "..", "deleted");

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

        Diagnostics.Log.Write(Diagnostics.Enums.LogArea.App, () => "recordings: '" + recording.Name + "' went into the bin");
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

            Diagnostics.Log.Write(Diagnostics.Enums.LogArea.App, () => "recordings: '" + name + "' came back out of the bin");

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

    /// <summary>False on a system with no graph to patch, and the line stays hidden.</summary>
    public bool IsRoutingAvailable => _routing.IsAvailable;

    /// <summary>
    /// Where the input is taken from, in words, for the page that says it rather than sets it.
    /// </summary>
    /// <remarks>
    /// **RECORD reads and the mixer chooses.** The picker is at the foot of the IN strip, since
    /// that is the strip it is about, and one choice offered in two places is two ways of doing
    /// one thing that eventually answer differently. Nothing chosen says so plainly: it is the
    /// ordinary state at startup and the meter reading nothing is the other half of it.
    /// </remarks>
    public string CaptureFrom => SelectedRoute?.Display ?? "Nothing yet. Pick a source on the mixer, at the foot of the IN strip.";

    /// <inheritdoc/>
    /// <remarks>
    /// The tools take a moment, so this happens off the UI thread.
    ///
    /// The route on show is matched to the current one by node rather than by object, because
    /// the list is read afresh every time and the object from before is not in it.
    ///
    /// One reading at a time: the timer fires every two seconds and the tools can take longer
    /// than that, so without the guard the readings would pile up on each other.
    ///
    /// **Every reading ends by asking for the arrangement, which is what covers starting up.**
    /// A source is chosen once and read back for ever after, and this page only ever told the
    /// input path about a choice: at startup there is none, since the source comes off the graph
    /// rather than out of the picker, so nothing asked for it to be taken off its own output and
    /// it sat on its own speakers until something else happened to change. Asking here costs
    /// nothing where the arrangement already stands, since the path answers the same question
    /// with the same answer and moves nothing, and it is the retry where the last attempt was
    /// refused. Silently, through <see cref="Arrange"/> rather than <see cref="Agree"/>, since
    /// nobody did anything and there is nothing to tell them.
    /// </remarks>
    public async void RefreshRoutes()
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

            PreferWhatIsPlaying();
            RestorePreferred(current);

            Arrange();

            await HoldAsideAsync();
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
    /// Keeps a source that is supposed to be aside off its own output, on the clock that is
    /// already keeping the capture standing.
    /// </summary>
    /// <remarks>
    /// **Taking a source aside is not a thing that stays done.** The graph belongs to the machine
    /// rather than to this application, and its session manager wires a stream back to the
    /// speakers whenever the stream is remade: a new tab, a page reloaded, a program moved
    /// between outputs. The capture was already put back every couple of seconds for exactly that
    /// reason and the other half of the arrangement was not, so the source came back onto its own
    /// output while it was still arriving here. What that sounds like is the same audio twice
    /// with a buffer between the two, which is how it was reported.
    ///
    /// Said on the status line only where something really had come back, since a line that
    /// appeared every two seconds saying nothing happened would be worse than none. Off the
    /// drawing thread, like everything else here that runs the tools.
    ///
    /// **Nothing is held while a route is still being applied**, and that is about the words as
    /// much as the wiring. Applying one runs the tools off this thread and writes its own line
    /// when it comes back, so a hold that ran in the middle of it had the useful sentence, that a
    /// source crept back and was taken off again, overwritten a moment later by the routine one.
    /// Which of the two landed last depended on how busy the machine was. The reading happens
    /// every couple of seconds, so what is skipped here is said by the next one.
    /// </remarks>
    private async System.Threading.Tasks.Task HoldAsideAsync()
    {
        if (_applyingRoute) return;

        if (SelectedRoute is not { } source) return;

        if (await Task.Run(() => _input.Hold()))
            Status = $"{source.Display} had got back onto its own output and was taken off again.";
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
        OnPropertyChanged(nameof(CaptureFrom));

        Listening();

        if (_readingRoute) return;

        if (value == null)
        {
            Agree();

            return;
        }

        _preferredRoute = value;

        ApplyRoute(value, announce: true);

        Agree();
    }

    /// <summary>Where a source is sent so nobody hears it, or nothing on a machine with a graph.</summary>
    private ISilentOutput? _silent;

    /// <summary>
    /// Tells the page where a source can be sent to be unheard.
    /// </summary>
    /// <remarks>
    /// Handed in rather than made here, because it is the same object the routing was given: two
    /// of them over one setting would be two answers to which output is the quiet one, and the
    /// picker would then be setting something the routing never reads.
    /// </remarks>
    /// <param name="silent">The choice and the list it comes from.</param>
    public void UseSilentOutput(ISilentOutput silent)
    {
        _silent = silent;

        OnPropertyChanged(nameof(NeedsSilentOutput));
        OnPropertyChanged(nameof(SilentOutputs));
        OnPropertyChanged(nameof(SilentOutput));
    }

    /// <inheritdoc/>
    public bool NeedsSilentOutput => SilentOutputs.Count > 0;

    /// <inheritdoc/>
    /// <remarks>
    /// **Never this application's own output**, which it used to offer and which is the single
    /// worst answer in the list. A source is sent somewhere so that nobody hears it; sent to the
    /// device JingleBox2 is playing through it is not quieted at all, it arrives on top of
    /// everything else and out of the same speakers. The picker was showing the Model 12 while the
    /// Model 12 was the output in SETTINGS.
    ///
    /// It had the settings in its hand the whole time and only ever read the half about which
    /// output is the quiet one, never the half about which output is ours. Audio goes out as well
    /// as in and a picker about outputs has to know both.
    ///
    /// By name, since the list and the output picker are two different enumerations and the name
    /// is the only half they share: the same trade-off
    /// <see cref="Audio.Routing.Interfaces.IAudioRouting.IsOurOutput"/> already names.
    /// </remarks>
    public IReadOnlyList<Audio.Records.AudioEndpoint> SilentOutputs
    {
        get
        {
            var all = _silent?.Outputs ?? Array.Empty<Audio.Records.AudioEndpoint>();

            if (string.IsNullOrWhiteSpace(PlayingOut)) return all;

            var kept = new List<Audio.Records.AudioEndpoint>(all.Count);

            foreach (var one in all)
                if (!string.Equals(one.Name.Trim(), PlayingOut.Trim(), StringComparison.OrdinalIgnoreCase))
                    kept.Add(one);

            return kept;
        }
    }

    /// <inheritdoc/>
    public Audio.Records.AudioEndpoint? SilentOutput
    {
        get
        {
            if (_silent?.Chosen is not { } chosen) return null;

            foreach (var output in SilentOutputs)
                if (string.Equals(output.Id, chosen, StringComparison.Ordinal)) return output;

            return null;
        }
        set
        {
            if (_silent == null) return;

            _silent.Chosen = value?.Id;

            OnPropertyChanged();

            Agree();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Kept on the recorder rather than here, since the capture is what pushes the audio and the
    /// switch has to survive this page being built again. Setting it while nothing is captured is
    /// not refused: the path opens with the capture.
    /// </remarks>
    public bool Hearing
    {
        get => _recordingService.Hearing;

        set
        {
            if (_recordingService.Hearing == value) return;

            _recordingService.Hearing = value;

            OnPropertyChanged();

            Standing();
            Agree();
        }
    }

    /// <summary>Whether the switches are holding the input open by themselves.</summary>
    private bool _standing;

    /// <summary>
    /// Holds the input open for as long as either switch is on, whatever page is in front.
    /// </summary>
    /// <remarks>
    /// **An arrangement is not a page.** The input is held open by whoever is showing its meter,
    /// which is right for a meter and wrong for these two: Hear it says a source is coming through
    /// the desk and Only here says it is coming through nothing else, and neither of those has
    /// anything to do with which tab is in front. Walking to the tracker or to the pads took the
    /// last watcher away, the input closed a second later, and the source stopped being heard: it
    /// was reported as those pages stopping the sound, and RECORD being the only page that did
    /// not, which is exactly the list of pages that show the meter.
    ///
    /// Counted rather than switched, through the same door a page goes through, so this and a page
    /// showing the meter can both be reasons at once and neither takes the input away from the
    /// other. One reason at most from here, which is what <see cref="_standing"/> is: the two
    /// switches are one arrangement.
    ///
    /// **The graph is watched for as long as the arrangement stands**, and the settling clock
    /// runs with it. Reading the graph is what puts the chosen source back on the capture and
    /// holds it off its own output against a session manager that keeps rewiring it, and neither
    /// is worth anything once nothing is chosen and nothing is being listened to. So the watch,
    /// the clock and the input go up and come down together.
    /// </remarks>
    private void Standing()
    {
        bool wanted = SelectedRoute is not null || Hearing;

        if (wanted == _standing) return;

        _standing = wanted;

        if (wanted)
        {
            Watch();
            WatchRoutes(reading: false);
            Settle();

            return;
        }

        StopSettling();
        LetGo();
        LetRoutesGo();
    }

    /// <summary>The clock that keeps asking while the graph is still moving, or nothing.</summary>
    private DispatcherTimer? _settling;

    /// <summary>When the settling began, so it can stop on its own.</summary>
    private readonly System.Diagnostics.Stopwatch _settled = new();

    /// <summary>How often the arrangement is checked while the graph is still settling.</summary>
    /// <remarks>
    /// Fast enough that nobody hears the gap, slow enough that it is a handful of tool runs
    /// rather than a spin: a fifth of a second is under what a hand notices and is four readings
    /// in the time the ordinary clock takes one.
    /// </remarks>
    private static readonly TimeSpan SettleInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>How long that goes on before the ordinary clock is left to it.</summary>
    /// <remarks>
    /// Long enough for a capture to appear in the graph and for the session manager to finish
    /// whatever it was doing, and short enough that a machine where this never succeeds is not
    /// running tools at this rate for the rest of the session.
    /// </remarks>
    private static readonly TimeSpan SettleFor = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Keeps asking for a few seconds after the switch is thrown, while the graph is still moving.
    /// </summary>
    /// <remarks>
    /// **One shutter is not enough, because the same gesture that closes it also moves the graph
    /// it is closing on.** Turning the switch on opens the input, and this application's capture
    /// appears in the graph a moment after that is asked for; turning it off puts the source's own
    /// links back, and the session manager takes a moment to make them. So the reading taken at
    /// the instant of the gesture is a reading of a graph that has not finished changing, and the
    /// ordinary clock is two seconds away, which is two seconds of the source playing in two
    /// places.
    ///
    /// It costs a couple of tool runs a fifth of a second for four seconds and then stops itself.
    /// Nothing here is a retry of a failure: each reading takes off whatever has come back since
    /// the last one, so a graph that settles at once is three readings that find nothing.
    /// </remarks>
    private void Settle()
    {
        _settled.Restart();

        if (_settling != null)
        {
            _settling.Start();

            return;
        }

        _settling = new DispatcherTimer { Interval = SettleInterval };

        _settling.Tick += (_, _) =>
        {
            if (_settled.Elapsed > SettleFor)
            {
                StopSettling();

                return;
            }

            HoldAsideNow();
        };

        _settling.Start();
    }

    /// <summary>Stops asking, for a switch that has gone off or an arrangement that is made.</summary>
    private void StopSettling()
    {
        _settling?.Stop();
        _settled.Reset();
    }

    /// <summary>Whether a reading is already out, so they cannot pile up on each other.</summary>
    private bool _holding;

    /// <summary>
    /// Takes off whatever the source has got back onto, off the drawing thread.
    /// </summary>
    /// <remarks>
    /// One at a time: the tools take longer than the settling clock's own interval on a busy
    /// machine, and without the guard the readings would queue up behind each other and go on
    /// long after the graph had stopped moving.
    /// </remarks>
    private async void HoldAsideNow()
    {
        if (_holding) return;

        try
        {
            _holding = true;

            await HoldAsideAsync();
        }
        finally
        {
            _holding = false;
        }
    }

    /// <summary>
    /// What this application plays out of, by name, so a loop can be told from a second card.
    /// </summary>
    /// <remarks>
    /// Told rather than looked up, because the page has no engine and no device list: whoever owns
    /// the output picker knows which one is chosen and is the only thing that hears it move.
    ///
    /// **Said again whenever it moves**, or the answer goes stale in the one direction that
    /// matters: an output changed to the very card the source is a monitor of would go on being
    /// heard, which is the loop this exists to refuse. See <see cref="OutputMoved"/>.
    /// </remarks>
    public string? PlayingOut
    {
        get => playingOut;
        set
        {
            if (string.Equals(playingOut, value, StringComparison.Ordinal)) return;

            playingOut = value;

            Listening();

            OnPropertyChanged(nameof(SilentOutputs));
            OnPropertyChanged(nameof(NeedsSilentOutput));
        }
    }

    /// <summary>Backing field for <see cref="PlayingOut"/>.</summary>
    private string? playingOut;

    /// <inheritdoc/>
    /// <remarks>
    /// Two questions and the second one belongs to the routing. Anything that is not an output's
    /// own playback cannot come back round however it is named, which is a microphone, a line in
    /// and a program; what an output is playing can, but only where that output is the one this
    /// application plays out of. Another output's is the ordinary way anybody records a second
    /// program and goes back to nowhere.
    ///
    /// **Which output it is is asked of the subsystem rather than worked out here**, because the
    /// names come from whatever wired the machine up and only the subsystem knows how its own
    /// work: see <see cref="Audio.Routing.Interfaces.IAudioRouting.IsOurOutput"/>. It has three
    /// answers and the comparison is against <c>false</c> deliberately, so that cannot tell falls
    /// in with ours: being wrong that way is a switch that does nothing, and being wrong the other
    /// way is a room full of feedback at whatever the master is set to.
    ///
    /// Reading the kind alone and refusing every monitor is right on a machine with one output
    /// and silently wrong on a machine with two, where another card's monitor is a perfectly good
    /// source.
    /// </remarks>
    public bool CanHear => _input.CanHear(SelectedRoute, PlayingOut);

    /// <summary>
    /// Says again whether the chosen source can be listened to, and leaves the capture off the
    /// recorder's bus where it cannot.
    /// </summary>
    /// <remarks>
    /// The switch itself is not touched: it says whether the recorder is heard, and what the
    /// recorder is carrying is a separate question. Picking the output this application plays
    /// through while Hear it is already on would otherwise make a loop, so the capture is taken
    /// off the bus instead and the status line says why.
    ///
    /// **Said for every way the source can move, including the ones nobody chose.** The graph is
    /// read on a clock and puts the picker back to whatever the machine says is current, and that
    /// path deliberately skips connecting, since it is answering a reading rather than making
    /// one, and it must not skip this with it: the picker can land on the output this application
    /// plays through without anybody having touched the page, and the capture has to come off the
    /// bus just the same.
    /// </remarks>
    private void Listening()
    {
        OnPropertyChanged(nameof(CanHear));

        _recordingService.HearsCapture = CanHear;
        _recordingService.HearsTheRoom =
            SelectedRoute?.Kind == Audio.Routing.Enums.AudioRouteKind.Input;

    }

    /// <summary>
    /// Says why listening stopped, in words somebody can act on.
    /// </summary>
    /// <remarks>
    /// **What it says is what to do, not what happened.** This is for whoever has just discovered
    /// a monitor path by accident, so naming the phenomenon would be the least useful sentence
    /// available: the two things that actually end it are headphones and distance.
    ///
    /// Handed to the drawing thread, since it arrives on the one the capture is on and everything
    /// under it draws.
    /// </remarks>
    private void Ringing() => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        OnPropertyChanged(nameof(Hearing));

        Status = "That started to ring, so listening stopped. It happens when what is coming out of "
            + "the speakers reaches the microphone again. Use headphones, or move the microphone "
            + "away from the speakers, then tick Hear it once more.";
    });

    /// <summary>
    /// Makes the machine agree with the source and the switch, and says what happened.
    /// </summary>
    /// <remarks>
    /// **The one place this page tells the input path anything.** Three things move the two facts
    /// it holds and every one of them ends here, so what the machine is doing can never disagree
    /// with what the strip is showing. What it comes to is
    /// <see cref="Audio.Routing.Interfaces.IInputPath"/>'s business and none of it is here: this
    /// hands over the two facts and puts the sentence that comes back on the status line.
    ///
    /// It was <c>Aside</c> and it was the act itself, giving a source back and taking the chosen
    /// one off its own output. That is the module's now, which is what leaves this two lines.
    /// </remarks>
    private void Agree()
    {
        Arrange();

        Said(connected: true);
    }

    /// <summary>
    /// Makes the machine agree with the source and the switch, without saying anything.
    /// </summary>
    /// <remarks>
    /// **The act on its own, for the reading pass, because the sentence is not wanted there.**
    /// <see cref="Said"/> writes the status line whenever it has something to say, which is right
    /// where somebody has just done something and wrong on a clock: asked every two seconds it
    /// would pin the bar to the input's own sentence and rub out whatever else the page had put
    /// there. The arrangement itself costs nothing when it already stands, so the two want
    /// different rates and are two calls.
    /// </remarks>
    private void Arrange() => _aside = _input.Set(SelectedRoute, Hearing, PlayingOut);

    /// <summary>What became of taking the chosen source off its own output.</summary>
    /// <remarks>
    /// Kept because the act and the wording happen at different moments: the arrangement is made
    /// the instant somebody chooses, and whether the source is really giving anything is only
    /// known once a connection has come back. Without this the second sentence would have to make
    /// the arrangement again to know what to say, which is giving a source back and taking it off
    /// again for the sake of a word.
    /// </remarks>
    private Audio.Routing.Enums.InputAside _aside;

    /// <summary>
    /// Puts the input channel's one sentence on the status line.
    /// </summary>
    /// <remarks>
    /// **Said twice on a route change and from one place both times, which is not the fault this
    /// replaced.** The first is the outcome as it is known at once and the second is the same
    /// sentence with the connection's answer in it, so the later one is strictly better informed
    /// rather than merely later. What was there before was five different writers whose order
    /// depended on when a thread came back.
    /// </remarks>
    /// <param name="connected">Whether the source is really giving anything yet.</param>
    private void Said(bool connected)
    {
        string said = _words.Line(SelectedRoute, Hearing, CanHear, _aside, connected);

        if (said.Length > 0) Status = said;
    }

    /// <summary>The one place the input channel's own wording lives.</summary>
    private readonly Audio.Routing.Interfaces.IInputWords _words = new Audio.Routing.InputWords();

    /// <summary>
    /// Makes the arrangement again against the output that has just been picked.
    /// </summary>
    /// <remarks>
    /// **Where the sound comes out is half of what taking a source aside means.** A source pointed
    /// at the input is heard through this application and nowhere else, and what "here" is, is the
    /// output in SETTINGS: picked another one and the arrangement is over a device nobody is
    /// listening to, with the source still unplugged from its own. Worse where the new output is
    /// the one the source was taken off or the one it was sent to, which is a source silenced into
    /// the very thing it is being played back through.
    ///
    /// **So the arrangement is made again rather than thrown away.** Clearing the source outright
    /// means the input silently emptying itself every time the output picker is touched, with the
    /// source handed back to its own speakers, which is heard as the sound coming back.
    ///
    /// **The one case that really does end it is the new output being the source**, which is
    /// hearing an output through itself. The path answers that through <see cref="CanHear"/>, and
    /// it answers it with the new output rather than the old, since that is what has just moved:
    /// so it puts the source back and says why, which is the one thing here worth a line on the
    /// status bar. Everything else is said in the log, since re-taking a source off an output it
    /// was already off is not news.
    /// </remarks>
    public void OutputMoved()
    {
        Listening();

        if (SelectedRoute is not { } source) return;

        Agree();

        if (CanHear)
        {
            Diagnostics.Log.Write(
                Diagnostics.Enums.LogArea.Audio,
                () => "routing: the output moved, so the source was taken off its own again");

            return;
        }

        Status = source.Display + " is what this application now plays out of, so it has been "
            + "put back: hearing an output through itself is a loop.";

        Diagnostics.Log.Write(
            Diagnostics.Enums.LogArea.Audio,
            () => "routing: the output moved onto the source, so the source was put back");
    }

    /// <summary>Whether the first reading of the graph has already been answered.</summary>
    /// <remarks>
    /// Once a session and not once a reading, so a source somebody chose is never overruled and
    /// nothing is quietly re-pointed while they are working. The graph is read every two seconds
    /// while a page carrying the picker is up.
    /// </remarks>
    private bool _preferredOnce;

    /// <summary>
    /// Points the input at what the machine is playing, the first time the graph is read.
    /// </summary>
    /// <remarks>
    /// **What is playing is what somebody almost always wants**, which is the whole reason the
    /// monitor of an output is offered at all: a jingle grabbed off a browser, a bed off a
    /// player, a caller off a telephone application. A capture device is the other case and it
    /// is one somebody goes and picks; picking that for them would mean the first take of a
    /// session is silence off a microphone nobody plugged in.
    ///
    /// Only where nothing has been chosen and only once, so what somebody picked stands for the
    /// rest of the session, and it does not fight the sound server: it goes through the same
    /// path a hand does, which is the same handler a picker goes through.
    /// </remarks>
    private void PreferWhatIsPlaying()
    {
        if (_preferredOnce) return;

        _preferredOnce = true;

        if (_preferredRoute != null) return;

        var chosen = Chosen() ?? Routes.FirstOrDefault(
            r => r.Kind == Audio.Routing.Enums.AudioRouteKind.Monitor);

        if (chosen == null) return;

        SelectedRoute = chosen;
    }

    /// <summary>
    /// The capture device named in the settings, where the machine is offering it.
    /// </summary>
    /// <remarks>
    /// **Audio goes in as well as out, and only the output half was being read.** There is an
    /// input in the settings and this page has always had it, and the first source was picked as
    /// whatever an output happened to be playing whether or not somebody had chosen a microphone.
    /// So a machine set up to record a microphone opened on the desktop's own playback, which is
    /// a picker that ignores the setting two pages away that exists to answer exactly this.
    ///
    /// By name, which is how <see cref="JingleBox2.Config.AppConfig.RecordInputDevice"/> is
    /// stored and for the reason written there: a device's number moves when hardware is plugged
    /// in and its name does not.
    ///
    /// Nothing where no input has been chosen or where the one that was is not here, and then
    /// what an output is playing is still the answer, which is what this did before and is what
    /// somebody usually wants on a machine that has never been set up.
    /// </remarks>
    private Audio.Routing.Records.AudioRoute? Chosen()
    {
        string wanted = _cfg.RecordInputDevice;

        if (string.IsNullOrWhiteSpace(wanted)) return null;

        foreach (var route in Routes)
        {
            if (route.Kind != Audio.Routing.Enums.AudioRouteKind.Input) continue;

            if (string.Equals(route.Name.Trim(), wanted.Trim(), StringComparison.OrdinalIgnoreCase))
                return route;
        }

        return null;
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
    ///
    /// **The loop is said again at the end, and it has to be, because this is what was writing
    /// over it.** Choosing a source says whether it can be heard at once, since by then the audio
    /// would already be going round; this then ran and put "Taking audio from" and "Recording
    /// from" on the line after it, so the one sentence explaining why nothing is heard was on the
    /// screen for as long as it took a thread to be given a core. From a chair that is a source
    /// that is silent with nothing anywhere saying why, which is exactly what the switch was
    /// reported as.
    ///
    /// It cost a test the day it was found, and the way it cost it is worth keeping: the test
    /// passed by relying on this method still being in flight when the source changed under it, so
    /// it was green for a reason that had nothing to do with what it was about. **A race can be
    /// stably won as well as stably lost**, and the tell was that it failed three times out of
    /// three after a change that only made the work either side of it a little longer.
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
            if (announce) Status = _words.Taking(route);

            bool connected = await Task.Run(() => _routing.Connect(route));

            _readingRoute = true;
            var showing = Routes.FirstOrDefault(r => r.Node == route.Node);
            if (connected && showing != null) SelectedRoute = showing;
            _readingRoute = false;

            Listening();

            Said(connected);
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

    /// <summary>How many pages showing the input's meter are on screen.</summary>
    private int _watching;

    /// <summary>Started when the last watcher goes, so a re-template does not close the input.</summary>
    /// <remarks><inheritdoc cref="IInputWatch.LetGo" path="/remarks"/></remarks>
    private DispatcherTimer? _closingInput;

    /// <summary>
    /// How long a departure has to last before the input is really let go of. A second is long
    /// enough to outlast a re-template and short enough that nobody is left holding the
    /// microphone after they have walked away.
    /// </summary>
    private static readonly TimeSpan InputCloseDelay = TimeSpan.FromSeconds(1);

    /// <inheritdoc/>
    /// <remarks>
    /// The second watcher and every one after it costs a comparison: the input is already open
    /// and opening it again would close and reopen the capture, which is where the routing is
    /// lost.
    /// </remarks>
    public void Watch()
    {
        _watching++;
        _closingInput?.Stop();

        if (_watching > 1) return;

        try
        {
            _recordingService.StartMonitoring();
            StartLevelPolling();
        }
        catch (Exception ex)
        {
            Status = $"Could not open the input: {ex.Message}";
        }
    }

    /// <inheritdoc/>
    public void LetGo()
    {
        if (_watching > 0) _watching--;
        if (_watching > 0) return;

        _closingInput ??= Closing();

        _closingInput.Stop();
        _closingInput.Start();
    }

    /// <summary>The clock that lets the input go, made once so it carries one handler.</summary>
    /// <remarks>
    /// Hung here rather than at each departure, because a handler added per call is a handler
    /// added per departure and the input would be closed as many times as the page had been
    /// left. That is the shape this codebase has already paid for elsewhere.
    /// </remarks>
    private DispatcherTimer Closing()
    {
        var timer = new DispatcherTimer { Interval = InputCloseDelay };

        timer.Tick += (_, _) =>
        {
            timer.Stop();

            if (_watching == 0) CloseInput();
        };

        return timer;
    }

    /// <summary>Lets the input go, unless a take is running, which keeps it open anyway.</summary>
    private void CloseInput()
    {
        _recordingService.StopMonitoring();

        if (_recordingService.IsRecording) return;

        StopLevelPolling();

        Level = 0;
        LevelLeft = 0;
        LevelRight = 0;
        IsClipping = false;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// **Deliberately not part of watching the input**, although it needs the input open. Reading
    /// the routes puts the preferred one back when the system has wired something else up, which
    /// is rewiring the machine's audio graph, so it belongs to a page carrying the picker rather
    /// than to every page showing a meter.
    ///
    /// The routes are read after the input is open and not before, because the recorder only
    /// appears in the graph once it is listening: reading first would show a graph with nothing
    /// to connect to.
    /// </remarks>
    public void WatchRoutes() => WatchRoutes(reading: true);

    /// <summary>
    /// Starts watching the graph, and says whether to read it at once as well as on the clock.
    /// </summary>
    /// <remarks>
    /// **A page wants the reading now and a switch does not.** A page carrying the picker has a
    /// list to fill and somebody looking at it, so it reads as it opens; a switch has neither,
    /// and the reading it would force lands a moment later and writes the chosen source back out
    /// of the picker. Thrown at the same moment somebody is changing the source, that arrives
    /// after the change and puts the old one back, which is a picker that will not stay put.
    ///
    /// The clock is the same either way and its first tick is a couple of seconds off, which is
    /// also about when the input a switch has just opened appears in the graph. So the switch
    /// loses nothing by waiting for it.
    /// </remarks>
    /// <param name="reading">Whether to read the graph at once as well as starting the clock.</param>
    private void WatchRoutes(bool reading)
    {
        _watchingRoutes++;

        if (_watchingRoutes > 1) return;

        if (reading) RefreshRoutes();

        StartRouteWatch();
    }

    /// <inheritdoc/>
    public void LetRoutesGo()
    {
        if (_watchingRoutes > 0) _watchingRoutes--;
        if (_watchingRoutes > 0) return;

        StopRouteWatch();
    }

    /// <summary>
    /// Puts back anything this application unplugged, on the way out.
    /// </summary>
    /// <remarks>
    /// **The one call in here that has to happen.** What was taken aside is somebody's own
    /// machine, so a browser left silent after this program has closed is the worst thing this
    /// feature could do, and there is nothing on the screen by then to say what happened.
    /// </remarks>
    private void GiveRoutesBack() => _input.GiveBack();

    /// <summary>How many pages carrying the source picker are on screen.</summary>
    /// <remarks>
    /// Counted for the reason the input's own watchers are: the mixer holds the picker and
    /// RECORD says what it reads, so both are reasons to keep reading the graph, and a flag
    /// would have whichever page left last stop the reading under the page still up.
    /// </remarks>
    private int _watchingRoutes;

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
    ///
    /// **Handed over rather than waited on.** It was a blocking call onto the drawing thread,
    /// which parks this timer's thread until a window gets round to it. That was survivable while
    /// the input was only open on the two pages that show its meter; it is not now that a switch
    /// holds it open on every page, since the tracker's drawing thread is the busiest in the
    /// application and every tick would take another thread out of the pool waiting for it.
    /// Nothing here needs the answer, since all it does is write four numbers, and a reading that
    /// lands a frame late is a meter rather than a fault.
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

            Dispatcher.UIThread.Post(() =>
            {
                Level = stereo.Peak;
                LevelLeft = stereo.Left;
                LevelRight = stereo.Right;
                IsClipping = clipping;

                if (recording) RecordingTime = _recordingTimer.Elapsed;
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

        ClearScratch(drop: true);

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

            string working = RecordingName.Trim();

            var written = await _recordingService.WriteTakeAsync(_scratch.Folder, ScratchName, ScratchCleanName);

            ScratchTake = Scratched(working, written.Path);
            ScratchClean = written.Clean == null ? null : Scratched(working + " (clean)", written.Clean);
            ScratchShowsClean = false;

            ReadScratchWaveform();

            Status = written.Clean == null
                ? "On the scratchpad. Save it under a name to keep it."
                : "On the scratchpad, through the chain and clean. Save it under a name to keep both.";

            if (clipped)
                Status = "The input clipped. Lower the input gain or the source level, and record it again.";

            Level = 0;
            LevelLeft = 0;
            LevelRight = 0;
            RecordingTime = TimeSpan.Zero;
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
    }

    /// <summary>Puts one written file on the shelf under a name.</summary>
    /// <param name="name">What it is called.</param>
    /// <param name="path">Where it was written.</param>
    /// <returns>The row that was added, so the caller can pick it.</returns>
    private Recording Shelve(string name, string path)
    {
        var recording = new Recording
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            FilePath = path,
            DurationMs = ReadDurationMs(path),
            CreatedAt = DateTime.Now
        };

        Recordings.Add(recording);

        return recording;
    }

    /// <summary>What the untouched capture is called beside the take that went through the chain.</summary>
    /// <remarks>
    /// The take's own name with a word after it, and a number after that where the name is taken,
    /// which is the rule an arriving song already keeps: a twin that quietly overwrote last
    /// week's would be the one thing this feature exists to prevent.
    ///
    /// Worked out even where there is no chain, since it costs a walk of a list somebody is
    /// looking at and the recorder is what decides whether there is a twin to name.
    /// </remarks>
    /// <param name="name">What the take is called.</param>
    private string CleanName(string name)
    {
        string wanted = name + " (clean)";

        if (!Taken(wanted)) return wanted;

        for (int at = 2; at < 1000; at++)
        {
            string another = name + " (clean " + at + ")";
            if (!Taken(another)) return another;
        }

        return name + " (clean " + Guid.NewGuid().ToString("N")[..8] + ")";
    }

    /// <summary>Whether a take of that name is already on the shelf, however it is cased.</summary>
    private bool Taken(string name) =>
        Recordings.Any(one => string.Equals(one.Name, name, StringComparison.OrdinalIgnoreCase));
}
