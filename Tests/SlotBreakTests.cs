using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A slot stopping short of its pattern's end and going on to the next slot.
/// </summary>
public class SlotBreakTests
{
    /// <summary>How long anything here is waited for.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(4);

    /// <summary>A song of so many patterns of so many lines, one slot each, quick enough to run through.</summary>
    private static Song Of(int patterns, int lines, double bpm = 400)
    {
        var song = new Song { Bpm = bpm, LinesPerBeat = 4 };

        for (int at = 0; at < patterns; at++)
        {
            song.Patterns.Add(new Pattern(lines, song.TrackCount) { Name = "P" + at });
            song.Order.Add(at);
        }

        song.Normalize();

        return song;
    }

    /// <summary>Waits for something to become true, or gives up.</summary>
    private static bool Until(Func<bool> said)
    {
        var clock = Stopwatch.StartNew();

        while (clock.Elapsed < Patience)
        {
            if (said()) return true;

            Thread.Sleep(2);
        }

        return said();
    }

    /// <summary>A break stops the slot after its line.</summary>
    [Fact]
    public void A_break_is_kept_inside_the_pattern()
    {
        var repeat = SlotRepeat.None.EndingAt(23, 64);

        Assert.Equal(23, repeat.Last);
        Assert.Equal(23, repeat.LastLine(64));
    }

    /// <summary>A break on the last line, past it, or negative is the whole pattern.</summary>
    [Theory]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(500)]
    [InlineData(-1)]
    [InlineData(-9)]
    public void A_break_at_or_past_the_end_is_the_whole_pattern(int last)
    {
        var repeat = SlotRepeat.None.EndingAt(last, 64);

        Assert.Equal(-1, repeat.Last);
        Assert.Equal(63, repeat.LastLine(64));
    }

    /// <summary>A loop reaching past the break goes, and one before it stays.</summary>
    [Fact]
    public void A_loop_past_the_break_goes()
    {
        var repeat = SlotRepeat.None
            .WithLoop(new LineLoop(4, 7, 2), 64, out _)
            .WithLoop(new LineLoop(40, 47, 2), 64, out _)
            .EndingAt(31, 64);

        Assert.Equal(new LineLoop(4, 7, 2), Assert.Single(repeat.Lines()));
    }

    /// <summary>A loop past the break cannot be added, and the break stays.</summary>
    [Fact]
    public void A_loop_past_the_break_is_refused()
    {
        var repeat = SlotRepeat.None.EndingAt(31, 64).WithLoop(new LineLoop(28, 40, 2), 64, out var blocking);

        Assert.Null(blocking);
        Assert.Empty(repeat.Lines());
        Assert.Equal(31, repeat.Last);
    }

    /// <summary>Adding and removing loops keeps the break.</summary>
    [Fact]
    public void Loops_keep_the_break()
    {
        var repeat = SlotRepeat.None.EndingAt(31, 64)
            .WithLoop(new LineLoop(0, 3, 2), 64, out _)
            .WithoutLoopsIn(0, 3, 64);

        Assert.Equal(31, repeat.Last);
    }

    /// <summary>A break comes back from the song file, and a song written before breaks plays whole.</summary>
    [Fact]
    public void A_break_comes_back_from_the_file()
    {
        var song = Of(2, 64);
        song.SetRepeat(1, SlotRepeat.None.EndingAt(15, 64));

        var back = SongStore.Uncopy(SongStore.Copy(song))!;
        back.Normalize();

        Assert.Equal(15, back.LastLineOf(1));
        Assert.Equal(63, back.LastLineOf(0));
    }

    /// <summary>Playing the song, a slot with a break goes on to the next slot after its line.</summary>
    [Fact]
    public void Playing_the_song_a_break_goes_on_to_the_next_slot()
    {
        var song = Of(2, 16);
        song.SetRepeat(0, SlotRepeat.None.EndingAt(3, 16));

        var reached = Play(song, TrackerPlayMode.Song, seen => seen.Any(at => at.OrderIndex == 1));

        Assert.Equal(new[] { 0, 1, 2, 3 }, reached.TakeWhile(at => at.OrderIndex == 0).Select(at => at.Line));
        Assert.Equal(new TrackerPosition(1, 0), reached.First(at => at.OrderIndex == 1));
    }

    /// <summary>A break with a repeat count goes round the shortened slot that many times.</summary>
    [Fact]
    public void A_repeated_slot_goes_round_its_shortened_length()
    {
        var song = Of(2, 16);
        song.SetRepeat(0, new SlotRepeat(Times: 2).EndingAt(1, 16));

        var reached = Play(song, TrackerPlayMode.Song, seen => seen.Any(at => at.OrderIndex == 1));

        Assert.Equal(new[] { 0, 1, 0, 1 }, reached.TakeWhile(at => at.OrderIndex == 0).Select(at => at.Line));
    }

    /// <summary>Looping one pattern plays all of it, since the break belongs to the slot in the song.</summary>
    [Fact]
    public void Looping_one_pattern_plays_all_of_it()
    {
        var song = Of(1, 8);
        song.SetRepeat(0, SlotRepeat.None.EndingAt(3, 8));

        var reached = Play(song, TrackerPlayMode.Pattern, seen => seen.Count >= 9);

        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 0 }, reached.Take(9).Select(at => at.Line));
    }

    /// <summary>Plays a song until enough positions are seen, and gives them back.</summary>
    private static System.Collections.Generic.List<TrackerPosition> Play(Song song, TrackerPlayMode mode,
        Func<System.Collections.Generic.List<TrackerPosition>, bool> enough)
    {
        var reached = new ConcurrentQueue<TrackerPosition>();

        using var player = new TrackerPlayer(new SilentAudio());
        player.Loop = mode == TrackerPlayMode.Pattern;
        player.PositionChanged += (_, at) => reached.Enqueue(at);
        player.Play(song, TrackerPosition.Start, mode);

        Until(() => enough(reached.ToList()));
        player.Stop();

        return reached.ToList();
    }
}
