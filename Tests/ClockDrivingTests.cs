using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Whether the transport really drives another machine's clock: the ticks, the rate, and the two
/// messages either side of them.
/// </summary>
/// <remarks>
/// The arithmetic and the bytes are each pinned on their own elsewhere. What is left, and what
/// only a running transport can say, is that the two are joined: that ticks come out of the
/// thread that keeps time, at the rate the tempo asks for, and that starting and stopping are
/// announced.
///
/// A real <see cref="TrackerPlayer"/> over a silent engine, so there is a clock thread and no
/// hardware. Fast songs, so a test is a moment rather than a wait.
/// </remarks>
public class ClockDrivingTests
{
    /// <summary>How long to wait for the clock to do something before giving up.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>A deck that writes down what it was told rather than sending anything.</summary>
    private sealed class Bench : IMidiClockDeck
    {
        /// <summary>How many ticks have been asked for.</summary>
        private int _ticks;

        /// <summary>What was said, in order, which is what most of these read.</summary>
        public readonly List<string> Said = new();

        /// <inheritdoc/>
        /// <remarks>True always, since a bench with nothing to drive would be tested by nothing.</remarks>
        public bool IsDriving => true;

        /// <summary>How many ticks have been asked for so far.</summary>
        public int Ticked => Volatile.Read(ref _ticks);

        /// <inheritdoc/>
        public void Drive(IReadOnlyList<string>? outputs)
        {
        }

        /// <inheritdoc/>
        public void Play(int line, int linesPerBeat)
        {
            lock (Said) Said.Add("play " + line + "/" + linesPerBeat);
        }

        /// <inheritdoc/>
        public void Ticks(int howMany) => Interlocked.Add(ref _ticks, howMany);

        /// <inheritdoc/>
        public void Halt()
        {
            lock (Said) Said.Add("halt");
        }

        /// <summary>What was said, as one string.</summary>
        public string Words
        {
            get { lock (Said) return string.Join(", ", Said); }
        }
    }

    /// <summary>A song of that many lines, at a tempo and a division worth measuring.</summary>
    private static Song Of(int lines, double bpm = 120, int linesPerBeat = 4)
    {
        var song = new Song { Bpm = bpm, LinesPerBeat = linesPerBeat };

        song.Patterns.Add(new Pattern(lines, song.TrackCount) { Name = "P" });
        song.Order.Add(0);
        song.Normalize();

        return song;
    }

    /// <summary>Waits for something to become true, or gives up.</summary>
    private static bool Until(Func<bool> said, TimeSpan? within = null)
    {
        var clock = Stopwatch.StartNew();

        while (clock.Elapsed < (within ?? Patience))
        {
            if (said()) return true;

            Thread.Sleep(2);
        }

        return said();
    }

    /// <summary>Nothing is sent when nothing is driven, which is the ordinary machine.</summary>
    /// <remarks>
    /// The deck is asked whether it drives anything before any arithmetic is done, so a machine
    /// that has never opened the MIDI page pays one comparison a line. Here the deck says no and
    /// must be left entirely alone.
    /// </remarks>
    [Fact]
    public void A_deck_driving_nothing_is_not_asked_for_ticks()
    {
        var bench = new Bench();

        using var player = new TrackerPlayer(new SilentAudio()) { Loop = true };

        player.ClockDeck = null;

        player.Play(Of(16), TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => player.Position.Line > 2), "the clock never moved");

        player.Stop();

