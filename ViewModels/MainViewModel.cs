// ===============================
// ViewModels/MainViewModel.cs
// ===============================
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Audio.Routing;
using JingleBox2.Config;
using JingleBox2.Midi;
using JingleBox2.Tracker;
using JingleBox2.Models;
using JingleBox2.UI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using JingleBox2.Machines.Ui;

namespace JingleBox2.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAudioEngine _audio;
    private readonly ConfigStore _store;
    private readonly AppConfig _cfg;

    private bool _suspendSave;

    public MidiViewModel Midi { get; }
    public RecordViewModel Record { get; }

    /// <summary>The shelf of takes, for filling a pad from it.</summary>
    public TakeFilter Takes { get; }
    public TrackerViewModel Tracker { get; }
    public MachineRackViewModel Machines { get; }

    /// <summary>Where a machine is built, as opposed to the rack, which is what is installed.</summary>
    public MachineEditorViewModel MachineEditor { get; } = new();

    /// <summary>
    /// Where everything in the app says where you are and what it has just done.
    /// </summary>
    public StatusBus Bus { get; } = new();

    /// <summary>What the bar along the bottom of the window shows.</summary>
    public StatusViewModel StatusLine { get; }

    /// <summary>The pads, as the transport sees them: one cap, and it silences the lot.</summary>
    private PadDeck? _padDeck;

    /// <summary>What the four caps at the top of the window are working.</summary>
    public TransportSwitch Transport { get; private set; } = null!;

    /// <summary>
    /// The deck of the page you are on.
    /// </summary>
    /// <remarks>
    /// Written out rather than defaulting, so a page added later has to say what its transport
    /// means. SETTINGS and PADS are not pages you play on, and they hand the caps the pads,
    /// which can only stop: what is sounding while you are setting things up is a pad.
    /// </remarks>
    private ITransportDeck DeckForPage => SelectedTab switch
    {
        RecordTab => Record,
        TrackerTab => Tracker,
        _ => _padDeck!
    };

    /// <summary>Which tab is open, so the bar can say where you are rather than where you were.</summary>
    [ObservableProperty] private int selectedTab;

    partial void OnSelectedTabChanged(int value)
    {
        Retell();

        // The caps are patched to the page you are on, so moving pages moves them.
        Transport?.Moved();

        OnPropertyChanged(nameof(ShowsTransport));
        OnPropertyChanged(nameof(TabStripRoom));
    }

    /// <summary>
    /// Whether the transport is worth showing on the page you are on.
    /// </summary>
    /// <remarks>
    /// The three pages you play on: the pads on FIRE, a recording on RECORD, the song on
    /// TRACKER. PADS and SETTINGS are where things are set up rather than played, and a
    /// transport standing over either is four buttons about something you are not doing.
    ///
    /// Written out rather than "not settings", so that adding a page makes somebody decide
    /// which kind it is instead of quietly getting a transport it has no use for.
    /// </remarks>
    public bool ShowsTransport => SelectedTab is UseTab or RecordTab or TrackerTab;

    /// <summary>
    /// Room kept at the end of the tab strip for the transport, and none when it is away.
    /// </summary>
    /// <remarks>
    /// Without this the tabs on SETTINGS wrap onto a second line to keep clear of buttons that
    /// are not being drawn.
    /// </remarks>
    /// <remarks>
    /// There is room under it as well. The transport is taller than the names beside it, so a
    /// strip only as tall as the words leaves it hanging over whatever the page starts with.
    /// </remarks>
    public Avalonia.Thickness TabStripRoom =>
        ShowsTransport ? new Avalonia.Thickness(0, 0, 160, 12) : new Avalonia.Thickness(0, 0, 0, 12);

    /// <summary>
    /// The pages, in the order the tab strip has them. Written out, because the context the bar
    /// shows depends on which one is open and a number read off a control is not a name.
    /// </summary>
    private const int RecordTab = 0;

    private const int PadsTab = 1;

    private const int UseTab = 2;

    private const int TrackerTab = 3;

    /// <summary>Named for the sake of the list, though nothing asks about it by name.</summary>
    private const int SettingsTab = 4;

    /// <summary>
    /// Tells the bar where you are now.
    /// </summary>
    /// <remarks>
    /// Pulled from whichever page is open rather than pushed by all of them, because only one of
    /// them is on screen and the others' idea of where you are is not true while you are not
    /// there. SETTINGS says nothing: there is nowhere to be on it.
    /// </remarks>
    private void Retell() => Bus.Context = SelectedTab switch
    {
        UseTab or PadsTab => Pads.Count + (Pads.Count == 1 ? " pad" : " pads") +
                             "  ·  profile " + (string.IsNullOrWhiteSpace(SelectedProfileName) ? "default" : SelectedProfileName),
        RecordTab => Record.Context,
        TrackerTab => Tracker.Context,
        _ => ""
    };

    /// <summary>
    /// Repeats whatever a page puts in its own status onto the bus.
    /// </summary>
    /// <remarks>
    /// A bridge, not the design. The pages still keep a Status property because a good deal of
    /// code writes to it, and this saves rewriting all of that at once; anything new should say
    /// what it has to say on the bus directly.
    /// </remarks>
    /// <summary>Re-asks the open page where you are whenever anything about it changes.</summary>
    /// <remarks>
    /// Any property, not just a named one: where you are is made of the cursor, the selection,
    /// the song's name and half a dozen other things, and listing them here would be a list to
    /// keep up to date every time a page grew one more.
    /// </remarks>
    private void Follow(ObservableObject page) => page.PropertyChanged += (_, _) => Retell();

    private void Watch(ObservableObject page, string from)
    {
        page.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName != "Status") return;

            string said = sender switch
            {
                TrackerViewModel tracker => tracker.Status,
                MachineRackViewModel instruments => instruments.Status,
                RecordViewModel record => record.Status,
                _ => ""
            };

            if (said.Length > 0) Bus.Say(said, from);
        };
    }

    /// <summary>
    /// What the engine runs at. Zero follows the output device, which is what keeps the audio
    /// from being resampled on its way out and tells a plugin the rate it is really fed at.
    /// </summary>
    public static (int Rate, string Label)[] EngineRates { get; } =
    {
        (Audio.SynthOutput.FollowDevice, "Follow the output device"),
        (44100, "44100 Hz"),
        (48000, "48000 Hz"),
        (96000, "96000 Hz")
    };

    public string SelectedEngineRate
    {
        get
        {
            foreach (var (rate, label) in EngineRates)
            {
                if (rate == _cfg.EngineSampleRate) return label;
            }

            return EngineRates[0].Label;
        }
        set
        {
            foreach (var (rate, label) in EngineRates)
            {
                if (label != value || _cfg.EngineSampleRate == rate) continue;

                _cfg.EngineSampleRate = rate;
                _store.Save(_cfg);

                OnPropertyChanged();
                OnPropertyChanged(nameof(EngineRateHint));
                return;
            }
        }
    }

    /// <summary>
    /// How far ahead of the sound card the tracker mixes, offered as words rather than numbers.
    /// </summary>
    /// <remarks>
    /// The names are what the choice actually does to you. In step is tightest and is what this
    /// did before there was a choice; the rest buy a plugin room to be late in, and cost you
    /// exactly what they say between playing a note and hearing it.
    /// </remarks>
    private static readonly (int Milliseconds, string Label)[] RenderAheads =
    {
        (0, "In step (tightest)"),
        (10, "10 ms cushion"),
        (20, "20 ms cushion"),
        (40, "40 ms cushion")
    };

    public string[] RenderAheadLabels { get; } = RenderAheads.Select(a => a.Label).ToArray();

    public string SelectedRenderAhead
    {
        get
        {
            foreach (var (milliseconds, label) in RenderAheads)
            {
                if (milliseconds == _cfg.RenderAheadMs) return label;
            }

            return RenderAheads[0].Label;
        }
        set
        {
            foreach (var (milliseconds, label) in RenderAheads)
            {
                if (label != value || _cfg.RenderAheadMs == milliseconds) continue;

                _cfg.RenderAheadMs = milliseconds;
                _store.Save(_cfg);

                OnPropertyChanged();
                OnPropertyChanged(nameof(RenderAheadHint));
                return;
            }
        }
    }

    /// <summary>What the choice means, said plainly enough to choose by.</summary>
    public string RenderAheadHint =>
        _cfg.RenderAheadMs <= 0
            ? "Each block is mixed inside the call that asks for it, which is as tight as this gets. " +
              "A plugin that takes a moment longer than usual has nowhere to take it from, and what " +
              "comes out is a gap. Give it a cushion if the sound breaks up while plugins are playing. " +
              "Takes effect when the app is started again."
            : "The mixer works " + _cfg.RenderAheadMs + " ms ahead on a thread of its own, so a plugin " +
              "being late eats into that instead of into the output. It also means what you hear was " +
              "mixed " + _cfg.RenderAheadMs + " ms ago, which is what a key you press waits before it " +
              "sounds. Takes effect when the app is started again.";

    /// <summary>
    /// Whether the app and its plugin processes write down what they are doing.
    /// </summary>
    /// <remarks>
    /// Off by default: a log nobody is reading is a file quietly growing on somebody's disk.
    /// On, it takes effect at once, for everything already running as well as for the next
    /// plugin process started. See <see cref="Diagnostics.Log"/>.
    /// </remarks>
    public bool WriteLog
    {
        get => _cfg.WriteLog;
        set
        {
            if (_cfg.WriteLog == value) return;

            _cfg.WriteLog = value;
            _store.Save(_cfg);

            if (value) Diagnostics.Log.Open(Config.AppFolder.Path(), true);
            else Diagnostics.Log.Close();

            OnPropertyChanged();
            OnPropertyChanged(nameof(LogHint));
        }
    }

    /// <summary>Where the file is, said out loud so it can be found without being hunted for.</summary>
    public string LogHint =>
        WriteLog
            ? "Writing to " + System.IO.Path.Combine(Config.AppFolder.Path(), Diagnostics.Log.FileName) +
              ". Plugin processes write to the same file. Started again from empty when it reaches a few megabytes."
            : "Off. Nothing is written and nothing is slowed down. Turn this on before doing whatever went wrong, then look in " +
              Config.AppFolder.Path() + ".";

    /// <summary>What is actually running, as against what has been asked for.</summary>
    public string EngineRateHint =>
        $"Running at {Tracker.EngineSampleRate} Hz. A change takes effect when the app is started again.";

    public string[] EngineRateLabels { get; } = EngineRates.Select(r => r.Label).ToArray();

    /// <summary>What plugins this machine has. Scanned from SETTINGS, on demand.</summary>
    public PluginLibraryViewModel Plugins { get; private set; } = new();

    public ObservableCollection<OutputDevice> OutputDevices { get; } = new();
    public ObservableCollection<PadViewModel> Pads { get; } = new();

    /// <summary>
    /// The pad the PADS page is about.
    /// </summary>
    /// <remarks>
    /// One editor and a grid to point it at, rather than every pad's settings stacked down a
    /// page you scroll. The grid is the pads as they are laid out, so picking the one to work
    /// on is the same movement as reaching for it while playing.
    /// </remarks>
    [ObservableProperty] private PadViewModel? selectedPad;

    // PADS header
    public ObservableCollection<string> ProfileNames { get; } = new();

    // THEME picker. The themes there are is ThemeManager's to say, since it is the one that
    // knows which file each is; a second list here could only drift from it.
    public ObservableCollection<string> ThemeNames { get; } = new(ThemeManager.Names);

    [ObservableProperty] private string selectedTheme = ThemeManager.Default;

    [ObservableProperty] private OutputDevice? selectedOutputDevice;

    // Selected profile name (bind to ComboBox + also show in FIRE)
    [ObservableProperty] private string selectedProfileName = "default";

    [ObservableProperty] private string newProfileName = "";

    // Matrix size (rows x columns)
    [ObservableProperty] private int rows = 4;
    [ObservableProperty] private int columns = 2;

    /// <summary>
    /// How many pads there actually are, as against how many the settings page is being typed
    /// towards.
    /// </summary>
    /// <remarks>
    /// From the settings file rather than from the two number fields. Those fields are what
    /// somebody is in the middle of typing, and everything that builds pads, resizes the audio
    /// engine or fills in a profile has to work from what is in force. Reading the fields
    /// instead meant that typing 4 by 4 and not pressing the button left the app running nine
    /// pads while every other part of it believed there were sixteen.
    /// </remarks>
    public int PadCount => Math.Max(1, _cfg.Rows) * Math.Max(1, _cfg.Columns);

    /// <summary>How many columns the pads are laid out in, for the pages that show them.</summary>
    public int PadColumns => Math.Max(1, _cfg.Columns);

    /// <summary>What the settings page would give you, for the settings page to say so.</summary>
    public int WantedPadCount => Rows * Columns;

    // Validation message for matrix size
    [ObservableProperty] private string matrixSizeError = "";

    public bool IsMatrixSizeValid => string.IsNullOrEmpty(MatrixSizeError);

    // Event to notify window to resize for square pads
    public event Action<int, int>? MatrixSizeChanged;

    public IRelayCommand AddProfileCommand { get; }
    public IRelayCommand DeleteProfileCommand { get; }
    public IRelayCommand ApplyMatrixSizeCommand { get; }

    public MainViewModel(
        IAudioEngine audio,
        ConfigStore store,
        AppConfig cfg,
        IMidiService midiService,
        IRecordingService recordingService,
        IWaveformService waveformService,
        IAudioRouting routing)
    {
        _audio = audio;
        _store = store;
        _cfg = cfg;

        Midi = new MidiViewModel(store, cfg, midiService);

        // The plugin list keeps the folders it was told to look in, which live with the rest
        // of the settings.
        Plugins = new PluginLibraryViewModel(store, cfg);
        Record = new RecordViewModel(recordingService, new LevelMeterService(), waveformService, store, cfg, routing);

        // What a pad is filled from. The same shelf the machines fetch takes off, narrowed the
        // same way: a pad plays a recording you own, not a file that happened to be on the disc
        // the day you built the profile.
        Takes = new TakeFilter(Record.Recordings);

        // The rack: the machines you have, and the plugins you have added. A song takes an
        // instrument off a machine and keeps its own copy of it.
        var rack = new MachineRack();

        Tracker = new TrackerViewModel(audio, rack, Record.Recordings, store, cfg, Plugins, waveformService);
        Machines = new MachineRackViewModel(rack, Tracker, Record.Recordings, waveformService, Plugins);

        // The four caps at the top belong to the page you are on. See TransportSwitch for
        // which deck they are patched to and when.
        _padDeck = new PadDeck(Pads);
        Transport = new TransportSwitch(() => DeckForPage, Record, _padDeck, Tracker);

        Machines.InstrumentChanged += (_, instrument) =>
        {
            Tracker.ApplyMachineEdit(instrument);

            // An instrument can be pointed at a different recording, which frees the old one
            // and claims the new one.
            Record.RefreshUsage();
        };

        Machines.RackChanged += (_, _) =>
        {
            Tracker.RefreshRack();
            Record.RefreshUsage();
        };

        // A recording that an instrument is built on cannot be thrown away, so the RECORD page
        // asks the rack before it deletes anything.
        Record.SampleUsage = rack;

        // Trimming a recording changes what its instruments sound like, and the player is
        // holding the old audio.
        Record.RecordingChanged += (_, path) => Tracker.ReloadSample(path);
        Record.RecordingRenamed += (_, moved) => Tracker.RenameSample(moved.From, moved.To);

        // One status bar for the whole window rather than one line per page. Three pages had
        // grown their own back when they were three tabs, and putting two of them inside the
        // third meant looking at two at once, one of which was the other one's own property
        // rendered a second time.
        Watch(Tracker, "Tracker");
        Watch(Machines, "Machines");
        Watch(Record, "Record");

        // The rack is brought into shape while the rack is being built, which is before any
        // of this exists. Moving somebody's instruments and saying nothing about it would be
        // the one thing worth saying out loud all session.
        if (Machines.Status.Length > 0) Bus.Warn(Machines.Status, "Machines");



        // The last run stopped without saying goodbye, and there is now a file saying what it
        // was in the middle of. Said out loud, because a report nobody knows about is a report
        // nobody sends.
        if (Diagnostics.CrashReport.FromLastTime.Length > 0)
        {
            Bus.Warn("JingleBox stopped unexpectedly last time. What it was doing is written in " +
                     Diagnostics.CrashReport.FromLastTime, "Crash");
        }

        // And what there is to be done about it. Said after the crash rather than before, because
        // this is the one worth leaving on the bar: the other is a file to send, this is work to
        // get back.
        if (Tracker.Recovered.Length > 0) Bus.Warn(Tracker.Recovered, "Tracker");

        StatusLine = new StatusViewModel(
            Bus,
            () => Record.Level,
            () => Math.Max(_audio.GetOutputLevel(), Tracker.OutputLevel));

        // Where you are changes as you move about inside a page, not only as you change pages.
        Follow(Tracker);
        Follow(Machines);
        Follow(Record);

        // And the pad page, which is this object: the profile and the matrix live here rather
        // than on a page view model of their own.
        Follow(this);
        Pads.CollectionChanged += (_, _) => Retell();

        Retell();

        AddProfileCommand = new RelayCommand(AddProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile);
        ApplyMatrixSizeCommand = new RelayCommand(ApplyMatrixSize, CanApplyMatrixSize);

        // Devices
        foreach (var d in _audio.GetOutputDevices())
            OutputDevices.Add(d);

        SelectedOutputDevice =
            OutputDevices.FirstOrDefault(d => d.Id == _cfg.SelectedOutputDeviceId)
            ?? OutputDevices.FirstOrDefault();

        if (SelectedOutputDevice != null)
        {
            _audio.SetOutputDevice(SelectedOutputDevice.Id);
            Tracker.ReopenAudio();
        }

        // Load matrix size from config
        Rows = _cfg.Rows;
        Columns = _cfg.Columns;

        // Ensure profiles exist + list them
        EnsureProfilesInitialized(PadCount);
        RefreshProfilesList();

        // Pick initial selected profile (must match an item in ProfileNames)
        var wanted = string.IsNullOrWhiteSpace(_cfg.SelectedProfile) ? "default" : _cfg.SelectedProfile.Trim();
        var resolved = ProfileNames.FirstOrDefault(n => string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase))
                      ?? ProfileNames.FirstOrDefault()
                      ?? "default";

        _suspendSave = true;
        try
        {
            SelectedProfileName = resolved;
            _cfg.SelectedProfile = resolved;

            // Theme: load from config, validate against known themes
            SelectedTheme = ThemeManager.Resolve(_cfg.SelectedTheme);
            _cfg.SelectedTheme = SelectedTheme;
        }
        finally
        {
            _suspendSave = false;
        }

        // Pads
        BuildPadsFromSelectedProfile(PadCount);

        // MIDI routing: global, profile-independent mapping. Which controller reaches which
        // half of the app is decided by the roles in SETTINGS, not here.
        var padRouter = new MidiRouter(_cfg.Midi, new PadTriggerAdapter(Pads));
        var noteRouter = new MidiNoteRouter(new TrackerNoteAdapter(Tracker, Machines));
        var dispatcher = new MidiDispatcher(_cfg.Midi, padRouter.Handle, noteRouter.Handle);

        // NOTE: MidiViewModel already subscribes for learn/status.
        // This subscription is for playing things.
        midiService.MessageReceived += (_, msg) => dispatcher.Handle(msg);

        // Apply initial theme once
        ThemeManager.Apply(SelectedTheme);

        PropertyChanged += OnMainChanged;
    }

    partial void OnRowsChanged(int value)
    {
        // The other one follows. Setting it to what it already is does nothing, so the two
        // hooks cannot chase each other.
        if (LinkPadMatrix) Columns = value;

        ValidateMatrixSize();
        (ApplyMatrixSizeCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    partial void OnColumnsChanged(int value)
    {
        if (LinkPadMatrix) Rows = value;

        ValidateMatrixSize();
        (ApplyMatrixSizeCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Whether the matrix may go past the usual sixteen pads.
    /// </summary>
    /// <remarks>
    /// Kept as a switch of its own rather than simply raising the ceiling: thirty-two pads is
    /// a different instrument from eight, wants a screen to match, and is not somewhere to end
    /// up by holding an arrow key down. Turning it off leaves a big matrix that is already in
    /// force alone; it only refuses the next one.
    /// </remarks>
    public bool ExtendedPadMatrix
    {
        get => _cfg.ExtendedPadMatrix;
        set
        {
            if (_cfg.ExtendedPadMatrix == value) return;

            _cfg.ExtendedPadMatrix = value;
            _store.Save(_cfg);

            OnPropertyChanged();
            OnPropertyChanged(nameof(MostPads));

            ValidateMatrixSize();
            (ApplyMatrixSizeCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Whether the machine editor has a page of its own in the menu along the top.
    /// </summary>
    /// <remarks>
    /// The rack lives inside the tracker, which is right when you are writing a song and wrong
    /// when the instrument is the work. Turning this on puts it in the menu beside the others,
    /// where it is one click away from wherever you are.
    /// </remarks>
    public bool ShowMachineEditor
    {
        get => _cfg.ShowMachineEditor;
        set
        {
            if (_cfg.ShowMachineEditor == value) return;

            _cfg.ShowMachineEditor = value;
            _store.Save(_cfg);

            OnPropertyChanged();
        }
    }

    /// <summary>The most pads the settings page will let you ask for as things stand.</summary>
    public int MostPads => ExtendedPadMatrix ? PadMatrix.Most : PadMatrix.Usual;

    /// <summary>
    /// Whether rows and columns move together, as the bracket between them shows.
    /// </summary>
    /// <remarks>
    /// A square grid is what most people want and the fiddliest to type, since it means
    /// getting two numbers to agree. Closed, either field sets both. Nothing happens at the
    /// moment it is closed: a drawing program does not resize the page when you close its
    /// lock either, it waits until you type.
    /// </remarks>
    public bool LinkPadMatrix
    {
        get => _cfg.LinkPadMatrix;
        set
        {
            if (_cfg.LinkPadMatrix == value) return;

            _cfg.LinkPadMatrix = value;
            _store.Save(_cfg);

            OnPropertyChanged();
        }
    }

    private void ValidateMatrixSize()
    {
        int total = Rows * Columns;
        if (Rows < 1 || Columns < 1)
            MatrixSizeError = "Rows and columns must be at least 1";
        else if (total < PadMatrix.Least)
            MatrixSizeError = $"Minimum {PadMatrix.Least} pads required (current: {total})";
        else if (total > MostPads)
            MatrixSizeError = ExtendedPadMatrix
                ? $"Maximum {PadMatrix.Most} pads allowed (current: {total})"
                : $"Maximum {PadMatrix.Usual} pads without the extended matrix (current: {total})";
        else
            MatrixSizeError = "";

        OnPropertyChanged(nameof(WantedPadCount));
        OnPropertyChanged(nameof(IsMatrixSizeValid));
    }

    private bool CanApplyMatrixSize() => IsMatrixSizeValid && (Rows != _cfg.Rows || Columns != _cfg.Columns);

    private void ApplyMatrixSize()
    {
        if (!IsMatrixSizeValid) return;

        // Save current pads into profile
        SavePadsIntoProfile(_cfg.SelectedProfile);

        // Update config
        _cfg.Rows = Rows;
        _cfg.Columns = Columns;

        // Resize audio engine
        _audio.Resize(PadCount);

        // Rebuild pads
        EnsureProfilesInitialized(PadCount);
        BuildPadsFromSelectedProfile(PadCount);

        // Update MIDI router pad count
        Midi.UpdatePadCount(PadCount);

        // Save
        _store.Save(_cfg);

        // The pages that show the pads follow what is in force, not what is being typed.
        OnPropertyChanged(nameof(PadCount));
        OnPropertyChanged(nameof(PadColumns));

        // Notify window to resize for square pads
        MatrixSizeChanged?.Invoke(Rows, Columns);

        (ApplyMatrixSizeCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    partial void OnSelectedProfileNameChanged(string value)
    {
        if (_suspendSave) return;

        var name = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        EnsureProfilesInitialized(PadCount);

        // Persist edits of current pads into currently selected profile BEFORE switching
        SavePadsIntoProfile(_cfg.SelectedProfile);

        // Switch selection to the requested name (must exist)
        _cfg.SelectedProfile = EnsureProfileExistsAndReturnResolved(name, padCount: PadCount);

        _store.Save(_cfg);

        _suspendSave = true;
        try
        {
            RefreshProfilesList();

            // Make sure SelectedProfileName matches an existing item (so ComboBox shows it)
            SelectedProfileName =
                ProfileNames.FirstOrDefault(n => string.Equals(n, _cfg.SelectedProfile, StringComparison.OrdinalIgnoreCase))
                ?? "default";

            ApplySelectedProfileToPads();
        }
        finally
        {
            _suspendSave = false;
        }
    }

    partial void OnSelectedThemeChanged(string value)
    {
        if (_suspendSave) return;

        var resolved = ThemeManager.Resolve(value);

        _cfg.SelectedTheme = resolved;
        ThemeManager.Apply(resolved);

        _store.Save(_cfg);
    }

    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suspendSave) return;

        if (e.PropertyName == nameof(SelectedOutputDevice))
        {
            if (SelectedOutputDevice != null)
            {
                _audio.SetOutputDevice(SelectedOutputDevice.Id);

                // Changing the device closed the old one, which took the tracker's stream with
                // it. Nothing else would notice until the next note.
                Tracker.ReopenAudio();
            }

            SaveNow();
        }
    }

    private void OnPadChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suspendSave) return;
        SaveNow();
    }

    private void AddProfile()
    {
        var raw = (NewProfileName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(raw)) return;

        var name = NormalizeProfileName(raw);
        if (string.IsNullOrWhiteSpace(name)) return;

        EnsureProfilesInitialized(padCount: PadCount);

        if (_cfg.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            return;

        // Persist current edits into current selected profile first
        SavePadsIntoProfile(_cfg.SelectedProfile);

        // IMPORTANT: new profile must be a CLEAN SLATE
        var padCount = PadCount;
        var newProfile = new ConfigProfile
        {
            Name = name,
            Pads = CreateDefaultPads(padCount)
        };

        _cfg.Profiles.Add(newProfile);
        _cfg.SelectedProfile = name;

        _store.Save(_cfg);

        _suspendSave = true;
        try
        {
            RefreshProfilesList();
            SelectedProfileName = ProfileNames.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) ?? name;

            ApplySelectedProfileToPads();
            NewProfileName = "";
        }
        finally
        {
            _suspendSave = false;
        }
    }

    private void DeleteProfile()
    {
        var cur = (_cfg.SelectedProfile ?? "default").Trim();
        if (string.IsNullOrWhiteSpace(cur)) return;
        if (string.Equals(cur, "default", StringComparison.OrdinalIgnoreCase)) return;

        EnsureProfilesInitialized(padCount: PadCount);

        // Persist current edits into current selected profile before deleting (optional but safe)
        SavePadsIntoProfile(cur);

        var idx = _cfg.Profiles.FindIndex(p => string.Equals(p.Name, cur, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;

        _cfg.Profiles.RemoveAt(idx);
        _cfg.SelectedProfile = "default";

        _store.Save(_cfg);

        _suspendSave = true;
        try
        {
            RefreshProfilesList();
            SelectedProfileName =
                ProfileNames.FirstOrDefault(n => string.Equals(n, "default", StringComparison.OrdinalIgnoreCase))
                ?? "default";

            ApplySelectedProfileToPads();
        }
        finally
        {
            _suspendSave = false;
        }
    }

    private void SaveNow()
    {
        EnsureProfilesInitialized(padCount: PadCount);

        _cfg.SelectedOutputDeviceId = SelectedOutputDevice?.Id ?? -1;

        // Persist pads into selected profile
        SavePadsIntoProfile(_cfg.SelectedProfile);

        // Keep SelectedProfileName + cfg in sync
        _cfg.SelectedProfile = string.IsNullOrWhiteSpace(SelectedProfileName) ? "default" : SelectedProfileName.Trim();

        // Theme is already stored via OnSelectedThemeChanged, but keep consistent:
        _cfg.SelectedTheme = string.IsNullOrWhiteSpace(SelectedTheme) ? "Dark" : SelectedTheme.Trim();

        _store.Save(_cfg);
    }

    private void BuildPadsFromSelectedProfile(int padCount)
    {
        EnsureProfilesInitialized(padCount);

        var profile = GetProfileByName(_cfg.SelectedProfile);

        foreach (var old in Pads)
            old.Dispose();
        Pads.Clear();
        for (int i = 0; i < profile.Pads.Count; i++)
        {
            var padCfg = profile.Pads[i];

            var pad = new PadViewModel(i, _audio)
            {
                Name = padCfg.Name,
                FilePath = padCfg.Source,
                Volume = (float)padCfg.Volume,
                SourceKind = PadViewModel.KindFor(padCfg.Kind),
                Loop = padCfg.Loop,
                FadeIn = padCfg.FadeIn,
                FadeOut = padCfg.FadeOut,
                PadColor = padCfg.Color
            };

            // Every pad gets an effect chain of its own, pointed at itself, and whatever the
            // profile saved is put back on it.
            pad.UsePlugins(Plugins);
            pad.RestoreEffects(padCfg.Plugins);

            pad.PropertyChanged += OnPadChanged;
            Pads.Add(pad);
        }

        // The page always has something to show. Whatever was picked before belonged to the
        // pads that have just been thrown away.
        SelectedPad = Pads.Count > 0 ? Pads[0] : null;
    }

    private void ApplySelectedProfileToPads()
    {
        EnsureProfilesInitialized(padCount: PadCount);

        var profile = GetProfileByName(_cfg.SelectedProfile);

        _suspendSave = true;
        try
        {
            // If pad count changed elsewhere, keep VM count stable but fill what we have
            var n = Math.Min(Pads.Count, profile.Pads.Count);
            for (int i = 0; i < n; i++)
            {
                var pc = profile.Pads[i];
                var vm = Pads[i];

                vm.Name = pc.Name;
                vm.FilePath = pc.Source;
                vm.Volume = (float)pc.Volume;
                vm.SourceKind = pc.Kind;
                vm.Loop = pc.Loop;
                vm.FadeIn = pc.FadeIn;
                vm.FadeOut = pc.FadeOut;
                vm.PadColor = pc.Color;
            }
        }
        finally
        {
            _suspendSave = false;
        }
    }

    private void SavePadsIntoProfile(string? profileName)
    {
        EnsureProfilesInitialized(padCount: PadCount);

        var name = string.IsNullOrWhiteSpace(profileName) ? "default" : profileName.Trim();
        var profile = GetProfileByName(name);

        for (int i = 0; i < Pads.Count && i < profile.Pads.Count; i++)
        {
            var vm = Pads[i];
            var pc = profile.Pads[i];

            pc.Name = vm.Name ?? $"Pad {i + 1}";
            pc.Source = vm.FilePath ?? "";
            pc.Volume = vm.Volume;
            pc.Kind = vm.SourceKind;
            pc.Loop = vm.Loop;
            pc.FadeIn = vm.FadeIn;
            pc.FadeOut = vm.FadeOut;
            pc.Color = vm.PadColor;

            // What is loaded on the pad right now, rather than what it was opened with.
            var captured = JingleBox2.Audio.Plugins.PluginChainState.Capture(
                _audio.GetPadInsert(i) as JingleBox2.Audio.Plugins.PluginChain);

            pc.Plugins = captured.IsEmpty ? null : captured;
        }
    }

    private void RefreshProfilesList()
    {
        EnsureProfilesInitialized(padCount: PadCount);

        ProfileNames.Clear();

        foreach (var n in _cfg.Profiles
                     .Select(p => (p.Name ?? "").Trim())
                     .Where(s => !string.IsNullOrWhiteSpace(s))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            ProfileNames.Add(n);
        }

        // Guarantee default exists in the list
        if (!ProfileNames.Any(n => string.Equals(n, "default", StringComparison.OrdinalIgnoreCase)))
            ProfileNames.Insert(0, "default");

        // Keep cfg selection valid
        if (string.IsNullOrWhiteSpace(_cfg.SelectedProfile) ||
            !_cfg.Profiles.Any(p => string.Equals(p.Name, _cfg.SelectedProfile, StringComparison.OrdinalIgnoreCase)))
        {
            _cfg.SelectedProfile = "default";
        }
    }

    private void EnsureProfilesInitialized(int padCount)
    {
        _cfg.Profiles ??= new System.Collections.Generic.List<ConfigProfile>();

        if (_cfg.Profiles.Count == 0)
        {
            // migrate legacy Pads if present
            var pads = (_cfg.Pads != null && _cfg.Pads.Count > 0)
                ? _cfg.Pads.Select(ClonePad).ToList()
                : CreateDefaultPads(padCount);

            _cfg.Profiles.Add(new ConfigProfile { Name = "default", Pads = pads });
        }

        if (!_cfg.Profiles.Any(p => string.Equals(p.Name, "default", StringComparison.OrdinalIgnoreCase)))
            _cfg.Profiles.Add(new ConfigProfile { Name = "default", Pads = CreateDefaultPads(padCount) });

        foreach (var pr in _cfg.Profiles)
        {
            pr.Name = (pr.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(pr.Name))
                pr.Name = "default";

            pr.Pads ??= new System.Collections.Generic.List<PadConfig>();

            while (pr.Pads.Count < padCount)
                pr.Pads.Add(new PadConfig { Name = $"Pad {pr.Pads.Count + 1}", Kind = PadSourceKind.Recording, Source = "", Volume = 1.0 });

            while (pr.Pads.Count > padCount)
                pr.Pads.RemoveAt(pr.Pads.Count - 1);
        }

        if (string.IsNullOrWhiteSpace(_cfg.SelectedProfile))
            _cfg.SelectedProfile = "default";

        if (!_cfg.Profiles.Any(p => string.Equals(p.Name, _cfg.SelectedProfile, StringComparison.OrdinalIgnoreCase)))
            _cfg.SelectedProfile = "default";
    }

    private string EnsureProfileExistsAndReturnResolved(string requested, int padCount)
    {
        EnsureProfilesInitialized(padCount);

        var name = NormalizeProfileName(requested);
        if (string.IsNullOrWhiteSpace(name))
            name = "default";

        if (!_cfg.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            _cfg.Profiles.Add(new ConfigProfile
            {
                Name = name,
                Pads = CreateDefaultPads(padCount)
            });
        }

        // return exact stored casing
        return _cfg.Profiles.First(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Name;
    }

    private ConfigProfile GetProfileByName(string? name)
    {
        EnsureProfilesInitialized(padCount: PadCount);

        var n = string.IsNullOrWhiteSpace(name) ? "default" : name.Trim();

        var p = _cfg.Profiles.FirstOrDefault(x => string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase));
        if (p != null) return p;

        // fallback
        return _cfg.Profiles.First(x => string.Equals(x.Name, "default", StringComparison.OrdinalIgnoreCase));
    }

    private static System.Collections.Generic.List<PadConfig> CreateDefaultPads(int padCount)
    {
        var pads = new System.Collections.Generic.List<PadConfig>(padCount);
        for (int i = 0; i < padCount; i++)
        {
            pads.Add(new PadConfig
            {
                Name = $"Pad {i + 1}",
                Kind = PadSourceKind.Recording,
                Source = "",
                Volume = 1.0
            });
        }
        return pads;
    }

    private static PadConfig ClonePad(PadConfig p) => new()
    {
        Name = p.Name,
        Kind = p.Kind,
        Source = p.Source,
        Volume = p.Volume,
        Loop = p.Loop,
        FadeIn = p.FadeIn,
        FadeOut = p.FadeOut,
        Color = p.Color,
        Plugins = p.Plugins?.Clone()
    };

    private static string NormalizeProfileName(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return "";

        var lower = name.ToLowerInvariant();
        var chars = lower.Select(c =>
            (c >= 'a' && c <= 'z') ||
            (c >= '0' && c <= '9') ||
            c == '-' || c == '_'
                ? c
                : '-').ToArray();

        var cleaned = new string(chars);
        while (cleaned.Contains("--"))
            cleaned = cleaned.Replace("--", "-");

        cleaned = cleaned.Trim('-', '_');
        return cleaned.Length == 0 ? "" : cleaned;
    }
}
