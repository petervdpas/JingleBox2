using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Config;
using JingleBox2.Diagnostics;
using JingleBox2.Audio.Records;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Machines;
using JingleBox2.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Tracker.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Machines.Interfaces;
using JingleBox2.ViewModels.Interfaces;
using JingleBox2.Tracker.Records;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Machines.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// Holds the song being edited and drives the player. All sequencing, editing, and cursor
/// maths live in the Tracker namespace; this class is the bridge to the view.
/// </summary>
/// <remarks>
/// It is the one object the tracker page, the mixer, the instrument panels and the surfaces all
/// reach through, which is why it answers to five interfaces rather than one: what a panel wants
/// (<see cref="ITrackerPanel"/>) is not what the transport bar wants
/// (<see cref="ITransportDeck"/>), and neither is what a MIDI keyboard wants
/// (<see cref="Midi.Interfaces.IPlaysNotes"/>). Each of those says what it is for on itself; what is here
/// is how this one implementation does it.
/// </remarks>
public sealed partial class TrackerViewModel : ObservableObject, IInstrumentAudition, ITrackerPanel, ITransportDeck, Midi.Interfaces.IPlaysNotes, Shortcuts.Interfaces.IShortcutContext
{
    /// <summary>The machines this run has, the one instance everything shares.</summary>
    private readonly IMachineProjects _machines;

    /// <summary>Every edit to a pattern, so each one lands in the undo history.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IPatternEdit Edits = new PatternEdit();

    /// <summary>The recordings a packed song carries inside it.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly ISongSamples Carried = new SongSamples();

    /// <summary>Which instruments play a given recording.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly ISampleUsers Usage = new SampleUsers();

    /// <summary>The machines a song wants that this installation has not got.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IMissingMachines Missing = new MissingMachines();

    /// <summary>Whether two paths are one file, by this machine's rules.</summary>
    /// <remarks>
    /// Shared, because the walk that reports a song's missing recordings is static and cannot
    /// reach an instance field. The rule holds nothing of its own.
    /// </remarks>
    private static readonly IFilePaths _paths = new FilePaths();

    /// <summary>The clock, the mixer and everything that makes a sound. One per tracker.</summary>
    private readonly TrackerPlayer _player;

    /// <summary>The songs folder, and the reading and writing of a song file.</summary>
    private readonly SongStore _store;

    /// <summary>
    /// What you own, which is where an instrument comes from and never where one lives.
    /// </summary>
    /// <remarks>
    /// Only read here, and only when a machine is brought into the song: a song's instruments
    /// are its own, so nothing on the rack reaches back into a song already written.
    /// </remarks>
    private readonly MachineRack _rack;

    /// <summary>
    /// Asks the mixer what every strip is reading, so the meters on the screen move.
    /// </summary>
    /// <remarks>
    /// Polled rather than pushed: the audio side should not be calling into the UI dozens of
    /// times a second, and a meter that misses a frame costs nothing. What it runs on is
    /// whether anything is sounding rather than whether the transport is playing; see
    /// <see cref="ReadMeters"/> for why that distinction cost two separate faults.
    /// </remarks>
    private readonly DispatcherTimer _meters;

    /// <summary>Writes the song down while it is unsaved, so a crash costs a minute, not a session.</summary>
    private readonly DispatcherTimer _keeping;

    /// <summary>How often unsaved work is written down.</summary>
    private const int KeepSeconds = 20;

    /// <summary>
    /// What a kept song is called: the name you gave it with this on the end.
    /// </summary>
    /// <remarks>
    /// Kept in the songs folder rather than somewhere of its own, so it turns up in the list of
    /// songs like anything else. Nobody has to be told where to look, and getting rid of one is
    /// the Delete button that is already there.
    /// </remarks>
    private const string RecoveredSuffix = " (recovered)";

    /// <summary>
    /// What a song is called before it is called anything.
    /// </summary>
    /// <remarks>
    /// A placeholder and never a file name. Saving under it would put a song called "untitled"
    /// in the list, and the next unnamed song would either overwrite it or sit beside it as
    /// "untitled 2", which is how a folder of songs stops being worth reading. Saving asks for
    /// a real name instead.
    /// </remarks>
    public const string Unnamed = "untitled";

    /// <summary>The file this session is keeping its unsaved work in, if it has needed to.</summary>
    private string _kept = "";

    /// <summary>Something found on the way in that the last session never got to save.</summary>
    public string Recovered { get; } = "";

    /// <summary>
    /// The shelf of takes, shared with RECORD rather than copied.
    /// </summary>
    /// <remarks>
    /// The same list the recording page owns, so a take made while the tracker is open is on
    /// the shelf an instrument picks from without anybody being told to look again. A packed
    /// song is the one case where it has to be told; see <see cref="OpenSong"/>.
    /// </remarks>
    private readonly ObservableCollection<Recording> _recordings;

    /// <summary>Where the velocity preference is kept. Null in a test or a headless run.</summary>
    private readonly ConfigStore? _configStore;

    /// <summary>The settings as they stand, for the handful of preferences the tracker reads.</summary>
    private readonly AppConfig? _config;

    /// <summary>The song being worked on. There is always one, even if it is empty.</summary>
    [ObservableProperty] private Song song;

    /// <summary>
    /// The pattern under the cursor, which is the one the grid draws and edits.
    /// </summary>
    /// <remarks>
    /// Always set through the property and never through this field. The generated setter
    /// subscribes to the pattern's own change event, and that is the part a field assignment
    /// skips; see the constructor.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PatternLines))]
    private Pattern? currentPattern;

    /// <summary>Where the caret is: the line, the track and the column within it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTrack))]
    [NotifyPropertyChangedFor(nameof(CursorTrackLabel))]
    [NotifyPropertyChangedFor(nameof(ColumnsHere))]
    [NotifyPropertyChangedFor(nameof(CanAddColumn))]
    [NotifyPropertyChangedFor(nameof(CanRemoveColumn))]
    private PatternCursor cursor = PatternCursor.Start;

    /// <summary>
    /// Moving the cursor moves what the panels under the pattern are about.
    /// </summary>
    /// <remarks>
    /// The track is the only part of the cursor that matters to them, and
    /// <see cref="FollowCursorTrack"/> does nothing when it has not changed, so a keystroke
    /// that walks down one column costs a comparison.
    /// </remarks>
    partial void OnCursorChanged(PatternCursor value) => FollowCursorTrack();

    /// <summary>Which slot of the order is being worked on, which decides the pattern.</summary>
    [ObservableProperty] private int orderIndex;

    /// <summary>The row the clock has reached, or -1 when nothing is playing.</summary>
    [ObservableProperty] private int playingLine = -1;

    /// <summary>How many rows the pattern has, for a panel showing where its track is.</summary>
    public int PatternLines => CurrentPattern?.Lines ?? 0;

    /// <summary>
    /// What is coming, and what has just been, shown dimmed above and below the pattern being
    /// worked on. The pattern itself is not dimmed: it is the one you can touch.
    /// </summary>
    /// <remarks>
    /// A pattern that is really coming, and nothing otherwise. In song mode that is the
    /// neighbouring slot, by its place rather than by the pattern, since the same pattern can be
    /// in a song twice and what follows it is a different answer each time; null at the two
    /// ends, because a song does not wrap. In pattern mode nothing is coming but this pattern
    /// again, so both are null and the space either side is simply blank.
    ///
    /// Null is not the same as no space. The room above and below is always there and the
    /// cursor is on the middle of the screen whatever is in it; these two only decide whether
    /// anything is drawn there.
    /// </remarks>
    public Pattern? PatternBefore =>
        PlayMode == TrackerPlayMode.Pattern ? null : Song.PatternAt(OrderIndex - 1);

    /// <summary>What is coming next, on the same terms as <see cref="PatternBefore"/>.</summary>
    public Pattern? PatternAfter =>
        PlayMode == TrackerPlayMode.Pattern ? null : Song.PatternAt(OrderIndex + 1);

    /// <summary>
    /// Says both neighbours may have changed, for the things that change them without either
    /// property being touched.
    /// </summary>
    /// <remarks>
    /// Neither is stored, so nothing raises them on its own: the play mode, the slot and the
    /// order all decide what is either side of this pattern, and an order edited in place moves
    /// them without the slot number moving at all.
    /// </remarks>
    private void NeighboursMoved()
    {
        OnPropertyChanged(nameof(PatternBefore));
        OnPropertyChanged(nameof(PatternAfter));
    }

    /// <summary>In pattern mode nothing is coming but this pattern again, so both go blank.</summary>
    partial void OnPlayModeChanged(TrackerPlayMode value) => NeighboursMoved();

    /// <summary>
    /// What has been done to this song's patterns, so it can be taken back.
    /// </summary>
    /// <remarks>
    /// The tracker's own, and only the tracker's. Undo belongs to the thing being edited rather
    /// than to the application: what a step means here is a call to <see cref="PatternEdit"/>,
    /// and what it would mean in the machine designer is something else entirely, so one shared
    /// history would have to pretend they were the same kind of thing.
    /// </remarks>
    public TrackerHistory History { get; } = new();

    /// <summary>
    /// What writes a turned knob into a lane. Always here, and does nothing until armed.
    /// </summary>
    public AutomationRecorder Automation { get; private set; } = null!;

    /// <summary>
    /// Gives the clock the door it needs to write a lane through.
    /// </summary>
    /// <remarks>
    /// Handed in from outside because resolving a lane means knowing the whole program, and
    /// the thing that knows it is built after this is. Called once, on the way up. A tracker
    /// nobody calls it on plays songs exactly as it did before automation existed, which is
    /// what every test that makes one relies on.
    ///
    /// Two panels come out of it, not one. <see cref="MasterLanes"/> is the same panel again
    /// pointed at the strip that is not a track, and it is its own rather than the one under
    /// the pattern for the same reason its chain is: that one follows the cursor, and the
    /// master is not somewhere the cursor can be.
    /// </remarks>
    public void UseAutomation(Midi.Interfaces.IControlTargets targets)
    {
        _player.Automation = new AutomationPlayer(targets);

        Lanes = new AutomationViewModel(
            targets, () => Song, () => CurrentPattern, () => LinesPerBeat, () => PlayingLine)
        {
            Taking = History.Taking,
            Dirtied = MarkDirty
        };

        MasterLanes = new AutomationViewModel(
            targets, () => Song, () => CurrentPattern, () => LinesPerBeat, () => PlayingLine)
        {
            Taking = History.Taking,
            Dirtied = MarkDirty
        };

        MasterLanes.Show(TrackerPlayer.MasterStrip);
    }

    /// <summary>
    /// Undo and redo, when the tracker is what you are looking at.
    /// </summary>
    /// <remarks>
    /// Saving is not answered here on purpose. It belongs to the page rather than to the grid,
    /// and the keystroke walks outwards until something takes it, so it reaches
    /// <see cref="MainViewModel"/> and saves the song exactly as it did before.
    /// </remarks>
    bool Shortcuts.Interfaces.IShortcutContext.Can(Shortcuts.Enums.ShortcutAction action) => action switch
    {
        Shortcuts.Enums.ShortcutAction.Undo => History.CanUndo,
        Shortcuts.Enums.ShortcutAction.Redo => History.CanRedo,
        _ => false
    };

    /// <inheritdoc/>
    /// <remarks>
    /// A step knows which pattern it is about, so both go through <see cref="TakeBack"/>, which
    /// goes there first rather than changing a pattern behind your back.
    /// </remarks>
    void Shortcuts.Interfaces.IShortcutContext.Do(Shortcuts.Enums.ShortcutAction action)
    {
        switch (action)
        {
            case Shortcuts.Enums.ShortcutAction.Undo: TakeBack(History.UndoIsAbout, History.Undo); break;
            case Shortcuts.Enums.ShortcutAction.Redo: TakeBack(History.RedoIsAbout, History.Redo); break;
        }
    }

    /// <summary>
    /// Says the song itself is about to change, so the change can be taken back.
    /// </summary>
    /// <remarks>
    /// For the edits a pattern snapshot cannot describe. Taking an instrument out renumbers
    /// every pattern that referred to it, which is an edit across the whole document, and it is
    /// exactly the sort of thing somebody does by accident and wants back.
    /// </remarks>
    private void Changing(string what) => History.Taking(Song, what, Pour);

    /// <summary>
    /// Pours a song read back out of the history into the one that is open.
    /// </summary>
    /// <remarks>
    /// Into rather than instead of. The player, the mixer, every panel and this view model all
    /// hold the song they were opened on, so handing back a different object would leave every
    /// one of them playing the song as it was before the undo. What comes back is its contents.
    ///
    /// Field by field and found rather than listed, the same as the machine designer's, and for
    /// the same reason: a list written out here would be right the day it was written and wrong
    /// the first time a field is added to a song, and an undo that silently drops one is worse
    /// than no undo at all. The patterns keep their identity, which is what stops the cheap
    /// steps in the history pointing at objects the song no longer holds. See
    /// <see cref="Song.TakeFrom"/>.
    ///
    /// The order of the four things it does is the whole of it. Any plugin window open over a
    /// chain that is about to be taken apart is dropped first: a plugin drawing into a window
    /// whose plugin has been disposed is a crash inside its own toolkit, and this is the one
    /// moment that can happen, since a song opens with no plugin windows up and an undo can be
    /// pressed with one on screen. Then the contents. Then the chains, because the song now
    /// says which plugins each track has while the mixer still holds the ones that were loaded
    /// a moment ago; they are made to agree only for the tracks where they differ, since
    /// rebuilding a chain is seconds a plugin and almost every undo changes none of them.
    /// Then everything the tracker hangs off the song is hung off it again: the instrument
    /// list, the mixer strips, whichever pattern the order now points at, and the effect slot,
    /// which builds its rows off whatever the chain now holds.
    /// </remarks>
    private bool Pour(Song live, Song was)
    {
        TrackEffect.Target = null;

        live.TakeFrom(was);

        var rebuilt = _player.MatchChains(live);

        if (rebuilt.Count > 0)
            Log.Write(LogArea.Plugins, () =>
                "history: " + rebuilt.Count + " track(s) had their inserts built again to match the step");

        SyncInstruments();

        CurrentPattern = Song.PatternAt(OrderIndex) ?? Song.PatternAt(0);

        PointEffectSlot();

        OnPropertyChanged(nameof(Song));
        OnPropertyChanged(nameof(TrackCount));
        OnPropertyChanged(nameof(PatternLines));

        return true;
    }

    /// <summary>
    /// Walks the history, having first gone to the pattern the step is about.
    /// </summary>
    /// <remarks>
    /// Undo after switching patterns puts the right one back, and the grid follows it there. The
    /// alternative is a pattern changing behind your back while you look at another, which is
    /// the one thing an undo must never do.
    /// </remarks>
    private void TakeBack(Pattern? about, Func<bool> walk)
    {
        if (about is not null && !ReferenceEquals(about, CurrentPattern))
            for (int at = 0; at < Song.Order.Count; at++)
                if (ReferenceEquals(Song.PatternAt(at), about)) { OrderIndex = at; break; }

        if (!walk()) return;

        MarkDirty();
    }

    /// <summary>
    /// The player's notes, passed straight through to whatever panels are open.
    /// </summary>
    /// <remarks>
    /// Passed through rather than repeated, so there is one list of listeners and a panel that
    /// lets go really has let go. What the panels want is what the player already says.
    /// </remarks>
    public event EventHandler<(int Track, Note Note, double Seconds)>? NotePlayed
    {
        add
        {
            _player.NotePlayed += value;
            _played += value;
        }
        remove
        {
            _player.NotePlayed -= value;
            _played -= value;
        }
    }

    /// <summary>
    /// The half of <see cref="NotePlayed"/> the player knows nothing about: notes played by
    /// hand, from the computer keyboard, a panel's keys, or a MIDI keyboard.
    /// </summary>
    private EventHandler<(int Track, Note Note, double Seconds)>? _played;

    /// <summary>
    /// Says a note played by hand, so a panel's keyboard lights for it the same as for one the
    /// pattern played.
    /// </summary>
    private void Played(int track, Note note, double seconds) =>
        _played?.Invoke(this, (track, note, seconds));

    /// <summary>What plugins this machine has, for the picker on the mixer page.</summary>
    public PluginLibraryViewModel Plugins { get; }

    /// <summary>
    /// The effect slot for whichever track is picked. One slot, retargeted as the selection
    /// moves, rather than a set of controls repeated down every channel.
    /// </summary>
    public PluginChainViewModel TrackEffect { get; }

    /// <summary>The effects across the whole mix, which belong to the master and not to a track.</summary>
    public PluginChainViewModel MasterEffect { get; }

    /// <summary>
    /// True while the master's chain is unfolded under the mixer.
    /// </summary>
    /// <remarks>
    /// Shut to begin with, the same as the automation under the pattern and for the same reason:
    /// almost no song has an effect across the whole mix, and a strip showing nothing is a strip
    /// taking room from the thing you came to look at.
    /// </remarks>
    [ObservableProperty] private bool showsMasterChain;

