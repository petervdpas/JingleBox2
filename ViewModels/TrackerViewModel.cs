using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Models;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Synth;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace JingleBox2.ViewModels;

/// <summary>
/// Holds the song being edited and drives the player. All sequencing, editing, and cursor
/// maths live in the Tracker namespace; this class is the bridge to the view.
/// </summary>
public sealed partial class TrackerViewModel : ObservableObject
{
    private readonly TrackerPlayer _player;
    private readonly SongStore _store;
    private readonly SynthPresetStore _presets = new();
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

    /// <summary>Pattern by default: most editing is done against a single looping pattern.</summary>
    [ObservableProperty] private TrackerPlayMode playMode = TrackerPlayMode.Pattern;
    [ObservableProperty] private int octave = 4;
    [ObservableProperty] private int selectedInstrument;
    [ObservableProperty] private int editStep = 1;
    [ObservableProperty] private string status = "Ready";
    [ObservableProperty] private string songName = "untitled";

    public ObservableCollection<InstrumentSlot> Instruments { get; } = new();

    /// <summary>The instrument open on the INSTRUMENTS page, or null when there is none.</summary>
    [ObservableProperty] private InstrumentEditorViewModel? editor;

    public ObservableCollection<SynthPreset> Presets { get; } = new();

    [ObservableProperty] private SynthPreset? selectedPreset;
    public ObservableCollection<string> OrderEntries { get; } = new();
    public ObservableCollection<SongFile> SavedSongs { get; } = new();

    [ObservableProperty] private SongFile? selectedSongFile;

    public TrackerViewModel(IAudioEngine audio, ObservableCollection<Recording> recordings)
    {
        _player = new TrackerPlayer(audio);
        _store = new SongStore();
        _recordings = recordings;

        song = Song.CreateDefault();
        currentPattern = song.Patterns[0];

        _player.PositionChanged += OnPositionChanged;
        _player.StateChanged += OnPlayerStateChanged;
        _player.Stopped += OnPlayerStopped;

        RefreshOrder();
        RefreshSavedSongs();
        RefreshPresets();
    }

