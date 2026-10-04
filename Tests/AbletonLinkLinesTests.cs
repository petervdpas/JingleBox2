using JingleBox2.Sync;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Where a pass begins on an Ableton Link timeline: at once alone, on the next quantum with
/// anybody else there, and never on a beat that is not a number.
/// </summary>
public class AbletonLinkLinesTests
{
    /// <summary>The rule under test.</summary>
    private readonly AbletonLinkLines _lines = new();

    /// <summary>Alone, a program is free to start wherever the timeline is.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(1.3)]
    [InlineData(-2.75)]
    [InlineData(1234.5)]
    public void Alone_a_pass_starts_at_once(double beat)
    {
        Assert.Equal(beat, _lines.StartBeat(beat, 4, peers: 0));
    }

    /// <summary>With anybody else there it waits for the next bar, so bar one lands together.</summary>
    [Theory]
    [InlineData(0.1, 4, 4.0)]
    [InlineData(3.99, 4, 4.0)]
    [InlineData(4.01, 4, 8.0)]
    [InlineData(1.3, 3, 3.0)]
    [InlineData(17.2, 16, 32.0)]
    [InlineData(0.5, 1, 1.0)]
    public void With_peers_a_pass_waits_for_the_next_quantum(double beat, double quantum, double expected)
    {
        Assert.Equal(expected, _lines.StartBeat(beat, quantum, peers: 2));
    }

    /// <summary>A beat already on the boundary is on it, and does not wait a whole bar more.</summary>
    [Theory]
    [InlineData(8.0)]
    [InlineData(8.0000000001)]
    [InlineData(0.0)]
    public void A_beat_on_the_boundary_starts_there(double beat)
    {
        Assert.Equal(System.Math.Round(beat), _lines.StartBeat(beat, 4, peers: 1));
    }

    /// <summary>A timeline behind nought, which a fresh session is for a moment, still lands on a bar.</summary>
    [Theory]
    [InlineData(-1.5, 0.0)]
    [InlineData(-4.0, -4.0)]
    [InlineData(-5.0, -4.0)]
    public void Negative_beats_land_on_a_bar(double beat, double expected)
    {
        Assert.Equal(expected, _lines.StartBeat(beat, 4, peers: 1));
    }

    /// <summary>A beat that is not a number gives one back, so the clock falls back rather than waiting on nothing.</summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void A_beat_that_is_not_a_number_gives_none(double beat)
    {
        Assert.True(double.IsNaN(_lines.StartBeat(beat, 4, peers: 0)));
        Assert.True(double.IsNaN(_lines.StartBeat(beat, 4, peers: 3)));
    }

    /// <summary>A quantum out of a hand-edited settings file is held to something that can be waited for.</summary>
    [Theory]
    [InlineData(4, 4)]
    [InlineData(3, 3)]
    [InlineData(1, 1)]
    [InlineData(16, 16)]
    [InlineData(64, 16)]
    [InlineData(0.5, 4)]
    [InlineData(0, 4)]
    [InlineData(-3, 4)]
    [InlineData(double.NaN, 4)]
    [InlineData(double.PositiveInfinity, 4)]
    public void A_quantum_is_held_to_one_to_sixteen(double asked, double expected)
    {
        Assert.Equal(expected, _lines.Quantum(asked));
    }

    /// <summary>And the held quantum is the one a start waits for, so nonsense waits a bar of four.</summary>
    [Fact]
    public void A_nonsense_quantum_waits_a_bar_of_four()
    {
        Assert.Equal(4.0, _lines.StartBeat(1.2, 0, peers: 1));
        Assert.Equal(4.0, _lines.StartBeat(1.2, double.NaN, peers: 1));
    }
}
