using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Config;
using JingleBox2.Midi;
using JingleBox2.Controllers;
using JingleBox2.Audio.Records;
using JingleBox2.UI;
using System;
using Avalonia.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using JingleBox2.Config.Enums;
using JingleBox2.Midi.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Midi.Interfaces;
using JingleBox2.ViewModels.Interfaces;
using JingleBox2.SoundDevices.SoundMachines.Interfaces;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins;
using JingleBox2.Controllers.Interfaces;
using JingleBox2.SoundDevices.SoundMachines;

namespace JingleBox2.ViewModels;

/// <summary>
/// The window itself: the pages, the settings behind them, and the wiring between the sound,
/// the MIDI ports and what is on screen.
/// </summary>
/// <remarks>
/// Almost nothing here is a page. The pages are their own view models and this holds them,
/// which is what lets the parts that belong to the whole window rather than to any one page
/// live in one place: the transport caps, the status bar, the pointing of a hardware knob at a
/// software one, and the settings that are read while the app is starting.
///
/// The pads are the exception. Their profile, their matrix and their history are here rather
/// than on a page view model of their own, because two pages show them: PADS is where they are
/// laid out and FIRE is where they are played.
/// </remarks>
public sealed partial class MainViewModel : ObservableObject, Shortcuts.Interfaces.IShortcutContext
{
    /// <summary>What is known about the controllers plugged in. Holds a cache, so it is shared rather than made twice.</summary>
    /// <summary>
    /// What is known about the controllers plugged in, for the whole application.
    /// </summary>
    /// <remarks>
    /// One of them, made here and handed to everything that asks. It remembers what a device
    /// has been seen doing, and which of a device's programs is running is worked out from the
    /// numbers arriving: a second one would be told nothing and would answer for a device it
    /// had never heard speak. Everything that takes one defaults to its own, so a test or a
    /// panel built on its own still works; the application passes this.
    /// </remarks>
    private readonly IControllerProfiles _profiles = new ControllerProfiles();

    /// <summary>A chain of effects, written down and read back. Holds nothing, so one is enough.</summary>
    private readonly IPluginChainState _chains = new PluginChainState();

    /// <summary>The machines this run has, the one instance everything shares.</summary>
    private readonly ISoundMachineProjects _machines;

    /// <summary>What effects this installation has, the one instance everything shares.</summary>
    /// <remarks>
    /// Handed in by whoever read the disc, or an empty one, so that everything already built on
    /// this view model keeps working with no effect world at all, which is what a test wants. One
    /// instance either way: the rack's Effects tab and the shelf in SETTINGS are two views of the
    /// same list, and adding an effect on one has to show on the other.
    /// </remarks>
    private readonly SoundDevices.SoundEffects.Interfaces.ISoundEffectProjects _effects;

    /// <summary>The controller scripts, kept because they watch their own folder.</summary>
    private readonly ControllerCodecs _codecs;

    /// <summary>
    /// Which chain has a face of ours open in front, shared by everything that makes a chain
    /// view and by the thing that resolves a link.
    /// </summary>
    /// <remarks>
    /// One screen, one thing on the front of it. A link on one of our effects names the effect
    /// and the key and never where it is standing, so this is what says which EchoBox: the
    /// tracks' chain follows the cursor, the master's follows nothing, and a pad's is not on a
    /// track at all, which is three ways for a track number to be the wrong answer.
    /// </remarks>
    private readonly Interfaces.ISoundEffectInFront _effectInFront = new SoundEffectInFront();

    /// <summary>
    /// What a controller does before anybody has pointed it at anything.
    /// </summary>
    /// <remarks>
    /// Eight faders are the first eight tracks' levels and the encoders are the knobs on the
    /// face in front of you, on hardware this application has never heard of and with nothing
    /// stored. Anything anybody linked beats it. See <see cref="Midi.DefaultLayout"/>.
    /// </remarks>
    public Midi.DefaultLayout Layout { get; }

    /// <summary>
    /// What has been done to the pads, so it can be taken back.
    /// </summary>
    /// <remarks>
    /// A step is every pad at once, which costs almost nothing at this size and answers the one
    /// question a per-pad history could not: how many pads there are is an edit too, and it is
    /// not about any one of them.
    ///
    /// It is opened afresh on the pads of whichever profile is open, and again whenever another
    /// one is opened. Nothing from before that can be undone, which is right: a history
    /// outliving its profile would put one profile's pads back onto another.
    /// </remarks>
    public PadHistory PadHistory { get; } = new();

    /// <summary>The pads' sound, shared with the tracker rather than opened twice.</summary>
    private readonly IAudioEngine _audio;

    /// <summary>Where the settings are written, which is the same file for all of them.</summary>
    private readonly ConfigStore _store;

    /// <summary>The settings as they stand, which is what everything here reads and writes.</summary>
    private readonly AppConfig _cfg;

    /// <summary>
    /// Set while this object is writing to its own properties, so the writes are not read back
    /// as edits and stored again.
    /// </summary>
    /// <remarks>
    /// Filling a combo box with the profile that is already selected, or pouring a profile into
    /// the pads, moves a dozen properties that nobody touched. Without this each of them saves
    /// the settings and the last one wins, which is how opening a profile used to overwrite it.
    /// </remarks>
    private bool _suspendSave;

    /// <summary>The MIDI ports, their roles, and what is being learned on them.</summary>
    public MidiViewModel Midi { get; }

    /// <summary>
    /// Which keys are down, for anything drawing a keyboard.
    /// </summary>
    /// <remarks>
    /// One monitor for the whole application, standing in front of the half that plays the
    /// notes and passing every one on untouched. A drawn keyboard reads this rather than the
    /// presses its own panel happened to hear, because a key on the hardware never touches a
    /// panel: it goes to whoever the notes are being played on. See
    /// <see cref="Midi.MidiMonitor"/>.
    /// </remarks>
    public Midi.MidiMonitor? Keys { get; private set; }

    /// <summary>
    /// What a hardware control is pointed at, in both layers: the desk's links and the open
    /// song's.
    /// </summary>
    /// <remarks>
    /// On the window rather than on a page, because a knob is pointed at machine panels, plugin
    /// panels and mixer strips alike and the answer has to be the same wherever the pointer
    /// happens to be. Pointing is done in the other mouse mode, Ctrl+Shift+M, by resting the
    /// pointer on a control and touching the one on the desk; a message is offered to this
    /// first and then driven anyway, so the turn that makes the link also moves the thing you
    /// pointed at, which is the only confirmation worth having.
    /// </remarks>
    public Midi.ControlLink ControlLink { get; private set; } = null!;

    /// <summary>What the controller is pointed at, for the list in SETTINGS.</summary>
    public ControlLinksViewModel Links { get; private set; } = null!;

    /// <summary>
    /// Which MIDI ports this computer has, asked rather than held.
    /// </summary>
    /// <remarks>
    /// A template names its controller as a profile calls it, since a port is spelled
    /// differently on every system, so opening one means looking through what is actually
    /// plugged in. Asked each time because a controller can be connected while the page is
    /// open, and a list read when the page was built would refuse the device somebody has just
    /// put on the desk.
    /// </remarks>
    private IEnumerable<string> Ports() => Midi.Devices.Select(one => one.Name).ToList();

    /// <summary>RECORD: taking a recording, and the shelf everything else fetches takes off.</summary>
    public RecordViewModel Record { get; }

    /// <summary>The shelf of takes, for filling a pad from it.</summary>
    /// <remarks>
    /// The same shelf the machines fetch takes off, narrowed the same way: a pad plays a
    /// recording you own, not a file that happened to be on the disc the day the profile was
    /// built.
    /// </remarks>
    public TakeFilter Takes { get; }

    /// <summary>TRACKER: the song, its patterns, its mixer and the rack beside it.</summary>
    public TrackerViewModel Tracker { get; }

    /// <summary>The machines you have, as a list to pick from and to open one of.</summary>
    public RackViewModel Machines { get; }

    /// <summary>Where a machine is built, as opposed to the rack, which is what is installed.</summary>
    /// <remarks>
    /// Given the same shelf of takes everything else reads, because it draws real waveforms and
    /// a panel laid out against a picture that is not there is laid out wrong. Its picker
    /// offers that shelf too: a machine that plays a recording is started from a recording, so
    /// what stands at the top of the panel is your takes and the categories they are filed
    /// under.
    /// </remarks>
    public DesignerViewModel Designer { get; } = new();

    /// <summary>
    /// The same page again, designing an effect instead of a machine.
    /// </summary>
    /// <remarks>
    /// A second one of the same class rather than a mode on the first: two pages of work that
    /// each have their own project open, their own undo and their own unsaved changes. Told which
    /// world it is in through <see cref="SoundDevices.SoundEffects.SoundEffectWorld"/>, which is where the
    /// handful of things that differ live, and given none of the machine world's take pickers,
    /// since an effect is sent no recordings.
    /// </remarks>
    public DesignerViewModel EffectDesigner { get; } = new(new SoundDevices.SoundEffects.SoundEffectWorld());

