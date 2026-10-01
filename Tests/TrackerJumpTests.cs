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

/// <summary>Moving a playing song to another line without stopping it.</summary>
/// <remarks>
/// Scrolling the pattern while it plays takes the music with it, the way a tracker has always
/// done: the song carries on from the line scrolled to at its next step, with nothing stopped and
/// nothing loaded again.
/// </remarks>
public class TrackerJumpTests
{
    /// <summary>How long anything here is waited for.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(3);

    /// <summary>One pattern of so many lines, at a tempo where a line is a quarter of a second.</summary>
    private static Song Of(int lines)
    {
        var song = new Song { Bpm = 60, LinesPerBeat = 4 };

        song.Patterns.Add(new Pattern(lines, song.TrackCount) { Name = "P" });
        song.Order.Add(0);
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

    /// <summary>A playing song carries on from the line it was moved to.</summary>
    [Fact]
    public void A_playing_song_carries_on_from_the_line_it_was_moved_to()
    {
        using var player = new TrackerPlayer(new SilentAudio());
        var reached = new ConcurrentQueue<int>();

        player.PositionChanged += (_, at) => reached.Enqueue(at.Line);
        player.Play(Of(64), TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => !reached.IsEmpty));

        int before = reached.Count;
        player.JumpTo(new TrackerPosition(0, 40));

        Assert.True(Until(() => reached.Count > before));

        Assert.Equal(40, reached.Skip(before).First());
        Assert.True(player.IsPlaying);
    }

    /// <summary>A line past the end of the pattern is held to its last line.</summary>
    [Fact]
    public void A_line_past_the_end_is_held_to_the_last()
    {
        using var player = new TrackerPlayer(new SilentAudio());
        var reached = new ConcurrentQueue<int>();

        player.PositionChanged += (_, at) => reached.Enqueue(at.Line);
        player.Play(Of(16), TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => !reached.IsEmpty));

        int before = reached.Count;
        player.JumpTo(new TrackerPosition(0, 500));

        Assert.True(Until(() => reached.Count > before));

        Assert.Equal(15, reached.Skip(before).First());
    }

    /// <summary>A line before the start is held to the first.</summary>
    [Fact]
    public void A_line_before_the_start_is_held_to_the_first()
    {
        using var player = new TrackerPlayer(new SilentAudio());
        var reached = new ConcurrentQueue<int>();

        player.PositionChanged += (_, at) => reached.Enqueue(at.Line);
        player.Play(Of(16), new TrackerPosition(0, 8), TrackerPlayMode.Pattern);

        Assert.True(Until(() => !reached.IsEmpty));

        int before = reached.Count;
        player.JumpTo(new TrackerPosition(0, -5));

        Assert.True(Until(() => reached.Count > before));

        Assert.Equal(0, reached.Skip(before).First());
    }

    /// <summary>Moving a song that is not playing does nothing and does not start it.</summary>
    [Fact]
    public void A_stopped_song_is_not_started()
    {
        using var player = new TrackerPlayer(new SilentAudio());

        player.JumpTo(new TrackerPosition(0, 10));

        Assert.False(player.IsPlaying);
    }
}
