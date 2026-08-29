using System;
using System.Linq;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Copying a pattern and moving a slot of the order, which are the two things an order list is
/// mostly used for and the two it could not do.
/// </summary>
/// <remarks>
/// The order is a list of numbers into the patterns, so both of these are indexed arithmetic
/// over a list that the clock thread reads while somebody edits it. A slot moved to the wrong
/// place plays the wrong part; a copy that shares its cells with the pattern it came from is
/// worse, because it looks right until somebody edits one of them and both change.
/// </remarks>
public class SongOrderTests
{
    /// <summary>A song with three slots playing three patterns, and a note in the first.</summary>
    private static Song Three()
    {
        var song = Song.CreateDefault();

        song.Order.Clear();

        for (int at = 0; at < 3; at++)
        {
            int pattern = at == 0 ? 0 : song.AddPattern();
            song.Order.Add(pattern);
        }

        song.Patterns[0][0, 0] = new TrackerCell(new Note(60), 0, 64, TrackerEffect.None);

        return song;
    }

    /// <summary>A copy holds the same music, which is the whole of what it is for.</summary>
    [Fact]
    public void A_copy_holds_the_same_music()
    {
        var song = Three();

        int copy = song.ClonePattern(0);

        Assert.True(copy > 0);
        Assert.Equal(new Note(60), song.Patterns[copy][0, 0].Note);
        Assert.Equal(64, song.Patterns[copy][0, 0].Volume);
    }

    /// <summary>
    /// And shares nothing with it. This is the half that looks right either way until somebody
    /// edits one of them, which is exactly why it is worth a test.
    /// </summary>
    [Fact]
    public void A_copy_shares_nothing_with_what_it_came_from()
    {
        var song = Three();

        int copy = song.ClonePattern(0);

        song.Patterns[copy][0, 0] = new TrackerCell(new Note(72), 1, 32, TrackerEffect.None);

        Assert.Equal(new Note(60), song.Patterns[0][0, 0].Note);
        Assert.Equal(new Note(72), song.Patterns[copy][0, 0].Note);
    }

    /// <summary>A copy is named the way a new pattern is, so the two ways cannot disagree.</summary>
    [Fact]
    public void A_copy_is_named_like_a_new_one()
    {
        var song = Three();

        int copy = song.ClonePattern(0);
        int added = song.AddPattern();

        Assert.NotEqual(song.Patterns[0].Name, song.Patterns[copy].Name);
        Assert.NotEqual(song.Patterns[copy].Name, song.Patterns[added].Name);
    }

    /// <summary>Copying a pattern that is not there is nothing, rather than an exception.</summary>
    [Fact]
    public void Copying_a_pattern_that_is_not_there_does_nothing()
    {
        var song = Three();
        int held = song.Patterns.Count;

        Assert.Equal(-1, song.ClonePattern(-1));
        Assert.Equal(-1, song.ClonePattern(song.Patterns.Count));
        Assert.Equal(-1, song.ClonePattern(int.MaxValue));
        Assert.Equal(held, song.Patterns.Count);
    }

    /// <summary>A slot dragged down the list lands where it was let go of.</summary>
    [Fact]
    public void A_slot_moves_to_where_it_was_dropped()
    {
        var song = Three();
        var was = song.Order.ToList();

        Assert.True(song.MoveOrder(0, 2));

        Assert.Equal(new[] { was[1], was[2], was[0] }, song.Order);
    }

    /// <summary>And upwards, which is the same move read the other way round.</summary>
    [Fact]
    public void A_slot_moves_upwards_too()
    {
        var song = Three();
        var was = song.Order.ToList();

        Assert.True(song.MoveOrder(2, 0));

        Assert.Equal(new[] { was[2], was[0], was[1] }, song.Order);
    }

    /// <summary>The order keeps every slot it had. A move is not a way to lose one.</summary>
    [Fact]
    public void A_move_loses_nothing()
    {
        var song = Three();
        var was = song.Order.ToList();

        song.MoveOrder(1, 0);

        Assert.Equal(was.OrderBy(p => p), song.Order.OrderBy(p => p));
        Assert.Equal(was.Count, song.Order.Count);
    }

    /// <summary>A slot dropped where it already was is nothing to do, and says so.</summary>
    [Fact]
    public void A_slot_dropped_where_it_was_does_nothing()
    {
        var song = Three();
        var was = song.Order.ToList();

        Assert.False(song.MoveOrder(1, 1));
        Assert.Equal(was, song.Order);
    }

