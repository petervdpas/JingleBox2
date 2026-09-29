using JingleBox2.Rack.Controls;
using JingleBox2.Rack.Controls.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A meter scale that runs past 0 dB, which is how every meter here reads: a desk meter.
/// </summary>
public sealed class MeterHeadroomTests
{
    /// <summary>The scale being asked.</summary>
    private readonly IMeterScale _scale = new MeterScale();

    /// <summary>0 dB sits below the top by the headroom, not at it.</summary>
    [Fact]
    public void Zero_sits_below_the_top()
    {
        Assert.Equal(60.0 / 66.0, _scale.Position(1, -60, true, 6), 6);
    }

    /// <summary>A level over 0 dB climbs into the headroom rather than piling up at 0.</summary>
    [Fact]
    public void Over_zero_climbs_into_the_headroom()
    {
        double atZero = _scale.Position(1, -60, true, 6);
        double atThree = _scale.Position(1.4125, -60, true, 6);

        Assert.True(atThree > atZero);
        Assert.True(atThree < 1);
    }

    /// <summary>Past the top of the headroom is the top, and no further.</summary>
    [Fact]
    public void Past_the_headroom_is_the_top()
    {
        Assert.Equal(1, _scale.Position(4, -60, true, 6));
    }

    /// <summary>Without a top of its own the scale still stops at full scale, as it always did.</summary>
    [Fact]
    public void Without_headroom_full_scale_is_the_top()
    {
        Assert.Equal(1, _scale.Position(1));
        Assert.Equal(1, _scale.Position(4));
    }

    /// <summary>A peak mark can be pushed into the headroom, and is held there.</summary>
    [Fact]
    public void A_peak_can_be_pushed_over_zero()
    {
        Assert.Equal(1.5, _scale.DecayPeak(0.5, 1.5, 0, 1.2, 20, 6), 6);
        Assert.Equal(1.0, _scale.DecayPeak(0.5, 1.5, 0, 1.2, 20), 6);
    }

    /// <summary>A peak over 0 dB falls at the same rate as one under it.</summary>
    [Fact]
    public void A_peak_over_zero_falls_at_the_same_rate()
    {
        double fallen = _scale.DecayPeak(1.4125, 0, 1.2 + 0.15, 1.2, 20, 6);

        Assert.Equal(0, _scale.Decibels(fallen, -60, 6), 1);
    }
}