    /// <summary>How tall it stands while it is open. See the strips under the pattern.</summary>
    [ObservableProperty] private double masterChainHeight = 104;

    /// <summary>Which track the effect slot is pointed at, so the cursor does not retarget it
    /// on every keystroke that stays in the same column.</summary>
    private int _effectTrack = -1;

    /// <summary>
    /// Points the effect slot at the track the cursor is on. Moving between tracks changes
    /// what the panel under the pattern is about; moving up and down a track does not.
    /// </summary>
    /// <remarks>
    /// The mixer is told as well, since it is the one page where the cursor is not on the
    /// screen to say which track is being worked on for itself. So is the automation under the
    /// chain, which is about the same track and for the same reason: both are what the column
    /// the cursor is in has on it. That last only while the strip is open, since reading it
    /// costs a walk over the track's machine and every plugin on it.
    /// </remarks>
    private void FollowCursorTrack()
    {
        int track = Cursor.Track;
        if (track == _effectTrack && TrackEffect.Target != null) return;

        _effectTrack = track;

        TrackEffect.Target = new TrackPluginTarget(_player, track);
        TrackEffect.Instrument = InstrumentBoxFor(track);

        foreach (var strip in Strips) strip.IsSelected = strip.Track == track;

        if (ShowsLanes) Lanes?.Show(track);
    }

    /// <summary>
    /// Points the effect slot at the track the cursor is on again, whether or not the cursor
    /// has moved.
    /// </summary>
    /// <remarks>
    /// The slot follows the cursor, so it only rebuilds itself when the cursor changes track.
    /// What a track plays can change while the cursor stays where it is, which is what
    /// dropping an instrument on a track does, and the box at the head of the strip has to
    /// follow that too: without this the instrument you just put on the track has no box
    /// until you leave it and come back.
    /// </remarks>
    private void PointEffectSlot()
    {
        _effectTrack = -1;
        FollowCursorTrack();
    }

    /// <summary>
    /// The box at the head of a track's strip: the plugin that track plays, when it plays one.
    /// </summary>
    /// <remarks>
    /// Made from what the song says is on the track, not from what is loaded: the plugin
    /// itself is not asked for until somebody opens the box.
    ///
    /// Every kind of instrument, not only plugins. What a track plays sits at the head of its
    /// strip whether the sound is Serum's or ours; they are the same thing to the track, and
    /// only what opens when you click differs.
    ///
    /// The same box is handed back every time, or coming back to a track would make a second
    /// one and open a second window onto one plugin's interface, which some plugins do not
    /// survive. A box for an instrument this track no longer plays is discarded: it is
    /// watching the tracker on behalf of a panel nobody can open any more.
    ///
    /// The box is told to report an edit through <c>InstrumentEdited</c> rather than
    /// <see cref="MarkDirty"/>, because what the panel changes is the song's own copy, so the
    /// row beside the pattern has to show it as well as the file having to be written.
    /// </remarks>
    private PluginInstrumentViewModel? InstrumentBoxFor(int track)
    {
        var instrument = Song.InstrumentAt(Song.GetTrackInstrument(track));

        if (instrument == null)
        {
            if (_instrumentBoxes.Remove(track, out var gone)) gone.Discard();
            return null;
        }

        if (_instrumentBoxes.TryGetValue(track, out var existing) &&
            ReferenceEquals(existing.Instrument, instrument))
        {
            return existing;
        }

        if (existing != null) existing.Discard();

        var box = new PluginInstrumentViewModel(
            instrument,
            () => _player.EnsurePlayerOn(track, instrument),
            _machines,
            InstrumentEdited,
            () => new TrackInstrumentDesigner(track, instrument, _machines, this, InstrumentEdited, _waveforms, this, _rack, _recordings, MidiKeys),
            () => ClearTrackInstrument(track));

        _instrumentBoxes[track] = box;

        return box;
    }

    /// <summary>
    /// Where a sample instrument's waveform is drawn from, for a designer opened on a track.
    /// Null draws nothing rather than failing, which is a flat line instead of a crash.
    /// </summary>
    private readonly IWaveformService? _waveforms;

    /// <summary>One box per track, kept so that a track always shows the same one.</summary>
    private readonly System.Collections.Generic.Dictionary<int, PluginInstrumentViewModel> _instrumentBoxes = new();

    /// <summary>
    /// Puts away every plugin instrument window, for a song being left. What is behind them is
    /// about to be somebody else's plugin.
    /// </summary>
    private void CloseInstrumentBoxes()
    {
        foreach (var box in _instrumentBoxes.Values) Views.PluginWindow.CloseFor(box);

        _instrumentBoxes.Clear();

        TrackEffect.Instrument = null;
    }

    /// <summary>
    /// The block being worked on, or nothing. Kept here rather than in the grid so the menu
    /// and the keyboard both act on the same thing.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(SelectionLabel))]
    private PatternSelection selection = PatternSelection.None;

    /// <summary>
    /// Drops how hard a key was hit, on the way in. A preference rather than part of a song,
    /// so it is remembered between runs.
    /// </summary>
    [ObservableProperty] private bool ignoreVelocity;

    /// <summary>
    /// Stopped, playing or paused, which is what the transport bar's three buttons read off.
    /// </summary>
    /// <remarks>
    /// One state rather than a flag per button, since the three are exclusive and two flags
    /// would eventually disagree about which.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlaying))]
    [NotifyPropertyChangedFor(nameof(IsPaused))]
    [NotifyPropertyChangedFor(nameof(IsStopped))]
    [NotifyPropertyChangedFor(nameof(CanPause))]
    private TrackerTransportState transport = TrackerTransportState.Stopped;

    /// <summary>Typed notes are written into the pattern only while this is on.</summary>
    [ObservableProperty] private bool isRecording;

    /// <summary>
    /// A knob turned while the song plays is written into a lane only while this is on.
    /// </summary>
    /// <remarks>
    /// Its own switch rather than the one above, because they arm two different hands. Typing a
    /// note is deliberate and a controller nudged on a desk is not, and a person mixing while a
    /// song loops would otherwise be editing it by leaning on the furniture.
    /// </remarks>
    [ObservableProperty] private bool isAutomating;

    /// <summary>Set by every edit, cleared by a save. Nothing here is on disk until then.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsSaving))]
    [NotifyPropertyChangedFor(nameof(CanRevertSong))]
    private bool isDirty;

    /// <summary>The switch and the recorder are one thing said twice, so they are kept in step.</summary>
    /// <remarks>
    /// Disarming ends the pass as well as stopping it. Otherwise a knob touched after it was
    /// switched off and on again would go on adding to the step the earlier pass took, and one
    /// undo would take back two sessions of mixing.
    /// </remarks>
    partial void OnIsAutomatingChanged(bool value)
    {
        Automation.Armed = value;

        if (!value) Automation.Stopped();
    }

    /// <summary>Pattern by default: most editing is done against a single looping pattern.</summary>
    [ObservableProperty] private TrackerPlayMode playMode = TrackerPlayMode.Pattern;

    /// <summary>
    /// The octave notes are typed and auditioned at, which is the song's and not the view's.
    /// </summary>
    [ObservableProperty] private int octave = 4;

    /// <summary>
    /// The octave is the song's, not the view's: a song reopens where it was left, and every
    /// instrument panel reads the same number rather than keeping one of its own.
    /// </summary>
    partial void OnOctaveChanged(int value)
    {
        if (Song.KeyboardOctave == value) return;

        Song.KeyboardOctave = value;

        if (!_followingOctave) MarkDirty();
    }

    /// <summary>True while the octave is chasing a note rather than being set by hand.</summary>
    private bool _followingOctave;

    /// <inheritdoc/>
    /// <remarks>
    /// Done by setting the same property a hand would, with <see cref="_followingOctave"/> up,
    /// rather than by a second path into the song: two ways of moving one number is how they
    /// come to disagree. The flag is what <c>OnOctaveChanged</c> reads to decide whether
    /// the song has been edited or has merely been kept up.
    /// </remarks>
    public void FollowOctave(int octave)
    {
        if (Octave == octave) return;

        _followingOctave = true;

        try
        {
            Octave = octave;
        }
        finally
        {
            _followingOctave = false;
        }
    }
    /// <summary>
    /// Which instrument is picked out in the list beside the pattern.
    /// </summary>
    /// <remarks>
    /// About the list and not about the sound: what a new track would be given, and what the
    /// rack is showing. What a track plays is the track's own instrument. The tracker answered
    /// the first question with this one whenever a track had none of its own, so the keyboard
    /// sounded an instrument the track had not got and the status bar named it.
    /// </remarks>
    [ObservableProperty] private int selectedInstrument;

    /// <summary>How far the caret drops after a note is typed, in lines.</summary>
    [ObservableProperty] private int editStep = 1;

    /// <summary>The line under the tracker page, saying what just happened.</summary>
    [ObservableProperty] private string status = "Ready";

    /// <summary>What the song is called, which is <see cref="Unnamed"/> until it is saved.</summary>
    [ObservableProperty] private string songName = Unnamed;

    /// <summary>The song's instruments, as rows: the list beside the pattern.</summary>
    public ObservableCollection<InstrumentSlot> Instruments { get; } = new();

    /// <summary>One channel strip per track, for the MIXER page.</summary>
    public ObservableCollection<TrackStripViewModel> Strips { get; } = new();

    /// <summary>
    /// The whole mix, after every track: a level, a place and one effect the song goes through.
    /// </summary>
    /// <remarks>
    /// Its own property rather than the last of <see cref="Strips"/>, because everything that
    /// walks the strips means the tracks: the meters, the surface's eight faders, the mix that
    /// is written down. A master on the end of that list would be found by all of them and be
    /// wrong in each.
    /// </remarks>
    [ObservableProperty] private TrackStripViewModel? masterStrip;

    /// <summary>
    /// The machine picked out on the rack, which is where an instrument added to this song
    /// comes from.
    /// </summary>
    /// <remarks>
    /// A machine and not an instrument: what goes into the song is a copy with an id of its
    /// own. See <see cref="AddInstrumentCommand"/>.
    /// </remarks>
    [ObservableProperty] private RackMachine? pickedMachine;

    /// <summary>The order, as rows: which pattern is played at each slot, in words.</summary>
    public ObservableCollection<string> OrderEntries { get; } = new();

    /// <summary>Every song on the shelf, which is what the open dialog is narrowed from.</summary>
    public ObservableCollection<SongFile> SavedSongs { get; } = new();

    /// <summary>
    /// The songs the open dialog shows: what is saved, narrowed by what has been typed.
    /// </summary>
    /// <remarks>
    /// Its own list rather than a filter on the one above, because the one above is what there
    /// is and this is what you are looking at. Deleting from the dialog acts on a song, not on
    /// a row, so the two never have to agree about anything but which songs exist.
    /// </remarks>
    public ObservableCollection<SongFile> ShownSongs { get; } = new();

    /// <summary>Part of a song's name to look for, or empty to look for nothing in particular.</summary>
    /// <remarks>
    /// The name and not the note under it. A note is what a song is about and a name is what it
    /// is called, and somebody opening a song is looking for the one they named.
    /// </remarks>
    [ObservableProperty] private string songSearch = "";

    /// <summary>Typing narrows the list as it is typed, so nothing has to be pressed.</summary>
    partial void OnSongSearchChanged(string value) => RestockSongs();

    /// <summary>True when there are songs but none of them match what was typed.</summary>
    /// <remarks>
    /// Told apart from having no songs at all, because an empty list means two different things
    /// and only one of them is worth doing something about.
    /// </remarks>
    public bool NoSongsFound => SavedSongs.Count > 0 && ShownSongs.Count == 0;

    /// <summary>The row picked out in the open dialog, which is what Open and Delete act on.</summary>
    [ObservableProperty] private SongFile? selectedSongFile;

    /// <summary>
    /// Builds a tracker on an audio engine and a rack, with nothing open but an empty song.
    /// </summary>
    /// <remarks>
    /// Everything after the rack is optional so the whole class can be made in a test with no
    /// settings file, no plugin library and no waveform service: those are the four things that
    /// would otherwise need a running application behind them.
    ///
    /// The order the pieces are built in matters, and in one place it has already cost a bug.
    ///
    /// <see cref="IPatternEdit.Watching"/> is pointed at the history here rather than by
    /// <see cref="PatternEdit"/> itself, because a history belongs to the thing being edited
    /// and a pattern has never heard of one. Every edit to any pattern goes through that class
    /// and tells the history before it happens, which is what makes an edit added later
    /// undoable without anybody remembering to hook it up.
    ///
    /// The master's chain is its own <see cref="PluginChainViewModel"/> rather than the one
    /// under the pattern, because that one follows the cursor and the master is not a track the
    /// cursor can be in: pointing it there would mean losing it the moment you touched an arrow
    /// key. Its target is set once and never again, unlike a track's, since it is always about
    /// the same strip.
    ///
    /// The two preferences are assigned to their backing fields rather than to the properties,
    /// deliberately: this is what was saved, not a change to save again.
    ///
    /// The automation recorder and the two chain history hooks are wired after the player
    /// because they read it. A lane written into is a pattern edit like any other and goes
    /// through the same history, one step per lane per pass. A chain about to change captures
    /// what is really loaded onto the song first, because a step has to hold that rather than
    /// whatever the song was last told, and the song's record of the chains is otherwise only
    /// refreshed at particular moments.
    ///
    /// The sample rate is settled before anything can sound, since it cannot move once the
    /// engine is built.
    ///
    /// <see cref="CurrentPattern"/> is set through the property and not through the field.
    /// Setting the field is what the generated setter does plus nothing, and the plus nothing
    /// is the part that subscribes to the pattern's own change event. Assigned directly, the
    /// song the application starts on never heard about its own edits: typing a note into it
    /// left the song looking saved, the Save button unmarked, and nothing in the log to say so.
    /// Every song opened afterwards went through the property and was fine, which is exactly
    /// why it survived. Worth remembering as a shape: a backing field assignment skips exactly
    /// the part that was worth having.
    /// </remarks>
    /// <param name="audio">The one engine, shared with the pads rather than opened again.</param>
    /// <param name="rack">Where a sound starts, which is not where a song's instruments live.</param>
    /// <param name="recordings">The shelf of takes, shared with RECORD rather than copied.</param>
    /// <param name="configStore">Where a preference is written down. Null in a test.</param>
    /// <param name="config">The settings as they stand. Null in a test.</param>
    /// <param name="plugins">The plugin library, shared with the pads. One is made if none is given.</param>
    /// <param name="waveforms">What draws a recording's shape. Null draws a flat line.</param>
    /// <param name="machines">
    /// The machines this run has, the one instance everything shares. Required rather than
    /// defaulted: a fresh one is empty, so a default would draw blank panels and report every
    /// machine missing, without an error anywhere to say why.
    /// </param>
    public TrackerViewModel(
        IAudioEngine audio,
        MachineRack rack,
        ObservableCollection<Recording> recordings,
        IMachineProjects machines,
        ConfigStore? configStore = null,
        AppConfig? config = null,
        PluginLibraryViewModel? plugins = null,
        IWaveformService? waveforms = null)
    {
        _machines = machines;
        _waveforms = waveforms;

        Edits.Watching = History.Taking;

        _configStore = configStore;
        _config = config;
        Plugins = plugins ?? new PluginLibraryViewModel();
        TrackEffect = new PluginChainViewModel(Plugins);
        TrackEffect.Changed += MarkDirty;

        MasterEffect = new PluginChainViewModel(Plugins)
        {
            Nothing = "No effect across the mix yet."
        };

        MasterEffect.Changed += MarkDirty;

        ignoreVelocity = config?.IgnoreKeyVelocity ?? false;
        recordNoteOffs = config?.RecordNoteOffs ?? false;

        _player = new TrackerPlayer(audio, machines);
        _player.UseSampleRate(config?.EngineSampleRate ?? Audio.SynthOutput.FollowDevice);

        _player.UseSizes(new Audio.AudioDefaults().Chosen(new Audio.Records.AudioSizes(
            config?.OutputBufferMs ?? 0,
            config?.OutputUpdatePeriodMs ?? 0,
            config?.OutputUpdateThreads ?? 0)));

        _player.UseRenderAhead(config?.RenderAheadMs ?? 0);

        Automation = new AutomationRecorder(
            () => Song,
            () => Transport == TrackerTransportState.Playing,
            () => _player.Position,
            () => FocusedTrack,
            work => Dispatcher.UIThread.Post(work))
        {
            Taking = History.Taking,
            Dirtied = MarkDirty
        };

        TrackEffect.Changing += () =>
        {
            _player.CaptureChains(Song);

            Changing("a plugin on a track");
        };

        MasterEffect.Changing += () =>
        {
            _player.CaptureChains(Song);

            Changing("a plugin on the master");
        };

        MasterEffect.Target = new TrackPluginTarget(_player, TrackerPlayer.MasterStrip);

        _store = new SongStore();
        _rack = rack;
        _recordings = recordings;

        song = Song.CreateDefault();
        _player.Use(song);

        CurrentPattern = song.Patterns[0];

        _meters = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _meters.Tick += (_, _) => ReadMeters();

        _keeping = new DispatcherTimer { Interval = TimeSpan.FromSeconds(KeepSeconds) };
        _keeping.Tick += (_, _) => Keep();
        _keeping.Start();

        Recovered = LookForRecovered();

        _player.PositionChanged += OnPositionChanged;
        _player.StateChanged += OnPlayerStateChanged;
        _player.Stopped += OnPlayerStopped;

        RefreshOrder();
        RefreshSavedSongs();
        RefreshRack();
        RefreshStrips();

        FollowCursorTrack();
    }

