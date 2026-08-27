using System;
using System.Threading;
using JingleBox2.Tracker;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Undo in the tracker: one history, two kinds of step.
/// </summary>
/// <remarks>
/// The bug worth keeping a test for is the last one here. A song step used to replace the
/// pattern list, which left every cheap step pointing at an object the song no longer held, so
/// undoing a note after undoing an instrument silently did nothing at all. It only shows when
/// both kinds are walked in sequence.
/// </remarks>
public class TrackerHistoryTests : IDisposable
{
    private readonly TrackerHistory _history = new();
    private readonly Song _song = new();

    public TrackerHistoryTests()
    {
        PatternEdit.Watching = _history.Taking;

        _song.Normalize();
        _song.Instruments.Add(new TrackerInstrument { Name = "One" });
        _song.Instruments.Add(new TrackerInstrument { Name = "Two" });

        foreach (var one in _song.Instruments) one.EnsureId();
    }

    public void Dispose() => PatternEdit.Watching = null;

    private Pattern First => _song.PatternAt(0)!;

    private static PatternCursor At(int line, int track = 0) => new() { Line = line, Track = track };

    private static bool Pour(Song live, Song was)
    {
        live.TakeFrom(was);

        return true;
    }

    private void Changing(string what) => _history.Taking(_song, what, Pour);

    // ---- patterns ------------------------------------------------------------------------

    [Fact]
    public void A_fresh_history_has_nothing_to_walk()
    {
        Assert.False(_history.CanUndo);
        Assert.False(_history.CanRedo);
    }

    [Fact]
    public void Every_note_typed_is_a_step()
    {
        for (int line = 0; line < 3; line++)
            PatternEdit.EnterNote(First, At(line), new Note(60 + line), 0);

        int steps = 0;
        while (_history.CanUndo) { _history.Undo(); steps++; }

        Assert.Equal(3, steps);
        Assert.False(First[0, 0].Note.IsPlayable);
    }

    [Fact]
    public void Undo_takes_the_last_one_and_leaves_the_rest()
    {
        PatternEdit.EnterNote(First, At(0), new Note(60), 0);
        PatternEdit.EnterNote(First, At(1), new Note(62), 0);

        _history.Undo();

        Assert.True(First[0, 0].Note.IsPlayable);
        Assert.False(First[1, 0].Note.IsPlayable);
    }

    [Fact]
    public void Redo_puts_it_back()
    {
        PatternEdit.EnterNote(First, At(0), new Note(60), 0);

        _history.Undo();
        Assert.False(First[0, 0].Note.IsPlayable);

        _history.Redo();
        Assert.True(First[0, 0].Note.IsPlayable);
    }

    [Fact]
    public void Doing_something_new_makes_what_was_undone_unreachable()
    {
        PatternEdit.EnterNote(First, At(0), new Note(60), 0);
        _history.Undo();

        Assert.True(_history.CanRedo);

        PatternEdit.EnterNote(First, At(1), new Note(62), 0);

        Assert.False(_history.CanRedo);
    }

    [Fact]
    public void An_edit_that_changed_nothing_leaves_no_step()
    {
        PatternEdit.EnterNote(First, At(0), new Note(60), 0);

        // Clearing cells that are already empty.
        PatternEdit.ClearAtCursor(First, At(8));
        PatternEdit.ClearAtCursor(First, At(9));

        PatternEdit.EnterNote(First, At(1), new Note(62), 0);

        int steps = 0;
        while (_history.CanUndo) { _history.Undo(); steps++; }

        Assert.Equal(2, steps);
    }

    [Fact]
    public void A_step_knows_which_pattern_it_is_about()
    {
        PatternEdit.EnterNote(First, At(0), new Note(60), 0);

        Assert.Same(First, _history.UndoIsAbout);
    }

    // ---- the song ------------------------------------------------------------------------