    /// <summary>What machines are on the disc, for the settings page to list and add to.</summary>
    /// <remarks>
    /// The disc rather than the rack: what is installed, including anything that has arrived
    /// since the app was started and is therefore not on the rack yet. A machine imported or
    /// thrown out while the app is running changes which boxes the rack has rather than what
    /// one of them looks like, so the rack builds its list again there and then rather than
    /// waiting for the next start, and builds the list rather than redrawing an open panel.
    /// </remarks>
    public SoundMachineShelfViewModel MachineShelf { get; }

    /// <summary>
    /// The same page again for effects, which are imported and thrown out exactly as machines are.
    /// </summary>
    public SoundEffectShelfViewModel EffectShelf { get; }

    /// <summary>
    /// Where everything in the app says where you are and what it has just done.
    /// </summary>
    /// <remarks>
    /// One bar for the whole window rather than one line per page. Three pages had grown their
    /// own back when they were three tabs, and putting two of them inside the third meant
    /// looking at two at once, one of which was the other one's own property rendered a second
    /// time.
    /// </remarks>
    public StatusBus Bus { get; } = new();

    /// <summary>What the bar along the bottom of the window shows.</summary>
    public StatusViewModel StatusLine { get; }

    /// <summary>The pads, as the transport sees them: one cap, and it silences the lot.</summary>
    private PadDeck? _padDeck;

    /// <summary>
    /// What the four caps at the top of the window are working.
    /// </summary>
    /// <remarks>
    /// They belong to the page you are on: the deck behind them is patched by
    /// <see cref="DeckForPage"/> and repatched whenever the page changes. See
    /// <see cref="TransportSwitch"/>.
    /// </remarks>
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

    /// <summary>
    /// The page you are on, when it answers keystrokes for itself.
    /// </summary>
    /// <remarks>
    /// Written out per page, like the transport's deck, and needed for a reason the dispatcher
    /// cannot fix on its own: it walks outwards from whatever has the keyboard, and when nothing
    /// has it the only thing that walk reaches is the window. A page with no focused control
    /// inside it would then never be asked, and pressing undo on RECORD straight after clicking
    /// a button in a dialog is exactly that situation: it silently did nothing.
    ///
    /// So the window hands off to the page rather than the page waiting to be found. Anything
    /// that does have focus is still asked first and still wins.
    /// </remarks>
    private Shortcuts.Interfaces.IShortcutContext? Page => SelectedTab switch
    {
        RecordTab => Record,
        TrackerTab => Tracker,
        _ => null
    };

    /// <inheritdoc/>
    /// <remarks>
    /// This is the outermost answer, so it is the last one asked and the one nothing nearer the
    /// keyboard claimed. What the open page says comes first, and only what it does not answer
    /// is decided here.
    ///
    /// Saying no is a good answer and the common one: the keystroke then carries on as though
    /// none of this were here, which is what stops Ctrl+S on the pads doing something nobody
    /// asked for. Every page is written out rather than defaulting, the same way the transport's
    /// deck is, so a page added later has to say what saving on it means instead of quietly
    /// inheriting somebody else's answer.
    /// </remarks>
    bool Shortcuts.Interfaces.IShortcutContext.Can(Shortcuts.Enums.ShortcutAction action) => action switch
    {
        _ when Page?.Can(action) == true => true,

        Shortcuts.Enums.ShortcutAction.Save => SelectedTab == TrackerTab && Tracker.SaveCommand.CanExecute(null),

        Shortcuts.Enums.ShortcutAction.Undo => OnThePads && PadHistory.CanUndo,
        Shortcuts.Enums.ShortcutAction.Redo => OnThePads && PadHistory.CanRedo,

        _ => false
    };

    /// <summary>
    /// True on the pages the pads are laid out or played on.
    /// </summary>
    /// <remarks>
    /// Which is where undo means the pads. They are not a document you save, so they are undone
    /// from wherever they are laid out, which is PADS, and from where they are fired, which is
    /// FIRE and USE.
    /// </remarks>
    private bool OnThePads => SelectedTab is PadsTab or UseTab;

    /// <inheritdoc/>
    /// <remarks>
    /// The page first, and only what it does not answer falls through to the window's own. The
    /// page is asked whether it can before being asked to, since an action it declines has to
    /// reach the answer below rather than being swallowed by whoever was offered it first.
    /// </remarks>
    void Shortcuts.Interfaces.IShortcutContext.Do(Shortcuts.Enums.ShortcutAction action)
    {
        if (Page is { } page && page.Can(action))
        {
            page.Do(action);

            return;
        }

        switch (action)
        {
            case Shortcuts.Enums.ShortcutAction.Save when SelectedTab == TrackerTab:
                Tracker.SaveCommand.Execute(null);
                break;

            case Shortcuts.Enums.ShortcutAction.Undo when OnThePads:
                PadsBack(PadHistory.Undo());
                break;

            case Shortcuts.Enums.ShortcutAction.Redo when OnThePads:
                PadsBack(PadHistory.Redo());
                break;
        }
    }

    /// <summary>Whether the last page was the tracker, so leaving it can be told from arriving.</summary>
    private bool _wasOnTracker;

