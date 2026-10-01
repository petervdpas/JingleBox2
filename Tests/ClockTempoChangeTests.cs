using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The MIDI clock this program sends an external synth following a tempo change while playing,
/// whether from a tempo lane, a knob or the tempo box.
/// </summary>
/// <remarks>
/// The ticks used to be counted from the moment play was pressed at whatever the tempo was now, so
/// a change rewrote the whole count at once: faster sent a burst of every tick the new tempo said
/// was owed since the start, slower sent nothing until the clock caught up. A synth following it
/// rushed or stalled. These say the ticks go on evenly at the new rate from the moment of the
/// change. Wide margins, since this runs on build machines that may be doing anything.
/// </remarks>
public class ClockTempoChangeTests
{
    /// <summary>A deck that writes down when each batch of ticks was asked for, and how many.</summary>
    private sealed class Stamps : IMidiClockDeck
    {
        /// <summary>When it was made.</summary>
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        /// <summary>Each batch: when, and how many ticks.</summary>
        public readonly ConcurrentQueue<(double Ms, int Count)> Batches = new();

        /// <inheritdoc/>
        public bool IsDriving => true;

        /// <inheritdoc/>
        public void Drive(IReadOnlyList<string>? outputs) { }

        /// <inheritdoc/>
        public void Play(int line, int linesPerBeat) { }

        /// <inheritdoc/>
        public void Ticks(int howMany) => Batches.Enqueue((_clock.Elapsed.TotalMilliseconds, howMany));

        /// <inheritdoc/>
        public void Halt() { }

        /// <inheritdoc/>
        public void Begin() { }

        /// <inheritdoc/>
        public void Resume() { }

        /// <inheritdoc/>
        public void Place(int at) { }
    }

    /// <summary>One pattern at the tempo given.</summary>
    private static Song At(double bpm)
    {
        var song = new Song { Bpm = bpm, LinesPerBeat = 4 };

        song.Patterns.Add(new Pattern(64, song.TrackCount) { Name = "P" });
        song.Order.Add(0);
        song.Normalize();

        return song;
    }

    /// <summary>Plays for a while, moving the tempo partway, and gives back the batches of ticks.</summary>
    private static (double Ms, int Count)[] Ticked(double from, double to, int ms)
    {
        var deck = new Stamps();

        using var player = new TrackerPlayer(new SilentAudio()) { Loop = true, ClockDeck = deck };

        player.Play(At(from), TrackerPosition.Start, TrackerPlayMode.Pattern);
        Thread.Sleep(300);
        player.PlayAt(to);
        Thread.Sleep(ms - 300);
        player.Stop();

        return deck.Batches.ToArray();
    }

    /// <summary>Speeding up sends no burst: the ticks go on one or two at a time.</summary>
    [Fact]
    public void Speeding_up_sends_no_burst()
    {
        var batches = Ticked(60, 240, 1600);

        Assert.True(batches.Length > 20, "too few ticks to judge: " + batches.Length);
        Assert.True(batches.Max(one => one.Count) <= 3,
            "a burst of " + batches.Max(one => one.Count) + " ticks at once");
    }

    /// <summary>Slowing down leaves no silence: the longest gap is about one tick at the new rate.</summary>
    [Fact]
    public void Slowing_down_leaves_no_silence()
    {
        var batches = Ticked(240, 60, 1400);
        double longest = batches.Zip(batches.Skip(1), (a, b) => b.Ms - a.Ms).Max();

        Assert.True(longest < 90, "the clock stopped for " + longest.ToString("0") + " ms");
    }

    /// <summary>After the change the ticks come at the new rate: 60 to the minute is 24 a second.</summary>
    [Fact]
    public void After_slowing_the_rate_is_the_new_tempo()
    {
        var batches = Ticked(240, 60, 2400);
        double since = batches[0].Ms + 1000;
        var after = batches.Where(one => one.Ms >= since).ToArray();
        double seconds = (after[^1].Ms - after[0].Ms) / 1000;
        double rate = (after.Sum(one => one.Count) - after[0].Count) / seconds;

        Assert.InRange(rate, 20, 28);
    }
}
