using System;
using System.Threading;
using JingleBox2.Tracker;
using Xunit;
using JingleBox2.Midi.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tests;

/// <summary>
/// Undo in the tracker: one history, two kinds of step.
/// </summary>
/// <remarks>
/// The bug worth keeping a test for is the last one here. A song step used to replace the
/// pattern list, which left every cheap step pointing at an object the song no longer held, so
/// undoing a note after undoing an instrument silently did nothing at all. It only shows when
/// both kinds are walked in sequence.
/// <para>
/// Two groups, in this order. First the patterns: the cheap step, which is a memory copy of one
/// pattern's cells and its lanes. Then the song: the expensive step, which is the song as its
/// own file would hold it, covering instruments, the tempo, the track count, the mix and the
/// song's own controller links.
/// </para>
/// </remarks>
public class TrackerHistoryTests : IDisposable
{
    /// <summary>Every edit to a pattern, so each one lands in the history.</summary>
    /// <remarks>One per test class, so nothing one test does reaches another.</remarks>
    private static readonly IPatternEdit Edits = new PatternEdit();

    /// <summary>The history under test, hooked to <see cref="PatternEdit"/> for the run.</summary>
    private readonly TrackerHistory _history = new();

    /// <summary>One song with two instruments on it, which every test here edits.</summary>
    private readonly Song _song = new();

    /// <summary>
    /// Points <see cref="PatternEdit"/> at this history, since edits are recorded inside that
    /// class rather than at its call sites, and gives the song two named instruments so that
    /// taking one out is something that can be seen.
    /// </summary>
    public TrackerHistoryTests()
    {
        Edits.Watching = _history.Taking;

        _song.Normalize();
        _song.Instruments.Add(new TrackerInstrument { Name = "One" });
        _song.Instruments.Add(new TrackerInstrument { Name = "Two" });

        foreach (var one in _song.Instruments) one.EnsureId();
    }

    /// <summary>
    /// Nothing to unhook. Kept as a method that does nothing so the shape of the class does not
    /// change, and as the place this note can live.
    /// </summary>
    /// <remarks>
    /// The watcher used to be static, so a test that hooked it hooked it for the whole process:
    /// forgetting this line left the next test class writing into a history that had been thrown
    /// away, and the failure turned up somewhere else entirely. The editor is an instance now and
    /// this class holds its own, so there is nothing that outlives the test to put back.
    /// </remarks>
    public void Dispose() { }

    /// <summary>The song's first pattern, which is the one every pattern test types into.</summary>
    private Pattern First => _song.PatternAt(0)!;

    /// <summary>A cursor at a line and track, spelled out so the tests read as positions.</summary>
    private static PatternCursor At(int line, int track = 0) => new() { Line = line, Track = track };

    /// <summary>
    /// Puts a remembered song back into the live one in place, which is what the tracker itself
    /// does: panels and the rack hold the song they were opened on, so it cannot be replaced.
    /// </summary>
    private static bool Pour(Song live, Song was)
    {
        live.TakeFrom(was);

        return true;
    }

    /// <summary>
    /// Announces a song edit before making it, naming what is about to change so that steps of
    /// one kind close together gather into one gesture.
    /// </summary>
    private void Changing(string what) => _history.Taking(_song, what, Pour);

    /// <summary>A movement recorded into a lane is a pattern edit and walks back as one.</summary>
    /// <remarks>
    /// The lanes had to be part of a pattern step or undo would put the notes back and leave the
    /// movement where it was, which is the shape of failure this codebase has met twice: doing
    /// nothing looks exactly like working.
    /// </remarks>
    [Fact]
    public void Undo_takes_back_a_recorded_lane()
    {
        var recorder = new AutomationRecorder(
            () => _song, () => true, () => new TrackerPosition(0, 4), () => 0)
        {
            Armed = true,
            Taking = _history.Taking
        };

        var link = new Midi.ControlMapping
        {
            Kind = Midi.Enums.ControlKind.Instrument,
            Scope = Midi.Enums.ControlScope.Focused,
            Machine = "zampler",
            Key = "cutoff"
        };

        recorder.Moved(link, new Knob(0.5, 0, 1), 0.75);

        Assert.Single(First.Lanes);

        Assert.True(_history.Undo());
        Assert.Empty(First.Lanes);

        Assert.True(_history.Redo());
        Assert.Equal(0.75, First.Lanes[0].Points[0].Value);
    }

    /// <summary>Undoing a note leaves the lane beside it exactly where it was.</summary>
    /// <remarks>
    /// And the other direction: a note typed after a sweep was recorded must not take the sweep
    /// with it. A step holding the live lane rather than a copy of it would do exactly that.
    /// </remarks>
    [Fact]
    public void Undoing_a_note_leaves_the_movement_alone()
    {
        var lane = First.Lane(new AutomationLane
        {
            Kind = Midi.Enums.ControlKind.Instrument, Machine = "zampler", Key = "cutoff"
        });

        lane.Put(0, 0.25);

        Edits.EnterNote(First, At(0), new Note(60), 0);
        Assert.True(_history.Undo());

        Assert.Single(First.Lanes);
        Assert.Equal(0.25, First.Lanes[0].Points[0].Value);
    }

    /// <summary>Nothing has happened yet, so neither direction is offered.</summary>
    [Fact]
    public void A_fresh_history_has_nothing_to_walk()
    {
        Assert.False(_history.CanUndo);
        Assert.False(_history.CanRedo);
    }

    /// <summary>
    /// Typing is not gathered the way a dragged value is: three notes are three things somebody
    /// did and have to come back one at a time.
    /// </summary>
    [Fact]
    public void Every_note_typed_is_a_step()
    {
        for (int line = 0; line < 3; line++)
            Edits.EnterNote(First, At(line), new Note(60 + line), 0);

        int steps = 0;
        while (_history.CanUndo) { _history.Undo(); steps++; }

        Assert.Equal(3, steps);
        Assert.False(First[0, 0].Note.IsPlayable);
    }