    public double Bpm
    {
        get => Song.Bpm;
        set
        {
            if (Math.Abs(Song.Bpm - value) < 0.001) return;
            Song.Bpm = Math.Clamp(value, TrackerTiming.MinBpm, TrackerTiming.MaxBpm);
            OnPropertyChanged();
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
    public IRelayCommand<Recording> AddInstrumentCommand => new RelayCommand<Recording>(AddInstrument);
    public IRelayCommand RemoveInstrumentCommand => new RelayCommand(RemoveSelectedInstrument);
    public IRelayCommand AddSynthInstrumentCommand => new RelayCommand(AddSynthInstrument);
    public IRelayCommand TestInstrumentCommand => new RelayCommand(TestInstrument);
    public IRelayCommand LoadPresetCommand => new RelayCommand(LoadPreset);
    public IRelayCommand SavePresetCommand => new RelayCommand(SavePreset);
    public IRelayCommand DeletePresetCommand => new RelayCommand(DeletePreset);
    public IRelayCommand ResetPresetsCommand => new RelayCommand(ResetPresets);

    public bool HasInstruments => Instruments.Count > 0;

    /// <summary>Recordings offered as instrument sources, shared with the RECORD tab.</summary>
    public ObservableCollection<Recording> AvailableRecordings => _recordings;

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

    partial void OnSelectedInstrumentChanged(int value) => BuildEditor();

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
        OrderIndex = Song.Order.Count - 1;
        Status = $"Added pattern {Song.Patterns[index].Name}";
    }

    private void RemoveOrderEntry()
    {
        if (Song.Order.Count <= 1) return;

        Song.Order.RemoveAt(Math.Clamp(OrderIndex, 0, Song.Order.Count - 1));
        RefreshOrder();
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

        // The grid redraws off the pattern's own Changed event; only the label needs telling.
        OnPropertyChanged(nameof(TrackCount));
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
    /// Opens whichever instrument is selected. Rebuilt rather than repointed: a synth and a
    /// sample are different pages, and the patch view model is tied to one patch object.
    /// </summary>
    private void BuildEditor()
    {
        var instrument = Song.InstrumentAt(SelectedInstrument);

        Editor = instrument == null
            ? null
            : new InstrumentEditorViewModel(SelectedInstrument, instrument, OnInstrumentEdited);
    }

    /// <summary>
    /// A field in the editor changed. The row in the list is refreshed in place: rebuilding the
    /// collection here would replace the editor under the cursor on every keystroke.
    /// </summary>
    private void OnInstrumentEdited()
    {
        foreach (var slot in Instruments)
        {
            if (slot.Index == SelectedInstrument) slot.Refresh();
        }
    }

    private void AddSynthInstrument()
    {
        var instrument = TrackerInstrument.CreateSynth(NextSynthName());

        Song.Instruments.Add(instrument);
        SyncInstruments();

        SelectedInstrument = Song.Instruments.Count - 1;
        BuildEditor();

        Status = $"Added '{instrument.Name}' as instrument {SelectedInstrument:00}";
    }

    /// <summary>A name that is not in use yet, so two synths are never both called "synth 01".</summary>
    private string NextSynthName()
    {
        for (int number = 1; ; number++)
        {
            string name = $"synth {number:00}";
            if (!Song.Instruments.Any(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)))
                return name;
        }
    }

    /// <summary>Sounds the selected instrument on its own, whatever the cursor is sitting on.</summary>
    private void TestInstrument()
    {
        var instrument = Song.InstrumentAt(SelectedInstrument);
        if (instrument == null)
        {
            Status = "No instrument to test.";
            return;
        }

        _player.Preview(instrument, Note.FromOctave(0, Octave), 1f);
        Status = $"Testing '{instrument.Name}'";
    }

    private void RefreshPresets()
    {
        string? keep = SelectedPreset?.Name;

        Presets.Clear();
        foreach (var preset in _presets.List())
            Presets.Add(preset);

        SelectedPreset = Presets.FirstOrDefault(p => p.Name == keep);
    }

    private void LoadPreset()
    {
        var preset = SelectedPreset;
        var instrument = Song.InstrumentAt(SelectedInstrument);

        if (preset == null || instrument == null || !instrument.IsSynth)
        {
            Status = "Pick a synth instrument and a preset first.";
            return;
        }

        // Copied into the patch the instrument already owns, so nothing else has to be repointed.
        instrument.Patch = preset.Patch.Clone();
        BuildEditor();
        OnInstrumentEdited();

        Status = $"Loaded preset '{preset.Name}' into '{instrument.Name}'";
    }

    private void SavePreset()
    {
        var instrument = Song.InstrumentAt(SelectedInstrument);
        if (instrument == null || !instrument.IsSynth)
        {
            Status = "Only a synth instrument can be saved as a preset.";
            return;
        }

        try
        {
            string name = SynthPresetStore.SafeName(instrument.Name);
            _presets.Save(name, instrument.Patch);

            RefreshPresets();
            SelectedPreset = Presets.FirstOrDefault(p => p.Name == name);
            Status = $"Saved preset '{name}'";
        }
        catch (Exception ex)
        {
            Status = $"Preset save failed: {ex.Message}";
        }
    }

    private void DeletePreset()
    {
        var preset = SelectedPreset;
        if (preset == null) return;

        try
        {
            _presets.Delete(preset.Name);
            RefreshPresets();
            Status = $"Deleted preset '{preset.Name}'";
        }
        catch (Exception ex)
        {
            Status = $"Preset delete failed: {ex.Message}";
        }
    }

    private void ResetPresets()
    {
        try
        {
            _presets.ResetStarters();
            RefreshPresets();
            Status = "Starter presets restored";
        }
        catch (Exception ex)
        {
            Status = $"Preset reset failed: {ex.Message}";
        }
    }

    private void AddInstrument(Recording? recording)
    {
        if (recording == null) return;

        var instrument = new TrackerInstrument
        {
            Name = recording.Name,
            FilePath = recording.FilePath,
            BaseNote = Note.C4
        };

        Song.Instruments.Add(instrument);
        SyncInstruments();

        SelectedInstrument = Song.Instruments.Count - 1;
        Status = $"Added '{instrument.Name}' as instrument {SelectedInstrument:00}";
    }

    private void RemoveSelectedInstrument()
    {
        int index = SelectedInstrument;
        var instrument = Song.InstrumentAt(index);
        if (instrument == null) return;

        // Cells point at instruments by number, so the song renumbers them as it removes one.
        if (!Song.RemoveInstrumentAt(index)) return;

        SyncInstruments();
        SelectedInstrument = Math.Clamp(index, 0, Math.Max(0, Song.Instruments.Count - 1));
        Status = $"Removed '{instrument.Name}'";
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

        Song = replacement;
        SongName = name;
        Song.Name = name;

        OrderIndex = 0;
        CurrentPattern = Song.PatternAt(0);
        Cursor = PatternCursor.Start.Clamp(CurrentPattern?.Lines ?? 0, Song.TrackCount);
        PlayingLine = -1;

        SyncInstruments();
        RefreshOrder();

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

        // The index may not have moved even though the instrument behind it did.
        BuildEditor();

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
