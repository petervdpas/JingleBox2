using System;
using JingleBox2.Midi;
using JingleBox2.Midi.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The tempo a followed clock is running at, worked out from when its ticks arrive.
/// </summary>
/// <remarks>
/// Fed made-up moments rather than a real clock, so a tempo can be asked about without waiting
/// for it. A stopwatch second here is a million ticks.
/// </remarks>
public class ClockTempoTests
{
    private const long Second = 1_000_000;

    /// <summary>Two beats of ticks at a steady tempo say that tempo, to a tenth.</summary>
    [Theory]
    [InlineData(120.0)]
    [InlineData(108.0)]
    [InlineData(97.5)]
    [InlineData(240.0)]
    public void Steady_ticks_say_their_tempo(double bpm)
    {
        var tempo = new ClockTempo(Second);

        double? heard = Feed(tempo, bpm, ClockTempo.Window + 1, 0);

        Assert.Equal(bpm, heard);
    }

    /// <summary>Nothing is said before there are enough ticks to say it.</summary>
    [Fact]
    public void Too_few_ticks_say_nothing()
    {
        var tempo = new ClockTempo(Second);

        Assert.Null(Feed(tempo, 120, ClockTempo.Window, 0));
    }

    /// <summary>A millisecond of wobble on every tick does not move the tempo off the device's own number.</summary>
    [Fact]
    public void Jitter_does_not_move_the_tempo()
    {
        var tempo = new ClockTempo(Second);
        var wobble = new Random(7);
        double tick = 60.0 / 108 / 24 * Second;

        double? said = null;

        for (int i = 0; i < ClockTempo.Window * 10; i++)
        {
            long at = (long)(i * tick) + wobble.Next(-1000, 1001);

            if (tempo.Heard(at) is { } now)
            {
                Assert.Null(said);
                said = now;
            }
        }

        Assert.Equal(108.0, said);
    }

    /// <summary>A change of tempo is said once it has settled, and saying the same tempo twice is not done.</summary>
    [Fact]
    public void A_new_tempo_is_said_once()
    {
        var tempo = new ClockTempo(Second);

        long at = 0;
        Assert.Equal(108.0, Feed(tempo, 108, ClockTempo.Window * 3, 0, ref at));
        Assert.Null(Feed(tempo, 108, ClockTempo.Window * 3, at, ref at));
        Assert.Equal(110.0, Feed(tempo, 110, ClockTempo.Window * 3, at, ref at));
    }

    /// <summary>A gap longer than a tick could ever be starts the measuring again, and so does time going backwards.</summary>
    [Fact]
    public void A_gap_or_time_backwards_starts_again()
    {
        var tempo = new ClockTempo(Second);

        long at = 0;
        Feed(tempo, 120, ClockTempo.Window / 2, 0, ref at);

        Assert.Null(Feed(tempo, 120, ClockTempo.Window / 2 + 2, at + Second * 5, ref at));

        Assert.Null(tempo.Heard(at - Second));
        Assert.Null(tempo.Heard(at - Second));
    }

    /// <summary>Starting again forgets what was measured, and an absurd rate is never said.</summary>
    [Fact]
    public void Forgetting_and_nonsense()
    {
        var tempo = new ClockTempo(Second);

        long at = 0;
        Feed(tempo, 120, ClockTempo.Window / 2, 0, ref at);
        tempo.Forget();

        Assert.Null(Feed(tempo, 120, ClockTempo.Window / 2 + 1, at, ref at));

        var fast = new ClockTempo(Second);
        Assert.Null(Feed(fast, 5000, ClockTempo.Window * 2, 0));

        var same = new ClockTempo(Second);
        for (int i = 0; i < ClockTempo.Window * 2; i++) Assert.Null(same.Heard(42));
    }

    private static double? Feed(IClockTempo tempo, double bpm, int ticks, long from)
    {
        long ignored = 0;
        return Feed(tempo, bpm, ticks, from, ref ignored);
    }

    /// <summary>Feeds ticks at a tempo and answers the last tempo said, leaving the moment after the last tick.</summary>
    private static double? Feed(IClockTempo tempo, double bpm, int ticks, long from, ref long next)
    {
        double tick = 60.0 / bpm / 24 * Second;
        double? said = null;

        for (int i = 0; i < ticks; i++)
            if (tempo.Heard(from + (long)Math.Round(i * tick)) is { } now) said = now;

        next = from + (long)Math.Round(ticks * tick);

        return said;
    }
}
