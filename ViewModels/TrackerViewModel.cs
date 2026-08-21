using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Models;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Synth;
using JingleBox2.Views;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JingleBox2.ViewModels;

/// <summary>
/// Holds the song being edited and drives the player. All sequencing, editing, and cursor
/// maths live in the Tracker namespace; this class is the bridge to the view.
/// </summary>
public sealed partial class TrackerViewModel : ObservableObject, IInstrumentAudition
{
    private readonly TrackerPlayer _player;
    private readonly SongStore _store;
    private readonly InstrumentLibrary _library;
    private readonly ObservableCollection<Recording> _recordings;

    [ObservableProperty] private Song song;
    [ObservableProperty] private Pattern? currentPattern;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTrack))]
    private PatternCursor cursor = PatternCursor.Start;
    [ObservableProperty] private int orderIndex;
    [ObservableProperty] private int playingLine = -1;

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
    [ObservableProperty] private int selectedInstrument;
    [ObservableProperty] private int editStep = 1;
    [ObservableProperty] private string status = "Ready";
    [ObservableProperty] private string songName = "untitled";

    public ObservableCollection<InstrumentSlot> Instruments { get; } = new();

    /// <summary>One channel strip per track, for the MIXER page.</summary>
    public ObservableCollection<TrackStripViewModel> Strips { get; } = new();

    /// <summary>Raised when this song puts an instrument into the library, so the library page follows.</summary>
    public event EventHandler? LibraryChanged;

    /// <summary>The library, for bringing an instrument into this song.</summary>
    public ObservableCollection<LibraryInstrument> LibraryInstruments { get; } = new();

    [ObservableProperty] private LibraryInstrument? selectedLibraryInstrument;
    public ObservableCollection<string> OrderEntries { get; } = new();
    public ObservableCollection<SongFile> SavedSongs { get; } = new();

    [ObservableProperty] private SongFile? selectedSongFile;

    public TrackerViewModel(IAudioEngine audio, InstrumentLibrary library, ObservableCollection<Recording> recordings)
    {
        _player = new TrackerPlayer(audio);
        _store = new SongStore();
        _library = library;
        _recordings = recordings;

        song = Song.CreateDefault();
        currentPattern = song.Patterns[0];

        _player.PositionChanged += OnPositionChanged;
        _player.StateChanged += OnPlayerStateChanged;
        _player.Stopped += OnPlayerStopped;

        RefreshOrder();
        RefreshSavedSongs();
        RefreshLibrary();
        RefreshStrips();
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
    public IAsyncRelayCommand RemoveInstrumentCommand => new AsyncRelayCommand(RemoveSelectedInstrument);
    public IRelayCommand AddFromLibraryCommand => new RelayCommand(AddFromLibrary);
    public IRelayCommand PromoteToLibraryCommand => new RelayCommand(PromoteToLibrary);
    public IRelayCommand RefreshLibraryCommand => new RelayCommand(RefreshLibrary);

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
        });

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
        if (oldValue != null) oldValue.Changed -= OnPatternEdited;
        if (newValue != null) newValue.Changed += OnPatternEdited;
    }

    private void OnPatternEdited(object? sender, EventArgs e) => MarkDirty();

    partial void OnSongNameChanged(string value) => MarkDirty();

    /// <summary>Something about the song changed and the file on disk no longer matches.</summary>
    private void MarkDirty() => IsDirty = true;

    partial void OnIsRecordingChanged(bool value) =>
        Status = value ? "Record armed: typing writes into the pattern" : "Record off: typing only auditions";

    /// <summary>Auditions the note under the cursor's instrument, for note entry feedback.</summary>
    public void PreviewNote(Note note) => PreviewNote(note, TrackerCell.NoVolume);

    /// <summary>Auditions at a given volume, which is what makes a keyboard's velocity audible.</summary>
    public void PreviewNote(Note note, int volume)
    {
        var instrument = Song.InstrumentAt(InstrumentForTrack(Cursor.Track));
        if (instrument == null) return;

        _player.Preview(instrument, note, GainFor(volume));
    }

    public void EnterNote(Note note) => EnterNote(note, TrackerCell.NoVolume);

    public void EnterNote(Note note, int volume)
    {
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
    /// Sounds one note on any instrument, for the library's auditioning. The engine lives
    /// here, so the library borrows it rather than opening a second one.
    /// </summary>
    public void Audition(TrackerInstrument instrument, Note note, int volume) =>
        _player.Preview(instrument, note, GainFor(volume));

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

    public void ClearAtCursor()
    {
        if (CurrentPattern == null || !IsRecording) return;

        PatternEdit.ClearAtCursor(CurrentPattern, Cursor);
        StepDown();
    }

    public void InsertLine()
    {
        if (CurrentPattern != null && IsRecording) PatternEdit.InsertLine(CurrentPattern, Cursor);
    }

    public void DeleteLine()
    {
        if (CurrentPattern != null && IsRecording) PatternEdit.DeleteLine(CurrentPattern, Cursor);
    }

    public void MoveCursor(int lineDelta, int trackDelta, int columnDelta)
    {
        if (CurrentPattern == null) return;

        var moved = Cursor;
        if (lineDelta != 0) moved = moved.MoveLine(lineDelta, CurrentPattern.Lines);
        if (trackDelta != 0) moved = moved.MoveTrack(trackDelta, CurrentPattern.TrackCount);
        if (columnDelta != 0) moved = moved.MoveColumn(columnDelta, CurrentPattern.TrackCount);

        Cursor = moved;
    }

    public void SetCursor(PatternCursor value) => Cursor = value;

    /// <summary>
    /// Points a track at an instrument. Existing notes keep the instrument they were written
    /// with; this only decides what new notes on that track get.
    /// </summary>
    public void AssignInstrumentToTrack(int track, int instrument)
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
            Strips.Add(new TrackStripViewModel(track, Song.Mix[track], instrument?.Name ?? "", OnMixChanged));
        }
    }

    /// <summary>
    /// A strip is named after whatever plays through it, so a rename in the library shows up
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

    private void RefreshOrder()
    {
        OrderEntries.Clear();
        for (int i = 0; i < Song.Order.Count; i++)
        {
            var pattern = Song.PatternAt(i);
            OrderEntries.Add($"{i:00}   {pattern?.Name ?? "--"}");
        }
    }

    /// <summary>
    /// The library, refreshed for the picker that brings an instrument into this song.
    /// </summary>
    public void RefreshLibrary()
    {
        string? keep = SelectedLibraryInstrument?.Id;

        LibraryInstruments.Clear();
        foreach (var instrument in _library.List())
            LibraryInstruments.Add(new LibraryInstrument(instrument));

        SelectedLibraryInstrument = LibraryInstruments.FirstOrDefault(i => i.Id == keep);
    }

    /// <summary>
    /// Gives the song a slot for a library instrument, so its cells can name it. The slot holds
    /// a copy: a song opened without the library still plays, and the copy is brought back up
    /// to date whenever the library has the instrument.
    /// </summary>
    private void AddFromLibrary()
    {
        var chosen = SelectedLibraryInstrument?.Instrument;
        if (chosen == null)
        {
            Status = "Pick an instrument from the library first.";
            return;
        }

        int existing = Song.Instruments.FindIndex(i => i.Id == chosen.Id && !string.IsNullOrEmpty(i.Id));
        if (existing >= 0)
        {
            SelectedInstrument = existing;
            Status = $"'{chosen.Name}' is already in this song as {existing:00}";
            return;
        }

        Song.Instruments.Add(chosen.Clone());
        SyncInstruments();
        MarkDirty();

        SelectedInstrument = Song.Instruments.Count - 1;
        Status = $"Added '{chosen.Name}' to the song as instrument {SelectedInstrument:00}";
    }

    /// <summary>
    /// Puts a song's own instrument into the library, which is how an instrument from before
    /// the library existed gets there, and how a one-off you built for this song becomes
    /// something the next song can use.
    /// </summary>
    private void PromoteToLibrary()
    {
        var instrument = Song.InstrumentAt(SelectedInstrument);
        if (instrument == null)
        {
            Status = "Pick an instrument in the song first.";
            return;
        }

        try
        {
            // The song's copy keeps the same id, so from here on the two are the same voice.
            instrument.EnsureId();
            _library.Save(instrument);

            RefreshLibrary();
            LibraryChanged?.Invoke(this, EventArgs.Empty);

            // The slot now carries an id, and that is part of the song file.
            MarkDirty();
            Status = $"'{instrument.Name}' is in the library now, and this song follows it.";
        }
        catch (Exception ex)
        {
            Status = $"Could not add it to the library: {ex.Message}";
        }
    }

    /// <summary>
    /// An instrument was edited in the library: bring this song's copy of it along, so what
    /// you hear here is what you just built there.
    /// </summary>
    public void ApplyLibraryEdit(TrackerInstrument edited)
    {
        if (edited == null || string.IsNullOrEmpty(edited.Id)) return;

        for (int i = 0; i < Song.Instruments.Count; i++)
        {
            if (Song.Instruments[i].Id != edited.Id) continue;

            Song.Instruments[i].CopyFrom(edited);
            if (i < Instruments.Count) Instruments[i].Refresh();
        }

        // The picker holds its own objects, listed when the library was last read, so they are
        // brought up to date rather than merely told to redraw the name they already had.
        foreach (var row in LibraryInstruments)
        {
            if (row.Id != edited.Id) continue;

            row.Instrument.CopyFrom(edited);
            row.Refresh();
        }

        RefreshStripNames();
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
                + "and the rest are renumbered. The instrument stays in the library.",
            "Remove");

        if (!confirmed) return;

        // Cells point at instruments by number, so the song renumbers them as it removes one.
        if (!Song.RemoveInstrumentAt(index)) return;

        SyncInstruments();
        MarkDirty();
        SelectedInstrument = Math.Clamp(index, 0, Math.Max(0, Song.Instruments.Count - 1));
        Status = $"Removed '{instrument.Name}' from the song. It is still in the library.";
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

        // The song stores a copy of every instrument it uses; the library is the master.
        _library.Rebind(replacement);

        Song = replacement;
        SongName = name;
        Song.Name = name;

        OrderIndex = 0;
        CurrentPattern = Song.PatternAt(0);
        Cursor = PatternCursor.Start.Clamp(CurrentPattern?.Lines ?? 0, Song.TrackCount);
        PlayingLine = -1;

        SyncInstruments();
        RefreshOrder();
        RefreshStrips();

        // Freshly opened or freshly created: it matches what is on disk, or has nothing to lose.
        IsDirty = false;

        // The tempo and track count live on the song, so the whole transport bar is stale.
        OnPropertyChanged(nameof(Bpm));
        OnPropertyChanged(nameof(LinesPerBeat));
        OnPropertyChanged(nameof(TrackCount));
    }

    /// <summary>Rebuilds the list so every row carries its current number.</summary>
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

    private void RefreshSavedSongs()
    {
        string? keep = SelectedSongFile?.Path;

        SavedSongs.Clear();
        foreach (var file in _store.ListSongs())
            SavedSongs.Add(file);

        SelectedSongFile = SavedSongs.FirstOrDefault(f => f.Path == keep);
    }

    public void Dispose() => _player.Dispose();
}
