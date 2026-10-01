using JingleBox2.Tracker;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What a slot of the order says about repeating, and that it stays with its slot.
/// </summary>
/// <remarks>
/// A slot can play its pattern more than once, and can go round a stretch of its lines a number of
/// times on each pass. That belongs to the slot and not to the pattern, so a pattern in the order
/// twice can repeat in one place and not the other, and it has to move with the slot wherever the
/// order is edited and come back from the song file as it went in.
/// </remarks>
public class SlotRepeatTests
{
    /// <summary>A song of so many patterns in order, one slot each.</summary>
    private static Song Of(int patterns)
    {
        var song = new Song();

        for (int at = 0; at < patterns; at++)
        {
            song.Patterns.Add(new Pattern(64, song.TrackCount) { Name = "P" + at });
            song.Order.Add(at);
        }

        song.Normalize();

        return song;
    }

    /// <summary>A repeat that is told apart from the others by its count.</summary>
    private static SlotRepeat Times(int times) => new(times, 32, 63, 4);

    /// <summary>Every slot has a repeat, and a slot nobody set plays once with no stretch.</summary>
    [Fact]
    public void Every_slot_plays_once_unless_told_otherwise()
    {
        var song = Of(3);

        Assert.Equal(3, song.Repeats.Count);
        Assert.All(song.Repeats, repeat => Assert.Equal(SlotRepeat.None, repeat));
    }

    /// <summary>A slot put in carries its repeat, and the slots after it keep theirs.</summary>
    [Fact]
    public void A_slot_put_in_keeps_the_others_in_place()
    {
        var song = Of(2);
        song.Repeats[1] = Times(3);

        song.InsertSlot(1, 0, Times(2));

        Assert.Equal(new[] { 0, 0, 1 }, song.Order);
        Assert.Equal(Times(2), song.Repeats[1]);
        Assert.Equal(Times(3), song.Repeats[2]);
    }

    /// <summary>A slot taken out takes its repeat with it.</summary>
    [Fact]
    public void A_slot_taken_out_takes_its_repeat()
    {
        var song = Of(3);
        song.Repeats[0] = Times(2);
        song.Repeats[2] = Times(5);

        song.RemoveSlot(1);

        Assert.Equal(new[] { 0, 2 }, song.Order);
        Assert.Equal(Times(2), song.Repeats[0]);
        Assert.Equal(Times(5), song.Repeats[1]);
    }

    /// <summary>A slot dragged elsewhere takes its repeat with it.</summary>
    [Fact]
    public void A_slot_moved_takes_its_repeat()
    {
        var song = Of(3);
        song.Repeats[0] = Times(7);

        Assert.True(song.MoveOrder(0, 2));

        Assert.Equal(new[] { 1, 2, 0 }, song.Order);
        Assert.Equal(Times(7), song.Repeats[2]);
        Assert.Equal(SlotRepeat.None, song.Repeats[0]);
    }

    /// <summary>A slot naming a pattern that is not there goes, and its repeat goes with it.</summary>
    [Fact]
    public void A_slot_that_is_dropped_drops_its_repeat()
    {
        var song = Of(2);
        song.Order.Insert(1, 99);
        song.Repeats.Insert(1, Times(9));
        song.Repeats[2] = Times(4);

        song.Normalize();

        Assert.Equal(new[] { 0, 1 }, song.Order);
        Assert.Equal(Times(4), song.Repeats[1]);
    }

    /// <summary>A song with fewer repeats than slots is given plain ones, and one with more loses the rest.</summary>
    [Fact]
    public void Repeats_are_kept_as_long_as_the_order()
    {
        var song = Of(3);
        song.Repeats.Clear();
        song.Normalize();

        Assert.Equal(3, song.Repeats.Count);

        song.Repeats.Add(Times(2));
        song.Normalize();

        Assert.Equal(3, song.Repeats.Count);
    }

    /// <summary>The repeats come back from the song file as they went in.</summary>
    [Fact]
    public void Repeats_come_back_from_the_file()
    {
        var song = Of(3);
        song.Repeats[1] = Times(2);

        var back = SongStore.Uncopy(SongStore.Copy(song))!;

        Assert.Equal(Times(2), back.Repeats[1]);
        Assert.Equal(SlotRepeat.None, back.Repeats[0]);
    }

    /// <summary>A song written before slots could repeat reads back with every slot playing once.</summary>
    [Fact]
    public void An_older_song_plays_every_slot_once()
    {
        var song = Of(2);
        string written = SongStore.Copy(song).Replace("\"Repeats\":", "\"Ignored\":");

        var back = SongStore.Uncopy(written)!;
        back.Normalize();

        Assert.Equal(2, back.Repeats.Count);
        Assert.All(back.Repeats, repeat => Assert.Equal(SlotRepeat.None, repeat));
    }

    /// <summary>A count below one plays once, and a stretch that is not inside the pattern is none.</summary>
    [Fact]
    public void Nonsense_is_read_as_playing_once()
    {
        var repeat = new SlotRepeat(0, 70, 10, -3).Held(64);

        Assert.Equal(1, repeat.Times);
        Assert.False(repeat.HasStretch);
    }

    /// <summary>A stretch drawn backwards is the same stretch.</summary>
    [Fact]
    public void A_stretch_drawn_backwards_is_the_same_stretch()
    {
        var repeat = new SlotRepeat(1, 63, 32, 4).Held(64);

        Assert.True(repeat.HasStretch);
        Assert.Equal(32, repeat.From);
        Assert.Equal(63, repeat.To);
    }
}