    /// <summary>One step back is one edit back, and the edit before it stays where it is.</summary>
    [Fact]
    public void Undo_takes_the_last_one_and_leaves_the_rest()
    {
        Edits.EnterNote(First, At(0), new Note(60), 0);
        Edits.EnterNote(First, At(1), new Note(62), 0);

        _history.Undo();

        Assert.True(First[0, 0].Note.IsPlayable);
        Assert.False(First[1, 0].Note.IsPlayable);
    }

    /// <summary>The walk goes both ways, and the cell comes back holding what it held.</summary>
    [Fact]
    public void Redo_puts_it_back()
    {
        Edits.EnterNote(First, At(0), new Note(60), 0);

        _history.Undo();
        Assert.False(First[0, 0].Note.IsPlayable);

        _history.Redo();
        Assert.True(First[0, 0].Note.IsPlayable);
    }

    /// <summary>
    /// Typing after an undo abandons what was undone, since keeping it would offer a redo onto a
    /// pattern that has moved on since.
    /// </summary>
    [Fact]
    public void Doing_something_new_makes_what_was_undone_unreachable()
    {
        Edits.EnterNote(First, At(0), new Note(60), 0);
        _history.Undo();

        Assert.True(_history.CanRedo);

        Edits.EnterNote(First, At(1), new Note(62), 0);

        Assert.False(_history.CanRedo);
    }

    /// <summary>
    /// An edit that moved nothing costs no step, or undo would spend presses doing nothing
    /// visible and read as broken.
    /// </summary>
    /// <remarks>
    /// The two clears in the middle are of cells that are already empty, which is what a hand
    /// resting on the delete key produces.
    /// </remarks>
    [Fact]
    public void An_edit_that_changed_nothing_leaves_no_step()
    {
        Edits.EnterNote(First, At(0), new Note(60), 0);

        Edits.ClearAtCursor(First, At(8));
        Edits.ClearAtCursor(First, At(9));

        Edits.EnterNote(First, At(1), new Note(62), 0);

        int steps = 0;
        while (_history.CanUndo) { _history.Undo(); steps++; }

        Assert.Equal(2, steps);
    }

    /// <summary>
    /// A step names its pattern, so undo after switching patterns goes back to the right one and
    /// takes the view with it rather than editing out of sight.
    /// </summary>
    [Fact]
    public void A_step_knows_which_pattern_it_is_about()
    {
        Edits.EnterNote(First, At(0), new Note(60), 0);

        Assert.Same(First, _history.UndoIsAbout);
    }

    /// <summary>
    /// Removing an instrument shifts every note's instrument number down, and one step has to put
    /// the instrument and the renumbering back together.
    /// </summary>
    [Fact]
    public void Taking_an_instrument_out_renumbers_the_patterns_and_undo_puts_both_back()
    {
        Edits.EnterNote(First, At(0), new Note(60), instrument: 1);
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

    /// <summary>
    /// A cheap step under an expensive one still works, which is the only place the two kinds
    /// meet.
    /// </summary>
    /// <remarks>
    /// The one that used to fail silently: a song step replaced the pattern objects, so the
    /// pattern step under it was pointing at an orphan and restoring it changed nothing.
    /// </remarks>
    [Fact]
    public void And_the_note_underneath_it_can_still_be_undone_afterwards()
    {
        Edits.EnterNote(First, At(0), new Note(60), instrument: 1);

        Changing("taking an instrument out");
        _song.RemoveInstrumentAt(0);

        _history.Undo();
        _history.Undo();

        Assert.False(_song.PatternAt(0)![0, 0].Note.IsPlayable);
        Assert.False(_history.CanUndo);
    }

    /// <summary>
    /// Two song settings that live nowhere near the patterns, walked back in the order they were
    /// changed.
    /// </summary>
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

    /// <summary>
    /// A fader dragged across its range says "the mix" a hundred times and is one thing a person
    /// did, so it comes back in one press.
    /// </summary>
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

    /// <summary>
    /// Gathering is by time as well as by kind: let go, wait, and move it again, and that is two
    /// decisions rather than one long one.
    /// </summary>
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

    /// <summary>
    /// Something else in between breaks the gathering, however fast it happened, or a tempo change
    /// would be swallowed by the fader move either side of it.
    /// </summary>
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

    /// <summary>
    /// A song step is about no pattern, so undoing one must not drag the view off to a pattern
    /// that had nothing to do with it.
    /// </summary>
    [Fact]
    public void A_step_about_the_song_sends_the_view_nowhere()
    {
        Changing("the tempo");
        _song.Bpm = 174;

        Assert.Null(_history.UndoIsAbout);
    }

    /// <summary>
    /// The links a song owns are part of the song, so pointing a knob at something is as
    /// undoable as typing a note.
    /// </summary>
    [Fact]
    public void The_songs_own_controller_links_come_back()
    {
        Changing("a controller link");
        _song.Controls.Add(new Midi.ControlMapping { Device = "Minilab3 MIDI", Cc = 86, Key = "duty" });

        Assert.Single(_song.Controls);

        _history.Undo();

        Assert.Empty(_song.Controls);
    }

    /// <summary>
    /// Opening a song empties the history, since the steps in it describe a song nobody is
    /// looking at any more.
    /// </summary>
    [Fact]
    public void Forgetting_empties_it()
    {
        Edits.EnterNote(First, At(0), new Note(60), 0);

        _history.Forget();

        Assert.False(_history.CanUndo);
        Assert.False(_history.CanRedo);
    }
}