    /// <summary>
    /// The tempo, in beats a minute, which belongs to the song rather than to the transport.
    /// </summary>
    /// <remarks>
    /// Announced to the history before it moves, so a tempo dragged across its range is one
    /// step rather than a hundred: steps are gathered by what they say they are about and when
    /// they happened.
    /// </remarks>
    public double Bpm
    {
        get => Song.Bpm;
        set
        {
            if (Math.Abs(Song.Bpm - value) < 0.001) return;
        Changing("the tempo");

            Song.Bpm = Math.Clamp(value, TrackerTiming.MinBpm, TrackerTiming.MaxBpm);
            OnPropertyChanged();
            MarkDirty();
        }
    }

    /// <summary>
    /// How many rows make a beat, which is what the pattern's stripes are drawn from.
    /// </summary>
    /// <remarks>
    /// Clamped to what the timing allows rather than refused, so a number typed into the box
    /// lands somewhere sensible instead of doing nothing.
    /// </remarks>
    public int LinesPerBeat
    {
        get => Song.LinesPerBeat;
        set
        {
            if (Song.LinesPerBeat == value) return;

            Changing("lines per beat");

            Song.LinesPerBeat = Math.Clamp(value, TrackerTiming.MinLinesPerBeat, TrackerTiming.MaxLinesPerBeat);
            OnPropertyChanged();
            OnPropertyChanged(nameof(QuantizeChoices));
            MarkDirty();
        }
    }

    /// <summary>
    /// How many tracks the song has, which is an edit like any other: adding one and taking it
    /// back is undoable.
    /// </summary>
    /// <remarks>
    /// The work is in <see cref="SetTrackCount"/>, because changing it moves the mixer, the
    /// cursor and every pattern at once.
    /// </remarks>
    public int TrackCount
    {
        get => Song.TrackCount;
        set => SetTrackCount(value);
    }

    /// <summary>The fewest tracks a song can have, for the box that sets the number.</summary>
    public int MinTrackCount => Song.MinTrackCount;

    /// <summary>And the most, which is also what bounds anything indexed by track.</summary>
    public int MaxTrackCount => Song.MaxTrackCount;

    /// <summary>The track the cursor is in, for the header to pick out.</summary>
    public int SelectedTrack => Cursor.Track;

    /// <summary>
    /// Which keys are down, handed to every panel this page opens.
    /// </summary>
    /// <remarks>
    /// The application's one monitor of the notes. Held here rather than looked up, because an
    /// instrument's window is built by this and there is nowhere else for it to come from.
    /// </remarks>
    public Midi.Interfaces.IMidiMonitor? MidiKeys { get; set; }

    /// <summary>The clock is running and the pattern is moving under the cursor.</summary>
    public bool IsPlaying => Transport == TrackerTransportState.Playing;

    /// <summary>Held at a line: still somewhere, rather than back at the beginning.</summary>
    public bool IsPaused => Transport == TrackerTransportState.Paused;

    /// <summary>Nothing is running, which is not the same as nothing sounding: a tail outlives a stop.</summary>
    public bool IsStopped => Transport == TrackerTransportState.Stopped;

    /// <summary>Pause only means anything while something is running.</summary>
    public bool CanPause => Transport == TrackerTransportState.Playing;

    /// <summary>
    /// True while there is work that is not on disc, which is what the Save button reads.
    /// </summary>
    /// <remarks>
    /// Shown by the Save button warming rather than by a star after its label. A star is a
    /// character somebody has to know the meaning of, and it moves the button's width when it
    /// comes and goes; a colour is read without being decoded and from further away.
    /// </remarks>
    public bool NeedsSaving => IsDirty;

    /// <summary>The two things the play button can walk through.</summary>
    public TrackerPlayMode[] PlayModes { get; } = { TrackerPlayMode.Pattern, TrackerPlayMode.Song };

    /// <summary>
    /// The song, as the transport at the top of the window sees it. Running means sounding or
    /// held at a line; armed for typing is not running, since nothing is being heard.
    /// </summary>
    bool ITransportDeck.IsRunning => Transport != TrackerTransportState.Stopped;

    /// <inheritdoc/>
    /// <remarks>A song can always be typed into, so the button is never dead here.</remarks>
    bool ITransportDeck.CanRecord => true;

    /// <inheritdoc/>
    /// <remarks>And can always be played, even when every pattern in it is empty.</remarks>
    bool ITransportDeck.CanPlay => true;

    /// <inheritdoc/>
    /// <remarks>
    /// Arming for typing rather than starting a recording: what the tracker records is
    /// keystrokes into a pattern, not audio.
    /// </remarks>
    void ITransportDeck.Record() => IsRecording = !IsRecording;

    /// <inheritdoc/>
    void ITransportDeck.Play() => Play();

    /// <inheritdoc/>
    void ITransportDeck.Pause() => Pause();

    /// <inheritdoc/>
    void ITransportDeck.Stop() => Stop();

    /// <summary>Starts the pattern or the song, whichever mode is set. Always enabled.</summary>
    public IRelayCommand PlayCommand => new RelayCommand(Play);

    /// <summary>Holds where it is, so play carries on from there. Always enabled; a pause with
    /// nothing running does nothing rather than being refused.</summary>
    public IRelayCommand PauseCommand => new RelayCommand(Pause);

    /// <summary>Stops and goes back to the top of the pattern. Always enabled.</summary>
    public IRelayCommand StopCommand => new RelayCommand(Stop);

    /// <summary>Arms and disarms typing into the pattern. Always enabled.</summary>
    public IRelayCommand ToggleRecordCommand => new RelayCommand(() => IsRecording = !IsRecording);

    /// <summary>Adds a pattern to the song and a slot at the end of the order. Always enabled.</summary>
    public IRelayCommand AddPatternCommand => new RelayCommand(AddPattern);

    /// <summary>Takes the picked slot out of the order, leaving the pattern itself alone.</summary>
    public IRelayCommand RemoveOrderEntryCommand => new RelayCommand(RemoveOrderEntry);

    /// <summary>
    /// Saves the song, asking for a name only if it has never had one.
    /// </summary>
    /// <remarks>
    /// Always enabled: saving a song with nothing to save is cheap and refusing it would mean
    /// explaining why the button is dead.
    /// </remarks>
    public IAsyncRelayCommand SaveCommand => new AsyncRelayCommand(SaveOrAsk);

    /// <summary>Saves under a name you give it, whether or not it already has one.</summary>
    public IAsyncRelayCommand SaveAsCommand => new AsyncRelayCommand(SaveAs);

    /// <summary>Shows the songs there are and opens the one picked.</summary>
    public IAsyncRelayCommand OpenSongCommand => new AsyncRelayCommand(OpenSong);

    /// <summary>Opens the song picked out in the list, without a dialog. Always enabled.</summary>
    public IRelayCommand LoadCommand => new RelayCommand(Load);

    /// <summary>Puts this song down and starts an empty one. Always enabled.</summary>
    public IRelayCommand NewSongCommand => new RelayCommand(NewSong);

    /// <summary>Reads the songs folder again, for a file that arrived from outside.</summary>
    public IRelayCommand RefreshSongsCommand => new RelayCommand(RefreshSavedSongs);

    /// <summary>
    /// The songs on disc, for anything that has to ask them a question.
    /// </summary>
    /// <remarks>
    /// RECORD asks before deleting a take, because a song owns its instruments and a recording
    /// nothing on the rack uses can still be the sound of three songs.
    /// </remarks>
    public SongStore Songs => _store;

    /// <summary>Raised when opening a song put recordings on the shelf that were not there.</summary>
    public event EventHandler? RecordingsArrived;

    /// <summary>
    /// The song's own controller layout changed, so there is something to save.
    /// </summary>
    /// <remarks>
    /// A link made while a song is open belongs to that song and travels in its file, so it
    /// counts as work the same as a note does. Without this, pointing a knob at something and
    /// closing the song would lose it without a word.
    /// </remarks>
    public void ControlsChanged() => MarkDirty();

    /// <summary>The song's own controller layout is about to change. See ControlLink.</summary>
    public void ControlsChanging() => Changing("a controller link");

    /// <summary>
    /// Puts down every plugin the song is holding, for going away to work somewhere else.
    /// </summary>
    /// <remarks>
    /// What is loaded is read back onto the song first, plugin windows included, exactly as
    /// saving does: a knob turned in Serum's own window and then a change of page would
    /// otherwise be a knob turned and lost, and nobody would connect the two.
    ///
    /// Not while it is playing. Leaving the page is not asking for the song to stop, and taking
    /// the plugins out from under a running pattern would be a bar of silence and a fright.
    /// </remarks>
    public void LetGoOfPlugins()
    {
        if (IsPlaying) return;

        _player.CaptureChains(Song);

        foreach (var box in _instrumentBoxes.Values) box.SyncPatch();

        CloseInstrumentBoxes();

        _player.LetGoOfPlugins(Song);
    }

    /// <summary>And picks them up again, for coming back.</summary>
    public void TakeUpPlugins() => _player.TakeUpPlugins(Song);

    /// <summary>
    /// The track a hardware control drives when its mapping does not name one.
    /// </summary>
    /// <remarks>
    /// An instrument's own window, when one is in front, and the cursor otherwise.
    ///
    /// The cursor is where you are working while you are working in the pattern, and that is
    /// most of the time. It stops being true the moment a panel is open in a window of its
    /// own: two of those on screen at once and the cursor is on neither of them, so a knob
    /// would drive whichever track the pattern last happened to be on while you look at
    /// something else entirely. A window brought to the front is as plain a statement of what
    /// you are working on as there is.
    /// </remarks>
    public int FocusedTrack => _panelTrack ?? Cursor.Track;

    /// <summary>The track whose panel is in front, or nothing when none is.</summary>
    private int? _panelTrack;

    /// <inheritdoc/>
    /// <remarks>Remembered in <see cref="_panelTrack"/>, which is what <see cref="FocusedTrack"/>
    /// prefers over the cursor while it holds anything.</remarks>
    public void PanelInFront(int track) => _panelTrack = track;