        Assert.Equal(0, bench.Ticked);
        Assert.Equal("", bench.Words);
    }

    /// <summary>Starting from the top says so, and says which line it began on.</summary>
    [Fact]
    public void Starting_is_announced_with_the_line_it_began_on()
    {
        var bench = new Bench();

        using var player = new TrackerPlayer(new SilentAudio()) { Loop = true, ClockDeck = bench };

        player.Play(Of(16), TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => bench.Words.Contains("play")), "the start was never announced");
        Assert.StartsWith("play 0/4", bench.Words);

        player.Stop();
    }

    /// <summary>And stopping says stop, so nothing following is left running.</summary>
    /// <remarks>
    /// The one that matters most of these: a slave told nothing goes on playing for ever, which
    /// is worse than one told twice.
    /// </remarks>
    [Fact]
    public void Stopping_is_announced()
    {
        var bench = new Bench();

        using var player = new TrackerPlayer(new SilentAudio()) { Loop = true, ClockDeck = bench };

        player.Play(Of(16), TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => player.Position.Line > 0), "the clock never moved");

        player.Stop();

        Assert.Contains("halt", bench.Words);
    }

    /// <summary>Ticks come out of the pass at all, which is the whole of the wiring.</summary>
    [Fact]
    public void Ticks_come_out_of_a_running_pass()
    {
        var bench = new Bench();

        using var player = new TrackerPlayer(new SilentAudio()) { Loop = true, ClockDeck = bench };

        player.Play(Of(64, bpm: 240), TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => bench.Ticked > 8), "no ticks left the pass: " + bench.Ticked);

        player.Stop();
    }

    /// <summary>
    /// And they come out at the rate the tempo asks for rather than at the rate of the lines.
    /// </summary>
    /// <remarks>
    /// **The assertion the whole feature rests on.** Twenty four ticks to the quarter note means
    /// 240 to the minute is 96 a second, whatever the lines to the beat: a tick count that
    /// followed the lines instead would be four times under at four to the beat and twice under
    /// at eight, and both would read at the other end as a tempo that is simply wrong.
    ///
    /// Measured over a stretch rather than at an instant, and given a wide band, because this
    /// runs on a build machine that may be doing anything: what is refused is being out by the
    /// factor a per-line count would give, not being a few per cent slow.
    /// </remarks>
    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void Ticks_follow_the_tempo_and_not_the_lines(int linesPerBeat)
    {
        var bench = new Bench();

        using var player = new TrackerPlayer(new SilentAudio()) { Loop = true, ClockDeck = bench };

        player.Play(Of(256, bpm: 240, linesPerBeat: linesPerBeat),
                    TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => bench.Ticked > 0), "the pass never ticked");

        int from = bench.Ticked;
        var clock = Stopwatch.StartNew();

        Thread.Sleep(500);

        int over = bench.Ticked - from;
        double seconds = clock.Elapsed.TotalSeconds;

        player.Stop();

        double rate = over / seconds;

        Assert.InRange(rate, 60, 130);
    }

    /// <summary>Starting further down the song says where, so a slave does not begin at its own top.</summary>
    /// <remarks>
    /// What the deck does with the line is its own business and is pinned on the deck; what is
    /// said here is that the line reaches it at all, since a start announced as nought from
    /// halfway down is the whole desk a chorus out.
    /// </remarks>
    [Fact]
    public void Starting_further_down_says_where()
    {
        var bench = new Bench();

        using var player = new TrackerPlayer(new SilentAudio()) { Loop = true, ClockDeck = bench };

        player.Play(Of(64), new TrackerPosition(0, 32), TrackerPlayMode.Pattern);

        Assert.True(Until(() => bench.Words.Contains("play")), "the start was never announced");
        Assert.StartsWith("play 32/4", bench.Words);

        player.Stop();
    }

    /// <summary>A pass that is stopped and started again ticks again rather than going quiet.</summary>
    /// <remarks>
    /// The tick count is the clock thread's and is reset where its stopwatch is started, so a
    /// second pass beginning with a count left over from the first would be told a great many
    /// ticks were already sent and would send none until it caught up.
    /// </remarks>
    [Fact]
    public void A_second_pass_ticks_like_the_first()
    {
        var bench = new Bench();

        using var player = new TrackerPlayer(new SilentAudio()) { Loop = true, ClockDeck = bench };

        player.Play(Of(64, bpm: 240), TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => bench.Ticked > 8), "the first pass did not tick");

        player.Stop();

        int after = bench.Ticked;

        player.Play(Of(64, bpm: 240), TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => bench.Ticked > after + 8), "the second pass did not tick");

        player.Stop();
    }
}
