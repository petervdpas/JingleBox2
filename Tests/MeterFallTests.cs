using JingleBox2.Rack.Ui;
using Xunit;
using JingleBox2.Rack.Ui.Interfaces;

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
    /// <summary>Where a level sits on a meter, which is decibels rather than amplitude.</summary>
    private readonly IMeterScale _scale = new MeterScale();

    /// <summary>How long the mark sits still before it starts down, in seconds.</summary>
    private const double Hold = 1.2;

    /// <summary>The rate of the fall in decibels a second, which is the meter's own.</summary>
    private const double PerSecond = 20;

    /// <summary>
    /// Nothing moves inside the hold, which is what makes the mark readable: a peak that began
    /// falling the instant it was set would be gone before anybody had looked at it.
    /// </summary>
    [Fact]
    public void A_mark_is_held_before_it_starts_to_drop()
    {
        Assert.Equal(1.0, _scale.DecayPeak(1.0, 0, Hold, Hold, PerSecond));
    }

    /// <summary>The fall is in decibels, not in a fraction of the bar.</summary>
    /// <remarks>
    /// One second past the hold is twenty decibels down, which is a tenth of the amplitude.
    /// </remarks>
    [Fact]
    public void And_then_falls_at_the_stated_rate()
    {
        double after = _scale.DecayPeak(1.0, 0, Hold + 1, Hold, PerSecond);

        Assert.Equal(0.1, after, 3);
    }

    /// <summary>
    /// And reaches the floor, which is what stops the control asking for another frame. A mark
    /// that only ever approached nought would keep the window drawing for as long as it was open.
    /// </summary>
    [Fact]
    public void And_reaches_the_floor_it_is_drawn_against()
    {
        double after = _scale.DecayPeak(1.0, 0, Hold + 4, Hold, PerSecond);

        Assert.Equal(0, _scale.Position(after));
    }

    /// <summary>
    /// A level louder than the mark takes it over at once, with no fall involved.
    /// </summary>
    [Fact]
    public void A_louder_level_takes_the_mark_with_it()
    {
        Assert.Equal(0.8, _scale.DecayPeak(0.5, 0.8, 10, Hold, PerSecond));
    }
}