    /// <summary>
    /// Whether the tracker puts its plugins down when you leave it.
    /// </summary>
    /// <remarks>
    /// Takes effect at the next change of page rather than at once: switched on while you are
    /// standing in the tracker, the thing it is about has not happened yet.
    /// </remarks>
    public bool FreeTrackerPlugins
    {
        get => _cfg.FreeTrackerPlugins;
        set
        {
            if (_cfg.FreeTrackerPlugins == value) return;

            _cfg.FreeTrackerPlugins = value;
            _store.Save(_cfg);

            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Changing pages: the bar is retold where you are, the transport is repatched, and the
    /// tracker's plugins are put down or picked up if that has been asked for.
    /// </summary>
    /// <remarks>
    /// Each plugin the song holds is a process with its patch loaded, and they go on holding it
    /// while you work on the pads, which is why leaving the tracker is the moment to let go of
    /// them. Whether that happens at all is <see cref="FreeTrackerPlugins"/>, and it is only
    /// ever done on the way out and undone on the way back in.
    ///
    /// The caps at the top are patched to the page you are on, so moving pages moves them.
    /// </remarks>
    partial void OnSelectedTabChanged(int value)
    {
        Retell();

        if (_cfg.FreeTrackerPlugins)
        {
            if (_wasOnTracker && value != TrackerTab) Tracker.LetGoOfPlugins();
            else if (!_wasOnTracker && value == TrackerTab) Tracker.TakeUpPlugins();
        }

        _wasOnTracker = value == TrackerTab;

        Transport?.Moved();

        if (value == TrackerTab) Tracker.RefreshRack();

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
    ///
    /// There is room under it as well. The transport is taller than the names beside it, so a
    /// strip only as tall as the words leaves it hanging over whatever the page starts with.
    /// Measured rather than judged: the caps ended six pixels above the first card once the
    /// pages were tightened, which is close enough to read as a mistake.
    ///
    /// The same on both branches, because the strip has to be one height whether or not the
    /// transport is standing in it: a page that started higher for having no transport would be
    /// the drift that was just taken out of the six pages.
    /// </remarks>
    public Avalonia.Thickness TabStripRoom =>
        ShowsTransport ? new Avalonia.Thickness(0, 0, 160, 15) : new Avalonia.Thickness(0, 0, 0, 15);

    /// <summary>
    /// The pages, in the order the tab strip has them. Written out, because the context the bar
    /// shows depends on which one is open and a number read off a control is not a name.
    /// </summary>
    private const int RecordTab = 0;

    /// <summary>Where the pads are laid out.</summary>
    private const int PadsTab = 1;

    /// <summary>And where they are played, which is FIRE.</summary>
    private const int UseTab = 2;

    /// <summary>The song, and the rack beside it.</summary>
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

    /// <summary>Re-asks the open page where you are whenever anything about it changes.</summary>
    /// <remarks>
    /// Any property, not just a named one: where you are is made of the cursor, the selection,
    /// the song's name and half a dozen other things, and listing them here would be a list to
    /// keep up to date every time a page grew one more. Where you are changes as you move about
    /// inside a page and not only as you change pages, which is the whole reason this exists.
    ///
    /// This object is one of the pages it follows, since the profile and the matrix live here
    /// rather than on a pad page view model of their own.
    /// </remarks>
    private void Follow(ObservableObject page) => page.PropertyChanged += (_, _) => Retell();

    /// <summary>
    /// Repeats whatever a page puts in its own status onto the bus.
    /// </summary>
    /// <remarks>
    /// A bridge, not the design. The pages still keep a Status property because a good deal of
    /// code writes to it, and this saves rewriting all of that at once; anything new should say
    /// what it has to say on the bus directly.
    /// </remarks>
    /// <param name="page">The page whose Status property is being repeated onto the bus.</param>
    /// <param name="from">Who is speaking, which is what the bar puts beside the line.</param>
    private void Watch(ObservableObject page, string from)
    {
        page.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName != "Status") return;

            string said = sender switch
            {
                TrackerViewModel tracker => tracker.Status,
                RackViewModel instruments => instruments.Status,
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
        (Audio.TrackerOutput.FollowDevice, "Follow the output device"),
        (44100, "44100 Hz"),
        (48000, "48000 Hz"),
        (96000, "96000 Hz")
    };

    /// <summary>
    /// The chosen rate, as the words the picker shows rather than as a number.
    /// </summary>
    /// <remarks>
    /// A rate the settings hold that is not on the list reads back as the first entry, which is
    /// following the device, since that is the only answer that is right whatever the card
    /// turns out to be. Takes effect when the app is started again.
    /// </remarks>
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
    /// What the audio is sized at where nothing has been chosen, per platform.
    /// </summary>
    /// <remarks>
    /// Held rather than made per read, since every one of the three settings below asks it what
    /// nought means.
    /// </remarks>
    private readonly Audio.Interfaces.IAudioDefaults _audioDefaults = new Audio.AudioDefaults();

    /// <summary>The three sizes as they stand, with nought resolved to this machine's default.</summary>
    private Audio.Records.AudioSizes Sizes => _audioDefaults.Chosen(new Audio.Records.AudioSizes(
        _cfg.OutputBufferSize, _cfg.OutputUpdatePeriodMs, _cfg.OutputUpdateThreads));

    /// <summary>
    /// The buffer sizes on offer, in frames.
    /// </summary>
    /// <remarks>
    /// **Frames on the slider and the latency printed beside it**, which is what every other
    /// audio application does: LMMS, Ardour and Reaper all read "Buffer size 512" with the
    /// milliseconds next to it. Frames are what the sound library takes and what somebody
    /// comparing this with their interface looks for; the milliseconds are what is actually felt,
    /// and they follow from the rate, so 512 frames is 12 ms at 44100 and 11 at 48000.
    ///
    /// The slider runs over the **places** in this list rather than over the sizes, because the
    /// sizes double at each step: a slider over the numbers themselves would give the whole low
    /// half of the range a hair's width and hand the top two sizes most of the travel.
    /// </remarks>
    private static readonly int[] BufferChoices =
        { 64, 128, 256, 512, 1024, 2048, 4096, 8192 };

    /// <summary>How far along that list the slider may go.</summary>
    public double BufferSteps => BufferChoices.Length - 1;

    /// <summary>
    /// Which place on the list is chosen, which is what the slider moves.
    /// </summary>
    /// <remarks>
    /// Nothing stored reads as this machine's own default, so the slider opens where the sound
    /// actually is rather than at one end. Storing it is what makes it stop being the default.
    /// </remarks>
    public double BufferStep
    {
        get
        {
            int wanted = Sizes.BufferFrames;
            int nearest = 0;

            for (int at = 1; at < BufferChoices.Length; at++)
            {
                if (Math.Abs(BufferChoices[at] - wanted) < Math.Abs(BufferChoices[nearest] - wanted))
                    nearest = at;
            }

            return nearest;
        }
        set
        {
            int at = Math.Clamp((int)Math.Round(value), 0, BufferChoices.Length - 1);
            int frames = BufferChoices[at];

            if (_cfg.OutputBufferSize == frames) return;

            _cfg.OutputBufferSize = frames;
            _store.Save(_cfg);

            ApplyAudioSizes();

            OnPropertyChanged(nameof(BufferStep));
            OnPropertyChanged(nameof(BufferReading));
            OnPropertyChanged(nameof(OutputSizesHint));
        }
    }

    /// <summary>What this machine can be asked about scheduling, for the settings page.</summary>
    private readonly Audio.Interfaces.IRealtimeThread _realtime = new Audio.RealtimeThread();

    /// <summary>Whether this platform has an answer for real-time scheduling at all.</summary>
    /// <remarks>
    /// Windows has its own way of saying a thread is for audio and it is not written here yet, so
    /// the switch is shown but cannot be moved there: a control that does nothing is worse than a
    /// control that says why.
    /// </remarks>
    public bool RealtimeAvailable => _realtime.Possible;

    /// <summary>
    /// Whether the threads that must not be late are scheduled as audio threads.
    /// </summary>
    /// <remarks>
    /// Written into the environment as well as the settings, because the other half that needs
    /// the answer is in another process: a plugin host reads no settings of its own and inherits
    /// this instead.
    ///
    /// The output is opened again, which is what makes it take effect on the mixing thread now.
    /// A plugin already loaded keeps the scheduling it started with, since that is decided when
    /// its process makes the thread; the next one loaded gets the new answer.
    /// </remarks>
    public bool RealtimeAudio
    {
        get => _cfg.RealtimeAudio;
        set
        {
            if (_cfg.RealtimeAudio == value) return;

            _cfg.RealtimeAudio = value;
            _store.Save(_cfg);

            Audio.RealtimeThread.Wants(value);

            ApplyAudioSizes();

            OnPropertyChanged();
            OnPropertyChanged(nameof(RealtimeHint));
        }
    }

    /// <summary>What the switch means, said plainly enough to choose by.</summary>
    public string RealtimeHint =>
        !RealtimeAvailable
            ? "Not on this system yet. Windows has its own way of saying a thread is for audio " +
              "and this application does not use it, so there is nothing to switch on here."
            : _cfg.RealtimeAudio
                ? "The mixing thread and each plugin's own audio thread run ahead of everything " +
                  "else on the machine, which is what every serious audio application here does. " +
                  "The system may refuse it, in which case the log says so and nothing breaks. A " +
                  "plugin already loaded keeps what it started with; the next one gets this."
                : "The threads that must not be late take their turn like everything else, so a " +
                  "browser laying out a page can delay the sound. Switch it on if plugins break " +
                  "up, and listen: it is the one setting here that changes how the machine treats " +
                  "this application rather than how much audio is held.";

    /// <summary>
    /// Puts the sizes on the running output, so a change is heard now rather than next time.
    /// </summary>
    /// <remarks>
    /// **A setting that needs a restart is a setting nobody tunes**, and finding a buffer that
    /// suits a machine means trying several: every one of those used to be a restart and a fresh
    /// listen. One place, called by all four setters, because they are one decision and four
    /// copies of this line would be four chances for one of them to forget.
    ///
    /// Not the sample rate, which is the one that still waits: the mixer, every voice in it and
    /// every plugin that has been told what it is fed at are all built from it.
    /// </remarks>
    private void ApplyAudioSizes() => Tracker.ApplyAudioSizes(Sizes, _cfg.RenderAheadMs);

    /// <summary>
    /// What the slider is on: the size, and the latency it comes to.
    /// </summary>
    /// <remarks>
    /// Short, because it stands beside the slider and the pair has to fit a narrow window. What
    /// it means, and whether it is this machine's default, is said once underneath rather than
    /// twice.
    /// </remarks>
    public string BufferReading =>
        Sizes.BufferFrames + " · " + MillisecondsFor(Sizes.BufferFrames) + " ms";

    /// <summary>What rate the arithmetic between frames and milliseconds is done at.</summary>
    /// <remarks>
    /// The setting rather than what the device came up at, since the slider has to answer before
    /// anything is open. They are the same number in every case anybody has run.
    /// </remarks>
    private int Rate => _cfg.EngineSampleRate > 0
        ? _cfg.EngineSampleRate
        : Audio.TrackerOutput.DefaultSampleRate;

    /// <summary>What a number of frames comes to in milliseconds, at the engine's rate.</summary>
    private int MillisecondsFor(int frames) =>
        Math.Max(1, (int)Math.Round(frames * 1000.0 / Rate));

    /// <summary>
    /// How often the sound library tops the buffer up.
    /// </summary>
    /// <remarks>
    /// Chosen beside the buffer because it is half of the same decision: a period that cannot keep
    /// up with the buffer is a dropout with no other explanation.
    /// </remarks>
    private static readonly (int Milliseconds, string Label)[] UpdatePeriods =
    {
        (0, "Default for this machine"),
        (5, "every 5 ms"),
        (10, "every 10 ms"),
        (20, "every 20 ms")
    };

    /// <summary>The choices, for the picker to show.</summary>
    public string[] UpdatePeriodLabels { get; } = UpdatePeriods.Select(u => u.Label).ToArray();

    /// <summary>Which one is in force, read back off the settings by its number.</summary>
    public string SelectedUpdatePeriod
    {
        get => Chosen(UpdatePeriods, _cfg.OutputUpdatePeriodMs);
        set => Take(UpdatePeriods, value, _cfg.OutputUpdatePeriodMs, ms =>
        {
            _cfg.OutputUpdatePeriodMs = ms;
            ApplyAudioSizes();
            OnPropertyChanged(nameof(SelectedUpdatePeriod));
            OnPropertyChanged(nameof(OutputSizesHint));
        });
    }

    /// <summary>
    /// How many threads do the topping up.
    /// </summary>
    /// <remarks>
    /// One is the sound library's own default and means one thread fills every stream in the
    /// application in turn: a pad decoding a file delays the tracker, and the tracker rendering a
    /// block with a plugin in it delays every pad back. More lets a slow stream stop holding up
    /// the others. Past four they wake to look at buffers that are already full.
    /// </remarks>
    private static readonly (int Milliseconds, string Label)[] UpdateThreads =
    {
        (0, "Default for this machine"),
        (1, "1 thread"),
        (2, "2 threads"),
        (3, "3 threads"),
        (4, "4 threads")
    };

    /// <summary>The choices, for the picker to show.</summary>
    public string[] UpdateThreadLabels { get; } = UpdateThreads.Select(u => u.Label).ToArray();

    /// <summary>Which one is in force, read back off the settings by its number.</summary>
    public string SelectedUpdateThreads
    {
        get => Chosen(UpdateThreads, _cfg.OutputUpdateThreads);
        set => Take(UpdateThreads, value, _cfg.OutputUpdateThreads, count =>
        {
            _cfg.OutputUpdateThreads = count;
            ApplyAudioSizes();
            OnPropertyChanged(nameof(SelectedUpdateThreads));
            OnPropertyChanged(nameof(OutputSizesHint));
        });
    }

    /// <summary>The label whose number matches, or the first, which is always the default.</summary>
    private static string Chosen((int Milliseconds, string Label)[] offered, int held)
    {
        foreach (var (number, label) in offered)
        {
            if (number == held) return label;
        }

        return offered[0].Label;
    }

    /// <summary>Takes a label back to its number, stores it, and says so.</summary>
    /// <remarks>
    /// Shared by the three above, which differ only in which field they write: three copies of
    /// this loop would be three chances for one of them to forget to save.
    /// </remarks>
    private void Take(
        (int Milliseconds, string Label)[] offered, string label, int held, Action<int> put)
    {
        foreach (var (number, name) in offered)
        {
            if (name != label || held == number) continue;

            put(number);
            _store.Save(_cfg);

            return;
        }
    }

    /// <summary>What the three add up to, in the numbers actually in force.</summary>
    /// <remarks>
    /// The resolved numbers rather than what is stored, since "Default for this machine" tells
    /// nobody what their machine is doing. Said once under the three, because they are one
    /// decision and reading them apart is how somebody sets a buffer of twenty and leaves it
    /// topped up every twenty.
    /// </remarks>
    public string OutputSizesHint =>
        "Running with " + Sizes.BufferFrames + " frames of buffer" +
        (_cfg.OutputBufferSize <= 0 ? " (this machine's default)" : "") + ", " +
        MillisecondsFor(Sizes.BufferFrames) + " ms, topped up every " +
        Sizes.UpdatePeriodMs + " ms by " +
        (Sizes.UpdateThreads > 0 ? Sizes.UpdateThreads + " threads" : "the sound library's own one thread") +
        ". The buffer is the latency: what you hear was mixed that long ago, and it is what a key " +
        "waits before it sounds. Too small for the machine and the mixing cannot keep up, which is " +
        "a stutter with no other explanation. These are per platform, since Linux is buffering " +
        "underneath us already and Windows is not the same. All three take effect at once, so the " +
        "right one can be found by listening. If the sound goes strange afterwards, restart: " +
        "reopening the output is not the same as starting clean, and the setting is remembered.";

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

    /// <summary>The four choices, for the picker to show.</summary>
    public string[] RenderAheadLabels { get; } = RenderAheads.Select(a => a.Label).ToArray();

    /// <summary>
    /// Which cushion is in force, as the words rather than the milliseconds.
    /// </summary>
    /// <remarks>
    /// Read back off the settings by matching the number, so a cushion the settings hold that
    /// nobody offers falls back to the tightest. Takes effect at once: the output is opened again
    /// with the new cushion, which starts or stops the mixing-ahead thread with it.
    /// </remarks>
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

                ApplyAudioSizes();

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
              "Takes effect at once."
            : "The mixer works " + _cfg.RenderAheadMs + " ms ahead on a thread of its own, so a plugin " +
              "being late eats into that instead of into the output. It also means what you hear was " +
              "mixed " + _cfg.RenderAheadMs + " ms ago, which is what a key you press waits before it " +
              "sounds. Takes effect at once.";

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

            if (value) Diagnostics.Log.Open(new Files.AppFolder().Path(), true, Written);
            else Diagnostics.Log.Close();

            OnPropertyChanged();
            OnPropertyChanged(nameof(LogHint));
        }
    }

    /// <summary>
    /// Throws away the log and the one before it, and starts a new one.
    /// </summary>
    /// <remarks>
    /// Not asked about first, unlike deleting a recording or throwing away a song's changes: a
    /// log is not somebody's work, it is what the program has been muttering about itself, and a
    /// question before every clear would be in the way of the one case this exists for, which is
    /// emptying it before doing the thing you want to read about.
    /// </remarks>
    public IRelayCommand ClearLogCommand => new RelayCommand(() =>
    {
        bool went = Diagnostics.Log.Clear();

        OnPropertyChanged(nameof(LogHint));

        Bus.Say(went ? "The log was cleared" : "There was no log to clear");
    });

    /// <summary>
    /// Which parts of the app write to the log, one switch each.
    /// </summary>
    /// <remarks>
    /// Built from the areas the log itself knows about rather than from a list written out
    /// here, so an area added to <see cref="Diagnostics.Enums.LogArea"/> turns up on the page without
    /// anybody being told to add it.
    /// </remarks>
    public System.Collections.ObjectModel.ObservableCollection<LogAreaViewModel> LogParts { get; } = new();

    /// <summary>The areas the settings ask for, with nothing said meaning all of them.</summary>
    private Diagnostics.Enums.LogArea Written =>
        _cfg.LogAreas == 0 ? Diagnostics.Enums.LogArea.Everything : (Diagnostics.Enums.LogArea)_cfg.LogAreas;

    /// <summary>
    /// Builds the tick boxes from the areas the log knows about, once, while starting.
    /// </summary>
    private void BuildLogParts()
    {
        var on = Written;

        foreach (var (area, name) in Diagnostics.Log.Everywhere)
            LogParts.Add(new LogAreaViewModel(area, name, (on & area) != 0, LogPartChanged));
    }

    /// <summary>
    /// One of the tick boxes moved, so the areas are gathered up and put in force.
    /// </summary>
    /// <remarks>
    /// Nothing ticked is the log off, and not the log on with nothing to say. Nought is stored
    /// as "whatever there is", which is what makes a settings file written before the areas
    /// existed read as the whole log rather than as silence. The cost of that is this: taking
    /// the last area off would store nought, which reads back as all of them, so the one action
    /// anybody would take to quieten a log turned every area back on and there was nothing on
    /// the page to suggest why. So taking the last one off turns the log off, which is what
    /// somebody doing it means, and switching it on again with nothing remembered gives them
    /// everything.
    ///
    /// Applied straight away rather than at the next start: the point of narrowing a log is
    /// usually that something is happening right now and it is too loud to read.
    /// </remarks>
    /// <param name="part">Which box was ticked, which is not read: they are all gathered.</param>
    private void LogPartChanged(LogAreaViewModel part)
    {
        var wanted = Diagnostics.Enums.LogArea.None;

        foreach (var one in LogParts)
            if (one.Writes) wanted |= one.Area;

        if (wanted == Diagnostics.Enums.LogArea.None)
        {
            WriteLog = false;

            return;
        }

        _cfg.LogAreas = (int)wanted;
        _store.Save(_cfg);

        if (WriteLog) Diagnostics.Log.Open(new Files.AppFolder().Path(), true, Written);

        OnPropertyChanged(nameof(LogHint));
    }

    /// <summary>Where the file is, said out loud so it can be found without being hunted for.</summary>
    public string LogHint =>
        WriteLog
            ? "Writing to " + System.IO.Path.Combine(new Files.AppFolder().Path(), Diagnostics.Log.FileName) +
              ". Plugin processes write to the same file. Started again from empty when it reaches a few megabytes."
            : "Off. Nothing is written and nothing is slowed down. Turn this on before doing whatever went wrong, then look in " +
              new Files.AppFolder().Path() + ".";

    /// <summary>What is actually running, as against what has been asked for.</summary>
    public string EngineRateHint =>
        $"Running at {Tracker.EngineSampleRate} Hz. A change takes effect when the app is started again.";

    /// <summary>The rates, for the picker to show.</summary>
    public string[] EngineRateLabels { get; } = EngineRates.Select(r => r.Label).ToArray();

    /// <summary>What plugins this machine has. Scanned from SETTINGS, on demand.</summary>
    /// <remarks>
    /// It keeps the folders it was told to look in, and those live with the rest of the
    /// settings rather than beside the scan, so where somebody's plugins are is remembered
    /// across a start like anything else on the settings page.
    /// </remarks>
    public PluginLibraryViewModel Plugins { get; private set; } = new();

    /// <summary>Every output the card offers, filled once while starting.</summary>
    public ObservableCollection<AudioOutput> OutputDevices { get; } = new();

    /// <summary>Why some outputs are not in that list, or nothing when they all are.</summary>
    /// <remarks>
    /// Read off the engine rather than worked out here: whether the ASIO library is beside the
    /// program is a fact about the machine, and a page that guessed it would be guessing.
    /// </remarks>
    public string OutputsMissing => _audio.OutputsMissing;

    /// <summary>True when there is a reason to show.</summary>
    public bool HasOutputsMissing => OutputsMissing.Length > 0;

    /// <summary>
    /// The pads of the open profile, in the order they are laid out.
    /// </summary>
    /// <remarks>
    /// Rebuilt rather than edited whenever the profile or the matrix changes, so anything
    /// holding one of these is holding a pad that has gone. The pages that show them bind to
    /// the collection, which is why it is replaced in place rather than swapped.
    /// </remarks>
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

    /// <summary>
    /// The profiles there are, sorted and with default always among them, for the picker at the
    /// head of PADS.
    /// </summary>
    public ObservableCollection<string> ProfileNames { get; } = new();

    /// <summary>
    /// The themes, taken from <see cref="ThemeSwitch"/> rather than written out again.
    /// </summary>
    /// <remarks>
    /// Which themes there are is that class's to say, since it is the one that knows which file
    /// each of them is. A second list here could only drift from it.
    /// </remarks>
    public ObservableCollection<string> ThemeNames { get; } = new(ThemeSwitch.Names);

    /// <summary>
    /// The theme in force, which is applied and stored the moment it is picked.
    /// </summary>
    /// <remarks>
    /// A name the settings hold that is not one of the themes is resolved to the default rather
    /// than left standing, so a hand-edited settings file cannot leave the window unstyled.
    /// </remarks>
    [ObservableProperty] private string selectedTheme = ThemeSwitch.Default;

    /// <summary>
    /// Where the sound goes. Changing it takes everything down and brings it up on the new
    /// card, and the tracker's stream is reopened after it.
    /// </summary>
    [ObservableProperty] private AudioOutput? selectedOutputDevice;

    /// <summary>
    /// Which profile is open. Shown at the head of PADS and again on FIRE, so what is under
    /// your hands is named on the page you play from.
    /// </summary>
    /// <remarks>
    /// Whatever is put here has to be one of <see cref="ProfileNames"/> exactly, or the picker
    /// showing it has nothing to select and comes up blank. A name that is not among them is
    /// resolved to one that is, falling back to default, which always exists.
    /// </remarks>
    [ObservableProperty] private string selectedProfileName = "default";

    /// <summary>What is being typed into the box beside Add, before it is a profile.</summary>
    [ObservableProperty] private string newProfileName = "";

    /// <summary>
    /// How many rows the settings page is being typed towards, which is not yet how many there
    /// are: see <see cref="PadCount"/>.
    /// </summary>
    [ObservableProperty] private int rows = 4;

    /// <summary>And the columns, which follow the rows while the two are bracketed together.</summary>
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

    /// <summary>
    /// Why the matrix being typed cannot be applied, in words, and empty when it can.
    /// </summary>
    /// <remarks>
    /// The message names the count as well as the limit, since a matrix is two numbers and
    /// "too many pads" leaves somebody working out which of the two to change.
    /// </remarks>
    [ObservableProperty] private string matrixSizeError = "";

    /// <summary>Whether what is being typed is a matrix this app will build.</summary>
    public bool IsMatrixSizeValid => string.IsNullOrEmpty(MatrixSizeError);

    /// <summary>
    /// The matrix changed, so the window can take its own shape from it.
    /// </summary>
    /// <remarks>
    /// The pads are square, and only the window knows what room it has: it is given the rows
    /// and columns and works out its own size from them, which is not something a view model
    /// can do for it.
    /// </remarks>
    public event Action<int, int>? MatrixSizeChanged;

    /// <summary>
    /// Makes a profile from what is typed beside it, with clean pads rather than a copy of the
    /// ones on screen, and opens it. Does nothing for an empty name or one that already exists.
    /// </summary>
    public IRelayCommand AddProfileCommand { get; }

    /// <summary>
    /// Throws away the open profile and goes back to default, which itself cannot be deleted:
    /// there has to be somewhere to land.
    /// </summary>
    public IRelayCommand DeleteProfileCommand { get; }

    /// <summary>
    /// Puts the typed matrix in force: the pads are stored, rebuilt at the new size, the engine
    /// is resized under them and the window is told to take its new shape.
    /// </summary>
    /// <remarks>
    /// Dead unless what is typed is valid and is not already what is in force, so the button
    /// says whether there is anything to apply.
    /// </remarks>
    public IRelayCommand ApplyMatrixSizeCommand { get; }

    /// <summary>
    /// Builds the window: the pages, the settings they read, and the wiring between them.
    /// </summary>
    /// <remarks>
    /// Almost all of this is wiring, and the order matters in one direction only: a page has to
    /// exist before anything can be hung off it. The pieces worth knowing about are these.
    ///
    /// One rack is made here and handed to both the tracker and the machine list, since they
    /// are two views of the same shelf and two racks would be two answers to what you own. A
    /// song takes an instrument off a machine and keeps its own copy of it, which is why the
    /// two words are not interchangeable.
    ///
    /// The recordings run through everything. An instrument pointed at a different take frees
    /// the old one and claims the new one, so the shelf is asked to work out its usage again;
    /// a take an instrument is built on cannot be thrown away, so RECORD is given somewhere to
    /// ask before it deletes, and it asks the songs as well as the rack, because a song owns
    /// its instruments and a take nothing on the rack plays can still be the sound of three
    /// songs; a packed song puts what it carried on the shelf as it opens; and trimming or
    /// renaming a take changes what its instruments sound like while the player is holding the
    /// old audio.
    ///
    /// Two things are said out loud while starting, and both are said here because this is
    /// where there is finally a bar to say them on. The rack is brought into shape while it is
    /// being built, which is before any of this exists, and moving somebody's instruments
    /// without saying so would be the one thing worth saying all session. And a run that
    /// stopped without saying goodbye leaves a file behind saying what it was in the middle of,
    /// which is said out loud because a report nobody knows about is a report nobody sends.
    /// What there is to be got back is said after it rather than before: the report is a file
    /// to send, the recovered work is work, and the second is the one worth leaving on the bar.
    ///
    /// The MIDI wiring is five routers over one dispatcher, and which of them hears a message
    /// is decided by the roles ticked in SETTINGS rather than here. The mappings are the whole
    /// application's rather than a profile's: which pad a note fires does not change when
    /// another set of pads is opened. In front of all of them is
    /// a controller's own codec, which gets first refusal on every message and can only say
    /// that these bytes mean those bytes: a device nobody has written a file for is passed
    /// straight through. Behind them, one <see cref="ControlTargets"/> shared by the lot, since
    /// two would be two caches of the same answers and two chances of them disagreeing, and a
    /// control surface asks exactly the question a knob does. Automation writes through those
    /// same targets, which is what makes the clock arriving at line 32 and a knob writing from
    /// CC 74 one act: a machine still only answers on a track that plays it, and an insert is
    /// still found by what it is rather than by where it sits, without any of that being said
    /// twice. Every value the router writes is then offered to the recorder, which does nothing
    /// at all unless somebody armed it and the song is playing. That is subscribed alongside
    /// the routers rather than beside the controller's screen, because a screen is for the one
    /// device that has one and this has to happen for every controller there is.
    ///
    /// FIRE is the pads and nothing else: the note router is skipped while that page is open.
    /// A controller with both jobs ticked sends one note to both lanes, and the same pad that
    /// fires a sample would also play the armed track's instrument, which is two sounds from
    /// one press and neither of them asked for. On the page whose whole purpose is the pads,
    /// the pads have it.
    ///
    /// Links live in two layers and the link object is handed both. The desk's are in the
    /// settings; a song keeps its own and takes it with it, so what is handed over is the list
    /// and a way of saying it moved, and where that list lives stays the tracker's business.
    /// The song's half is announced before it changes as well as after, so a layout can be
    /// taken back like anything else in a song; the desk's half is not a song's business and is
    /// not recorded. Both are then shown twice: <see cref="Links"/> in SETTINGS is everything,
    /// and the tracker's is the same links narrowed to what the open song holds. Two lists of
    /// one thing, wanted in two places for two reasons.
    ///
    /// A Mackie surface is wired in both directions, and the two halves are one piece of work
    /// rather than two. Writing to it is what makes a desk feel attached to the music rather
    /// than wired to it, and it is also what makes the reading half correct: a fader there
    /// lands on the value rather than picking up, which is only right because the motor has
    /// already driven it to where the value is. It needs no file and no learning, since the
    /// protocol says what every control on it is, and it reaches the mixer through the same
    /// targets a hand-made link does. Its transport buttons and its faders come out of one
    /// device on one port, so the transport router and the surface router divide that stream
    /// between them rather than competing for it: the five transport notes are the one place
    /// they could overlap and are refused by name in the second. The mix is drawn on it
    /// whenever the tracker says the mix moved, since the levels are under its own faders and
    /// the names are on its own display and it has no other way of hearing that either moved.
    ///
    /// A controller with a screen is told what the knob under your hand is doing, including
    /// when it has not caught up yet, which is drawn where the parameter is rather than where
    /// the knob is: what you need to know is where to turn to. Nothing asks whether a device
    /// has a screen, since one with no output is answered with a quiet false and a few bytes
    /// down a port nobody reads cost nothing. It is written only to the ports carrying the
    /// controls, not to everything with a role: a screen sits with the knobs, and the transport
    /// often arrives on a port of its own speaking Mackie Control, where Arturia's own system
    /// exclusive is a foreign language and stopped the transport answering until the device was
    /// power cycled. What it says at rest is this app's name and the song's, put there over
    /// whichever DAW the device was told about; a knob's reading lands on top of that and comes
    /// back to it. A control that has not caught up yet says so in the same place, and when the
    /// reading meets the bar it takes over: without that the knob is simply dead for half a
    /// turn and nothing anywhere says why.
    ///
    /// Two other things happen while starting that could have waited and deliberately do not.
    /// The controller profiles are read now rather than when something first asks, because a
    /// startup that says what it found is the difference between "the names are missing" and
    /// "the file is not there", and the log is the only place either of those is ever visible.
    /// And the messages are watched here for playing things, which is a second subscription:
    /// the MIDI page has its own for learning and for showing what arrived, and it deliberately
    /// sees the raw bytes rather than what a codec made of them, which is what a monitor is
    /// for. Each control change is also shown to the profiles, which work out from the numbers
    /// which of a device's programs is running, since it will not say and cannot be asked. That
    /// is a clue rather than an answer, only means anything for a device with a file, and
    /// changes nothing except what its controls are called.
    /// </remarks>
    public MainViewModel(
        IAudioEngine audio,
        ConfigStore store,
        AppConfig cfg,
        IMidiService midiService,
        IRecordingService recordingService,
        IWaveformService waveformService,
        IAudioRouting routing,
        ISoundMachineProjects machines,
        SoundDevices.SoundEffects.Interfaces.ISoundEffectProjects? effects = null)
    {
        _machines = machines;
        _effects = effects ?? new SoundDevices.SoundEffects.SoundEffectProjects();
        _audio = audio;
        _store = store;
        _cfg = cfg;

        Layout = new Midi.DefaultLayout(_profiles);

        Midi = new MidiViewModel(store, cfg, midiService, _profiles);

        Plugins = new PluginLibraryViewModel(store, cfg);
        Record = new RecordViewModel(recordingService, new LevelMeterService(), waveformService, store, cfg, routing);

        Takes = new TakeFilter(Record.Recordings);

        Designer.Browse = Takes;

        Designer.Takes = new SoundDevices.SoundMachines.TakeLibrary(Record.Recordings, waveformService);

        Designer.Shelf = new SoundDevices.SoundMachines.TakeShelf(
            Record.Recordings, take => Designer.PutTake(take.FilePath));

        var rack = new SoundMachineRack();

        Tracker = new TrackerViewModel(
            audio, rack, Record.Recordings, _machines, store, cfg, Plugins, waveformService, _effects,
            _effectInFront);
        Machines = new RackViewModel(rack, Tracker, _machines, Record.Recordings, waveformService, Plugins, _effects);

        MachineShelf = new SoundMachineShelfViewModel(_machines);

        EffectShelf = new SoundEffectShelfViewModel(_effects);

        MachineShelf.Changed += () => Machines.Refresh();

        EffectShelf.Changed += () => Machines.Refresh();

        _padDeck = new PadDeck(Pads);
        Transport = new TransportSwitch(() => DeckForPage, Record, _padDeck, Tracker);

        Machines.InstrumentChanged += (_, instrument) =>
        {
            Tracker.ApplyMachineEdit(instrument);

            Record.RefreshUsage();
        };

        Machines.RackChanged += (_, _) =>
        {
            Tracker.RefreshRack();
            Record.RefreshUsage();
        };

        Machines.Machines.CollectionChanged += (_, _) => Tracker.RefreshRack();

        Record.SampleUsage = new JingleBox2.Tracker.SampleUsers(rack, Tracker.Songs);

        Tracker.RecordingsArrived += (_, _) => Record.Rescan();

        Record.RecordingChanged += (_, path) => Tracker.ReloadSample(path);
        Record.RecordingRenamed += (_, moved) => Tracker.RenameSample(moved.From, moved.To);

        Watch(Tracker, "Tracker");
        Watch(Machines, "Machines");
        Watch(Record, "Record");

        if (Machines.Status.Length > 0) Bus.Warn(Machines.Status, "Machines");

        if (Diagnostics.CrashReport.FromLastTime.Length > 0)
        {
            Bus.Warn("JingleBox stopped unexpectedly last time. What it was doing is written in " +
                     Diagnostics.CrashReport.FromLastTime, "Crash");
        }

        if (Tracker.Recovered.Length > 0) Bus.Warn(Tracker.Recovered, "Tracker");

        StatusLine = new StatusViewModel(
            Bus,
            () => Record.Level,
            () => Math.Max(_audio.GetOutputLevel(), Tracker.OutputLevel));

        Follow(Tracker);
        Follow(Machines);
        Follow(Record);

        Follow(this);
        Pads.CollectionChanged += (_, _) => Retell();

        Retell();

        AddProfileCommand = new RelayCommand(AddProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile);
        ApplyMatrixSizeCommand = new RelayCommand(ApplyMatrixSize, CanApplyMatrixSize);

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

        Rows = _cfg.Rows;
        Columns = _cfg.Columns;

        EnsureProfilesInitialized(PadCount);
        RefreshProfilesList();

        var wanted = string.IsNullOrWhiteSpace(_cfg.SelectedProfile) ? "default" : _cfg.SelectedProfile.Trim();
        var resolved = ProfileNames.FirstOrDefault(n => string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase))
                      ?? ProfileNames.FirstOrDefault()
                      ?? "default";

        _suspendSave = true;
        try
        {
            SelectedProfileName = resolved;
            _cfg.SelectedProfile = resolved;

            SelectedTheme = ThemeSwitch.Resolve(_cfg.SelectedTheme);
            _cfg.SelectedTheme = SelectedTheme;
        }
        finally
        {
            _suspendSave = false;
        }

        BuildPadsFromSelectedProfile(PadCount);

        PadHistory.Opened(PadsInProfile());

        BuildLogParts();

        var padRouter = new MidiRouter(_cfg.Midi, new PadTriggerAdapter(Pads));

        Keys = new MidiMonitor(new TrackerNoteAdapter(Tracker, Machines));

        Machines.MidiKeys = Keys;
        Tracker.MidiKeys = Keys;

        var noteRouter = new MidiNoteRouter(Keys);

        ControlLink = new ControlLink(_cfg.Midi.Controls, () => _store.Save(_cfg));

        var targets = new ControlTargets(
            Tracker, _machines, Machines, new TransportPresses(Transport), _effects, _effectInFront);

        var controlRouter = new MidiControlRouter(
            () => ControlLink.Mappings,
            targets,
            () => ControlLink.Say(),
            Layout,
            _profiles);
        ControlLink.UseThis();

        Tracker.UseAutomation(targets);

        controlRouter.Moved += (mapping, target, value) => Tracker.Automation.Moved(mapping, target, value);

        ControlLink.Song = () => Tracker.Song?.Controls;
        ControlLink.SongChanged = Tracker.ControlsChanged;

        ControlLink.SongChanging = () => Tracker.ControlsChanging();

        Links = new ControlLinksViewModel(ControlLink, profiles: _profiles, ports: Ports);

        Tracker.DeskControls = Links;

        var transport = new MidiTransportRouter(new TransportAdapter(Transport), _profiles);

        var surface = new MackieSurface(
            midiService, targets, () => Tracker.TrackCount,
            track => Tracker.Strips.FirstOrDefault(one => one.Track == track)?.InstrumentName
                         is { Length: > 0 } named
                     ? named
                     : "TR-" + (track + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture));

        var mackie = new MidiMackieRouter(targets, () => Tracker.TrackCount, surface, _profiles);

        Tracker.MixShown = surface.Draw;

        var dispatcher = new MidiDispatcher(
            _cfg.Midi,
            padRouter.Handle,
            msg => { if (SelectedTab != UseTab) noteRouter.Handle(msg); },
            msg =>
            {
                if (ControlLink.Handle(msg) is { } made) controlRouter.Caught(made);

                controlRouter.Handle(msg);
            },
            msg =>
            {
                transport.Handle(msg);

                mackie.Handle(msg);
            });

        var screen = new ControllerScreens(
            () => new MidiPortBindings().DevicesWith(_cfg.Midi.Devices, MidiPortBindings.EveryRole),
            _profiles,
            new ArturiaDisplay(midiService, null, _profiles),
            new MackieDisplay(midiService, _profiles, () => surface.Device));

        screen.Standing("JingleBox2", Tracker.SongName);

        SayItAgain(screen);

        Tracker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TrackerViewModel.SongName))
                screen.Standing("JingleBox2", Tracker.SongName);
        };

        controlRouter.Moved += (mapping, target, value) =>
        {
            double range = target.Max - target.Min;

            screen.Moved(
                mapping.Device,
                mapping.Kind switch
                {
                    ControlKind.Action => ScreenKind.Pad,
                    ControlKind.Mix => ScreenKind.Fader,
                    _ => ScreenKind.Knob
                },
                range > 0 ? (value - target.Min) / range : 0,
                target.Name,
                target.Reads(value));
        };

        _codecs = new ControllerCodecs(midiService, _profiles);

        _profiles.Reload();

        controlRouter.Reaching += (mapping, target, wanted) =>
        {
            double range = target.Max - target.Min;

            screen.Moved(
                mapping.Device,
                ScreenKind.Knob,
                range > 0 ? (target.Value - target.Min) / range : 0,
                target.Name,
                "pick up " + target.Reads(target.Value));
        };

        midiService.MessageReceived += (_, msg) =>
        {
            if (_codecs.Read(msg) is not { } read) return;

            if (read.Type == MidiMessageType.ControlChange)
                _profiles.Saw(read.Device, read.Channel, read.Value);

            dispatcher.Handle(read);
        };

        ThemeSwitch.Apply(SelectedTheme);

        PropertyChanged += OnMainChanged;
    }

    /// <summary>
    /// The rows were typed: the columns follow while the two are bracketed together, and the
    /// button says again whether what is now typed can be applied.
    /// </summary>
    /// <remarks>
    /// The two hooks set each other and cannot chase each other, because setting a property to
    /// what it already holds raises nothing.
    /// </remarks>
    partial void OnRowsChanged(int value)
    {
        if (LinkPadMatrix) Columns = value;

        ValidateMatrixSize();
        (ApplyMatrixSizeCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    /// <inheritdoc cref="OnRowsChanged(int)"/>
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

    /// <summary>
    /// Works out whether what is typed is a matrix this app will build, and says why not.
    /// </summary>
    /// <remarks>
    /// Four pads at least, since fewer is not a matrix, and sixteen at most unless the extended
    /// matrix has been turned on. The message names both the limit and what is currently typed:
    /// a matrix is two numbers, and "too many pads" alone leaves somebody working out which of
    /// the two to change.
    /// </remarks>
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

    /// <summary>
    /// Whether there is anything to apply: a valid matrix that is not already the one in force.
    /// </summary>
    private bool CanApplyMatrixSize() => IsMatrixSizeValid && (Rows != _cfg.Rows || Columns != _cfg.Columns);

    /// <summary>
    /// Puts the typed matrix in force, everywhere that counts pads.
    /// </summary>
    /// <remarks>
    /// The order is the point. What is on the pads now is stored into the open profile first,
    /// or it would be thrown away with the pads it is on; then the settings, since everything
    /// below reads the count from them; then the engine, which stops anything on a pad that is
    /// going away and leaves the rest playing; then the pads themselves and the MIDI router.
    ///
    /// The pages that show pads are told about the count and the columns by hand, because both
    /// are worked out from the settings rather than held, so nothing else would ever say they
    /// had changed. The window is told last, since the pads are square and only it can work out
    /// what room that needs.
    /// </remarks>
    private void ApplyMatrixSize()
    {
        if (!IsMatrixSizeValid) return;

        SavePadsIntoProfile(_cfg.SelectedProfile);

        _cfg.Rows = Rows;
        _cfg.Columns = Columns;

        _audio.Resize(PadCount);

        EnsureProfilesInitialized(PadCount);
        BuildPadsFromSelectedProfile(PadCount);

        Midi.UpdatePadCount(PadCount);

        _store.Save(_cfg);

        OnPropertyChanged(nameof(PadCount));
        OnPropertyChanged(nameof(PadColumns));

        MatrixSizeChanged?.Invoke(Rows, Columns);

        (ApplyMatrixSizeCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Another profile was picked: the pads on screen are stored into the one being left, and
    /// the new one is poured into them.
    /// </summary>
    /// <remarks>
    /// Stored before switching, or the edits made since the profile was opened would go with
    /// it. The name is then resolved against the list and put back, because the picker showing
    /// it can only show a name that is really in the list.
    ///
    /// The history is opened afresh at the end. A different profile is a different set of pads,
    /// and what was done to the last one is not something to undo onto this one.
    /// </remarks>
    partial void OnSelectedProfileNameChanged(string value)
    {
        if (_suspendSave) return;

        var name = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        EnsureProfilesInitialized(PadCount);

        SavePadsIntoProfile(_cfg.SelectedProfile);

        _cfg.SelectedProfile = EnsureProfileExistsAndReturnResolved(name, padCount: PadCount);

        _store.Save(_cfg);

        _suspendSave = true;
        try
        {
            RefreshProfilesList();

            SelectedProfileName =
                ProfileNames.FirstOrDefault(n => string.Equals(n, _cfg.SelectedProfile, StringComparison.OrdinalIgnoreCase))
                ?? "default";

            ApplySelectedProfileToPads();
        }
        finally
        {
            _suspendSave = false;
        }

        PadHistory.Opened(PadsInProfile());
    }

    /// <summary>
    /// A theme was picked: resolved against the ones there are, applied at once and stored.
    /// </summary>
    partial void OnSelectedThemeChanged(string value)
    {
        if (_suspendSave) return;

        var resolved = ThemeSwitch.Resolve(value);

        _cfg.SelectedTheme = resolved;
        ThemeSwitch.Apply(resolved);

        _store.Save(_cfg);
    }

    /// <summary>
    /// Watches this object's own properties for the one that has to reach the sound: the output
    /// device.
    /// </summary>
    /// <remarks>
    /// Changing the device closes the old one, which takes the tracker's stream with it, so the
    /// tracker is asked to open its stream again straight afterwards. Nothing else would notice
    /// until the next note, which is a long way from here.
    /// </remarks>
    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suspendSave) return;

        if (e.PropertyName == nameof(SelectedOutputDevice))
        {
            if (SelectedOutputDevice != null)
            {
                _audio.SetOutputDevice(SelectedOutputDevice.Id);

                Tracker.ReopenAudio();
            }

            SaveNow();
        }
    }

    /// <summary>
    /// Anything on any pad moved: the settings are written, and the history is told.
    /// </summary>
    /// <remarks>
    /// The one place every pad edit already ended, which is why the history is hooked here
    /// rather than at each thing that edits a pad.
    ///
    /// Told after the settings have been written, so what the history reads back is what was
    /// stored. What it is told is which pad and which setting, so a step gathers by both: a
    /// level is a fader and a fader is a stream, and dragging one is one thing somebody did.
    /// </remarks>
    private void OnPadChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suspendSave) return;

        SaveNow();

        PadsMoved(sender is PadViewModel pad
            ? Pads.IndexOf(pad) + "." + e.PropertyName
            : e.PropertyName ?? "");
    }

    /// <summary>Tells the history what the pads look like now, if it is listening.</summary>
    /// <remarks>
    /// Reads them out of the settings rather than off the view models, because the settings are
    /// what a step has to put back and reading them twice is a chance for the two to disagree.
    /// </remarks>
    private void PadsMoved(string about)
    {
        if (PadHistory.Walking) return;

        PadHistory.Did(PadsInProfile(), about);
    }

    /// <summary>The pads of the profile that is open, as they are stored.</summary>
    private System.Collections.Generic.List<Config.PadConfig>? PadsInProfile()
    {
        string name = string.IsNullOrWhiteSpace(SelectedProfileName) ? "default" : SelectedProfileName.Trim();

        return GetProfileByName(name)?.Pads;
    }

    /// <summary>
    /// Puts a kept set of pads back, and has the ones on screen read themselves again.
    /// </summary>
    /// <remarks>
    /// Into the profile rather than instead of it, so anything holding the profile is still
    /// holding the one that is open. The view models are rebuilt from it afterwards, which is
    /// the same path opening a profile takes.
    /// </remarks>
    private void PadsBack(System.Collections.Generic.List<Config.PadConfig>? wanted)
    {
        if (wanted is null) return;

        string name = string.IsNullOrWhiteSpace(SelectedProfileName) ? "default" : SelectedProfileName.Trim();

        var profile = GetProfileByName(name);
        if (profile is null) return;

        PadHistory.Walking = true;

        try
        {
            profile.Pads.Clear();
            profile.Pads.AddRange(wanted);

            BuildPadsFromSelectedProfile(wanted.Count);

            _store.Save(_cfg);
        }
        finally
        {
            PadHistory.Walking = false;
        }
    }

    /// <summary>
    /// Makes a profile out of the name typed beside the button and opens it.
    /// </summary>
    /// <remarks>
    /// The new one starts on clean pads rather than on a copy of what is on screen: somebody
    /// making a second profile is starting again, and a copy of the first is the one thing they
    /// can already get by not making one. What is on the pads now is stored into the profile
    /// being left first, as everywhere else here.
    ///
    /// The name is put through <see cref="NormalizeProfileName"/>, and a name that is already
    /// taken is refused quietly: two profiles with one name would be two files nobody could
    /// tell apart in the picker.
    /// </remarks>
    private void AddProfile()
    {
        var raw = (NewProfileName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(raw)) return;

        var name = NormalizeProfileName(raw);
        if (string.IsNullOrWhiteSpace(name)) return;

        EnsureProfilesInitialized(padCount: PadCount);

        if (_cfg.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            return;

        SavePadsIntoProfile(_cfg.SelectedProfile);

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

    /// <summary>
    /// Throws away the open profile and lands on default.
    /// </summary>
    /// <remarks>
    /// Default cannot be deleted, since there has to be somewhere to land, and the pads are
    /// stored into the profile on the way out even though it is about to go: a delete that is
    /// interrupted part way through should not also have lost the last edit.
    /// </remarks>
    private void DeleteProfile()
    {
        var cur = (_cfg.SelectedProfile ?? "default").Trim();
        if (string.IsNullOrWhiteSpace(cur)) return;
        if (string.Equals(cur, "default", StringComparison.OrdinalIgnoreCase)) return;

        EnsureProfilesInitialized(padCount: PadCount);

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

    /// <summary>
    /// Writes the window's own settings out: the device, the pads of the open profile, which
    /// profile that is, and the theme.
    /// </summary>
    /// <remarks>
    /// The theme has already been stored by the hook that applied it. It is written again here
    /// so that one save is the whole of what this object holds, rather than most of it plus
    /// whatever another path happened to have got round to.
    /// </remarks>
    private void SaveNow()
    {
        EnsureProfilesInitialized(padCount: PadCount);

        _cfg.SelectedOutputDeviceId = SelectedOutputDevice?.Id ?? -1;

        SavePadsIntoProfile(_cfg.SelectedProfile);

        _cfg.SelectedProfile = string.IsNullOrWhiteSpace(SelectedProfileName) ? "default" : SelectedProfileName.Trim();

        _cfg.SelectedTheme = string.IsNullOrWhiteSpace(SelectedTheme) ? "Dark" : SelectedTheme.Trim();

        _store.Save(_cfg);
    }

    /// <summary>
    /// Throws the pads away and builds them again from the open profile.
    /// </summary>
    /// <remarks>
    /// The old ones are disposed rather than dropped, since each holds a channel and an effect
    /// chain of its own. Every new pad is given the plugin library and whatever chain the
    /// profile saved for it, so a pad comes back with its effects on it and pointed at itself.
    ///
    /// A pad is always selected afterwards, because whatever the PADS page was showing belonged
    /// to the pads that have just gone.
    /// </remarks>
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

            pad.UsePlugins(Plugins, _effects, _effectInFront);
            pad.RestoreEffects(padCfg.Plugins);

            pad.PropertyChanged += OnPadChanged;
            Pads.Add(pad);
        }

        SelectedPad = Pads.Count > 0 ? Pads[0] : null;
    }

    /// <summary>
    /// Pours the open profile into the pads that already exist, without rebuilding them.
    /// </summary>
    /// <remarks>
    /// For switching profiles, where the matrix has not changed and the view models can stay.
    /// If the two counts have drifted apart it fills what it can and leaves the rest, since the
    /// pads on screen are what the audio engine was sized for and quietly growing the list here
    /// would leave the two disagreeing.
    ///
    /// Saving is suspended throughout: every one of these writes is this object pouring, not
    /// somebody editing, and each would otherwise save the settings and post an undo step.
    /// </remarks>
    private void ApplySelectedProfileToPads()
    {
        EnsureProfilesInitialized(padCount: PadCount);

        var profile = GetProfileByName(_cfg.SelectedProfile);

        _suspendSave = true;
        try
        {
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

    /// <summary>
    /// Writes what is on the pads into a profile, including what their plugins are holding.
    /// </summary>
    /// <remarks>
    /// The chain is read off the audio engine rather than off the pad, so what is stored is
    /// what is loaded right now rather than what the pad was opened with. The patches are not
    /// read here: this runs on every property a pad has, and a level dragged is a hundred of
    /// those, while asking a plugin for its patch is a round trip to another process and a
    /// third of a megabyte. The pad reads its own patches when its chain settles, on the same
    /// 600ms tick that makes it save at all, and what it read is what is written in here.
    /// </remarks>
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

            var captured = _chains.Capture(
                _audio.GetPadInsert(i) as JingleBox2.Audio.Plugins.PluginChain);

            for (int device = 0; device < captured.Devices.Count && device < vm.Patches.Count; device++)
                captured.Devices[device].State = vm.Patches[device];

            pc.Plugins = captured.IsEmpty ? null : captured;
        }
    }

    /// <summary>
    /// Rebuilds the list the picker shows: the profiles there are, sorted, without duplicates
    /// and with default among them whatever happens.
    /// </summary>
    /// <remarks>
    /// Names are compared without regard to case throughout, here and everywhere else that
    /// looks a profile up, because they are typed and a profile found by one path and missed by
    /// another is how a profile comes to be created twice. The stored selection is brought back
    /// to default if it names something that is not there, so a hand-edited settings file
    /// cannot leave the app pointed at a profile it does not have.
    /// </remarks>
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

        if (!ProfileNames.Any(n => string.Equals(n, "default", StringComparison.OrdinalIgnoreCase)))
            ProfileNames.Insert(0, "default");

        if (string.IsNullOrWhiteSpace(_cfg.SelectedProfile) ||
            !_cfg.Profiles.Any(p => string.Equals(p.Name, _cfg.SelectedProfile, StringComparison.OrdinalIgnoreCase)))
        {
            _cfg.SelectedProfile = "default";
        }
    }

    /// <summary>
    /// Brings the stored profiles into a shape the rest of this class can rely on, and is
    /// called before anything reads them.
    /// </summary>
    /// <remarks>
    /// There is always at least one profile, it is always called something, there is always one
    /// called default, and every one of them holds exactly as many pads as the matrix says.
    /// Profiles that are short are filled with empty pads and ones that are long are cut from
    /// the end, which is what makes a matrix change something a profile written for another
    /// size survives.
    ///
    /// A settings file from before profiles existed kept its pads in one flat list. Those are
    /// carried into a profile called default rather than dropped, which is the only reason that
    /// list is still read at all.
    /// </remarks>
    private void EnsureProfilesInitialized(int padCount)
    {
        _cfg.Profiles ??= new System.Collections.Generic.List<ConfigProfile>();

        if (_cfg.Profiles.Count == 0)
        {
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

    /// <summary>
    /// Finds a profile by name, making it if there is none, and answers the name as it is
    /// really stored.
    /// </summary>
    /// <remarks>
    /// The stored spelling rather than the asked-for one, because the picker matches on the
    /// exact string: handing back what somebody typed would leave the list showing nothing
    /// selected while the right profile was open.
    /// </remarks>
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

        return _cfg.Profiles.First(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Name;
    }

    /// <summary>
    /// The profile of that name, or default, which is guaranteed to exist by then.
    /// </summary>
    /// <remarks>
    /// Answers a profile rather than null on purpose. Every caller here is about to read or
    /// write pads and has nothing sensible to do with nothing, and default is the one profile
    /// that cannot be missing.
    /// </remarks>
    private ConfigProfile GetProfileByName(string? name)
    {
        EnsureProfilesInitialized(padCount: PadCount);

        var n = string.IsNullOrWhiteSpace(name) ? "default" : name.Trim();

        var p = _cfg.Profiles.FirstOrDefault(x => string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase));
        if (p != null) return p;

        return _cfg.Profiles.First(x => string.Equals(x.Name, "default", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Empty pads, named Pad 1 upwards, at full level and playing nothing.</summary>
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

    /// <summary>
    /// A copy of a stored pad, effect chain included.
    /// </summary>
    /// <remarks>
    /// The chain is copied rather than shared, or two profiles would be holding one chain and
    /// editing either would edit both.
    /// </remarks>
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

    /// <summary>
    /// Turns a typed name into one that can be a key: lower case, letters, digits, hyphen and
    /// underscore, with runs of hyphens collapsed and the ends trimmed.
    /// </summary>
    /// <remarks>
    /// Answers an empty string for a name with nothing usable left in it, which every caller
    /// reads as "not a name" and refuses rather than storing a profile called "-".
    /// </remarks>
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

    /// <summary>
    /// Says the greeting a second and third time, a few seconds after the application starts.
    /// </summary>
    /// <remarks>
    /// A controller is not ready the instant the operating system lists it. A KeyLab mkII powered
    /// on a moment before this starts is on the bus, opens for writing, takes the message without
    /// complaint and shows nothing, while the identical bytes sent by hand a minute later appear at
    /// once. There is no message that asks a device whether it is ready, so the only answer is to
    /// say it again.
    ///
    /// Two repeats and then it stops, because this is about a device settling rather than about
    /// keeping a screen up to date: anything that changes afterwards says so itself. A timer rather
    /// than a wait, so nothing about starting up is held back for it, and it disposes itself.
    /// </remarks>
    /// <param name="screen">The screens to greet again.</param>
    private static void SayItAgain(IControllerScreen screen)
    {
        int said = 0;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };

        timer.Tick += (_, _) =>
        {
            screen.Again();

            if (++said >= 2) timer.Stop();
        };

        timer.Start();
    }

}
