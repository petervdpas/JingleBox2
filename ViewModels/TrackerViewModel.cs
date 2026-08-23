using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Audio.Plugins;
using JingleBox2.Config;
using JingleBox2.Diagnostics;
using JingleBox2.Models;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Synth;
using JingleBox2.Views;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JingleBox2.ViewModels;

/// <summary>
/// Holds the song being edited and drives the player. All sequencing, editing, and cursor
/// maths live in the Tracker namespace; this class is the bridge to the view.
/// </summary>
public sealed partial class TrackerViewModel : ObservableObject, IInstrumentAudition, ITrackerPanel
{
    private readonly TrackerPlayer _player;
    private readonly SongStore _store;
    private readonly MachineRack _rack;
    private readonly DispatcherTimer _meters;
    private readonly ObservableCollection<Recording> _recordings;

    /// <summary>Where the velocity preference is kept. Null in a test or a headless run.</summary>
    private readonly ConfigStore? _configStore;

    private readonly AppConfig? _config;

    [ObservableProperty] private Song song;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PatternLines))]
    private Pattern? currentPattern;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTrack))]
    [NotifyPropertyChangedFor(nameof(CursorTrackLabel))]
    private PatternCursor cursor = PatternCursor.Start;

    partial void OnCursorChanged(PatternCursor value) => FollowCursorTrack();
    [ObservableProperty] private int orderIndex;
    [ObservableProperty] private int playingLine = -1;

    /// <summary>How many rows the pattern has, for a panel showing where its track is.</summary>
    public int PatternLines => CurrentPattern?.Lines ?? 0;

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

    private void Played(int track, Note note, double seconds) =>
        _played?.Invoke(this, (track, note, seconds));

    /// <summary>What plugins this machine has, for the picker on the mixer page.</summary>
    public PluginLibraryViewModel Plugins { get; }

    /// <summary>
    /// The effect slot for whichever track is picked. One slot, retargeted as the selection
    /// moves, rather than a set of controls repeated down every channel.
    /// </summary>
    public PluginChainViewModel TrackEffect { get; }

    /// <summary>Which track the effect slot is pointed at, so the cursor does not retarget it
    /// on every keystroke that stays in the same column.</summary>
    private int _effectTrack = -1;

    /// <summary>
    /// Points the effect slot at the track the cursor is on. Moving between tracks changes
    /// what the panel under the pattern is about; moving up and down a track does not.
    /// </summary>
    private void FollowCursorTrack()
    {
        int track = Cursor.Track;
        if (track == _effectTrack && TrackEffect.Target != null) return;

        _effectTrack = track;

        TrackEffect.Target = new TrackPluginTarget(_player, track);
        TrackEffect.Instrument = InstrumentBoxFor(track);
    }

    /// <summary>
    /// The box at the head of a track's strip: the plugin that track plays, when it plays one.
    /// </summary>
    /// <remarks>
    /// Made from what the song says is on the track, not from what is loaded: the plugin
    /// itself is not asked for until somebody opens the box.
    /// </remarks>
    private PluginInstrumentViewModel? InstrumentBoxFor(int track)
    {
        var instrument = Song.InstrumentAt(Song.GetTrackInstrument(track));

        // Every kind, not only plugins. What a track plays sits at the head of its strip
        // whether the sound is Serum's or ours; they are the same thing to the track, and
        // only what opens when you click differs.
        if (instrument == null)
        {
            if (_instrumentBoxes.Remove(track, out var gone)) gone.Discard();
            return null;
        }

        // The same box every time, or coming back to a track would make a second one and open
        // a second window onto one plugin's interface, which some plugins do not survive.
        if (_instrumentBoxes.TryGetValue(track, out var existing) &&
            ReferenceEquals(existing.Instrument, instrument))
        {
            return existing;
        }

        // A box for an instrument this track no longer plays is finished with: it is watching
        // the tracker on behalf of a panel nobody can open any more.
        if (existing != null) existing.Discard();

        // InstrumentEdited rather than MarkDirty: what the panel changes is the song's own copy,
        // so the row beside the pattern has to show it as well as the file having to be written.
        var box = new PluginInstrumentViewModel(
            instrument,
            () => _player.EnsurePlayerOn(track, instrument),
            InstrumentEdited,
            () => new TrackInstrumentDesigner(track, instrument, this, InstrumentEdited, _waveforms, this, _rack, _recordings));

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlaying))]
    [NotifyPropertyChangedFor(nameof(IsPaused))]
    [NotifyPropertyChangedFor(nameof(IsStopped))]
    [NotifyPropertyChangedFor(nameof(CanPause))]
    private TrackerTransportState transport = TrackerTransportState.Stopped;

    /// <summary>Typed notes are written into the pattern only while this is on.</summary>
    [ObservableProperty] private bool isRecording;

    /// <summary>Set by every edit, cleared by a save. Nothing here is on disk until then.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveSongText))]
    private bool isDirty;

    /// <summary>Pattern by default: most editing is done against a single looping pattern.</summary>
    [ObservableProperty] private TrackerPlayMode playMode = TrackerPlayMode.Pattern;
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

    /// <summary>
    /// The octave moved because a panel's keyboard had to show a note. The song keeps the new
    /// value, but it is not an edit, so it does not ask to be saved.
    /// </summary>
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
    [ObservableProperty] private int selectedInstrument;
    [ObservableProperty] private int editStep = 1;
    [ObservableProperty] private string status = "Ready";
    [ObservableProperty] private string songName = "untitled";

    public ObservableCollection<InstrumentSlot> Instruments { get; } = new();

    /// <summary>One channel strip per track, for the MIXER page.</summary>
    public ObservableCollection<TrackStripViewModel> Strips { get; } = new();

    /// <summary>The rack, for bringing an instrument into this song.</summary>

    [ObservableProperty] private RackMachine? pickedMachine;
    public ObservableCollection<string> OrderEntries { get; } = new();
    public ObservableCollection<SongFile> SavedSongs { get; } = new();

    [ObservableProperty] private SongFile? selectedSongFile;

    public TrackerViewModel(
        IAudioEngine audio,
        MachineRack rack,
        ObservableCollection<Recording> recordings,
        ConfigStore? configStore = null,
        AppConfig? config = null,
        PluginLibraryViewModel? plugins = null,
        IWaveformService? waveforms = null)
    {
        _waveforms = waveforms;

        _configStore = configStore;
        _config = config;
        Plugins = plugins ?? new PluginLibraryViewModel();
        TrackEffect = new PluginChainViewModel(Plugins);
        TrackEffect.Changed += MarkDirty;


        // Assigned to the field rather than the property: this is what was saved, not a
        // change to save again.
        ignoreVelocity = config?.IgnoreKeyVelocity ?? false;
        recordNoteOffs = config?.RecordNoteOffs ?? false;

        _player = new TrackerPlayer(audio);

        // Before anything sounds: the rate cannot move once the engine is built.
        _player.UseSampleRate(config?.EngineSampleRate ?? Audio.SynthOutput.FollowDevice);
        _store = new SongStore();
        _rack = rack;
        _recordings = recordings;

        song = Song.CreateDefault();
        currentPattern = song.Patterns[0];

        // The meters are polled rather than pushed: the audio side should not be calling into
        // the UI dozens of times a second, and a meter that misses a frame costs nothing.
        _meters = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _meters.Tick += (_, _) => ReadMeters();

        _player.PositionChanged += OnPositionChanged;
        _player.StateChanged += OnPlayerStateChanged;
        _player.Stopped += OnPlayerStopped;

        RefreshOrder();
        RefreshSavedSongs();
        RefreshRack();
        RefreshStrips();

        FollowCursorTrack();
    }

    public double Bpm
    {
        get => Song.Bpm;
        set
        {
            if (Math.Abs(Song.Bpm - value) < 0.001) return;
            Song.Bpm = Math.Clamp(value, TrackerTiming.MinBpm, TrackerTiming.MaxBpm);
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public int LinesPerBeat
    {
        get => Song.LinesPerBeat;
        set
        {
            if (Song.LinesPerBeat == value) return;
            Song.LinesPerBeat = Math.Clamp(value, TrackerTiming.MinLinesPerBeat, TrackerTiming.MaxLinesPerBeat);
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public int TrackCount
    {
        get => Song.TrackCount;
        set => SetTrackCount(value);
    }

    public int MinTrackCount => Song.MinTrackCount;
    public int MaxTrackCount => Song.MaxTrackCount;

    /// <summary>The track the cursor is in, for the header to pick out.</summary>
    public int SelectedTrack => Cursor.Track;

    public bool IsPlaying => Transport == TrackerTransportState.Playing;
    public bool IsPaused => Transport == TrackerTransportState.Paused;
    public bool IsStopped => Transport == TrackerTransportState.Stopped;

    /// <summary>Pause only means anything while something is running.</summary>
    public bool CanPause => Transport == TrackerTransportState.Playing;

    /// <summary>The save button carries the unsaved marker, so it is visible where it matters.</summary>
    public string SaveSongText => IsDirty ? "Save song *" : "Save song";

    /// <summary>The two things the play button can walk through.</summary>
    public TrackerPlayMode[] PlayModes { get; } = { TrackerPlayMode.Pattern, TrackerPlayMode.Song };

    public IRelayCommand PlayCommand => new RelayCommand(Play);
    public IRelayCommand PauseCommand => new RelayCommand(Pause);
    public IRelayCommand StopCommand => new RelayCommand(Stop);
    public IRelayCommand ToggleRecordCommand => new RelayCommand(() => IsRecording = !IsRecording);
    public IRelayCommand AddPatternCommand => new RelayCommand(AddPattern);
    public IRelayCommand RemoveOrderEntryCommand => new RelayCommand(RemoveOrderEntry);
    public IRelayCommand SaveCommand => new RelayCommand(Save);
    public IRelayCommand LoadCommand => new RelayCommand(Load);
    public IRelayCommand NewSongCommand => new RelayCommand(NewSong);
    public IRelayCommand RefreshSongsCommand => new RelayCommand(RefreshSavedSongs);

    /// <summary>Throws away a saved song. The one you are working on stays where it is.</summary>
    public IAsyncRelayCommand DeleteSongCommand => new AsyncRelayCommand(DeleteSong);
    public IAsyncRelayCommand RemoveInstrumentCommand => new AsyncRelayCommand(RemoveSelectedInstrument);
    public IRelayCommand AddInstrumentCommand => new RelayCommand(AddInstrument);
    public IRelayCommand RefreshLibraryCommand => new RelayCommand(RefreshRack);

    public bool HasInstruments => Instruments.Count > 0;

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

    private void Pause()
    {
        _player.Pause();
        Status = "Paused";
    }

    private void Stop()
    {
        _player.Stop();
        PlayingLine = -1;
        Status = "Stopped";
    }

    private void OnPositionChanged(object? sender, TrackerPosition position) =>
        Dispatcher.UIThread.Post(() =>
        {
            PlayingLine = position.Line;
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
    private void OnPlayerStateChanged(object? sender, TrackerTransportState state) =>
        Dispatcher.UIThread.Post(() =>
        {
            Transport = state;
            if (state == TrackerTransportState.Stopped) PlayingLine = -1;

            if (state == TrackerTransportState.Playing) _meters.Start();
            else StopMeters();
        });

    /// <summary>Reads what each track is sounding and hands it to its strip and instruments.</summary>
    private void ReadMeters()
    {
        foreach (var strip in Strips)
        {
            var (left, right) = _player.LevelFor(strip.Track);

            strip.Left = left;
            strip.Right = right;
        }

        foreach (var instrument in Instruments)
        {
            if (instrument.Track < 0) continue;

            var (left, right) = _player.LevelFor(instrument.Track);
            instrument.Level = Math.Max(left, right);
        }
    }

    /// <summary>Stops polling and empties the meters, so none is left holding a level.</summary>
    private void StopMeters()
    {
        _meters.Stop();

        foreach (var strip in Strips)
        {
            strip.Left = 0;
            strip.Right = 0;
        }

        foreach (var instrument in Instruments)
        {
            instrument.Level = 0;
        }
    }

    private void OnPlayerStopped(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            var failed = _player.FailedInstruments;
            if (failed.Count > 0)
                Status = $"Stopped. {failed.Count} instrument file(s) could not be loaded.";
        });

    partial void OnOrderIndexChanged(int value) => CurrentPattern = Song.PatternAt(value);

    /// <summary>
    /// Patterns are edited in place, so the one on screen is watched rather than every edit
    /// method remembering to report itself.
    /// </summary>
    partial void OnCurrentPatternChanged(Pattern? oldValue, Pattern? newValue)
    {
        // A block belongs to the pattern it was drawn on.
        Selection = PatternSelection.None;

        if (oldValue != null) oldValue.Changed -= OnPatternEdited;
        if (newValue != null) newValue.Changed += OnPatternEdited;
    }

    private void OnPatternEdited(object? sender, EventArgs e) => MarkDirty();

    partial void OnSongNameChanged(string value) => MarkDirty();

    public bool HasSelection => !Selection.IsEmpty;

    /// <summary>What the menu is about to act on: a block, or the track the cursor is on.</summary>
    public string SelectionLabel => HasSelection ? Selection.Describe() : CursorTrackLabel;

    /// <summary>Starts a block at the cursor, for a shift-click or a drag.</summary>
    public void BeginSelection(PatternCursor at) => Selection = PatternSelection.At(at);

    /// <summary>Drags the loose corner of the block to here.</summary>
    public void ExtendSelection(PatternCursor to) => Selection = Selection.ExtendTo(to);

    public void ClearSelection() => Selection = PatternSelection.None;

    public IRelayCommand SelectAllCommand => new RelayCommand(SelectAll);

    public IRelayCommand CopySelectionCommand => new RelayCommand(CopySelection);

    public IRelayCommand CutSelectionCommand => new RelayCommand(CutSelection);

    public IRelayCommand PasteCommand => new RelayCommand(Paste);

    /// <summary>
    /// What was last copied. Held here rather than in a pattern, so a phrase can be carried
    /// between patterns and between songs for as long as the app is open.
    /// </summary>
    private PatternBlock? _clipboard;

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

    public void CutSelection()
    {
        var taken = HasSelection ? Selection : PatternSelection.At(Cursor);

        CopySelection();

        if (CurrentPattern == null || _clipboard == null) return;

        PatternEdit.ClearRegion(CurrentPattern, taken);
        Status = "Cut " + _clipboard.Describe();
    }

    /// <summary>
    /// Puts the copy down with its corner at the cursor, and leaves it selected: paste, move,
    /// paste again is how a pattern gets built.
    /// </summary>
    public void Paste()
    {
        if (CurrentPattern == null || _clipboard == null) return;

        var landed = _clipboard.Paste(CurrentPattern, Cursor);
        if (landed.IsEmpty)
        {
            Status = "Nowhere to paste from here";
            return;
        }

        Selection = landed;
        Status = "Pasted " + landed.Describe();
    }

    public IRelayCommand ClearSelectionCommand => new RelayCommand(DeleteSelection);

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

        int cleared = PatternEdit.ClearRegion(CurrentPattern, Selection);

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
    /// </remarks>
    public string Context
    {
        get
        {
            string song = SongName.Length > 0 ? SongName : "untitled";

            if (ShowsMixer) return song + "  ·  mixer  ·  " + TrackCount + " tracks";

            if (ShowsMachines)
                return song + "  ·  machines  ·  " + Song.Instruments.Count +
                       (Song.Instruments.Count == 1 ? " instrument in this song" : " instruments in this song");

            string line = "line " + Cursor.Line.ToString("00", CultureInfo.InvariantCulture);
            string track = CursorTrackLabel;

            var playing = Song.Instruments.ElementAtOrDefault(SelectedInstrument);
            string sound = playing == null ? "no instrument" : playing.Name;

            return song + "  ·  " + line + "  ·  " + track + "  ·  " + sound;
        }
    }

    public IRelayCommand<string> QuantizeTrackCommand => new RelayCommand<string>(QuantizeTrack);

    public IRelayCommand ClearTrackCommand => new RelayCommand(ClearTrack);

    public IRelayCommand ClearPatternCommand => new RelayCommand(ClearPattern);

    public IRelayCommand<string> TransposeTrackCommand => new RelayCommand<string>(TransposeTrack);

    public IRelayCommand<string> SetTrackVolumeCommand => new RelayCommand<string>(SetTrackVolume);

    public IRelayCommand ClearTrackInstrumentCommand =>
        new RelayCommand(() => ClearTrackInstrument(Cursor.Track));

    /// <summary>
    /// Pulls the track's notes onto every nth line. The grid comes from the menu as text,
    /// since that is what a menu item can carry.
    /// </summary>
    private void QuantizeTrack(string? grid)
    {
        if (CurrentPattern == null) return;
        if (!int.TryParse(grid, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lines)) return;

        int moved = 0;

        if (HasSelection)
        {
            // Whole tracks, even from a part-height block: a note is early or late against
            // the beat, which is a property of the track's timeline, not of the lines picked.
            for (int track = Selection.FirstTrack; track <= Selection.LastTrack; track++)
                moved += PatternEdit.Quantize(CurrentPattern, track, lines);
        }
        else
        {
            moved = PatternEdit.Quantize(CurrentPattern, Cursor.Track, lines);
        }

        Status = moved == 0
            ? $"{SelectionLabel} was already on {lines}"
            : $"Quantized {SelectionLabel} to {lines}: {moved} note(s) moved";
    }

    private void ClearTrack()
    {
        if (CurrentPattern == null) return;

        PatternEdit.ClearTrack(CurrentPattern, Cursor.Track);
        Status = $"Cleared {CursorTrackLabel}";
    }

    private void ClearPattern()
    {
        if (CurrentPattern == null) return;

        PatternEdit.ClearPattern(CurrentPattern);
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
            ? PatternEdit.SetRegionVolume(CurrentPattern, Selection, level)
            : PatternEdit.SetTrackVolume(CurrentPattern, Cursor.Track, level);

        string what = level == TrackerCell.NoVolume
            ? "the instrument's own level"
            : level.ToString("X2", CultureInfo.InvariantCulture);

        Status = changed == 0
            ? $"{SelectionLabel} was already at {what}"
            : $"{SelectionLabel} set to {what}: {changed} note(s) changed";
    }

    private void TransposeTrack(string? semitones)
    {
        if (CurrentPattern == null) return;
        if (!int.TryParse(semitones, NumberStyles.Integer, CultureInfo.InvariantCulture, out int steps)) return;

        if (HasSelection) PatternEdit.TransposeRegion(CurrentPattern, Selection, steps);
        else PatternEdit.TransposeTrack(CurrentPattern, Cursor.Track, steps);

        Status = $"Transposed {SelectionLabel} by {steps:+0;-0} semitone(s)";
    }

    /// <summary>
    /// Whether a key coming up on a MIDI keyboard writes a note-off, as Renoise's own
    /// RecordNoteOffs does. Off by default, and remembered between runs.
    /// </summary>
    [ObservableProperty] private bool recordNoteOffs;

    partial void OnRecordNoteOffsChanged(bool value)
    {
        Status = value
            ? "Note-offs recorded: letting a key up writes OFF where the cursor is"
            : "Note-offs not recorded: use the note-off key to write one";

        if (_configStore == null || _config == null) return;

        _config.RecordNoteOffs = value;
        _configStore.Save(_config);
    }

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

    /// <summary>Forgets a cached recording, for a file that has been edited under us.</summary>
    /// <summary>What the tracker's own stream is putting out, 0 to 1.</summary>
    public double OutputLevel => _player.OutputLevel;

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

    private const string MachinesPage = "Machines";

    private const string MixerPage = "Mixer";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPattern))]
    [NotifyPropertyChangedFor(nameof(ShowsMachines))]
    [NotifyPropertyChangedFor(nameof(ShowsMixer))]
    private string page = PatternPage;

    public bool ShowsPattern => Page == PatternPage;

    public bool ShowsMachines => Page == MachinesPage;

    public bool ShowsMixer => Page == MixerPage;

    /// <summary>
    /// Shows a page, or goes back to the pattern when the page asked for is already up.
    /// </summary>
    /// <remarks>
    /// Two buttons rather than three, because the pattern is where you are: the other two are
    /// somewhere you go and come back from, and pressing the lit button again is the way back.
    /// </remarks>
    public IRelayCommand<string> ShowCommand => new RelayCommand<string>(which =>
        Page = which == Page || which is not (MachinesPage or MixerPage) ? PatternPage : which);

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
            if (SampleUsage.Repoint(instrument, from, to)) moved = true;

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
    /// </remarks>
    public void ReopenAudio()
    {
        try
        {
            _player.EnsureEngine();
        }
        catch (Exception)
        {
            // No audio device is a quiet app, not a broken one.
        }
    }

    /// <summary>Something about the song changed and the file on disk no longer matches.</summary>
    private void MarkDirty()
    {
        // Once, when it changes: one turn of a plugin's knob is eighty of these.
        if (!IsDirty) Log.Write(LogArea.Tracker, "the song has something unsaved in it now");

        IsDirty = true;
    }

    partial void OnIsRecordingChanged(bool value) =>
        Status = value ? "Record armed: typing writes into the pattern" : "Record off: typing only auditions";

    /// <summary>Auditions the note under the cursor's instrument, for note entry feedback.</summary>
    public void PreviewNote(Note note) => PreviewNote(note, TrackerCell.NoVolume);

    /// <summary>Auditions at a given volume, which is what makes a keyboard's velocity audible.</summary>
    public void PreviewNote(Note note, int volume)
    {
        var instrument = Song.InstrumentAt(InstrumentForTrack(Cursor.Track));
        if (instrument == null) return;

        // Played on the track the cursor is on, so a plugin instrument sounds through the copy
        // that track plays rather than through an audition copy of its own.
        double held = _player.Preview(instrument, note, GainFor(volume), Cursor.Track);

        // And said out loud, so a panel's keyboard lights for a note played by hand the same as
        // for one the pattern played. Only the pattern used to say anything, which is why a MIDI
        // key sounded and nothing on screen moved.
        Played(Cursor.Track, note, held);
    }

    public void EnterNote(Note note) => EnterNote(note, TrackerCell.NoVolume);

    public void EnterNote(Note note, int volume)
    {
        // A velocity sensitive keyboard makes every hit a little different. With this on, how
        // hard a key is pressed is dropped on the way in, so a part comes out even and the
        // instrument's own level is the only thing deciding how loud it is.
        if (IgnoreVelocity) volume = TrackerCell.NoVolume;

        PreviewNote(note, volume);

        if (CurrentPattern == null || !IsRecording) return;

        // While playing, notes land on the line you can hear, not the line you left the cursor on.
        var target = IsPlaying && PlayingLine >= 0 ? Cursor with { Line = PlayingLine } : Cursor;

        PatternEdit.EnterNote(CurrentPattern, target, note, InstrumentForTrack(target.Track), volume);
        if (!IsPlaying) StepDown();
    }

    /// <summary>
    /// A note from a MIDI keyboard. It arrives on the MIDI thread, and everything it touches
    /// from there (the cursor, the pattern, the grid's redraw) belongs to the UI thread.
    /// </summary>
    public void PlayMidiNote(Note note, int volume) =>
        Dispatcher.UIThread.Post(() => EnterNote(note, volume));

    /// <summary>
    /// A key coming up on the keyboard, which writes a note-off when that has been asked for.
    /// </summary>
    /// <remarks>
    /// The note is not looked at. A note-off ends whatever that track is sounding rather than
    /// one particular note, so which key was let go of does not change what gets written; it
    /// is here because the caller has it and a later reading of this may want it.
    /// </remarks>
    public void ReleaseMidiNote(Note note)
    {
        if (!RecordNoteOffs) return;

        Dispatcher.UIThread.Post(EnterNoteOff);
    }

    /// <summary>
    /// Sounds one note on any instrument, for the rack's auditioning. The engine lives
    /// here, so the rack borrows it rather than opening a second one.
    /// </summary>
    public double Audition(TrackerInstrument instrument, Note note, int volume) =>
        _player.Preview(instrument, note, GainFor(volume));

    /// <summary>
    /// The running plugin behind a plugin instrument, for the editor to show and to read a
    /// patch out of. Loading it here also means the first note played is not the one that
    /// waits for the plugin to open.
    /// </summary>
    public Audio.Plugins.IPluginInstrument? PluginFor(TrackerInstrument instrument)
    {
        if (instrument == null || !instrument.IsPlugin) return null;

        _player.EnsureEngine();
        return _player.PreviewPlayerFor(instrument);
    }

    private static float GainFor(int volume) =>
        volume == TrackerCell.NoVolume
            ? 1f
            : Math.Clamp(volume, 0, TrackerCell.MaxVolume) / (float)TrackerCell.MaxVolume;

    public void EnterNoteOff()
    {
        if (CurrentPattern == null || !IsRecording) return;

        PatternEdit.EnterNoteOff(CurrentPattern, Cursor);
        StepDown();
    }

    public void EnterHexDigit(char digit)
    {
        if (CurrentPattern == null || !IsRecording) return;
        if (PatternEdit.EnterHexDigit(CurrentPattern, Cursor, digit)) StepDown();
    }

    public void EnterEffectCommand(char command)
    {
        if (CurrentPattern == null || !IsRecording) return;
        PatternEdit.EnterEffectCommand(CurrentPattern, Cursor, command);
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

        PatternEdit.ClearAtCursor(CurrentPattern, Cursor);
        if (IsRecording) StepDown();
    }

    public void InsertLine()
    {
        if (CurrentPattern != null) PatternEdit.InsertLine(CurrentPattern, Cursor);
    }

    public void DeleteLine()
    {
        if (CurrentPattern != null) PatternEdit.DeleteLine(CurrentPattern, Cursor);
    }

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
        if (columnDelta != 0) moved = moved.MoveColumn(columnDelta, CurrentPattern.TrackCount);

        if (extend) Selection = Selection.IsEmpty ? PatternSelection.At(Cursor).ExtendTo(moved) : Selection.ExtendTo(moved);
        else Selection = PatternSelection.None;

        Cursor = moved;
    }

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

        Song.SetTrackInstrument(track, instrument);
        SyncInstruments();
        RefreshStrips();
        MarkDirty();

        // An instrument lives on one track, so say what moved and what was pushed off.
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

        int changed = Song.PointNotesAtTrackInstrument(track, instrument);

        RefreshStrips();
        MarkDirty();

        Status = $"Pointed {changed} note(s) on track {track + 1:00} at '{chosen.Name}'";
    }

    /// <summary>
    /// Moves a whole track to another position: its notes, its instrument, its effects and its
    /// mixer strip, in the song and in what is playing.
    /// </summary>
    public void MoveTrack(int from, int to)
    {
        if (from == to) return;
        if (from < 0 || from >= Song.TrackCount || to < 0 || to >= Song.TrackCount) return;

        var moved = Song.InstrumentAt(Song.GetTrackInstrument(from));

        if (!Song.MoveTrack(from, to)) return;

        // The song and what is playing have to move together, or the notes arrive at the new
        // track and the sound answers on the old one.
        _player.MoveTrack(from, to);

        SyncInstruments();
        RefreshStrips();
        MarkDirty();

        // The cursor follows the track it was on, so a drag does not also move the caret.
        Cursor = Cursor with { Track = Song.WhereTrackWent(Cursor.Track, from, to) };

        Status = moved == null
            ? $"Moved track {from + 1:00} to {to + 1:00}"
            : $"Moved track {from + 1:00} to {to + 1:00}, '{moved.Name}' with it";
    }

    /// <summary>Clears a track's default so it falls back to the selected instrument.</summary>
    public void ClearTrackInstrument(int track)
    {
        Song.SetTrackInstrument(track, TrackerCell.NoInstrument);
        SyncInstruments();
        RefreshStrips();
        MarkDirty();
        Status = $"Track {track + 1:00} has no instrument";
    }

    /// <summary>
    /// The instrument a note typed on this track should carry: the track's own if it has one,
    /// otherwise whatever is selected in the instrument list.
    /// </summary>
    private int InstrumentForTrack(int track)
    {
        int assigned = Song.GetTrackInstrument(track);
        return assigned == TrackerCell.NoInstrument ? SelectedInstrument : assigned;
    }

    private void StepDown()
    {
        if (CurrentPattern == null || EditStep <= 0) return;
        Cursor = Cursor.MoveLine(EditStep, CurrentPattern.Lines);
    }

    private void AddPattern()
    {
        int index = Song.AddPattern();
        Song.Order.Add(index);
        RefreshOrder();
        MarkDirty();
        OrderIndex = Song.Order.Count - 1;
        Status = $"Added pattern {Song.Patterns[index].Name}";
    }

    private void RemoveOrderEntry()
    {
        if (Song.Order.Count <= 1) return;

        Song.Order.RemoveAt(Math.Clamp(OrderIndex, 0, Song.Order.Count - 1));
        RefreshOrder();
        MarkDirty();
        OrderIndex = Math.Clamp(OrderIndex, 0, Song.Order.Count - 1);
        CurrentPattern = Song.PatternAt(OrderIndex);
    }

    private void SetTrackCount(int trackCount)
    {
        int clamped = Math.Clamp(trackCount, Song.MinTrackCount, Song.MaxTrackCount);
        if (clamped == Song.TrackCount) return;

        Song.SetTrackCount(clamped);
        Cursor = Cursor.Clamp(CurrentPattern?.Lines ?? 0, clamped);
        SyncInstruments();
        RefreshStrips();
        MarkDirty();

        // The grid redraws off the pattern's own Changed event; only the label needs telling.
        OnPropertyChanged(nameof(TrackCount));
    }

    /// <summary>
    /// Rebuilds the mixer from the song. Called whenever the track count or the instrument on a
    /// track changes, since a strip is named after what plays through it.
    /// </summary>
    private void RefreshStrips()
    {
        Song.Normalize();

        Strips.Clear();
        for (int track = 0; track < Song.TrackCount && track < Song.Mix.Count; track++)
        {
            var instrument = Song.InstrumentAt(Song.GetTrackInstrument(track));
            Strips.Add(new TrackStripViewModel(
                track, Song.Mix[track], instrument?.Name ?? "", Song.TrackCount, OnMixChanged));
        }
    }

    /// <summary>
    /// A strip is named after whatever plays through it, so a rename in the rack shows up
    /// here. Updated in place rather than rebuilt: the mixer is full of controls you may be
    /// holding on to.
    /// </summary>
    private void RefreshStripNames()
    {
        foreach (var strip in Strips)
        {
            var instrument = Song.InstrumentAt(Song.GetTrackInstrument(strip.Track));
            strip.InstrumentName = instrument?.Name ?? "";
        }
    }

    /// <summary>A fader or a mute moved: hear it now, and remember the song has changed.</summary>
    private void OnMixChanged()
    {
        _player.ApplyMix();
        MarkDirty();
    }

    /// <summary>
    /// Rebuilds the order list, keeping the slot that was selected. Emptying the list makes
    /// the ListBox drop its selection, and that writes -1 straight back into OrderIndex, which
    /// takes the pattern off the screen with it. So the wanted slot is held here and put back
    /// once the list is whole again.
    /// </summary>
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

        // Set outright rather than left to the change hook: restoring the same number is not
        // a change, and the grid would stay empty.
        CurrentPattern = Song.PatternAt(OrderIndex);
    }

    /// <summary>
    /// The rack, refreshed for the picker that brings an instrument into this song.
    /// </summary>
    /// <summary>
    /// The rack has changed under the picker.
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
    /// a copy: a song opened without the rack still plays, and the copy is brought back up
    /// to date whenever the rack has the instrument.
    /// </summary>
    private void AddInstrument()
    {
        var chosen = PickedMachine?.Instrument;
        if (chosen == null)
        {
            Status = "Pick an instrument from the rack first.";
            return;
        }

        // A copy with an id of its own, because it is the song's from here on: name it what you
        // like, set it how you like, and take a second one off the same machine if you want one.
        // Sharing the rack's id would have meant one Zampler to a song and a name you could not
        // change, since a machine on the rack keeps the machine's name.
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

    private async Task RemoveSelectedInstrument()
    {
        int index = SelectedInstrument;
        var instrument = Song.InstrumentAt(index);
        if (instrument == null) return;

        // Cells are renumbered around the gap, so this rewrites the pattern as well.
        bool confirmed = await ConfirmDialog.AskAsync(
            "Remove from song",
            $"Take '{instrument.Name}' out of this song? Cells that used it lose their instrument, "
                + "and the rest are renumbered. The instrument stays in the rack.",
            "Remove");

        if (!confirmed) return;

        // Cells point at instruments by number, so the song renumbers them as it removes one.
        if (!Song.RemoveInstrumentAt(index)) return;

        SyncInstruments();
        MarkDirty();
        SelectedInstrument = Math.Clamp(index, 0, Math.Max(0, Song.Instruments.Count - 1));
        Status = $"Removed '{instrument.Name}' from the song. It is still in the rack.";
    }

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

            // What is actually loaded on the tracks is what gets saved, rather than whatever
            // the song was opened with.
            _player.CaptureChains(Song);

            // And the same for the sound every plugin instrument is making: whatever was
            // turned in its own window is read back onto the instrument before the song is
            // written. Every track, not the one the cursor is on: the effect slot follows the
            // cursor, so asking it alone saved one track's plugin and quietly dropped what
            // every other track's plugin was set to.
            foreach (var box in _instrumentBoxes.Values) box.SyncPatch();

            string path = _store.PathFor(name);
            _store.Save(Song, path);

            RefreshSavedSongs();
            SelectedSongFile = SavedSongs.FirstOrDefault(f => f.Path == path);

            IsDirty = false;
            Status = $"Saved '{name}'";
        }
        catch (Exception ex)
        {
            Status = $"Save failed: {ex.Message}";
        }
    }

    private void Load()
    {
        var file = SelectedSongFile;
        if (file == null)
        {
            Status = "Pick a song to open.";
            return;
        }

        var loaded = _store.Load(file.Path);
        if (loaded == null)
        {
            Status = $"'{file.Name}' could not be read.";
            return;
        }

        Adopt(loaded, file.Name);
        Status = $"Opened '{file.Name}'";
    }

    private void NewSong()
    {
        Adopt(Song.CreateDefault(), "untitled");
        SelectedSongFile = null;
        Status = "New song";
    }

    /// <summary>
    /// Swaps in a different song and brings every view-facing collection with it. Loading
    /// touches the pattern, the order, the instruments, and the cursor, so it all happens here
    /// rather than being spread across the callers.
    /// </summary>
    private void Adopt(Song replacement, string name)
    {
        _player.Stop();

        replacement.Normalize();

        // The song's instruments are the song's own. They are not fetched from the rack on
        // the way in: a song opens sounding exactly the way it was saved, and an instrument
        // built here belongs to the work it was built for. The rack is where a sound
        // starts, not something that reaches back into a song already written.
        Song = replacement;
        SongName = name;
        Song.Name = name;

        // The octave came with the song, so the pattern editor and every panel open on it.
        Octave = Math.Clamp(Song.KeyboardOctave, 0, 9);

        SyncInstruments();
        RefreshStrips();

        // The plugins the last song had on its tracks belong to that song, not this one.
        // Left in place they would keep playing under the new song's notes.
        CloseInstrumentBoxes();
        _player.ClearPlayers();

        // The effects come back with the song. A plugin that is not on this machine is
        // reported rather than passed over in silence.
        var missing = _player.RestoreChains(Song);
        if (missing.Count > 0)
            Status = "Missing plugin(s): " + string.Join(", ", missing);

        // The panel is about a track whose chain has just been rebuilt.
        _effectTrack = -1;
        FollowCursorTrack();

        // The order list is rebuilt before the slot is chosen, so a fresh song opens on its
        // first pattern rather than on nothing.
        RefreshOrder();

        OrderIndex = 0;
        CurrentPattern = Song.PatternAt(0);
        Cursor = PatternCursor.Start.Clamp(CurrentPattern?.Lines ?? 0, Song.TrackCount);
        PlayingLine = -1;

        // Freshly opened or freshly created: it matches what is on disk, or has nothing to lose.
        IsDirty = false;

        // The tempo and track count live on the song, so the whole transport bar is stale.
        OnPropertyChanged(nameof(Bpm));
        OnPropertyChanged(nameof(LinesPerBeat));
        OnPropertyChanged(nameof(TrackCount));
    }

    /// <summary>Rebuilds the list so every row carries its current number.</summary>
    /// <summary>
    /// Gives the song's instrument a name of your choosing.
    /// </summary>
    /// <remarks>
    /// A dialog rather than an editable row, because the row has to stay readable while you
    /// pick through the list. This is the song's own copy, so the name is the song's: the
    /// machine it came off the rack from keeps its own.
    /// </remarks>
    public IAsyncRelayCommand RenameInstrumentCommand => new AsyncRelayCommand(RenameInstrument);

    private async Task RenameInstrument()
    {
        var slot = Instruments.ElementAtOrDefault(SelectedInstrument);

        if (slot == null) return;

        string? wanted = await NameDialog.AskAsync(
            "Rename instrument",
            "What this instrument is called in this song. The machine it came from keeps its own name.",
            slot.Name);

        if (wanted == null || wanted == slot.Name) return;

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

    private void SyncInstruments()
    {
        int selected = SelectedInstrument;

        Instruments.Clear();
        for (int i = 0; i < Song.Instruments.Count; i++)
            Instruments.Add(new InstrumentSlot(i, Song.Instruments[i], Song.GetInstrumentTrack(i)));

        // Rebuilding the list drops the selection; put it back where it was.
        SelectedInstrument = Math.Clamp(selected, 0, Math.Max(0, Instruments.Count - 1));

        OnPropertyChanged(nameof(HasInstruments));
    }

    /// <summary>
    /// Removes a saved song from disc.
    /// </summary>
    /// <remarks>
    /// What is open stays open, even when it is the one that was deleted: what you are working
    /// on is in memory and throwing away the file is not a reason to take it off you. It simply
    /// has nowhere to go back to, which is what an untitled song is, so it is marked unsaved and
    /// the picker forgets it.
    /// </remarks>
    private async Task DeleteSong()
    {
        var file = SelectedSongFile;

        if (file == null)
        {
            Status = "Pick a song to delete first.";
            return;
        }

        bool confirmed = await ConfirmDialog.AskAsync(
            "Delete song",
            "Delete '" + file.Name + "' from disc? The instruments it used are untouched. " +
                "This cannot be undone.",
            "Delete");

        if (!confirmed) return;

        try
        {
            bool wasOpen = string.Equals(SongName, file.Name, StringComparison.OrdinalIgnoreCase);

            _store.Delete(file.Path);

            SelectedSongFile = null;

            RefreshSavedSongs();

            if (wasOpen) MarkDirty();

            Status = wasOpen
                ? "Deleted '" + file.Name + "'. What is open is still here, but unsaved."
                : "Deleted '" + file.Name + "'";
        }
        catch (Exception ex)
        {
            Status = "Could not delete '" + file.Name + "': " + ex.Message;
        }
    }

    private void RefreshSavedSongs()
    {
        string? keep = SelectedSongFile?.Path;

        SavedSongs.Clear();
        foreach (var file in _store.ListSongs())
            SavedSongs.Add(file);

        SelectedSongFile = SavedSongs.FirstOrDefault(f => f.Path == keep);
    }

    public void Dispose()
    {
        _meters.Stop();
        _player.Dispose();
    }
}