    /// <summary>
    /// Past the end is the end. Somebody dragging to the bottom of a list means the bottom of
    /// the list, and refusing it would leave the one drop everybody tries doing nothing.
    /// </summary>
    [Fact]
    public void Past_the_end_is_the_end()
    {
        var song = Three();
        var was = song.Order.ToList();

        Assert.True(song.MoveOrder(0, 99));

        Assert.Equal(was[0], song.Order[^1]);
    }

    /// <summary>And a slot that is not there is refused rather than clamped into one that is.</summary>
    [Fact]
    public void A_slot_that_is_not_there_is_refused()
    {
        var song = Three();
        var was = song.Order.ToList();

        Assert.False(song.MoveOrder(-1, 0));
        Assert.False(song.MoveOrder(3, 0));
        Assert.False(song.MoveOrder(int.MaxValue, 0));

        Assert.Equal(was, song.Order);
    }

    /// <summary>An order with one slot in it has nowhere to move it to.</summary>
    [Fact]
    public void One_slot_cannot_be_moved()
    {
        var song = Song.CreateDefault();

        Assert.False(song.MoveOrder(0, 0));
        Assert.Single(song.Order);
    }

    /// <summary>
    /// The same pattern twice in the order is two slots, and moving one leaves the other where
    /// it was. The slot moves, not the pattern.
    /// </summary>
    [Fact]
    public void The_slot_moves_and_not_the_pattern()
    {
        var song = Three();

        song.Order.Add(song.Order[0]);

        var was = song.Order.ToList();

        song.MoveOrder(3, 0);

        Assert.Equal(was[0], song.Order[0]);
        Assert.Equal(was[0], song.Order[1]);
        Assert.Equal(4, song.Order.Count);
    }

    /// <summary>
    /// The same pattern in two slots plays twice and then stops, which is the whole point of
    /// being able to put one in the order more than once.
    /// </summary>
    /// <remarks>
    /// The sequencer walks slots and not patterns, so a repeated pattern is two passes over the
    /// same cells rather than one. This is the walk the clock thread makes, with no audio and
    /// no window.
    /// </remarks>
    [Fact]
    public void A_repeated_pattern_is_played_twice()
    {
        var song = Song.CreateDefault();

        song.Patterns[0].Resize(4);

        song.Order.Clear();
        song.Order.Add(0);
        song.Order.Add(0);

        var walk = new System.Collections.Generic.List<string>();
        var at = TrackerPosition.Start;

        for (int step = 0; step < 20; step++)
        {
            walk.Add($"{at.OrderIndex}:{at.Line}");

            var next = TrackerSequencer.Advance(song, at, loop: false);
            if (next == null) break;

            at = next.Value;
        }

        Assert.Equal(
            new[] { "0:0", "0:1", "0:2", "0:3", "1:0", "1:1", "1:2", "1:3" },
            walk);
    }

    /// <summary>
    /// And looping a pattern stays on the slot it is on, which is the other half of the picker
    /// and the half that used to be the only one a running pass could hear.
    /// </summary>
    [Fact]
    public void Looping_a_pattern_stays_on_its_own_slot()
    {
        var song = Song.CreateDefault();

        song.Patterns[0].Resize(2);

        song.Order.Clear();
        song.Order.Add(0);
        song.Order.Add(0);

        var at = new TrackerPosition(1, 0);

        for (int step = 0; step < 6; step++)
        {
            var next = TrackerSequencer.AdvanceWithinPattern(song, at, loop: true);

            Assert.NotNull(next);

            at = next!.Value;

            Assert.Equal(1, at.OrderIndex);
        }
    }

    /// <summary>A range marked over the order loops back to its first slot at its last.</summary>
    [Fact]
    public void A_loop_range_goes_round_at_its_last_slot()
    {
        var song = Four();

        song.SetLoop(1, 2);

        var at = new TrackerPosition(2, song.Patterns[0].Lines - 1);

        var next = TrackerSequencer.Advance(song, at, loop: false);

        Assert.Equal(new TrackerPosition(1, 0), next);
    }

    /// <summary>
    /// And it loops whatever the standing loop flag says, since marking a range is somebody
    /// saying "go round these" in as many words.
    /// </summary>
    [Fact]
    public void A_range_loops_even_with_looping_off()
    {
        var song = Four();

        song.SetLoop(0, 1);

        var at = new TrackerPosition(1, song.Patterns[0].Lines - 1);

        Assert.Equal(new TrackerPosition(0, 0), TrackerSequencer.Advance(song, at, loop: false));
        Assert.Equal(new TrackerPosition(0, 0), TrackerSequencer.Advance(song, at, loop: true));
    }

