using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using JingleBox2.Audio.Records;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Where a note played by hand lands while the transport is running, which is the line it was
/// played on and not the line the hand happened to start on.
/// </summary>
/// <remarks>
/// What a chord is depends on the transport. Stopped, there is no clock to ask, so the keys held
/// together are the chord and the second of them goes into the next note column. Running, the
/// moment each key was struck has already decided its line, so the notes that share a line are
/// the chord, whether or not one key was still down when the next arrived.
///
/// The fault this holds shut is a phrase played legato coming back as a chord: each key still
/// held as the next was struck, so every note of it went into the columns of whichever line the
/// first one fell on, however far the transport had moved. Two seconds to a line here, so
/// nothing in it depends on how fast the machine running it is.
/// </remarks>
public class LiveNoteLineTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>Keys overlapping a line apart are two notes on two lines, not a chord on one.</summary>
    [Fact]
    public void A_phrase_played_legato_gets_a_line_per_note()
    {
        var tracker = Playing();
        long now = Stopwatch.GetTimestamp();

        tracker.EnterNote(new Note(40), 90, now);
        tracker.EnterNote(new Note(41), 90, now + Line(3, 2));

        var pattern = tracker.Song.Patterns[0];

        Assert.Equal(new Note(40), pattern[1, 0, 0].Note);
        Assert.Equal(new Note(41), pattern[2, 0, 0].Note);
        Assert.Equal(1, tracker.Song.ColumnsOn(0));

        Done(tracker);
    }

    /// <summary>Two notes struck inside one line share it, though the first key was up by then.</summary>
    /// <remarks>
    /// The other half of the rule: with the line deciding rather than the hand, a chord played
    /// staccato is still a chord, and a key coming up may not give the line away.
    /// </remarks>
    [Fact]
    public void Two_notes_inside_one_line_share_it_though_neither_key_was_held()
    {
        var tracker = Playing();
        long now = Stopwatch.GetTimestamp();

        tracker.EnterNote(new Note(40), 90, now);
        tracker.LetNote(new Note(40));
        tracker.EnterNote(new Note(44), 90, now + Line(1, 10));

        var pattern = tracker.Song.Patterns[0];

        Assert.Equal(new Note(40), pattern[1, 0, 0].Note);
        Assert.Equal(new Note(44), pattern[1, 0, 1].Note);

        Done(tracker);
    }

    /// <summary>A chord struck either side of the half way point between two lines stays on one.</summary>
    /// <remarks>
    /// The line nearest a moment changes at the half way point, so a chord whose notes are a few
    /// milliseconds apart falls across two lines whenever the hand lands near it: at a hundred
    /// and twenty to the minute that is one chord in six. The window is what keeps it together,
    /// and the boundary is searched for here rather than assumed, since where the clock has got
    /// to is not this test's to decide.
    /// </remarks>
    [Fact]
    public void A_chord_struck_across_the_line_boundary_stays_one_chord()
    {
        var tracker = Playing();
        long boundary = Boundary(tracker);

        tracker.EnterNote(new Note(64), 90, boundary - Ms(5));
        tracker.EnterNote(new Note(67), 90, boundary + Ms(5));

        var pattern = tracker.Song.Patterns[0];

        Assert.Equal(new Note(64), pattern[1, 0, 0].Note);
        Assert.Equal(new Note(67), pattern[1, 0, 1].Note);
        Assert.Equal(TrackerCell.Empty, pattern[2, 0, 0]);

        Done(tracker);
    }

    /// <summary>Past the window the same pair is two notes on two lines again.</summary>
    [Fact]
    public void Notes_further_apart_than_the_window_keep_their_own_lines()
    {
        var tracker = Playing();
        long boundary = Boundary(tracker);

        tracker.EnterNote(new Note(64), 90, boundary - Ms(5));
        tracker.EnterNote(new Note(67), 90, boundary + Ms(50));

        var pattern = tracker.Song.Patterns[0];

        Assert.Equal(new Note(64), pattern[1, 0, 0].Note);
        Assert.Equal(new Note(67), pattern[2, 0, 0].Note);
        Assert.Equal(1, tracker.Song.ColumnsOn(0));

        Done(tracker);
    }

    /// <summary>A chord recorded live is kept in pitch order, whatever order the fingers landed.</summary>
    [Fact]
    public void A_chord_recorded_live_is_kept_in_pitch_order()
    {
        var tracker = Playing();
        long now = Stopwatch.GetTimestamp();

        tracker.EnterNote(new Note(67), 90, now);
        tracker.EnterNote(new Note(71), 90, now + Ms(8));
        tracker.EnterNote(new Note(64), 90, now + Ms(16));

        var pattern = tracker.Song.Patterns[0];

        Assert.Equal(new Note(64), pattern[1, 0, 0].Note);
        Assert.Equal(new Note(67), pattern[1, 0, 1].Note);
        Assert.Equal(new Note(71), pattern[1, 0, 2].Note);
        Assert.Equal(3, tracker.Song.ColumnsOn(0));

        Done(tracker);
    }

    /// <summary>A track's own MIDI in keeps a straddling chord together too.</summary>
    [Fact]
    public void A_tracks_chord_across_the_line_boundary_stays_one_chord()
    {
        var tracker = Playing();
        long boundary = Boundary(tracker);

        tracker.EnterTrackNote(3, new Note(36), 100, boundary - Ms(5));
        tracker.EnterTrackNote(3, new Note(40), 100, boundary + Ms(5));

        var pattern = tracker.Song.Patterns[0];

        Assert.Equal(new Note(36), pattern[1, 3, 0].Note);
        Assert.Equal(new Note(40), pattern[1, 3, 1].Note);
        Assert.Equal(TrackerCell.Empty, pattern[2, 3, 0]);

        Done(tracker);
    }

    /// <summary>The window is a hand's chord and nothing anybody plays on purpose.</summary>
    [Fact]
    public void A_chord_is_the_notes_struck_within_the_window_of_the_first()
    {
        var window = new ChordWindow();
        long line = Ms(125);

        Assert.True(window.Together(0, Ms(39), line));
        Assert.False(window.Together(0, Ms(41), line));
        Assert.True(window.Together(Ms(39), 0, line), "the two moments are the same distance apart either way round");
    }

    /// <summary>And it is never more than half a line, whatever the song is doing.</summary>
    [Fact]
    public void The_window_is_never_wider_than_half_a_line()
    {
        var window = new ChordWindow();

        Assert.False(window.Together(0, Ms(15), Ms(20)));
        Assert.True(window.Together(0, Ms(9), Ms(20)));
        Assert.True(window.Together(0, Ms(39), 0), "a line nobody knows the length of leaves the window alone");
    }

    /// <summary>A letter row repeating under a held key fills no second column.</summary>
    [Fact]
    public void A_repeat_while_the_transport_runs_fills_no_second_column()
    {
        var tracker = Playing();
        long now = Stopwatch.GetTimestamp();

        tracker.EnterNote(new Note(40), 90, now);
        tracker.EnterNote(new Note(40), 90, now + Line(1, 10));

        var pattern = tracker.Song.Patterns[0];

        Assert.Equal(new Note(40), pattern[1, 0, 0].Note);
        Assert.Equal(1, tracker.Song.ColumnsOn(0));

        Done(tracker);
    }

    /// <summary>Recording over a line that already had a note plays over it rather than beside it.</summary>
    /// <remarks>
    /// Which is why the line being filled is remembered rather than counted off the pattern: a
    /// second pass over a part would otherwise stack a column on every line it was played on,
    /// and a track is eight columns wide at the most.
    /// </remarks>
    [Fact]
    public void A_line_that_already_had_a_note_is_played_over_rather_than_added_to()
    {
        var tracker = Tracker();
        tracker.Song.Patterns[0][1, 0] =
            new TrackerCell(new Note(52), TrackerCell.NoInstrument, TrackerCell.NoVolume, TrackerCommand.None);

        Start(tracker);

        tracker.EnterNote(new Note(40), 90, Stopwatch.GetTimestamp());

        var pattern = tracker.Song.Patterns[0];

        Assert.Equal(new Note(40), pattern[1, 0, 0].Note);
        Assert.Equal(1, tracker.Song.ColumnsOn(0));

        Done(tracker);
    }

    /// <summary>Stopped, the keys held together are the chord, as they always were.</summary>
    [Fact]
    public void Keys_held_together_are_still_a_chord_while_the_transport_is_stopped()
    {
        var tracker = Tracker();
        tracker.IsRecording = true;

        tracker.EnterNote(new Note(64), 90);
        tracker.EnterNote(new Note(67), 90);

        var pattern = tracker.Song.Patterns[0];

        Assert.Equal(new Note(64), pattern[0, 0, 0].Note);
        Assert.Equal(new Note(67), pattern[0, 0, 1].Note);
        Assert.Equal(1, tracker.Cursor.Line);

        tracker.Finished();
    }

    /// <summary>A track's own MIDI in follows the same rule: a line per note, not a chord.</summary>
    [Fact]
    public void A_track_hearing_a_legato_phrase_gets_a_line_per_note()
    {
        var tracker = Playing();
        long now = Stopwatch.GetTimestamp();

        tracker.EnterTrackNote(3, new Note(36), 100, now);
        tracker.EnterTrackNote(3, new Note(37), 100, now + Line(3, 2));

        var pattern = tracker.Song.Patterns[0];

        Assert.Equal(new Note(36), pattern[1, 3, 0].Note);
        Assert.Equal(new Note(37), pattern[2, 3, 0].Note);
        Assert.Equal(1, tracker.Song.ColumnsOn(3));

        Done(tracker);
    }

    /// <summary>And two notes on one track inside one line share it, a column each.</summary>
    [Fact]
    public void Two_notes_on_one_track_inside_one_line_share_it()
    {
        var tracker = Playing();
        long now = Stopwatch.GetTimestamp();

        tracker.EnterTrackNote(3, new Note(36), 100, now);
        tracker.LetTrackNote(3, new Note(36));
        tracker.EnterTrackNote(3, new Note(40), 100, now + Line(1, 10));

        var pattern = tracker.Song.Patterns[0];

        Assert.Equal(new Note(36), pattern[1, 3, 0].Note);
        Assert.Equal(new Note(40), pattern[1, 3, 1].Note);

        Done(tracker);
    }

    /// <summary>Milliseconds, as the stopwatch counts.</summary>
    private static long Ms(double many) => (long)(Stopwatch.Frequency * many / 1000);

    /// <summary>
    /// The moment the line nearest it stops being the one the clock is on, found by asking.
    /// </summary>
    private static long Boundary(TrackerViewModel tracker)
    {
        long now = Stopwatch.GetTimestamp();
        int line = tracker.Player.NearestLine(now).Line;

        for (long step = Ms(1); step < Line(2, 1); step += Ms(1))
            if (tracker.Player.NearestLine(now + step).Line != line) return now + step;

        Assert.Fail("the next line was never nearer than this one");

        return now;
    }

    /// <summary>A share of one line, as the stopwatch counts.</summary>
    private static long Line(int over, int under) => Stopwatch.Frequency * 2 * over / under;

    /// <summary>A tracker armed, playing, and standing on line 1 with the clock running.</summary>
    private static TrackerViewModel Playing()
    {
        var tracker = Tracker();

        Start(tracker);

        return tracker;
    }

    /// <summary>Starts the pass and waits for the clock to reach line 1.</summary>
    private static void Start(TrackerViewModel tracker)
    {
        tracker.IsRecording = true;
        tracker.Player.Play(tracker.Song, TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => tracker.Player.Position.Line == 1), "the clock never reached line 1");
    }

    private static void Done(TrackerViewModel tracker)
    {
        tracker.Player.Stop();
        tracker.Finished();
    }

    /// <summary>Two seconds to a line, so the arithmetic is nowhere near the machine's own jitter.</summary>
    private static TrackerViewModel Tracker()
    {
        var tracker = new TrackerViewModel(
            new QuietAudio(),
            new SoundMachineRack(),
            new ObservableCollection<Recording>(),
            new SoundMachineProjects());

        tracker.Song.Bpm = 30;
        tracker.Song.LinesPerBeat = 1;
        tracker.Player.Loop = false;

        return tracker;
    }

    private static bool Until(Func<bool> said)
    {
        var clock = Stopwatch.StartNew();

        while (clock.Elapsed < Patience)
        {
            if (said()) return true;
            Thread.Sleep(5);
        }

        return said();
    }
}