    [Fact]
    public void Taking_an_instrument_out_renumbers_the_patterns_and_undo_puts_both_back()
    {
        PatternEdit.EnterNote(First, At(0), new Note(60), instrument: 1);
        Assert.Equal(1, First[0, 0].Instrument);

        Changing("taking an instrument out");
        _song.RemoveInstrumentAt(0);

        Assert.Single(_song.Instruments);
        Assert.Equal(0, _song.PatternAt(0)![0, 0].Instrument);

        _history.Undo();

        Assert.Equal(2, _song.Instruments.Count);
        Assert.Equal("Two", _song.Instruments[1].Name);
        Assert.Equal(1, _song.PatternAt(0)![0, 0].Instrument);
    }

    [Fact]
    public void And_the_note_underneath_it_can_still_be_undone_afterwards()
    {
        // The one that used to fail silently: a song step replaced the pattern objects, so the
        // pattern step under it was pointing at an orphan and restoring it changed nothing.
        PatternEdit.EnterNote(First, At(0), new Note(60), instrument: 1);

        Changing("taking an instrument out");
        _song.RemoveInstrumentAt(0);

        _history.Undo();
        _history.Undo();

        Assert.False(_song.PatternAt(0)![0, 0].Note.IsPlayable);
        Assert.False(_history.CanUndo);
    }

    [Fact]
    public void The_tempo_and_the_track_count_come_back()
    {
        double bpm = _song.Bpm;
        int tracks = _song.TrackCount;

        Changing("the tempo");
        _song.Bpm = 174;

        Changing("how many tracks");
        _song.SetTrackCount(8);

        _history.Undo();
        Assert.Equal(tracks, _song.TrackCount);

        _history.Undo();
        Assert.Equal(bpm, _song.Bpm);
    }

    [Fact]
    public void A_drag_of_one_kind_is_gathered_into_one_step()
    {
        while (_song.Mix.Count < _song.TrackCount) _song.Mix.Add(new TrackMix());

        double level = _song.Mix[0].Volume;

        for (int at = 1; at <= 100; at++)
        {
            Changing("the mix");
            _song.Mix[0].Volume = at / 200.0;
        }

        Assert.Equal(0.5, _song.Mix[0].Volume, 3);

        _history.Undo();

        Assert.Equal(level, _song.Mix[0].Volume, 3);
        Assert.False(_history.CanUndo);
    }

    [Fact]
    public void A_pause_between_drags_makes_them_two()
    {
        while (_song.Mix.Count < _song.TrackCount) _song.Mix.Add(new TrackMix());

        Changing("the mix");
        _song.Mix[0].Volume = 0.4;

        Thread.Sleep(TrackerHistory.SameGesture + TimeSpan.FromMilliseconds(200));

        Changing("the mix");
        _song.Mix[0].Volume = 0.8;

        int steps = 0;
        while (_history.CanUndo) { _history.Undo(); steps++; }

        Assert.Equal(2, steps);
    }

    [Fact]
    public void A_different_kind_of_edit_is_never_the_same_gesture()
    {
        while (_song.Mix.Count < _song.TrackCount) _song.Mix.Add(new TrackMix());

        Changing("the mix"); _song.Mix[0].Pan = 0.2;
        Changing("the tempo"); _song.Bpm = 100;
        Changing("the mix"); _song.Mix[0].Pan = 0.5;

        int steps = 0;
        while (_history.CanUndo) { _history.Undo(); steps++; }

        Assert.Equal(3, steps);
    }

    [Fact]
    public void A_step_about_the_song_sends_the_view_nowhere()
    {
        Changing("the tempo");
        _song.Bpm = 174;

        Assert.Null(_history.UndoIsAbout);
    }

    [Fact]
    public void The_songs_own_controller_links_come_back()
    {
        Changing("a controller link");
        _song.Controls.Add(new Midi.ControlMapping { Device = "Minilab3 MIDI", Cc = 86, Key = "duty" });

        Assert.Single(_song.Controls);

        _history.Undo();

        Assert.Empty(_song.Controls);
    }

    [Fact]
    public void Forgetting_empties_it()
    {
        PatternEdit.EnterNote(First, At(0), new Note(60), 0);

        _history.Forget();

        Assert.False(_history.CanUndo);
        Assert.False(_history.CanRedo);
    }
}