    /// <summary>
    /// Playing from before the range runs into it rather than being dragged into it early, and
    /// playing from after it is not dragged backwards at all.
    /// </summary>
    [Fact]
    public void A_range_is_answered_only_at_its_last_slot()
    {
        var song = Four();

        song.SetLoop(1, 2);

        int last = song.Patterns[0].Lines - 1;

        Assert.Equal(new TrackerPosition(1, 0), TrackerSequencer.Advance(song, new TrackerPosition(0, last), false));
        Assert.Equal(new TrackerPosition(2, 0), TrackerSequencer.Advance(song, new TrackerPosition(1, last), false));
        Assert.Null(TrackerSequencer.Advance(song, new TrackerPosition(3, last), false));
    }

    /// <summary>Either end may be given first, since a range is drawn by dragging both ways.</summary>
    [Fact]
    public void A_range_may_be_drawn_from_either_end()
    {
        var song = Four();

        song.SetLoop(3, 1);

        Assert.True(song.HasLoop);
        Assert.Equal(1, song.LoopFirst);
        Assert.Equal(3, song.LoopLast);
        Assert.True(song.Loops(2));
        Assert.False(song.Loops(0));
    }

    /// <summary>A range drawn past the end is a range to the end, which is what the hand meant.</summary>
    [Fact]
    public void A_range_past_the_end_is_held_to_the_end()
    {
        var song = Four();

        song.SetLoop(2, 99);

        Assert.Equal(2, song.LoopFirst);
        Assert.Equal(song.Order.Count - 1, song.LoopLast);
    }

    /// <summary>And it can be taken off again, which leaves the order playing straight through.</summary>
    [Fact]
    public void A_range_can_be_taken_off()
    {
        var song = Four();

        song.SetLoop(1, 2);
        song.SetLoop(Song.NoLoop, Song.NoLoop);

        Assert.False(song.HasLoop);
        Assert.False(song.Loops(1));

        int last = song.Patterns[0].Lines - 1;

        Assert.Equal(new TrackerPosition(2, 0), TrackerSequencer.Advance(song, new TrackerPosition(1, last), false));
    }

    /// <summary>
    /// A range left pointing past an order that has since shrunk is no range at all, rather
    /// than one over slots that are not there.
    /// </summary>
    [Fact]
    public void A_range_over_slots_that_are_gone_is_no_range()
    {
        var song = Four();

        song.SetLoop(2, 3);

        song.Order.RemoveAt(3);
        song.Order.RemoveAt(2);

        Assert.False(song.HasLoop);
        Assert.False(song.Loops(1));
    }

    /// <summary>The range travels in the song file, since it is about the music and not the desk.</summary>
    [Fact]
    public void The_range_is_written_down_and_read_back()
    {
        var song = Four();

        song.SetLoop(1, 2);

        var back = SongStore.Uncopy(SongStore.Copy(song))!;

        Assert.True(back.HasLoop);
        Assert.Equal(1, back.LoopFirst);
        Assert.Equal(2, back.LoopLast);
    }

    /// <summary>And a song written before ranges existed reads back with none.</summary>
    [Fact]
    public void A_song_that_never_heard_of_ranges_has_none()
    {
        var song = Four();

        string written = string.Join(
            Environment.NewLine,
            SongStore.Copy(song).Split('\n').Where(line => !line.Contains("Loop")));

        var back = SongStore.Uncopy(written)!;

        Assert.False(back.HasLoop);
    }

    /// <summary>Four slots, each on its own pattern, for the range tests to mark up.</summary>
    private static Song Four()
    {
        var song = Song.CreateDefault();

        song.Order.Clear();

        for (int at = 0; at < 4; at++)
            song.Order.Add(at == 0 ? 0 : song.AddPattern());

        return song;
    }

    /// <summary>Both survive being written down and read back, which is where a song lives.</summary>
    [Fact]
    public void A_copied_pattern_and_a_moved_slot_come_back()
    {
        var song = Three();

        int copy = song.ClonePattern(0);
        song.Order.Insert(1, copy);
        song.MoveOrder(0, 2);

        var back = SongStore.Uncopy(SongStore.Copy(song))!;

        Assert.Equal(song.Order, back.Order);
        Assert.Equal(song.Patterns.Count, back.Patterns.Count);
        Assert.Equal(new Note(60), back.Patterns[copy][0, 0].Note);
    }
}
