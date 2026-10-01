using System;
using System.Linq;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Synth;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// <c>Sxx</c>: a recording started partway in rather than at its start, so one long break can be
/// played as many hits.
/// </summary>
public class SampleOffsetTests
{
    /// <summary>The rate everything renders at.</summary>
    private const int Rate = 44100;

    /// <summary>The window moves its start that share of the way through itself.</summary>
    [Theory]
    [InlineData(0.0, 0.0, 1.0, 0.0)]
    [InlineData(0.5, 0.0, 1.0, 0.5)]
    [InlineData(0.5, 0.2, 0.6, 0.4)]
    [InlineData(0.25, 0.0, 0.8, 0.2)]
    public void The_start_moves_through_the_window(double share, double start, double end, double expected)
    {
        var moved = new SampleShape { Start = start, End = end }.Skipping(share);

        Assert.Equal(expected, moved.Start, 6);
        Assert.Equal(end, moved.End, 6);
    }

    /// <summary>Played backwards the window starts at its end, so the end is what moves.</summary>
    [Fact]
    public void Backwards_the_end_moves()
    {
        var moved = new SampleShape { Start = 0, End = 1, Reverse = true }.Skipping(0.25);

        Assert.Equal(0, moved.Start, 6);
        Assert.Equal(0.75, moved.End, 6);
    }

    /// <summary>Nothing, nought and not a number leave the window as it is, the very same one.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(double.NaN)]
    public void No_offset_is_the_same_window(double share)
    {
        var shape = new SampleShape { Start = 0.1, End = 0.9 };

        Assert.Same(shape, shape.Skipping(share));
    }

    /// <summary>An offset of all of it still leaves something to play.</summary>
    [Fact]
    public void A_whole_offset_leaves_something_to_play()
    {
        var moved = new SampleShape().Skipping(1.0);

        Assert.True(moved.End > moved.Start);
    }

    /// <summary>A loop that began before the new start begins at it.</summary>
    [Fact]
    public void A_loop_before_the_start_begins_at_it()
    {
        var moved = new SampleShape { LoopMode = SampleLoopMode.Forward, LoopStart = 0.1, LoopEnd = 0.9 }.Skipping(0.5);

        Assert.Equal(0.5, moved.LoopStart, 6);
        Assert.Equal(0.9, moved.LoopEnd, 6);
    }

    /// <summary>The original is never touched, since it belongs to an instrument somebody is editing.</summary>
    [Fact]
    public void The_original_is_left_alone()
    {
        var shape = new SampleShape();

        shape.Skipping(0.5);

        Assert.Equal(0, shape.Start);
    }

    /// <summary>
    /// A recording silent for its first half and loud for its second: from the start the first
    /// stretch rendered is silent, and from halfway it sounds at once.
    /// </summary>
    [Fact]
    public void Starting_halfway_skips_the_silent_half()
    {
        var samples = new short[Rate];

        for (int at = Rate / 2; at < Rate; at++) samples[at] = (short)(at % 100 < 50 ? 20000 : -20000);

        var sample = new SampleData(samples, 1, Rate);

        Assert.Equal(0f, FirstTenth(sample, 0));
        Assert.True(FirstTenth(sample, 0.5) > 0.1f);
    }

    /// <summary>The loudest sample in the first tenth of a second of a recording, started that far in.</summary>
    private static float FirstTenth(SampleData sample, double offset)
    {
        var mixer = new TrackMixer(Rate);
        var instrument = new TrackerInstrument { Kind = TrackerInstrumentKind.Sample };

        mixer.NoteOn(0, 0, instrument, sample, instrument.BaseNote, 1f, 0f, offset);

        var buffer = new float[Rate / 10 * 2];
        mixer.Render(buffer, Rate / 10);

        return buffer.Max(Math.Abs);
    }
}
