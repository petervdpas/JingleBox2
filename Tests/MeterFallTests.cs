using JingleBox2.UI;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The mark riding the loudest recent moment, and the fall that takes it back down.
/// </summary>
/// <remarks>
/// The fall was always here. What was missing was anybody asking for it: it is worked out while
/// the meter draws, and a meter draws when a value changes, so the moment the levels stopped
/// arriving the mark stayed where the loudest moment had left it. These say what the fall is
/// meant to do, so the control above them is only about asking to be drawn again.
/// </remarks>
public class MeterFallTests
{
    private const double Hold = 1.2;
    private const double PerSecond = 20;

    [Fact]
    public void A_mark_is_held_before_it_starts_to_drop()
    {
        Assert.Equal(1.0, MeterScale.DecayPeak(1.0, 0, Hold, Hold, PerSecond));
    }

    [Fact]
    public void And_then_falls_at_the_stated_rate()
    {
        // One second past the hold is twenty decibels down, which is a tenth of the amplitude.
        double after = MeterScale.DecayPeak(1.0, 0, Hold + 1, Hold, PerSecond);

        Assert.Equal(0.1, after, 3);
    }

    /// <summary>
    /// And reaches the floor, which is what stops the control asking for another frame. A mark
    /// that only ever approached nought would keep the window drawing for as long as it was open.
    /// </summary>
    [Fact]
    public void And_reaches_the_floor_it_is_drawn_against()
    {
        double after = MeterScale.DecayPeak(1.0, 0, Hold + 4, Hold, PerSecond);

        Assert.Equal(0, MeterScale.Position(after));
    }

    /// <summary>A level louder than the mark takes it over at once, with no fall involved.</summary>
    [Fact]
    public void A_louder_level_takes_the_mark_with_it()
    {
        Assert.Equal(0.8, MeterScale.DecayPeak(0.5, 0.8, 10, Hold, PerSecond));
    }
}