    /// <summary>
    /// A track was picked somewhere other than the pattern, which is the mixer.
    /// </summary>
    /// <remarks>
    /// Through the cursor rather than a second answer beside it, so there is one place that
    /// says which track you are on and the mixer, the pattern, the chain under it and the
    /// automation under that all agree without being told about each other. Coming back from the
    /// mixer leaves the cursor on the track you were mixing, which is where a hand would have
    /// put it anyway.
    ///
    /// A panel in front still wins, since that is a window somebody is looking at.
    /// </remarks>
    public void PickTrack(int track)
    {
        if (track < 0 || track >= Song.TrackCount || track == Cursor.Track) return;

        Cursor = Cursor with { Track = track };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Only when the one that left is the one that was in front. Closing the window behind the
    /// one you are using is not you leaving the one you are using.
    /// </remarks>
    public void PanelGone(int track)
    {
        if (_panelTrack == track) _panelTrack = null;
    }

    /// <summary>Which machine a track plays, by its slot id, or nothing when it plays a plugin.</summary>
    public string MachineOn(int track)
    {
        var instrument = Song.InstrumentAt(Song.GetTrackInstrument(track));

        return instrument is null || instrument.IsPlugin ? "" : instrument.Machine.SlotId;
    }

    /// <summary>
    /// Where a track's machine keeps its settings, for something that wants to move one.
    /// </summary>
    /// <remarks>
    /// Through the track's own box, which is the same one the panel uses, so a knob turned by
    /// hand and a knob turned from a controller are turning the one thing. Built if it has not
    /// been opened yet: a controller works whether or not the panel is on screen, and the panel
    /// showing the change afterwards is the same object having been moved.
    /// </remarks>
    public IMachineValues? MachineValuesOn(int track) =>
        InstrumentBoxFor(track)?.Designer?.Editor?.Values;

    /// <summary>
    /// What is inserted on a strip, the master included, for something that wants to move one
    /// of their knobs.
    /// </summary>
    /// <remarks>
    /// The master answers here because its chain is a chain like any other; it is only the
    /// tracks that have to be counted, and it is not one of them.
    /// </remarks>
    public Audio.Plugins.PluginChain? InsertsOn(int track) =>
        track == TrackerPlayer.MasterStrip || (track >= 0 && track < Song.TrackCount)
            ? _player.ChainFor(track)
            : null;

    /// <summary>
    /// Throws away the song that is open, as it stands on disc. What is on the screen stays
    /// there, unsaved, so a delete by mistake costs a save rather than the work.
    /// </summary>
    public IAsyncRelayCommand DeleteSongCommand => new AsyncRelayCommand(DeleteSong);

    /// <summary>
    /// What the song says about itself, for the bar to show beside its name.
    /// </summary>
    /// <remarks>
    /// Read through here rather than bound straight to the song, because a song is a plain
    /// object and says nothing when one of its fields changes. This is the one that is told.
    /// </remarks>
    public string SongDescription => Song.Description;

    /// <summary>
    /// True when there is a saved copy to go back to and something to go back from.
    /// </summary>
    /// <remarks>
    /// Both halves matter. A song never written down has nothing to return to, and a song with
    /// no changes has nothing to lose, so in either case the button is dead rather than a thing
    /// that looks like it might do something.
    /// </remarks>
    public bool CanRevertSong => IsDirty && CanDeleteSong;

    /// <summary>
    /// Throws away everything since the last save and reads the song back off disc.
    /// </summary>
    /// <remarks>
    /// Asked first, because this is the one button on the bar that destroys work and cannot be
    /// undone: the history goes with the song, which is the point of it.
    ///
    /// Read from the file rather than kept in memory. What is on disc is what the song is, and
    /// holding a copy of the last save in memory would be a second answer to that question and
    /// a chance for the two to disagree.
    /// </remarks>
    public IAsyncRelayCommand RevertSongCommand => new AsyncRelayCommand(RevertSong);

    /// <summary>
    /// Asks, then reads the song back off disc and adopts it as though it had just been opened.
    /// </summary>
    /// <remarks>
    /// A song that will not read is reported and nothing is changed, rather than the open song
    /// being emptied to make room for what did not arrive.
    /// </remarks>
    private async Task RevertSong()
    {
        if (!CanRevertSong) return;

        string name = SongName.Trim();

        bool confirmed = await ConfirmDialog.AskAsync(
            "Cancel the changes",
            $"Throw away everything done to '{name}' since it was last saved, and read it back "
                + "as it was? What you have undone and redone goes with it.",
            "Cancel changes");

        if (!confirmed) return;

        string path = _store.PathFor(name);

        var loaded = _store.Load(path, out var arrived);

        if (loaded == null)
        {
            Status = $"'{name}' could not be read, so nothing was changed.";
            return;
        }

        Adopt(loaded, name);

        if (arrived.Count > 0) RecordingsArrived?.Invoke(this, EventArgs.Empty);

        Status = $"'{name}' is back as it was last saved.";
    }

    /// <summary>False for a song that has never been written down, which has nothing to delete.</summary>
    public bool CanDeleteSong
    {
        get
        {
            string name = SongName.Trim();

            return !Needs(name) && _store.Exists(name);
        }
    }
    /// <summary>
    /// Takes the picked instrument out of the song, asking first.
    /// </summary>
    /// <remarks>
    /// The edit that reaches furthest: every pattern that named it is renumbered around the
    /// gap, so the step it leaves is the whole song rather than one pattern.
    /// </remarks>
    public IAsyncRelayCommand RemoveInstrumentCommand => new AsyncRelayCommand(RemoveSelectedInstrument);

    /// <summary>
    /// Brings the machine picked out on the rack into the song as an instrument of its own.
    /// </summary>
    /// <remarks>
    /// Enabled by there being a machine picked; with none it does nothing rather than being
    /// refused.
    /// </remarks>
    public IRelayCommand AddInstrumentCommand => new RelayCommand(AddInstrument);

    /// <summary>Reads the rack again, for a machine or plugin added while the song was open.</summary>
    public IRelayCommand RefreshLibraryCommand => new RelayCommand(RefreshRack);

    /// <summary>Whether the song has any instruments, for the list to say so when it has none.</summary>
    public bool HasInstruments => Instruments.Count > 0;

    /// <summary>
    /// Starts the clock, from where it was paused or from the top of the pattern.
    /// </summary>
    /// <remarks>
    /// No audio device is a quiet application rather than a broken one, which is why what this
    /// does is caught and said in the status line rather than thrown.
    /// </remarks>
    private void Play()
    {
        try
        {
            if (Transport == TrackerTransportState.Paused)
            {
                _player.Resume();
                Status = "Resumed";
                return;
            }

            Song.Normalize();
            _player.Play(Song, new TrackerPosition(OrderIndex, 0), PlayMode);
            Status = PlayMode == TrackerPlayMode.Pattern ? "Playing pattern" : "Playing song";
        }
        catch (Exception ex)
        {
            Status = $"Play failed: {ex.Message}";
        }
    }

    /// <summary>Holds the clock where it is, so play carries on from the same line.</summary>
    private void Pause()
    {
        _player.Pause();
        Status = "Paused";
    }

    /// <summary>
    /// Stops the clock and takes the playhead off the pattern.
    /// </summary>
    /// <remarks>
    /// The meters are not stopped here, deliberately: a release tail outlives the stop, and
    /// what the meters are about is whether anything is sounding. See <see cref="ReadMeters"/>.
    /// </remarks>
    private void Stop()
    {
        _player.Stop();
        PlayingLine = -1;
        Status = "Stopped";
    }

    /// <summary>Both automation panels follow the playing line, so their pictures show it.</summary>
    private void Running()
    {
        Lanes?.Running();
        MasterLanes?.Running();
    }

    /// <summary>
    /// The clock reached a new line, so the playhead, the order and the pattern follow it.
    /// </summary>
    /// <remarks>
    /// Posted to the drawing thread, since this arrives on the clock's. A song crossing into
    /// the next slot changes which pattern is on the screen, which is the one thing here that
    /// is not simply a number moving.
    /// </remarks>
    private void OnPositionChanged(object? sender, TrackerPosition position) =>
        Dispatcher.UIThread.Post(() =>
        {
            PlayingLine = position.Line;

            Running();

            if (position.OrderIndex != OrderIndex)
            {
                OrderIndex = position.OrderIndex;
                CurrentPattern = Song.PatternAt(position.OrderIndex);
            }
        });

    /// <summary>
    /// The player owns the transport state; the view model only mirrors it. Deriving it here
    /// instead is what let a teardown from one run switch the buttons off during the next.
    /// </summary>
    /// <remarks>
    /// A pass ending ends the automation pass with it, so the next one takes its own steps
    /// rather than adding to the last one's: stopping and starting again is two things a person
    /// did.
    ///
    /// The meters are started here and never stopped here. What they are about is whether
    /// anything is sounding, and a pass ending is not that: a release tail outlives the stop,
    /// and a note played by hand does not need a pass at all. See <see cref="ReadMeters"/>.
    /// </remarks>
    private void OnPlayerStateChanged(object? sender, TrackerTransportState state) =>
        Dispatcher.UIThread.Post(() =>
        {
            Transport = state;
            if (state == TrackerTransportState.Stopped)
            {
                PlayingLine = -1;

                Automation.Stopped();
            }

            if (state == TrackerTransportState.Playing) Meters();
        });

    /// <summary>
    /// Polls the meters, for anything that is about to make a sound.
    /// </summary>
    /// <remarks>
    /// Told from wherever the sound was asked for, which is not always the drawing thread: a
    /// panel's keyboard is, a MIDI key on its way to the pattern is not, and a timer belongs to
    /// the thread that owns it.
    /// </remarks>
    private void Meters()
    {
        if (_meters.IsEnabled) return;

        if (Dispatcher.UIThread.CheckAccess()) _meters.Start();
        else Dispatcher.UIThread.Post(() => _meters.Start());
    }

    /// <summary>Reads what each track is sounding and hands it to its strip and instruments.</summary>
    /// <remarks>
    /// Polling ends when there is nothing left to read rather than when the transport says so,
    /// which are two different moments. Stopping on the transport is what left the master lit:
    /// the last thing the timer did was read a level that was still true, and the first thing it
    /// did not do was read it again, so the reading stayed on screen for ever. It also meant a
    /// note played by hand with the transport stopped moved no meter at all, since nothing was
    /// reading them.
    ///
    /// <see cref="Tracker.Synth.TrackMixer.Sounding"/> is the rule: polling runs while anything
    /// is sounding, and only then while a pass runs, since a pass between two notes is silent
    /// and is not over. Auditioning starts the timer through <see cref="Meters"/> and the timer
    /// stops itself when everything reads nought. The mixer was never wrong about any of this;
    /// both faults were in what was asking.
    /// </remarks>
    private void ReadMeters()
    {
        float loudest = 0;

        foreach (var strip in Strips)
        {
            var (left, right) = _player.LevelFor(strip.Track);

            strip.Left = left;
            strip.Right = right;

            loudest = Math.Max(loudest, Math.Max(left, right));
        }

        if (MasterStrip is { } master)
        {
            var (left, right) = _player.LevelFor(TrackerPlayer.MasterStrip);

            master.Left = left;
            master.Right = right;

            loudest = Math.Max(loudest, Math.Max(left, right));
        }

        foreach (var instrument in Instruments)
        {
            if (instrument.Track < 0) continue;

            var (left, right) = _player.LevelFor(instrument.Track);
            instrument.Level = Math.Max(left, right);
        }

        if (!Tracker.Synth.TrackMixer.Sounding(IsPlaying, loudest)) Quiet();
    }

    /// <summary>Stops polling and empties every meter, so none is left holding a level.</summary>
    private void Quiet()
    {
        _meters.Stop();

        foreach (var strip in Strips)
        {
            strip.Left = 0;
            strip.Right = 0;
        }

        if (MasterStrip is { } master)
        {
            master.Left = 0;
            master.Right = 0;
        }

        foreach (var instrument in Instruments)
        {
            instrument.Level = 0;
        }
    }

    /// <summary>
    /// The player came to rest, which is the moment to say what would not load.
    /// </summary>
    /// <remarks>
    /// Said here rather than as each one fails: a song whose recordings are not on this machine
    /// would otherwise report every missing file on every pass, and the one line that matters is
    /// how many there were.
    /// </remarks>
    private void OnPlayerStopped(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            var failed = _player.FailedInstruments;
            if (failed.Count > 0)
                Status = $"Stopped. {failed.Count} instrument file(s) could not be loaded.";
        });

    /// <summary>
    /// Moving to another slot moves the pattern under the cursor, and both neighbours with it.
    /// </summary>
    partial void OnOrderIndexChanged(int value)
    {
        CurrentPattern = Song.PatternAt(value);
        NeighboursMoved();
    }

    /// <summary>
    /// Patterns are edited in place, so the one on screen is watched rather than every edit
    /// method remembering to report itself.
    /// </summary>
    /// <remarks>
    /// The selection is dropped, because a block belongs to the pattern it was drawn on: kept,
    /// it would name lines and tracks in a pattern that never had them.
    /// </remarks>
    partial void OnCurrentPatternChanged(Pattern? oldValue, Pattern? newValue)
    {
        Selection = PatternSelection.None;

        if (oldValue != null) oldValue.Changed -= OnPatternEdited;
        if (newValue != null) newValue.Changed += OnPatternEdited;

        NeighboursMoved();
    }

    /// <summary>Any edit to the pattern on screen is work that is not on disc.</summary>
    private void OnPatternEdited(object? sender, EventArgs e) => MarkDirty();

    /// <summary>
    /// A different song is a different description, different neighbours, and a different mix
    /// for anything played by hand.
    /// </summary>
    /// <remarks>
    /// The player is told first. It used to learn which song was open only when a pass started,
    /// so until somebody pressed play a note played by hand went through no strip at all and a
    /// fader moved reached nothing that was sounding.
    /// </remarks>
    partial void OnSongChanged(Song value)
    {
        _player.Use(value);

        OnPropertyChanged(nameof(SongDescription));
        NeighboursMoved();
    }

    /// <summary>
    /// Renaming a song is an edit, and it changes what Delete and Cancel changes can do, since
    /// both ask whether a file of that name exists.
    /// </summary>
    partial void OnSongNameChanged(string value)
    {
        MarkDirty();
        OnPropertyChanged(nameof(CanDeleteSong));
        OnPropertyChanged(nameof(CanRevertSong));
    }

    /// <summary>Whether a block is drawn, which decides what the edit commands act on.</summary>
    public bool HasSelection => !Selection.IsEmpty;

    /// <summary>What the menu is about to act on: a block, or the track the cursor is on.</summary>
    public string SelectionLabel => HasSelection ? Selection.Describe() : CursorTrackLabel;

    /// <summary>Starts a block at the cursor, for a shift-click or a drag.</summary>
    public void BeginSelection(PatternCursor at) => Selection = PatternSelection.At(at);

    /// <summary>Drags the loose corner of the block to here.</summary>
    public void ExtendSelection(PatternCursor to) => Selection = Selection.ExtendTo(to);

    /// <summary>Lets the block go, leaving the cursor where it is.</summary>
    public void ClearSelection() => Selection = PatternSelection.None;

    /// <summary>Draws a block over the whole pattern. Always enabled.</summary>
    public IRelayCommand SelectAllCommand => new RelayCommand(SelectAll);

    /// <summary>Copies the block, or the cell under the cursor when there is none.</summary>
    public IRelayCommand CopySelectionCommand => new RelayCommand(CopySelection);

    /// <summary>Copies and then empties what was copied.</summary>
    public IRelayCommand CutSelectionCommand => new RelayCommand(CutSelection);

    /// <summary>Puts the copy down with its corner at the cursor.</summary>
    public IRelayCommand PasteCommand => new RelayCommand(Paste);

    /// <summary>
    /// What was last copied. Held here rather than in a pattern, so a phrase can be carried
    /// between patterns and between songs for as long as the app is open.
    /// </summary>
    private PatternBlock? _clipboard;

    /// <summary>Whether there is anything to paste, for the menu to grey itself out.</summary>
    public bool HasClipboard => _clipboard != null;

    /// <summary>What the paste would put down, for the menu.</summary>
    public string ClipboardLabel => _clipboard == null ? "nothing copied" : _clipboard.Describe();

    /// <summary>
    /// Copies the block, or the single cell under the cursor when there is no block. Copying
    /// one cell is the quickest way to repeat a note down a track.
    /// </summary>
    public void CopySelection()
    {
        var block = PatternBlock.Copy(CurrentPattern, HasSelection ? Selection : PatternSelection.At(Cursor));
        if (block == null) return;

        _clipboard = block;

        OnPropertyChanged(nameof(HasClipboard));
        OnPropertyChanged(nameof(ClipboardLabel));

        Status = "Copied " + block.Describe();
    }

    /// <summary>
    /// Copies the block and then empties it, which is two edits and one gesture.
    /// </summary>
    /// <remarks>
    /// What is emptied is worked out before the copy, since copying with no block selects the
    /// cell under the cursor and the two answers have to be the same one.
    /// </remarks>
    public void CutSelection()
    {
        var taken = HasSelection ? Selection : PatternSelection.At(Cursor);

        CopySelection();

        if (CurrentPattern == null || _clipboard == null) return;

        Edits.ClearRegion(CurrentPattern, taken);
        Status = "Cut " + _clipboard.Describe();
    }

    /// <summary>
    /// Puts the copy down with its corner at the cursor, and leaves it selected: paste, move,
    /// paste again is how a pattern gets built.
    /// </summary>
    public void Paste()
    {
        if (CurrentPattern == null || _clipboard == null) return;

        var landed = _clipboard.Paste(Edits, CurrentPattern, Cursor);
        if (landed.IsEmpty)
        {
            Status = "Nowhere to paste from here";
            return;
        }

        Selection = landed;
        Status = "Pasted " + landed.Describe();
    }

    /// <summary>Empties the block without copying it.</summary>
    public IRelayCommand ClearSelectionCommand => new RelayCommand(DeleteSelection);

    /// <summary>Draws a block over every line and every track of the pattern.</summary>
    public void SelectAll()
    {
        if (CurrentPattern == null) return;

        Selection = PatternSelection.All(CurrentPattern.Lines, CurrentPattern.TrackCount);
        Status = "Selected " + Selection.Describe();
    }

    /// <summary>
    /// Empties the block. This is what Delete does when there is one, and it is not gated on
    /// record: selecting a block and pressing delete is not something anyone does by accident.
    /// </summary>
    public void DeleteSelection()
    {
        if (CurrentPattern == null || !HasSelection) return;

        int cleared = Edits.ClearRegion(CurrentPattern, Selection);

        Status = cleared == 0
            ? "Nothing to clear in " + Selection.Describe()
            : $"Cleared {cleared} cell(s) in " + Selection.Describe();
    }

    /// <summary>The track the cursor is on, named as the grid and the mixer name it.</summary>
    public string CursorTrackLabel => "Track " + (Cursor.Track + 1).ToString("00", CultureInfo.InvariantCulture);

    /// <summary>
    /// Where you are, for the bar along the bottom: the song, the page, and what the cursor is on.
    /// </summary>
    /// <remarks>
    /// True for as long as you are there rather than something that just happened, which is the
    /// whole difference between the two halves of that bar.
    ///
    /// The instrument it names is what the track under the cursor plays, not what is picked out
    /// in the list beside the pattern. Those are two questions: the bar says where you are, and
    /// where you are is the track. Reading the list instead named an instrument on a track that
    /// had none.
    /// </remarks>
    public string Context
    {
        get
        {
            string song = SongName.Length > 0 ? SongName : Unnamed;

            if (ShowsMixer) return song + "  ·  mixer  ·  " + TrackCount + " tracks";

            if (ShowsMachines)
                return song + "  ·  rack  ·  " + Song.Instruments.Count +
                       (Song.Instruments.Count == 1 ? " instrument in this song" : " instruments in this song");

            string line = "line " + Cursor.Line.ToString("00", CultureInfo.InvariantCulture);
            string track = CursorTrackLabel;

            var playing = Song.InstrumentAt(Song.GetTrackInstrument(Cursor.Track));
            string sound = playing == null ? "no instrument" : playing.Name;

            return song + "  ·  " + line + "  ·  " + track + "  ·  " + sound;
        }
    }

    /// <summary>Pulls the notes onto every nth line. See <see cref="QuantizeChoices"/>.</summary>
    public IRelayCommand<int> QuantizeTrackCommand => new RelayCommand<int>(QuantizeTrack);

    /// <summary>What quantizing can snap to. See <see cref="IQuantizeGrid"/>.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IQuantizeGrid Grids = new QuantizeGrid();

    /// <summary>
    /// The quantize menu, which is worked out from the song's lines per beat rather than being
    /// a list of numbers somebody has to translate.
    /// </summary>
    /// <remarks>
    /// Read again whenever lines per beat moves, since every entry in it is about that number.
    /// </remarks>
    public IReadOnlyList<QuantizeChoice> QuantizeChoices => Grids.Choices(LinesPerBeat);

    /// <summary>Empties the track the cursor is on, across the whole pattern.</summary>
    public IRelayCommand ClearTrackCommand => new RelayCommand(ClearTrack);

    /// <summary>Empties every track of the pattern.</summary>
    public IRelayCommand ClearPatternCommand => new RelayCommand(ClearPattern);

    /// <summary>Moves the notes up or down, by however many semitones the menu carries.</summary>
    public IRelayCommand<string> TransposeTrackCommand => new RelayCommand<string>(TransposeTrack);

    /// <summary>Sets the volume column on every note, or takes it off. See <see cref="SetTrackVolume"/>.</summary>
    public IRelayCommand<string> SetTrackVolumeCommand => new RelayCommand<string>(SetTrackVolume);

    /// <summary>Takes the instrument off the track the cursor is on, leaving its notes alone.</summary>
    public IRelayCommand ClearTrackInstrumentCommand =>
        new RelayCommand(() => ClearTrackInstrument(Cursor.Track));

    /// <summary>
    /// Pulls the track's notes onto every nth line, the menu having already worked out which n
    /// the chosen note value comes to at this song's lines per beat.
    /// </summary>
    /// <remarks>
    /// A block quantises whole tracks even when it covers only part of their height: a note is
    /// early or late against the beat, which is a property of the track's timeline rather than
    /// of the lines somebody happened to draw round.
    /// </remarks>
    private void QuantizeTrack(int lines)
    {
        if (CurrentPattern == null) return;
        if (lines < 1) return;

        int moved = 0;

        if (HasSelection)
        {
            for (int track = Selection.FirstTrack; track <= Selection.LastTrack; track++)
                moved += Edits.Quantize(CurrentPattern, track, lines);
        }
        else
        {
            moved = Edits.Quantize(CurrentPattern, Cursor.Track, lines);
        }

        string grid = lines == 1 ? "every line" : $"every {lines} lines";

        Status = moved == 0
            ? $"{SelectionLabel} was already on {grid}"
            : $"Quantized {SelectionLabel} to {grid}: {moved} note(s) moved";
    }

