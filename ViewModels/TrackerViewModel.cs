using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Models;
using JingleBox2.Tracker;
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

    [ObservableProperty] private TrackerPlayMode playMode = TrackerPlayMode.Song;
    [ObservableProperty] private int octave = 4;
    [ObservableProperty] private int selectedInstrument;
    [ObservableProperty] private int editStep = 1;
    [ObservableProperty] private string status = "Ready";
    [ObservableProperty] private string songName = "untitled";

    public ObservableCollection<TrackerInstrument> Instruments { get; } = new();
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

    public int TrackCount => Song.TrackCount;

    /// <summary>The track the cursor is in, for the header to pick out.</summary>
    public int SelectedTrack => Cursor.Track;

    public bool IsPlaying => Transport == TrackerTransportState.Playing;
    public bool IsPaused => Transport == TrackerTransportState.Paused;
    public bool IsStopped => Transport == TrackerTransportState.Stopped;

    /// <summary>Pause only means anything while something is running.</summary>
    public bool CanPause => Transport == TrackerTransportState.Playing;

    /// <summary>The two things the play button can walk through.</summary>
    public TrackerPlayMode[] PlayModes { get; } = { TrackerPlayMode.Song, TrackerPlayMode.Pattern };

    public IRelayCommand PlayCommand => new RelayCommand(Play);
    public IRelayCommand PauseCommand => new RelayCommand(Pause);
    public IRelayCommand StopCommand => new RelayCommand(Stop);
    public IRelayCommand ToggleRecordCommand => new RelayCommand(() => IsRecording = !IsRecording);
    public IRelayCommand AddPatternCommand => new RelayCommand(AddPattern);
    public IRelayCommand RemoveOrderEntryCommand => new RelayCommand(RemoveOrderEntry);
    public IRelayCommand AddTrackCommand => new RelayCommand(() => SetTrackCount(Song.TrackCount + 1));
    public IRelayCommand RemoveTrackCommand => new RelayCommand(() => SetTrackCount(Song.TrackCount - 1));
    public IRelayCommand SaveCommand => new RelayCommand(Save);
    public IRelayCommand LoadCommand => new RelayCommand(Load);
    public IRelayCommand NewSongCommand => new RelayCommand(NewSong);
    public IRelayCommand RefreshSongsCommand => new RelayCommand(RefreshSavedSongs);
    public IRelayCommand<Recording> AddInstrumentCommand => new RelayCommand<Recording>(AddInstrument);
    public IRelayCommand<TrackerInstrument> RemoveInstrumentCommand =>
        new RelayCommand<TrackerInstrument>(RemoveInstrument);

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

    partial void OnIsRecordingChanged(bool value) =>
        Status = value ? "Record armed: typing writes into the pattern" : "Record off: typing only auditions";

    /// <summary>Auditions the note under the cursor's instrument, for note entry feedback.</summary>
    public void PreviewNote(Note note)
    {
        var instrument = Song.InstrumentAt(SelectedInstrument);
        if (instrument != null) _player.Preview(instrument, note);
    }

    public void EnterNote(Note note)
    {
        PreviewNote(note);

        if (CurrentPattern == null || !IsRecording) return;

        // While playing, notes land on the line you can hear, not the line you left the cursor on.
        var target = IsPlaying && PlayingLine >= 0 ? Cursor with { Line = PlayingLine } : Cursor;

        PatternEdit.EnterNote(CurrentPattern, target, note, SelectedInstrument);
        if (!IsPlaying) StepDown();
    }

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
        Instruments.Add(instrument);
        SelectedInstrument = Song.Instruments.Count - 1;
        Status = $"Added instrument '{instrument.Name}'";
    }

    private void RemoveInstrument(TrackerInstrument? instrument)
    {
        if (instrument == null) return;

        Song.Instruments.Remove(instrument);
        Instruments.Remove(instrument);
        SelectedInstrument = Math.Clamp(SelectedInstrument, 0, Math.Max(0, Song.Instruments.Count - 1));
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

    private void SyncInstruments()
    {
        Instruments.Clear();
        foreach (var instrument in Song.Instruments)
            Instruments.Add(instrument);

        SelectedInstrument = Song.Instruments.Count > 0 ? 0 : 0;
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
