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
    [ObservableProperty] private PatternCursor cursor = PatternCursor.Start;
    [ObservableProperty] private int orderIndex;
    [ObservableProperty] private int playingLine = -1;
    [ObservableProperty] private bool isPlaying;
    [ObservableProperty] private int octave = 4;
    [ObservableProperty] private int selectedInstrument;
    [ObservableProperty] private int editStep = 1;
    [ObservableProperty] private string status = "Ready";
    [ObservableProperty] private string songName = "untitled";

    public ObservableCollection<TrackerInstrument> Instruments { get; } = new();
    public ObservableCollection<string> OrderEntries { get; } = new();

    public TrackerViewModel(IAudioEngine audio, ObservableCollection<Recording> recordings)
    {
        _player = new TrackerPlayer(audio);
        _store = new SongStore();
        _recordings = recordings;

        song = Song.CreateDefault();
        currentPattern = song.Patterns[0];

        _player.PositionChanged += OnPositionChanged;
        _player.Stopped += OnPlayerStopped;

        RefreshOrder();
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

    public IRelayCommand PlaySongCommand => new RelayCommand(() => Play(TrackerPlayMode.Song));
    public IRelayCommand PlayPatternCommand => new RelayCommand(() => Play(TrackerPlayMode.Pattern));
    public IRelayCommand StopCommand => new RelayCommand(Stop);
    public IRelayCommand AddPatternCommand => new RelayCommand(AddPattern);
    public IRelayCommand RemoveOrderEntryCommand => new RelayCommand(RemoveOrderEntry);
    public IRelayCommand AddTrackCommand => new RelayCommand(() => SetTrackCount(Song.TrackCount + 1));
    public IRelayCommand RemoveTrackCommand => new RelayCommand(() => SetTrackCount(Song.TrackCount - 1));
    public IRelayCommand SaveCommand => new RelayCommand(Save);
    public IRelayCommand<Recording> AddInstrumentCommand => new RelayCommand<Recording>(AddInstrument);
    public IRelayCommand<TrackerInstrument> RemoveInstrumentCommand =>
        new RelayCommand<TrackerInstrument>(RemoveInstrument);

    /// <summary>Recordings offered as instrument sources, shared with the RECORD tab.</summary>
    public ObservableCollection<Recording> AvailableRecordings => _recordings;

    private void Play(TrackerPlayMode mode)
    {
        try
        {
            Song.Normalize();
            _player.Play(Song, new TrackerPosition(OrderIndex, 0), mode);
            IsPlaying = true;
            Status = mode == TrackerPlayMode.Pattern ? "Playing pattern" : "Playing song";
        }
        catch (Exception ex)
        {
            Status = $"Play failed: {ex.Message}";
        }
    }

    private void Stop()
    {
        _player.Stop();
        IsPlaying = false;
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

    private void OnPlayerStopped(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            IsPlaying = false;
            PlayingLine = -1;

            var failed = _player.FailedInstruments;
            Status = failed.Count > 0
                ? $"Stopped. {failed.Count} instrument file(s) could not be loaded."
                : "Stopped";
        });

    partial void OnOrderIndexChanged(int value) => CurrentPattern = Song.PatternAt(value);

    /// <summary>Auditions the note under the cursor's instrument, for note entry feedback.</summary>
    public void PreviewNote(Note note)
    {
        var instrument = Song.InstrumentAt(SelectedInstrument);
        if (instrument != null) _player.Preview(instrument, note);
    }

    public void EnterNote(Note note)
    {
        if (CurrentPattern == null) return;

        PatternEdit.EnterNote(CurrentPattern, Cursor, note, SelectedInstrument);
        PreviewNote(note);
        StepDown();
    }

    public void EnterNoteOff()
    {
        if (CurrentPattern == null) return;

        PatternEdit.EnterNoteOff(CurrentPattern, Cursor);
        StepDown();
    }

    public void EnterHexDigit(char digit)
    {
        if (CurrentPattern == null) return;
        if (PatternEdit.EnterHexDigit(CurrentPattern, Cursor, digit)) StepDown();
    }

    public void EnterEffectCommand(char command)
    {
        if (CurrentPattern == null) return;
        PatternEdit.EnterEffectCommand(CurrentPattern, Cursor, command);
    }

    public void ClearAtCursor()
    {
        if (CurrentPattern == null) return;

        PatternEdit.ClearAtCursor(CurrentPattern, Cursor);
        StepDown();
    }

    public void InsertLine()
    {
        if (CurrentPattern != null) PatternEdit.InsertLine(CurrentPattern, Cursor);
    }

    public void DeleteLine()
    {
        if (CurrentPattern != null) PatternEdit.DeleteLine(CurrentPattern, Cursor);
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

        OnPropertyChanged(nameof(TrackCount));
        OnPropertyChanged(nameof(CurrentPattern));
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
        try
        {
            Song.Name = SongName;
            string path = _store.PathFor(SongName);
            _store.Save(Song, path);
            Status = $"Saved to {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            Status = $"Save failed: {ex.Message}";
        }
    }

    public void Dispose() => _player.Dispose();
}