    /// <summary>
    /// Empties the cursor's track across every line of the pattern, and gives back the note
    /// columns that emptied it leaves nothing in.
    /// </summary>
    /// <remarks>
    /// The cells first and the room after, which is the order the two steps have to be pushed
    /// in: undo then widens the track back before it puts the notes into it, and each press
    /// does something you can see.
    /// </remarks>
    private void ClearTrack()
    {
        if (CurrentPattern == null) return;

        Edits.ClearTrack(CurrentPattern, Cursor.Track);
        Narrow(Cursor.Track);

        Status = $"Cleared {CursorTrackLabel}";
    }

    /// <summary>
    /// Empties every track of the pattern, and gives back every note column that leaves nothing
    /// in.
    /// </summary>
    /// <remarks>
    /// One edit and one step for the cells. The room is one more, however many tracks give some
    /// back, because a song step is gathered by what it says it is about and these all say the
    /// same thing.
    /// </remarks>
    private void ClearPattern()
    {
        if (CurrentPattern == null) return;

        Edits.ClearPattern(CurrentPattern);

        for (int track = 0; track < Song.TrackCount; track++) Narrow(track);

        Status = $"Cleared pattern '{CurrentPattern.Name}'";
    }

    /// <summary>
    /// Levels a track's notes out. The menu carries the value as text, with -1 meaning the
    /// volume column comes off and the instrument's own level takes over.
    /// </summary>
    private void SetTrackVolume(string? volume)
    {
        if (CurrentPattern == null) return;
        if (!int.TryParse(volume, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)) return;

        int changed = HasSelection
            ? Edits.SetRegionVolume(CurrentPattern, Selection, level)
            : Edits.SetTrackVolume(CurrentPattern, Cursor.Track, level);

        string what = level == TrackerCell.NoVolume
            ? "the instrument's own level"
            : level.ToString("X2", CultureInfo.InvariantCulture);

        Status = changed == 0
            ? $"{SelectionLabel} was already at {what}"
            : $"{SelectionLabel} set to {what}: {changed} note(s) changed";
    }

    /// <summary>
    /// Moves the notes by that many semitones, over the block when there is one and over the
    /// whole track otherwise.
    /// </summary>
    private void TransposeTrack(string? semitones)
    {
        if (CurrentPattern == null) return;
        if (!int.TryParse(semitones, NumberStyles.Integer, CultureInfo.InvariantCulture, out int steps)) return;

        if (HasSelection) Edits.TransposeRegion(CurrentPattern, Selection, steps);
        else Edits.TransposeTrack(CurrentPattern, Cursor.Track, steps);

        Status = $"Transposed {SelectionLabel} by {steps:+0;-0} semitone(s)";
    }

    /// <summary>
    /// Whether a key coming up on a MIDI keyboard writes a note-off, as Renoise's own
    /// RecordNoteOffs does. Off by default, and remembered between runs.
    /// </summary>
    [ObservableProperty] private bool recordNoteOffs;

    /// <summary>
    /// Says which way it went and writes it down: it is a preference, so it outlives the run.
    /// </summary>
    partial void OnRecordNoteOffsChanged(bool value)
    {
        Status = value
            ? "Note-offs recorded: letting a key up writes OFF where the cursor is"
            : "Note-offs not recorded: use the note-off key to write one";

        if (_configStore == null || _config == null) return;

        _config.RecordNoteOffs = value;
        _configStore.Save(_config);
    }

    /// <summary>
    /// The same for how hard a key was hit. Nothing is stored in a test or a headless run,
    /// where there is no settings file to write to.
    /// </summary>
    partial void OnIgnoreVelocityChanged(bool value)
    {
        Status = value
            ? "Key velocity ignored: notes come in at the instrument's own level"
            : "Key velocity followed: how hard you play is written into the volume column";

        if (_configStore == null || _config == null) return;

        _config.IgnoreKeyVelocity = value;
        _configStore.Save(_config);
    }

    /// <summary>What the engine ended up running at, for SETTINGS to report.</summary>
    public int EngineSampleRate => _player.SampleRate;

    /// <summary>What the tracker's own stream is putting out, 0 to 1.</summary>
    /// <remarks>
    /// Half of what the status bar's meter shows; the pads are the other half, on a channel of
    /// their own, and whoever wants the main output takes the louder of the two.
    /// </remarks>
    public double OutputLevel => _player.OutputLevel;

    /// <summary>How far through its recording a track is, for a panel drawing a playhead.</summary>
    public double SamplePosition(int track) => _player.SamplePosition(track);

    /// <summary>
    /// Which of the tracker's three pages is on show.
    /// </summary>
    /// <remarks>
    /// The instruments and the mixer are the tracker's, not the application's. The mixer mixes
    /// its tracks and nothing else's; the rack exists so a song has something to play, and
    /// the pads never touch either. As sibling tabs they looked like three separate parts of
    /// the program rather than three ways of looking at one song.
    ///
    /// Written out rather than an enum with a converter, because these three strings are what
    /// the buttons pass and what the bindings ask about, and a name that only exists inside a
    /// format string is a name nothing can find.
    /// </remarks>
    private const string PatternPage = "Pattern";

    /// <summary>The rack: what this song has to play, and where an instrument comes from.</summary>
    private const string MachinesPage = "Machines";

    /// <summary>The mixer: the song's tracks and the strip that is not one.</summary>
    private const string MixerPage = "Mixer";

    /// <summary>What this song's controller is pointed at, which travels in its file.</summary>
    private const string ControlsPage = "Controls";

    /// <summary>Which of the four pages is up. The pattern, since that is what a song is.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPattern))]
    [NotifyPropertyChangedFor(nameof(ShowsMachines))]
    [NotifyPropertyChangedFor(nameof(ShowsMixer))]
    [NotifyPropertyChangedFor(nameof(ShowsControls))]
    private string page = PatternPage;

    /// <summary>True while the pattern is showing, which is where the music is written.</summary>
    public bool ShowsPattern => Page == PatternPage;

    /// <summary>True while the rack is showing.</summary>
    public bool ShowsMachines => Page == MachinesPage;

    /// <summary>True while the mixer is showing, which is the one page with no cursor on it.</summary>
    public bool ShowsMixer => Page == MixerPage;

    /// <summary>
    /// True while the song's own controller layout is showing.
    /// </summary>
    /// <remarks>
    /// Beside the rack and the mixer because it is the same kind of thing: something the song
    /// holds, wanted while you are working on the song. It was only in the settings, which is
    /// where the desk's layout belongs and is the wrong place entirely for this song's, since
    /// it changes when the song does.
    /// </remarks>
    public bool ShowsControls => Page == ControlsPage;

