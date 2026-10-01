using System.Collections.Generic;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>Playing a slot's pattern more than once, and going round a stretch of its lines.</summary>
/// <remarks>
/// Walked the way the clock walks, a line at a time with no audio: each line played is put to the
/// rule, which answers where the song goes back to or leaves it to go on as it always has. The
/// lines are written as slot and line so a wrong jump reads as the line it went to.
/// </remarks>
public class SlotPassesTests
{
    /// <summary>A song of so many patterns of so many lines, one slot each.</summary>
    private static Song Of(int patterns, int lines)
    {
        var song = new Song();

        for (int at = 0; at < patterns; at++)
        {
            song.Patterns.Add(new Pattern(lines, song.TrackCount) { Name = "P" + at });
            song.Order.Add(at);
        }

        song.Normalize();

        return song;
    }

    /// <summary>The lines played from the start, as slot.line, for so many steps or until the song ends.</summary>
    private static List<string> Walk(Song song, int steps, TrackerPlayMode mode = TrackerPlayMode.Song, bool loop = false)
    {
        ISlotPasses passes = new SlotPasses();
        var played = new List<string>();
        TrackerPosition? at = TrackerPosition.Start;

        for (int step = 0; step < steps && at is { } now; step++)
        {
            played.Add(now.OrderIndex + "." + now.Line);

            at = passes.After(song, now, mode == TrackerPlayMode.Song)
                 ?? (mode == TrackerPlayMode.Pattern
                     ? TrackerSequencer.AdvanceWithinPattern(song, now, loop)
                     : TrackerSequencer.Advance(song, now, loop));
        }

        return played;
    }

    /// <summary>A slot with nothing set plays once, exactly as before.</summary>
    [Fact]
    public void Nothing_set_plays_straight_through()
    {
        Assert.Equal(new[] { "0.0", "0.1", "1.0", "1.1" }, Walk(Of(2, 2), 10));
    }

    /// <summary>A pattern played twice goes back to its top once, then the song moves on.</summary>
    [Fact]
    public void A_pattern_played_twice()
    {
        var song = Of(2, 2);
        song.Repeats[0] = new SlotRepeat(2);

        Assert.Equal(new[] { "0.0", "0.1", "0.0", "0.1", "1.0", "1.1" }, Walk(song, 20));
    }

    /// <summary>A stretch played three times goes back to its first line twice, then carries on.</summary>
    [Fact]
    public void A_stretch_played_three_times()
    {
        var song = Of(1, 4);
        song.Repeats[0] = new SlotRepeat(1, 1, 2, 3);

        Assert.Equal(new[] { "0.0", "0.1", "0.2", "0.1", "0.2", "0.1", "0.2", "0.3" }, Walk(song, 20));
    }

    /// <summary>Both: the stretch goes round on every pass of the pattern.</summary>
    [Fact]
    public void A_stretch_on_every_pass()
    {
        var song = Of(2, 3);
        song.Repeats[0] = new SlotRepeat(2, 1, 1, 2);

        Assert.Equal(
            new[] { "0.0", "0.1", "0.1", "0.2", "0.0", "0.1", "0.1", "0.2", "1.0", "1.1", "1.2" },
            Walk(song, 30));
    }

    /// <summary>A stretch that ends on the last line finishes before the pattern is played again.</summary>
    [Fact]
    public void A_stretch_ending_on_the_last_line()
    {
        var song = Of(1, 3);
        song.Repeats[0] = new SlotRepeat(2, 1, 2, 2);

        Assert.Equal(
            new[] { "0.0", "0.1", "0.2", "0.1", "0.2", "0.0", "0.1", "0.2", "0.1", "0.2" },
            Walk(song, 30));
    }

    /// <summary>Played by the pattern alone, the stretch still goes round and the pattern count is left to the loop.</summary>
    [Fact]
    public void Pattern_mode_keeps_the_stretch()
    {
        var song = Of(1, 3);
        song.Repeats[0] = new SlotRepeat(5, 0, 1, 2);

        Assert.Equal(
            new[] { "0.0", "0.1", "0.0", "0.1", "0.2", "0.0", "0.1", "0.0", "0.1", "0.2" },
            Walk(song, 10, TrackerPlayMode.Pattern, loop: true));
    }

    /// <summary>Coming round to the same slot again, by the song looping, repeats it again.</summary>
    [Fact]
    public void Coming_round_again_repeats_again()
    {
        var song = Of(1, 2);
        song.Repeats[0] = new SlotRepeat(2);

        Assert.Equal(
            new[] { "0.0", "0.1", "0.0", "0.1", "0.0", "0.1", "0.0", "0.1" },
            Walk(song, 8, TrackerPlayMode.Song, loop: true));
    }

    /// <summary>A stretch past the end of the pattern is ignored rather than jumped into.</summary>
    [Fact]
    public void A_stretch_outside_the_pattern_is_ignored()
    {
        var song = Of(1, 2);
        song.Repeats[0] = new SlotRepeat(1, 5, 9, 3);

        Assert.Equal(new[] { "0.0", "0.1" }, Walk(song, 10));
    }
}