    /// <summary>
    /// One track's automation, which exists only once something has told the tracker how to
    /// resolve a parameter. See <see cref="UseAutomation"/>.
    /// </summary>
    public AutomationViewModel? Lanes
    {
        get => _lanes;
        private set
        {
            _lanes = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Behind <see cref="Lanes"/>, which is set once and then only read.</summary>
    private AutomationViewModel? _lanes;

    /// <summary>What the master makes move over this pattern, and what else on it could.</summary>
    public AutomationViewModel? MasterLanes
    {
        get => _masterLanes;
        private set
        {
            _masterLanes = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Behind <see cref="MasterLanes"/>. Pointed at strip -1 and never moved.</summary>
    private AutomationViewModel? _masterLanes;

    /// <summary>True while the master's automation is unfolded under the mixer.</summary>
    [ObservableProperty] private bool showsMasterLanes;

    /// <summary>How tall it stands while it is open. See the strips under the pattern.</summary>
    [ObservableProperty] private double masterLanesHeight = 120;

    /// <summary>
    /// Read when it is opened, the same rule the pattern's automation follows, since reading a
    /// strip's parameters costs a walk over everything on it.
    /// </summary>
    partial void OnShowsMasterLanesChanged(bool value)
    {
        if (value) MasterLanes?.Show(TrackerPlayer.MasterStrip);
    }

    /// <summary>
    /// True while the automation strip is open under the pattern.
    /// </summary>
    /// <remarks>
    /// Folded away by default, because this sits under the pattern and every pixel it takes is
    /// a line of music nobody can see. The chain above it is always there since a track always
    /// has one; automation a track has not got is nothing to look at, and the handle is one row
    /// tall whether it is open or shut.
    /// </remarks>
    [ObservableProperty] private bool showsLanes;

    /// <summary>
    /// Read when it is opened rather than kept in step while it is shut. Everything on it moves
    /// underneath it, an instrument swapped, a plugin taken off a chain, a pattern changed to,
    /// and following all of that would be a subscription per kind for a panel that is folded
    /// away most of the time.
    /// </summary>
    partial void OnShowsLanesChanged(bool value)
    {
        if (value) Lanes?.Show(Cursor.Track);
    }

    /// <summary>
    /// True while the chain under the pattern is unfolded. It starts open.
    /// </summary>
    /// <remarks>
    /// The chain and the automation fold the same way and for the same reason, which is that
    /// every pixel under the pattern is a line of music nobody can see. The chain starts open
    /// because a track always has one; the automation starts shut because a track usually has
    /// none.
    /// </remarks>
    [ObservableProperty] private bool showsChain = true;

    /// <summary>
    /// How tall each strip stands while it is open, kept here rather than in the control.
    /// </summary>
    /// <remarks>
    /// Kept so a strip folded away and opened again comes back the size it was, and so the two
    /// answers survive changing page. Neither is a share of anything: each strip asks for what
    /// it wants and the pattern is measured in what is left, which is what makes one grip move
    /// one strip.
    /// </remarks>
    [ObservableProperty] private double chainHeight = 104;

    /// <summary>And how tall the automation stands, which is its own answer and not a share.</summary>
    [ObservableProperty] private double lanesHeight = 120;

    /// <summary>What this song has its controller pointed at, for the page that shows it.</summary>
    /// <remarks>
    /// Handed in rather than built here, because the same list narrowed differently is what
    /// SETTINGS shows, and both are views of the one registry. It says when it arrives, so the
    /// page is not left bound to nothing if it happens to be built first.
    /// </remarks>
    public ControlLinksViewModel? SongControls
    {
        get => _songControls;
        set
        {
            _songControls = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Behind <see cref="SongControls"/>, which is set from outside once.</summary>
    private ControlLinksViewModel? _songControls;

    /// <summary>
    /// Shows a page, or goes back to the pattern when the page asked for is already up.
    /// </summary>
    /// <remarks>
    /// The pattern is where you are: the others are somewhere you go and come back from, and
    /// pressing the lit button again is the way back.
    /// </remarks>
    public IRelayCommand<string> ShowCommand => new RelayCommand<string>(which =>
        Page = which == Page || which is not (MachinesPage or MixerPage or ControlsPage)
            ? PatternPage
            : which);

    /// <summary>
    /// Forgets a decoded recording, for a file that has been edited under us.
    /// </summary>
    /// <remarks>
    /// A take is decoded once and shared by every instrument pointing at it, so trimming one on
    /// RECORD leaves the tracker playing what the file used to be until it is told.
    /// </remarks>
    public void ReloadSample(string filePath) => _player.ReloadInstrument(filePath);

    /// <summary>
    /// Follows a recording that has been renamed, for the song that is open.
    /// </summary>
    /// <remarks>
    /// A song owns its instruments, so the shelf being repointed does nothing for the one being
    /// worked on. Both are told, and neither knows about the other.
    /// </remarks>
    public void RenameSample(string from, string to)
    {
        bool moved = false;

        foreach (var instrument in Song.Instruments)
            if (Usage.Repoint(instrument, from, to)) moved = true;

        _player.ReloadInstrument(from);
        _player.ReloadInstrument(to);

        if (moved) MarkDirty();
    }

    /// <summary>
    /// Opens the audio again after the output device has been changed underneath it.
    /// </summary>
    /// <remarks>
    /// The tracker's stream belongs to whichever device was open when it was made, and
    /// changing devices closes that one. Without this the stream is gone and nothing notices
    /// until the next note, which means a track's effects stop being given anything at all.
    ///
    /// A failure is swallowed on purpose: no audio device is a quiet application, not a broken
    /// one, and there is nothing a person could do about it here.
    /// </remarks>
    public void ReopenAudio()
    {
        try
        {
            _player.EnsureEngine();
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// Writes the song down as it stands, if it has anything in it that is not saved.
    /// </summary>
    /// <remarks>
    /// Not a save: it goes to a file of its own and the song is still unsaved afterwards. What
    /// it is for is the twenty minutes of work between two saves, which is what a plugin taking
    /// the application down costs today. Doing nothing while there is nothing to do, so a
    /// tracker sitting idle writes no files.
    ///
    /// Never written under the placeholder name: a rescue file is the one song on the shelf
    /// nobody named, and calling it "untitled" makes it look like one somebody saved. What goes
    /// into it is what the tracks are actually playing, chains and patches read back exactly as
    /// a real save reads them, or the rescue would be worth less than the thing it is rescuing.
    ///
    /// A song that will not be written down is logged and let go rather than allowed to stop
    /// the song being written: it will be tried again in <see cref="KeepSeconds"/> seconds.
    /// </remarks>
    private void Keep()
    {
        if (!IsDirty) return;

        try
        {
            string name = SongName.Trim();
            if (name.Length == 0 || Needs(name)) name = "unsaved song";

            if (name.EndsWith(RecoveredSuffix, StringComparison.Ordinal)) return;
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return;

            _player.CaptureChains(Song);
            foreach (var box in _instrumentBoxes.Values) box.SyncPatch();

            string path = _store.PathFor(name + RecoveredSuffix);

            _store.Save(Song, path);

            if (!_paths.Same(_kept, path))
            {
                Drop();
                _kept = path;

                Log.Write(LogArea.Tracker, () => "unsaved work is being kept in " + path);
            }
        }
        catch (Exception error)
        {
            Log.Fault(LogArea.Tracker, "keeping the unsaved song", error);
        }
    }

    /// <summary>
    /// Throws away what was being kept, for work that is now saved or deliberately abandoned.
    /// </summary>
    private void Drop()
    {
        if (_kept.Length == 0) return;

        try { _store.Delete(_kept); } catch (Exception) { }

        _kept = "";
    }

    /// <summary>
    /// Anything the last session was keeping when it stopped, said out loud rather than left
    /// lying in the songs list for somebody to notice.
    /// </summary>
    private string LookForRecovered()
    {
        foreach (var file in _store.ListSongs())
        {
            if (file.Name.EndsWith(RecoveredSuffix, StringComparison.Ordinal))
            {
                return "'" + file.Name + "' in the songs list is work the last session never saved.";
            }
        }

        return "";
    }

    /// <summary>Something about the song changed and the file on disk no longer matches.</summary>
    /// <remarks>
    /// The log line is written once, where it changes, and not on every call: one turn of a
    /// plugin's knob is eighty of these.
    /// </remarks>
    private void MarkDirty()
    {
        if (!IsDirty) Log.Write(LogArea.Tracker, "the song has something unsaved in it now");

        IsDirty = true;
    }

    /// <summary>Says which way it went, since the difference is invisible until you type.</summary>
    partial void OnIsRecordingChanged(bool value) =>
        Status = value ? "Record armed: typing writes into the pattern" : "Record off: typing only auditions";

    /// <summary>Auditions the note under the cursor's instrument, for note entry feedback.</summary>
    public void PreviewNote(Note note) => PreviewNote(note, TrackerCell.NoVolume);

    /// <summary>
    /// Auditions at a given volume, which is what makes a keyboard's velocity audible.
    /// </summary>
    /// <remarks>
    /// Played on the track the cursor is in, which is the whole point: it goes through that
    /// track's inserts, so a plugin instrument sounds through the copy the track plays rather
    /// than through an audition copy of its own, and the strip's meter and the master's move
    /// for it exactly as they do for a pass, and through its fader, its mute and its placement,
    /// which is what makes an audition tell you what the part will actually sound like. Everything but the plugin path used
    /// to go to the loose audition bus instead and moved no track meter at all.
    ///
    /// The meters are started here rather than by the transport, since a note played by hand
    /// with the transport stopped is exactly the case nothing was reading them for.
    ///
    /// And the note is said out loud through <see cref="Played"/>, so a panel's keyboard lights
    /// for a note played by hand the same as for one the pattern played. Only the pattern used
    /// to say anything, which is why a MIDI key sounded and nothing on the screen moved.
    /// </remarks>
    public void PreviewNote(Note note, int volume)
    {
        var instrument = Song.InstrumentAt(InstrumentForTrack(Cursor.Track));
        if (instrument == null) return;

        _player.Preview(instrument, note, GainFor(volume), Cursor.Track, HeldNoteSeconds);

        _sounding[note.Semitone] = instrument;

        Meters();

        Played(Cursor.Track, note, 0d);
    }

    /// <summary>
    /// How long a note played here sounds if nothing ever lets go of it.
    /// </summary>
    /// <remarks>
    /// Long, because both keyboards on this page do let go: the hardware sends the other half
    /// of the press and the letter rows have a release of their own now. So this is a safety net
    /// for a release that never arrives rather than the length of the note, and it wants to be
    /// long enough that nobody ever hears it.
    ///
    /// It was the fixed moment a clicked key wants, four tenths of a second, and that was the
    /// difference between what a chord sounded like under your hands and what it sounded like
    /// coming back: three short stabs against three notes ringing until the pattern played
    /// something else. A panel's own keys still hold for the fixed moment, because a click
    /// really has nothing to let go of it.
    ///
    /// Ten seconds rather than a minute, because the net is only ever reached when something
    /// went wrong, and a note left ringing for a minute after a lost release is worse than one
    /// cut short after ten. Nobody holds a key that long while writing a part.
    /// </remarks>
    private const double HeldNoteSeconds = 10;

    /// <summary>
    /// Which instrument each held note was sounded on, so the release reaches the same one.
    /// </summary>
    /// <remarks>
    /// Read back rather than worked out again from the cursor. The cursor can be moved between
    /// a press and its release, and an audition is let go of by naming the instrument that is
    /// holding it: a release that named the track's new instrument would reach nothing and
    /// leave the note sounding until its safety net ran out.
    /// </remarks>
    private readonly Dictionary<int, TrackerInstrument> _sounding = new();

    /// <summary>Types a note at the instrument's own level, which is what a letter key sends.</summary>
    public void EnterNote(Note note) => EnterNote(note, TrackerCell.NoVolume);

    /// <summary>
    /// Sounds a note and, while record is armed, writes it into the pattern.
    /// </summary>
    /// <remarks>
    /// A velocity sensitive keyboard makes every hit a little different. With
    /// <see cref="IgnoreVelocity"/> on, how hard a key was pressed is dropped here, on the way
    /// in, so a part comes out even and the instrument's own level is the only thing deciding
    /// how loud it is.
    ///
    /// While the song is playing a note lands on the line you can hear rather than the line you
    /// left the cursor on, and the cursor is not stepped down: the music is already moving.
    ///
    /// A key already down arriving again is the letter row repeating, which is how a column is
    /// filled quickly and stays. It is dropped while another key is down, because there it is
    /// not somebody filling a column: it is a hand resting on a chord, and every repeat would
    /// spray a single note down the pattern under the chord that was just written. Hardware
    /// never reaches this, since a key that is down cannot be pressed again.
    /// </remarks>
    public void EnterNote(Note note, int volume)
    {
        if (IgnoreVelocity) volume = TrackerCell.NoVolume;

        bool again = _holding.Contains(note.Semitone);

        if (again && _holding.Count > 1) return;

        bool chord = _chordLine >= 0 && _holding.Count > 0 && !again;

        PreviewNote(note, volume);

        _holding.Add(note.Semitone);

        if (CurrentPattern == null || !IsRecording) return;

        if (chord)
        {
            MakeRoom(Cursor.Track, _chordStart + _chordFilled);

            var into = Cursor with { Line = _chordLine, NoteColumn = _chordStart };

            Edits.EnterChordNote(
                CurrentPattern, into, _chordFilled, note, InstrumentForTrack(into.Track), volume);

            _chordFilled++;

            return;
        }

        var target = IsPlaying && PlayingLine >= 0 ? Cursor with { Line = PlayingLine } : Cursor;

        Edits.EnterNote(CurrentPattern, target, note, InstrumentForTrack(target.Track), volume);

        _chordLine = target.Line;
        _chordStart = target.NoteColumn;
        _chordFilled = 1;

        if (!IsPlaying) StepDown();
    }

    /// <summary>
    /// The note column the next note of a chord goes into, widening the track to fit it.
    /// </summary>
    /// <remarks>
    /// The rule is <see cref="Song.RoomForChord"/>, which is where it can be put a question to
    /// without a window. What is here is what a view model has to do about it: tell the page
    /// that the track just got wider. Where the note then goes is not this: a chord is kept in
    /// pitch order, so <see cref="IPatternEdit.EnterChordNote"/> decides that and this only
    /// makes sure the column exists.
    ///
    /// Deliberately no undo step of its own. The notes leave one, so undo takes the chord back
    /// off and leaves the track wide, which is an empty column and not worth a press. A step
    /// here would mean a three note chord costing three presses to undo, two of which appear to
    /// do nothing.
    /// </remarks>
    private void MakeRoom(int track, int column)
    {
        int was = Song.ColumnsOn(track);

        Song.RoomForChord(track, column - 1);

        if (Song.ColumnsOn(track) == was) return;

        OnPropertyChanged(nameof(ColumnsHere));
        OnPropertyChanged(nameof(CanAddColumn));
        OnPropertyChanged(nameof(CanRemoveColumn));
        OnPropertyChanged(nameof(Widths));
    }

    /// <summary>Which notes are still held, however they arrived, so a chord can be recognised.</summary>
    /// <remarks>
    /// A press while another key is still down is a chord and goes into the next note column;
    /// the same key arriving again is the keyboard repeating and is an ordinary note. Both
    /// sources are counted together, since a hand on the hardware and a hand on the letter rows
    /// are the same hand.
    /// </remarks>
    private readonly HashSet<int> _holding = new();

    /// <summary>Which line the chord being played is being written on, or -1 when none is.</summary>
    private int _chordLine = -1;

    /// <summary>Which note column it began in, which is the one the cursor was on.</summary>
    private int _chordStart;

    /// <summary>
    /// And how many of its columns are written, which is how many notes it has so far.
    /// </summary>
    /// <remarks>
    /// A count rather than the last column used, because the notes are kept in pitch order and
    /// the one that just arrived may have gone anywhere among them.
    /// </remarks>
    private int _chordFilled;

    /// <summary>
    /// A key has come up, so the chord it was part of may be over.
    /// </summary>
    /// <remarks>
    /// The note is let go of here, which is the whole of what a key coming up means: it starts
    /// the sound's own release, the same as a pattern's OFF. A recording that is one shot is
    /// left alone by <c>LetAudition</c>, since a take cut off part way through is not the sound
    /// the instrument makes.
    ///
    /// Called for both kinds of keyboard. A letter key has no release of its own in the note
    /// path, so the view raises one, and without it the first chord anybody typed would go on
    /// filling columns for the rest of the session.
    /// </remarks>
    public void LetNote(Note note)
    {
        if (!note.IsPlayable) return;

        _holding.Remove(note.Semitone);

        if (_holding.Count == 0) _chordLine = -1;

        if (_sounding.Remove(note.Semitone, out var instrument)) _player.LetPreview(instrument, note);
    }

    /// <summary>Forgets every held key, for the moment the keyboard goes somewhere else.</summary>
    /// <remarks>
    /// The release will be delivered wherever the keys went instead and this will never hear
    /// it, so without this the next note typed would be read as part of a chord begun before
    /// somebody clicked away.
    /// </remarks>
    public void LetAllNotes()
    {
        _holding.Clear();
        _chordLine = -1;

        foreach (var (semitone, instrument) in _sounding)
            _player.LetPreview(instrument, new Note(semitone));

        _sounding.Clear();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// It arrives on the MIDI thread, and everything it touches from there, the cursor, the
    /// pattern and the grid's redraw, belongs to the drawing thread.
    /// </remarks>
    public void PlayMidiNote(Note note, int volume) =>
        Dispatcher.UIThread.Post(() => EnterNote(note, volume));

    /// <inheritdoc/>
    /// <remarks>
    /// Here it writes a note-off into the pattern, and only when
    /// <see cref="RecordNoteOffs"/> has asked for that.
    ///
    /// The note is not looked at. A note-off ends whatever that track is sounding rather than
    /// one particular note, so which key was let go of does not change what gets written; it
    /// is here because the caller has it and a later reading of this may want it.
    /// </remarks>
    public void ReleaseMidiNote(Note note)
    {
        Dispatcher.UIThread.Post(() => LetNote(note));

        if (!RecordNoteOffs) return;

        Dispatcher.UIThread.Post(EnterNoteOff);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// No track is named, deliberately: the rack's keyboard may be playing an instrument that
    /// is in no song at all, so it goes through nobody's fader and moves no strip's meter.
    /// The master's still moves, because everything reaches the card through the master and
    /// that meter is the one measuring what you actually hear.
    /// </remarks>
    public double Audition(TrackerInstrument instrument, Note note, int volume)
    {
        Meters();

        return _player.Preview(instrument, note, GainFor(volume));
    }

    /// <inheritdoc/>
    public void Let(TrackerInstrument instrument, Note note) => _player.LetPreview(instrument, note);

    /// <inheritdoc/>
    public void Silence(TrackerInstrument instrument) => _player.CutPreview(instrument);

    /// <inheritdoc/>
    /// <remarks>
    /// The engine is brought up first, so the first note played is not the one that waits for
    /// the plugin to open.
    /// </remarks>
    public Audio.Plugins.Interfaces.IPluginInstrument? PluginFor(TrackerInstrument instrument)
    {
        if (instrument == null || !instrument.IsPlugin) return null;

        _player.EnsureEngine();
        return _player.PreviewPlayerFor(instrument);
    }

    /// <summary>
    /// Turns a volume column into a gain, with an empty column meaning the instrument's own
    /// level rather than silence.
    /// </summary>
    private static float GainFor(int volume) =>
        volume == TrackerCell.NoVolume
            ? 1f
            : Math.Clamp(volume, 0, TrackerCell.MaxVolume) / (float)TrackerCell.MaxVolume;

    /// <summary>
    /// Writes OFF where the cursor is, which ends whatever that track is sounding.
    /// </summary>
    /// <remarks>
    /// Only while record is armed, unlike Delete: this is typing, and typing into a pattern
    /// nobody armed is how a song gets edited by accident.
    /// </remarks>
    public void EnterNoteOff()
    {
        if (CurrentPattern == null || !IsRecording) return;

        Edits.EnterNoteOff(CurrentPattern, Cursor);
        StepDown();
    }

    /// <summary>
    /// Types one hexadecimal digit into whichever column the cursor is in.
    /// </summary>
    /// <remarks>
    /// The caret only steps down when the digit finished the value it was filling, so a two
    /// digit column takes two keystrokes and moves once.
    /// </remarks>
    public void EnterHexDigit(char digit)
    {
        if (CurrentPattern == null || !IsRecording) return;
        if (Edits.EnterHexDigit(CurrentPattern, Cursor, digit)) StepDown();
    }

    /// <summary>Types the letter half of an effect, leaving its digits where they are.</summary>
    public void EnterEffectCommand(char command)
    {
        if (CurrentPattern == null || !IsRecording) return;
        Edits.EnterEffectCommand(CurrentPattern, Cursor, command);
    }

    /// <summary>
    /// Delete. Clears the block if there is one, otherwise the column under the cursor.
    /// </summary>
    /// <remarks>
    /// Not gated on record, unlike typing a note. Hitting a letter key while jamming is easy
    /// to do by accident; pressing Delete is not, and a tracker where notes cannot be taken
    /// out without arming a record button is a tracker nobody can edit.
    /// </remarks>
    public void ClearAtCursor()
    {
        if (CurrentPattern == null) return;

        if (HasSelection)
        {
            DeleteSelection();
            return;
        }

        Edits.ClearAtCursor(CurrentPattern, Cursor);
        if (IsRecording) StepDown();
    }

    /// <summary>
    /// Pushes the track's cells down from the cursor, leaving an empty line where it is.
    /// </summary>
    /// <remarks>
    /// One track and not the whole pattern: a line inserted across every track would move parts
    /// nobody was editing out of time with the ones that were.
    /// </remarks>
    public void InsertLine()
    {
        if (CurrentPattern != null) Edits.InsertLine(CurrentPattern, Cursor);
    }

    /// <summary>Pulls the track's cells up over the cursor, which is the other half of it.</summary>
    public void DeleteLine()
    {
        if (CurrentPattern != null) Edits.DeleteLine(CurrentPattern, Cursor);
    }

    /// <summary>Moves the cursor and drops whatever block was drawn, which is the plain case.</summary>
    public void MoveCursor(int lineDelta, int trackDelta, int columnDelta) =>
        MoveCursor(lineDelta, trackDelta, columnDelta, extend: false);

    /// <summary>
    /// Moves the cursor, and with <paramref name="extend"/> drags the block along with it.
    /// Moving without extending drops the block, the way every editor does it.
    /// </summary>
    public void MoveCursor(int lineDelta, int trackDelta, int columnDelta, bool extend)
    {
        if (CurrentPattern == null) return;

        var moved = Cursor;
        if (lineDelta != 0) moved = moved.MoveLine(lineDelta, CurrentPattern.Lines);
        if (trackDelta != 0) moved = moved.MoveTrack(trackDelta, CurrentPattern.TrackCount);
        if (columnDelta != 0) moved = moved.MoveColumn(columnDelta, CurrentPattern.TrackCount, Widths);

        if (extend) Selection = Selection.IsEmpty ? PatternSelection.At(Cursor).ExtendTo(moved) : Selection.ExtendTo(moved);
        else Selection = PatternSelection.None;

        Cursor = moved;
    }

    /// <summary>
    /// How many note columns each track of this song shows, as the cursor and the metrics ask.
    /// </summary>
    /// <remarks>
    /// The song's list rather than the pattern's copy of it, because this is what the cursor is
    /// moved against and the cursor belongs to the song rather than to whichever pattern is
    /// open. They cannot differ: every pattern is given the song's counts.
    /// </remarks>
    public NoteColumns Widths => new(Song.NoteColumns);

    /// <summary>How many note columns the track the cursor is in shows.</summary>
    public int ColumnsHere => Song.ColumnsOn(Cursor.Track);

    /// <summary>Whether that track could show one more, and one fewer.</summary>
    public bool CanAddColumn => ColumnsHere < Song.MaxNoteColumns;

    /// <summary>And whether it could show one fewer.</summary>
    public bool CanRemoveColumn => ColumnsHere > Song.MinNoteColumns;

    /// <summary>
    /// Gives the track the cursor is in one more note column, or takes its last one away.
    /// </summary>
    /// <remarks>
    /// An edit like any other: it leaves an undo step, marks the song unsaved and is announced
    /// as a song step rather than a pattern step, since the count belongs to the song and a
    /// narrowing throws cells out of every pattern at once.
    ///
    /// The cursor is pulled back inside the track afterwards, or taking away the column it was
    /// sitting in would leave it pointing at a cell that is no longer there.
    /// </remarks>
    public void SetColumns(int count)
    {
        int track = Cursor.Track;
        if (track < 0 || track >= Song.TrackCount) return;
        if (count == Song.ColumnsOn(track)) return;

        Changing("a track's note columns");

        if (!Song.SetColumns(track, count)) return;

        Cursor = Cursor.Clamp(CurrentPattern?.Lines ?? 0, Song.TrackCount, Widths);
        Selection = PatternSelection.None;

        MarkDirty();
        ColumnsMoved();
    }

    /// <summary>A track is a different width, so everything measured in columns is stale.</summary>
    private void ColumnsMoved()
    {
        OnPropertyChanged(nameof(ColumnsHere));
        OnPropertyChanged(nameof(CanAddColumn));
        OnPropertyChanged(nameof(CanRemoveColumn));
        OnPropertyChanged(nameof(Widths));
    }

    /// <summary>
    /// Takes back the note columns nothing is written in any more.
    /// </summary>
    /// <remarks>
    /// What clearing asks for. A track that grew to three columns while a chord was played into
    /// it stays three columns wide once the chord is deleted, and every one of them is width on
    /// the screen, so emptying a track has to be allowed to give the room back.
    ///
    /// By what the whole song uses rather than what this pattern does, since the count is the
    /// song's: narrowing on one pattern's emptiness would throw away another pattern's chords.
    ///
    /// It leaves a step of its own and is meant to. Clearing a track is two things that undo
    /// separately, the notes and the room, and both of them are visible: the first press back
    /// widens the track and the second puts the music in it.
    /// </remarks>
    private void Narrow(int track)
    {
        int used = Song.ColumnsUsed(track);
        if (used == Song.ColumnsOn(track)) return;

        Changing("a track's note columns");

        if (!Song.SetColumns(track, used)) return;

        Cursor = Cursor.Clamp(CurrentPattern?.Lines ?? 0, Song.TrackCount, Widths);
        Selection = PatternSelection.None;

        MarkDirty();
        ColumnsMoved();
    }

    /// <summary>One more note column on the track the cursor is in.</summary>
    [RelayCommand]
    private void AddColumn() => SetColumns(ColumnsHere + 1);

    /// <summary>And one fewer, which throws away what was written in the one that goes.</summary>
    [RelayCommand]
    private void RemoveColumn() => SetColumns(ColumnsHere - 1);

    /// <summary>
    /// Puts the cursor exactly there, for a click on the grid.
    /// </summary>
    /// <remarks>
    /// The block is left alone, since a click that begins a drag sets the cursor before the
    /// drag has said anything about a selection.
    /// </remarks>
    public void SetCursor(PatternCursor value) => Cursor = value;

    /// <summary>
    /// Points a track at an instrument. Existing notes keep the instrument they were written
    /// with; this only decides what new notes on that track get.
    /// </summary>
    public async Task AssignInstrumentToTrack(int track, int instrument)
    {
        if (track < 0 || track >= Song.TrackCount) return;

        var chosen = Song.InstrumentAt(instrument);
        if (chosen == null) return;

        int previous = Song.GetInstrumentTrack(instrument);
        var displaced = Song.InstrumentAt(Song.GetTrackInstrument(track));

        Changing("pointing a track at an instrument");

        Song.SetTrackInstrument(track, instrument);
        SyncInstruments();
        RefreshStrips();
        PointEffectSlot();
        MarkDirty();

        if (previous == track)
            Status = $"'{chosen.Name}' is already on track {track + 1:00}";
        else if (displaced != null && displaced != chosen)
            Status = $"Track {track + 1:00} now plays '{chosen.Name}'. '{displaced.Name}' came off it.";
        else if (previous >= 0)
            Status = $"Moved '{chosen.Name}' from track {previous + 1:00} to track {track + 1:00}";
        else
            Status = $"Track {track + 1:00} plays '{chosen.Name}'";

        await OfferToPointNotesAt(track, instrument, chosen);
    }

    /// <summary>
    /// Notes already written on a track keep the instrument they were typed with, so binding a
    /// new one to the track leaves them addressed to the old. This is where that is offered to
    /// be put right.
    /// </summary>
    /// <remarks>
    /// Asked rather than done. Renumbering is the answer nearly every time, because a track
    /// showing one instrument and playing another is not something anybody means; but a track
    /// deliberately carrying two instruments turn and turn about is a real thing to write, and
    /// rewriting that without being asked would be taking somebody's arrangement apart.
    /// </remarks>
    private async Task OfferToPointNotesAt(int track, int instrument, TrackerInstrument chosen)
    {
        int stranded = Song.NotesAddressedElsewhere(track, instrument);
        if (stranded == 0) return;

        bool confirmed = await ConfirmDialog.AskAsync(
            "Point the notes at it",
            $"Track {track + 1:00} has {stranded} note(s) still addressed to another instrument. "
                + $"They will go on playing what they name, and '{chosen.Name}' will never sound. "
                + "Point them at it? The notes, volumes and effects stay as they are.",
            "Point them at it");

        if (!confirmed)
        {
            Status = $"Track {track + 1:00} plays '{chosen.Name}', but its {stranded} note(s) still name another instrument.";
            return;
        }

        Changing("pointing the notes at an instrument");

        int changed = Song.PointNotesAtTrackInstrument(track, instrument);

        RefreshStrips();
        MarkDirty();

        Status = $"Pointed {changed} note(s) on track {track + 1:00} at '{chosen.Name}'";
    }

    /// <summary>
    /// Moves a whole track to another position: its notes, its instrument, its effects and its
    /// mixer strip, in the song and in what is playing.
    /// </summary>
    /// <remarks>
    /// The song and what is playing have to move together, or the notes arrive at the new track
    /// and the sound answers on the old one.
    ///
    /// The cursor follows the track it was on rather than staying at its number, so dragging a
    /// track does not also move the caret to somebody else's part. An instrument lives on one
    /// track, which is why the status line says what moved and what was pushed off it.
    /// </remarks>
    public void MoveTrack(int from, int to)
    {
        if (from == to) return;
        if (from < 0 || from >= Song.TrackCount || to < 0 || to >= Song.TrackCount) return;

        var moved = Song.InstrumentAt(Song.GetTrackInstrument(from));

        Changing("moving a track");

        if (!Song.MoveTrack(from, to)) return;

        _player.MoveTrack(from, to);

        SyncInstruments();
        RefreshStrips();
        PointEffectSlot();
        MarkDirty();

        Cursor = Cursor with { Track = Song.WhereTrackWent(Cursor.Track, from, to) };

        Status = moved == null
            ? $"Moved track {from + 1:00} to {to + 1:00}"
            : $"Moved track {from + 1:00} to {to + 1:00}, '{moved.Name}' with it";
    }

    /// <summary>Takes the instrument off a track, which leaves the track with no sound source.</summary>
    /// <remarks>
    /// Nothing, rather than something else. It used to fall back to whichever instrument was
    /// picked out in the list, which is how a track with nothing on it came to make a sound.
    /// </remarks>
    public void ClearTrackInstrument(int track)
    {
        Changing("taking an instrument off a track");

        Song.SetTrackInstrument(track, TrackerCell.NoInstrument);
        SyncInstruments();
        RefreshStrips();
        PointEffectSlot();
        MarkDirty();
        Status = $"Track {track + 1:00} has no instrument";
    }

    /// <summary>
    /// What a track plays, and nothing when it plays nothing. This is also the instrument a
    /// note typed on that track carries.
    /// </summary>
    /// <remarks>
    /// The track's own instrument and never the one picked out in the list beside the pattern.
    /// Those are two different questions and answering the first with the second is what made a
    /// track with no sound source play somebody else's: the keyboard sounded an instrument the
    /// track had not got, and typing wrote that instrument's number into a cell on it. A track
    /// with nothing on it makes no sound, playing or stopped, and a note typed into it goes in
    /// with the instrument column blank, which is what the sequencer already understands as
    /// "whatever this track last played", and that is nothing either.
    ///
    /// Which one is picked in the list is about the list: it is what a new track would be given
    /// and what the rack is showing, and it has never been a fact about this track.
    /// </remarks>
    private int InstrumentForTrack(int track) => Song.GetTrackInstrument(track);

    /// <summary>
    /// Drops the caret by the edit step, which is how typing a part walks down a track.
    /// </summary>
    /// <remarks>
    /// A step of nought is a deliberate setting and means the caret stays put, which is how a
    /// chord is typed into one line.
    /// </remarks>
    private void StepDown()
    {
        if (CurrentPattern == null || EditStep <= 0) return;
        Cursor = Cursor.MoveLine(EditStep, CurrentPattern.Lines);
    }

    /// <summary>
    /// Adds an empty pattern and a slot at the end of the order pointing at it, then goes there.
    /// </summary>
    private void AddPattern()
    {
        Changing("adding a pattern");

        int index = Song.AddPattern();
        Song.Order.Add(index);
        RefreshOrder();
        MarkDirty();
        OrderIndex = Song.Order.Count - 1;
        Status = $"Added pattern {Song.Patterns[index].Name}";
    }

    /// <summary>
    /// Takes the picked slot out of the order. The pattern itself stays in the song, since it
    /// may be in the order somewhere else and is somebody's work either way.
    /// </summary>
    /// <remarks>
    /// The last slot is kept: a song with an empty order has nothing to play and nowhere to put
    /// the cursor.
    /// </remarks>
    private void RemoveOrderEntry()
    {
        Changing("removing an order slot");

        if (Song.Order.Count <= 1) return;

        Song.Order.RemoveAt(Math.Clamp(OrderIndex, 0, Song.Order.Count - 1));
        RefreshOrder();
        MarkDirty();
        OrderIndex = Math.Clamp(OrderIndex, 0, Song.Order.Count - 1);
        CurrentPattern = Song.PatternAt(OrderIndex);
    }

    /// <summary>
    /// Sets how many tracks the song has, clamped to what a song allows rather than refused.
    /// </summary>
    /// <remarks>
    /// The grid redraws off the pattern's own change event, so only the label has to be told
    /// that the number moved.
    /// </remarks>
    private void SetTrackCount(int trackCount)
    {
        int clamped = Math.Clamp(trackCount, Song.MinTrackCount, Song.MaxTrackCount);
        if (clamped == Song.TrackCount) return;

        Changing("how many tracks");

        Song.SetTrackCount(clamped);
        Cursor = Cursor.Clamp(CurrentPattern?.Lines ?? 0, clamped);
        SyncInstruments();
        RefreshStrips();
        MarkDirty();

        OnPropertyChanged(nameof(TrackCount));
    }

    /// <summary>
    /// Rebuilds the mixer from the song. Called whenever the track count or the instrument on a
    /// track changes, since a strip is named after what plays through it.
    /// </summary>
    /// <remarks>
    /// The master is made separately and kept out of <see cref="Strips"/>, so nothing that
    /// walks the tracks ever finds it by counting. Rebuilding is what
    /// <see cref="MixShown"/> exists for: anything holding a strip is holding the last song's.
    /// </remarks>
    private void RefreshStrips()
    {
        Song.Normalize();

        Strips.Clear();
        for (int track = 0; track < Song.TrackCount && track < Song.Mix.Count; track++)
        {
            var instrument = Song.InstrumentAt(Song.GetTrackInstrument(track));
            Strips.Add(new TrackStripViewModel(
                track, Song.Mix[track], instrument?.Name ?? "", Song.TrackCount, OnMixChanged)
            {
                IsSelected = track == Cursor.Track
            });
        }

        MasterStrip = new TrackStripViewModel(
            TrackerPlayer.MasterStrip, Song.Master, "", Song.TrackCount, OnMixChanged);

        MixShown?.Invoke();
    }

    /// <summary>
    /// A strip is named after whatever plays through it, so a rename in the rack shows up
    /// here. Updated in place rather than rebuilt: the mixer is full of controls you may be
    /// holding on to.
    /// </summary>
    /// <remarks>
    /// The master is remade rather than updated, since it carries no name to update.
    /// </remarks>
    private void RefreshStripNames()
    {
        foreach (var strip in Strips)
        {
            var instrument = Song.InstrumentAt(Song.GetTrackInstrument(strip.Track));
            strip.InstrumentName = instrument?.Name ?? "";
        }

        MasterStrip = new TrackStripViewModel(
            TrackerPlayer.MasterStrip, Song.Master, "", Song.TrackCount, OnMixChanged);

        MixShown?.Invoke();
    }

    /// <summary>
    /// Told whenever the mix moves, for anything showing it that is not on the screen.
    /// </summary>
    /// <remarks>
    /// A control surface, which has the levels under its own faders and the names on its own
    /// display and has no other way of hearing that either changed. Deliberately not a
    /// subscription to each strip: the strips are rebuilt whenever the song is, so anything
    /// holding them would be holding the last song's.
    /// </remarks>
    public Action? MixShown { get; set; }

    /// <summary>A fader or a mute moved: hear it now, and remember the song has changed.</summary>
    /// <remarks>
    /// The step it takes is gathered by what it says it is about, or a fader dragged across its
    /// range would be a hundred of them and one undo would move it by a hair.
    /// </remarks>
    private void OnMixChanged()
    {
        Changing("the mix");

        _player.ApplyMix();
        MarkDirty();

        MasterStrip = new TrackStripViewModel(
            TrackerPlayer.MasterStrip, Song.Master, "", Song.TrackCount, OnMixChanged);

        MixShown?.Invoke();
    }

    /// <summary>
    /// Rebuilds the order list, keeping the slot that was selected. Emptying the list makes
    /// the ListBox drop its selection, and that writes -1 straight back into OrderIndex, which
    /// takes the pattern off the screen with it. So the wanted slot is held here and put back
    /// once the list is whole again.
    /// </summary>
    /// <remarks>
    /// The pattern and the two neighbours are set outright at the end rather than left to the
    /// change hooks. Restoring the same slot number is not a change, so nothing would fire and
    /// the grid would stay empty; and the order is what decides what is either side of this
    /// pattern, so a slot added or taken out moves both without the number moving at all.
    /// </remarks>
    private void RefreshOrder()
    {
        int wanted = OrderIndex;

        OrderEntries.Clear();
        for (int i = 0; i < Song.Order.Count; i++)
        {
            var pattern = Song.PatternAt(i);
            OrderEntries.Add($"{i:00}   {pattern?.Name ?? "--"}");
        }

        OrderIndex = OrderEntries.Count == 0 ? -1 : Math.Clamp(wanted, 0, OrderEntries.Count - 1);

        CurrentPattern = Song.PatternAt(OrderIndex);

        NeighboursMoved();
    }

    /// <summary>
    /// The rack has changed under the picker that brings an instrument into this song.
    /// </summary>
    /// <remarks>
    /// The picker is filled straight from the rack now rather than from a second reading of the
    /// folder, so there is no list here to rebuild. There was, and it was read before the rack
    /// had been brought into shape, which is how the picker came to be offering four machines
    /// twice each. All that is left to do is let go of a choice that is no longer on it.
    /// </remarks>
    public void RefreshRack()
    {
        if (PickedMachine == null) return;

        if (_rack.Load(PickedMachine.Id) == null) PickedMachine = null;
    }

    /// <summary>
    /// Gives the song a slot for a rack instrument, so its cells can name it. The slot holds
    /// a copy: a song opened without the rack of instruments still plays, and the copy is
    /// brought back up to date whenever the rack has the instrument. The machine it is on is a
    /// separate matter and does have to be installed, or the instrument is silent.
    /// </summary>
    /// <remarks>
    /// The copy is given an id of its own, because it is the song's from here on: name it what
    /// you like, set it how you like, and take a second one off the same machine if you want
    /// one. Sharing the rack's id would have meant one Zampler to a song and a name you could
    /// not change, since a machine on the rack keeps the machine's name.
    /// </remarks>
    private void AddInstrument()
    {
        var chosen = PickedMachine?.Instrument;
        if (chosen == null)
        {
            Status = "Pick an instrument from the rack first.";
            return;
        }

        Changing("adding an instrument");

        var taken = chosen.Clone();

        taken.Id = "";
        taken.EnsureId();

        Song.Instruments.Add(taken);
        SyncInstruments();
        MarkDirty();

        SelectedInstrument = Song.Instruments.Count - 1;
        Status = $"Added '{chosen.Name}' to the song as instrument {SelectedInstrument:00}";
    }


    /// <summary>
    /// An instrument was edited in the rack. The picker follows it; the song does not.
    /// </summary>
    /// <remarks>
    /// The song's instruments are the song's, so an edit made on the rack does not reach a song
    /// that already has one. Nor is there anything to update in the picker any more: it is
    /// filled from the rack's own rows, which the rack has already refreshed by the time this
    /// is called. What is left is letting go of a choice that has gone.
    /// </remarks>
    public void ApplyMachineEdit(TrackerInstrument edited)
    {
        if (edited == null || string.IsNullOrEmpty(edited.Id)) return;

        RefreshRack();
    }

    /// <summary>
    /// Takes the picked instrument out of the song, asking first.
    /// </summary>
    /// <remarks>
    /// Cells point at instruments by number, so removing one renumbers every cell that named a
    /// later one: this rewrites the patterns as well as the list, which is why it is asked
    /// about and why the step is taken before it happens. It is the edit that reaches furthest,
    /// and nothing smaller than the whole song would put it back.
    /// </remarks>
    private async Task RemoveSelectedInstrument()
    {
        int index = SelectedInstrument;
        var instrument = Song.InstrumentAt(index);
        if (instrument == null) return;

        bool confirmed = await ConfirmDialog.AskAsync(
            "Remove from song",
            $"Take '{instrument.Name}' out of this song? Cells that used it lose their instrument, "
                + "and the rest are renumbered. The instrument stays in the rack.",
            "Remove");

        if (!confirmed) return;

        Changing("taking an instrument out");

        if (!Song.RemoveInstrumentAt(index)) return;

        SyncInstruments();
        MarkDirty();
        SelectedInstrument = Math.Clamp(index, 0, Math.Max(0, Song.Instruments.Count - 1));
        Status = $"Removed '{instrument.Name}' from the song. It is still in the rack.";
    }

    /// <summary>
    /// Shows the list of saved songs and opens whichever one is picked.
    /// </summary>
    /// <remarks>
    /// The list is read again first: songs are files, and a folder somebody has copied one
    /// into since the app started should not need the app restarting to notice.
    /// </remarks>
    private async Task OpenSong()
    {
        RefreshSavedSongs();

        if (!await Views.SongDialog.PickAsync(this)) return;

        Load();

        NoteMissingMachines();
    }

    /// <summary>
    /// Puts which machines this song needs and this installation has not got on the status line.
    /// </summary>
    /// <remarks>
    /// A line and not a dialog. It used to be a dialog on the way in, and that was the wrong
    /// moment twice over: it interrupts the opening of a song to talk about instruments nobody
    /// has looked at yet, and by the time somebody does look it has long been dismissed. What
    /// answers at the moment it is wanted is the panel refusing to open, which says the same
    /// thing about the one instrument being asked for.
    ///
    /// So this is the quiet half: a note that the song is not all here, where the song's other
    /// notes go, for somebody who wants to know before they start rather than when they click.
    ///
    /// It points at the registry rather than describing it, as the dialog does, and for the same
    /// reason: that page shows what is waiting to be added and what is not there at all, and it
    /// shows it while somebody is looking at it.
    ///
    /// It tells and does not offer. Putting a machine on the rack is a thing you do to this
    /// installation, not to a song, and doing it from a song being opened is how an installation
    /// ends up in a state nobody chose. The two rows of buttons in SETTINGS stay the only two.
    /// </remarks>
    public void NoteMissingMachines()
    {
        var wanted = Missing.For(Song);

        if (wanted.Count == 0) return;

        Status = "Silent, not registered: "
                 + Listed(wanted.Select(machine => machine.Name).ToList())
                 + ". Check the machine registry under SETTINGS, System.";
    }

    /// <summary>Names in a row, the way anybody would say them out loud.</summary>
    private static string Listed(IReadOnlyList<string> names) => names.Count switch
    {
        0 => "",
        1 => names[0],
        _ => string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1],
    };

    /// <summary>
    /// Asks what to call it and saves it under that.
    /// </summary>
    /// <remarks>
    /// The name box used to stand on the page, which meant a song could be renamed by a stray
    /// keystroke in a field nobody was looking at. Asking is a moment, and the moment is the
    /// point: this is the one place a song changes its name, and the one place it says what it
    /// is. Save as on a song that already has a name is therefore also how the description is
    /// changed later.
    /// </remarks>
    private async Task SaveAs()
    {
        var details = await Views.SongDetailsDialog.AskAsync(SongName, Song.Description);

        if (details == null) return;

        if (Needs(details.Name))
        {
            Status = "'" + Unnamed + "' is not a name. Call it something you will know again.";
            return;
        }

        SongName = details.Name;
        Song.Description = details.Description;
        OnPropertyChanged(nameof(SongDescription));

        Save();
    }

    /// <summary>Saves it, asking for a name first when it has not got one.</summary>
    private async Task SaveOrAsk()
    {
        if (Needs(SongName))
        {
            await SaveAs();
            return;
        }

        Save();
    }

    /// <summary>True while what the song is called is not yet a name.</summary>
    private static bool Needs(string name)
    {
        string wanted = name.Trim();

        return wanted.Length == 0 || string.Equals(wanted, Unnamed, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Writes the song into the songs folder under its own name.
    /// </summary>
    /// <remarks>
    /// What is saved is what the tracks are actually playing rather than whatever the song was
    /// opened with: the chains are read off the player, and the patch of every plugin
    /// instrument is read back out of the plugin before the file is written, so a knob turned in
    /// Serum's own window is in the song.
    ///
    /// Every track, not the one the cursor is on. The effect slot follows the cursor, so asking
    /// it alone saved one track's plugin and quietly dropped what every other track's plugin was
    /// set to.
    ///
    /// A real save also drops whatever was being kept: the rescue file is no longer anybody's
    /// safety net once the work is on disc under its own name.
    /// </remarks>
    private void Save()
    {
        string name = SongName.Trim();
        if (name.Length == 0)
        {
            Status = "Give the song a name before saving.";
            return;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            Status = "That name cannot be used as a file name.";
            return;
        }

        try
        {
            SongName = name;
            Song.Name = name;

            _player.CaptureChains(Song);

            foreach (var box in _instrumentBoxes.Values) box.SyncPatch();

            string path = _store.PathFor(name);
            _store.Save(Song, path);

            RefreshSavedSongs();
            SelectedSongFile = SavedSongs.FirstOrDefault(f => _paths.Same(f.Path, path));

            IsDirty = false;

            Drop();

            RefreshSavedSongs();

            OnPropertyChanged(nameof(CanDeleteSong));
            OnPropertyChanged(nameof(CanRevertSong));
        OnPropertyChanged(nameof(CanRevertSong));

            Status = $"Saved '{name}'";
        }
        catch (Exception ex)
        {
            Status = $"Save failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Opens the song picked out in the list.
    /// </summary>
    /// <remarks>
    /// A packed song brought its takes with it and they are on the shelf by the time this
    /// returns, so <see cref="RecordingsArrived"/> is raised: without it they are there and
    /// nobody can see them.
    /// </remarks>
    private void Load()
    {
        var file = SelectedSongFile;
        if (file == null)
        {
            Status = "Pick a song to open.";
            return;
        }

        var loaded = _store.Load(file.Path, out var arrived);
        if (loaded == null)
        {
            Status = $"'{file.Name}' could not be read.";
            return;
        }

        Adopt(loaded, file.Name);

        if (arrived.Count > 0) RecordingsArrived?.Invoke(this, EventArgs.Empty);

        Status = arrived.Count == 0
            ? $"Opened '{file.Name}'"
            : arrived.Count == 1
                ? $"Opened '{file.Name}', and one recording it carried is now on the shelf"
                : $"Opened '{file.Name}', and {arrived.Count} recordings it carried are now on the shelf";
    }

    /// <summary>The recordings this song names that are not on this machine.</summary>
    private static IReadOnlyList<string> Lost(Song song)
    {
        var lost = new List<string>();
        if (song == null) return lost;

        var seen = new HashSet<string>(_paths.Comparer);

        foreach (var instrument in song.Instruments)
            foreach (string path in Usage.Files(instrument))
            {
                if (string.IsNullOrWhiteSpace(path)) continue;

                string full = _paths.Full(path);

                if (!seen.Add(full)) continue;
                if (File.Exists(full)) continue;

                lost.Add(Path.GetFileName(full));
            }

        return lost;
    }

    /// <summary>
    /// Writes the open song somewhere with its recordings inside it, for handing over.
    /// </summary>
    /// <remarks>
    /// Not a save: this one does not become the song being worked on, does not clear the star
    /// on the save button, and is not written to the songs folder. It is a copy that plays on a
    /// machine that has none of your takes.
    /// </remarks>
    public void Pack(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            _player.CaptureChains(Song);
            foreach (var box in _instrumentBoxes.Values) box.SyncPatch();

            int carried = Carried.Wanted(Song).Count;

            _store.Save(Song, path, withSamples: true);

            Status = carried == 0
                ? $"Packed '{SongName}'. Everything it plays ships with the program, so it carries nothing."
                : carried == 1
                    ? $"Packed '{SongName}' with one recording inside it."
                    : $"Packed '{SongName}' with {carried} recordings inside it.";
        }
        catch (Exception ex)
        {
            Status = $"Pack failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Puts the open song down and starts an empty one under the placeholder name.
    /// </summary>
    /// <remarks>
    /// Nothing is asked, because nothing is lost that was not already unsaved; what was being
    /// kept goes with it through <see cref="Adopt"/>.
    /// </remarks>
    private void NewSong()
    {
        Adopt(Song.CreateDefault(), Unnamed);
        SelectedSongFile = null;
        Status = "New song";
    }

    /// <summary>
    /// Swaps in a different song and brings every view-facing collection with it. Loading
    /// touches the pattern, the order, the instruments, and the cursor, so it all happens here
    /// rather than being spread across the callers.
    /// </summary>
    /// <remarks>
    /// The song's instruments are the song's own and are not fetched from the rack on the way
    /// in: a song opens sounding exactly the way it was saved, and an instrument built here
    /// belongs to the work it was built for. The rack is where a sound starts, not something
    /// that reaches back into a song already written.
    ///
    /// The history is emptied, because a history outliving its song would hand somebody another
    /// song's notes back. The octave came with the song, so the pattern editor and every panel
    /// open on it follow it there.
    ///
    /// The plugins the last song had on its tracks belong to that song: left in place they
    /// would go on playing under the new song's notes. The new song's come back with it, and a
    /// plugin that is not on this machine is reported rather than passed over in silence, as are
    /// recordings the song names and this machine has not got: a song missing those opens
    /// perfectly and plays nothing on those tracks, and nothing about that looks like a fault
    /// until you go looking for one.
    ///
    /// Plugin instruments are loaded now rather than on the note that wants one. Each is a
    /// process of its own with a patch to swallow, and left to the clock they came up one at a
    /// time, each stall landing on the first bar somebody was listening to.
    ///
    /// The order list is rebuilt before the slot is chosen, so a fresh song opens on its first
    /// pattern rather than on nothing. The song's own controller layout came with it, so
    /// anything showing that layout is showing the last song's until it is told; nothing else
    /// notices, since the mappings are read per message and were already right, and it is only
    /// the list on the screen that was wrong.
    ///
    /// Whatever was being kept belonged to the song that has just been put down. Leaving it
    /// would offer somebody their old work back every time they opened anything.
    ///
    /// The tempo and the track count live on the song, so the whole transport bar is stale and
    /// is told so at the end.
    /// </remarks>
    private void Adopt(Song replacement, string name)
    {
        _player.Stop();

        replacement.Normalize();

        Song = replacement;
        SongName = name;
        Song.Name = name;

        History.Forget();

        Octave = Math.Clamp(Song.KeyboardOctave, 0, 9);

        SyncInstruments();
        RefreshStrips();

        CloseInstrumentBoxes();
        _player.ClearPlayers();

        var missing = _player.RestoreChains(Song);
        var lost = Lost(Song);

        if (missing.Count > 0 || lost.Count > 0)
        {
            var said = new List<string>();

            if (missing.Count > 0) said.Add("Missing plugin(s): " + string.Join(", ", missing));

            if (lost.Count > 0) said.Add("Missing recording(s): " + string.Join(", ", lost));

            Status = string.Join("  ", said);
        }

        _player.PreloadPlugins(Song);

        PointEffectSlot();

        RefreshOrder();

        OrderIndex = 0;
        CurrentPattern = Song.PatternAt(0);
        Cursor = PatternCursor.Start.Clamp(CurrentPattern?.Lines ?? 0, Song.TrackCount);
        PlayingLine = -1;

        SongControls?.Reread();

        IsDirty = false;

        Drop();

        OnPropertyChanged(nameof(Bpm));
        OnPropertyChanged(nameof(LinesPerBeat));
        OnPropertyChanged(nameof(QuantizeChoices));
        OnPropertyChanged(nameof(TrackCount));
    }

    /// <summary>
    /// Gives the song's instrument a name of your choosing.
    /// </summary>
    /// <remarks>
    /// A dialog rather than an editable row, because the row has to stay readable while you
    /// pick through the list. This is the song's own copy, so the name is the song's: the
    /// machine it came off the rack from keeps its own.
    /// </remarks>
    public IAsyncRelayCommand RenameInstrumentCommand => new AsyncRelayCommand(RenameInstrument);

    /// <summary>
    /// Asks for the new name and puts it on the song's own copy.
    /// </summary>
    /// <remarks>
    /// Nothing happens when the name comes back unchanged, so opening the dialog and pressing
    /// return does not leave a step in the history.
    /// </remarks>
    private async Task RenameInstrument()
    {
        var slot = Instruments.ElementAtOrDefault(SelectedInstrument);

        if (slot == null) return;

        string? wanted = await NameDialog.AskAsync(
            "Rename instrument",
            "What this instrument is called in this song. The machine it came from keeps its own name.",
            slot.Name);

        if (wanted == null || wanted == slot.Name) return;

        Changing("renaming an instrument");

        slot.Instrument.Name = wanted;
        slot.Refresh();

        MarkDirty();

        Status = "Renamed instrument " + slot.Number + " to '" + wanted + "'";
    }

    /// <summary>
    /// Says the rows again after the panel changed one of the song's instruments.
    /// </summary>
    /// <remarks>
    /// The song holds its own copy of every instrument, so what a panel changes is changed in
    /// the song: the row beside it has to show that rather than what the sound was when it was
    /// taken off the rack.
    /// </remarks>
    public void InstrumentEdited()
    {
        foreach (var slot in Instruments) slot.Refresh();

        MarkDirty();
    }

    /// <summary>
    /// Rebuilds the rows beside the pattern so every one carries its current number and the
    /// track it is on.
    /// </summary>
    /// <remarks>
    /// Rebuilding the list drops the selection, which is then put back where it was: without
    /// that, every edit that touched the list moved the picked instrument to the top.
    /// </remarks>
    private void SyncInstruments()
    {
        int selected = SelectedInstrument;

        Instruments.Clear();
        for (int i = 0; i < Song.Instruments.Count; i++)
            Instruments.Add(new InstrumentSlot(i, Song.Instruments[i], Song.GetInstrumentTrack(i)));

        SelectedInstrument = Math.Clamp(selected, 0, Math.Max(0, Instruments.Count - 1));

        OnPropertyChanged(nameof(HasInstruments));
    }

    /// <summary>
    /// Removes the open song from disc, which is what the button in the bar does.
    /// </summary>
    /// <remarks>
    /// What is open stays open, even though it is the one that was deleted: what you are working
    /// on is in memory and throwing away the file is not a reason to take it off you. It simply
    /// has nowhere to go back to, which is what an untitled song is, so it is marked unsaved and
    /// the picker forgets it.
    ///
    /// The work is <see cref="DeleteSongFile"/>, so there is one delete and one question rather
    /// than two that could come to disagree.
    /// </remarks>
    private async Task DeleteSong()
    {
        string name = SongName.Trim();

        if (!CanDeleteSong)
        {
            Status = "'" + name + "' has never been saved, so there is nothing to delete.";
            return;
        }

        string path = _store.PathFor(name);

        await DeleteSongFile(new SongFile(name, path, Song.Description));
    }

    /// <summary>
    /// Deletes a song off the list, whichever one, rather than the one that is open.
    /// </summary>
    /// <remarks>
    /// The same delete and the same question, so there is one way of getting rid of a song and
    /// not two. Deleting the one that happens to be open leaves it on the screen, unsaved, the
    /// way the button in the bar does: what is in front of you is not something a list of files
    /// gets to take away.
    ///
    /// The open song is marked unsaved when its file goes, because what is on the screen is now
    /// the only copy of itself.
    /// </remarks>
    public async Task DeleteSongFile(SongFile? file)
    {
        if (file is null) return;

        bool confirmed = await ConfirmDialog.AskAsync(
            "Delete song",
            "Delete '" + file.Name + "' from disc? The instruments it used are untouched. " +
                "This cannot be undone.",
            "Delete");

        if (!confirmed) return;

        try
        {
            bool wasOpen = _paths.Same(file.Path, _store.PathFor(SongName.Trim()));

            _store.Delete(file.Path);

            if (SelectedSongFile != null && _paths.Same(SelectedSongFile.Path, file.Path))
                SelectedSongFile = null;

            RefreshSavedSongs();

            if (wasOpen)
            {
                MarkDirty();
                OnPropertyChanged(nameof(CanDeleteSong));
                OnPropertyChanged(nameof(CanRevertSong));
            OnPropertyChanged(nameof(CanRevertSong));
        OnPropertyChanged(nameof(CanRevertSong));

                Status = "Deleted '" + file.Name + "'. What is open is still here, but unsaved.";
            }
            else
            {
                Status = "Deleted '" + file.Name + "'.";
            }
        }
        catch (Exception ex)
        {
            Status = "Could not delete '" + file.Name + "': " + ex.Message;
        }
    }

    /// <summary>
    /// Reads the songs folder again, keeping whichever row was picked.
    /// </summary>
    /// <remarks>
    /// Kept by path rather than by object, since every read builds new
    /// <see cref="SongFile"/> records and the old one would never match.
    /// </remarks>
    private void RefreshSavedSongs()
    {
        string? keep = SelectedSongFile?.Path;

        SavedSongs.Clear();
        foreach (var file in _store.ListSongs())
            SavedSongs.Add(file);

        RestockSongs();

        SelectedSongFile = SavedSongs.FirstOrDefault(f => _paths.Same(f.Path, keep));
    }

    /// <summary>
    /// Puts the songs the search allows in the shown list, and only those.
    /// </summary>
    /// <remarks>
    /// A song that is in both lists is left where it is rather than being taken out and put
    /// back, so the one you had picked stays picked while you type past it and back.
    ///
    /// Both passes ask the same question, which is what a song is rather than which object it
    /// is. Every refresh reads the folder again and builds new <see cref="SongFile"/> records,
    /// so a pass that kept a row by what it says and then replaced it by what it is put every
    /// song in the list twice.
    /// </remarks>
    private void RestockSongs()
    {
        var wanted = SavedSongs.Where(Named).ToList();

        for (int i = ShownSongs.Count - 1; i >= 0; i--)
            if (!wanted.Contains(ShownSongs[i])) ShownSongs.RemoveAt(i);

        for (int i = 0; i < wanted.Count; i++)
            if (i >= ShownSongs.Count || !Equals(ShownSongs[i], wanted[i]))
                ShownSongs.Insert(i, wanted[i]);

        OnPropertyChanged(nameof(NoSongsFound));
    }

    /// <summary>
    /// Whether a song's name carries what was typed, in the reader's own alphabet rather than
    /// the machine's: an accented name should match the way somebody would expect it to.
    /// </summary>
    private bool Named(SongFile file) =>
        SongSearch.Length == 0 ||
        (file.Name ?? "").Contains(SongSearch, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    /// Stops the meters and puts the player down, which takes the audio and every plugin with
    /// it.
    /// </summary>
    /// <remarks>
    /// The player owns the stream and the plugin processes, so this is where they go. Nothing
    /// here writes the song down: what was unsaved is already in the kept file.
    /// </remarks>
    public void Dispose()
    {
        _meters.Stop();
        _player.Dispose();
    }
}
