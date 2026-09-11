using JingleBox2.Rack.Controls;
using JingleBox2.Rack.Controls.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which pixel a reading lands on, which is what a meter is allowed to repaint for.
/// </summary>
/// <remarks>
/// Read off a profile of the running application rather than guessed at: with a song playing,
/// the busiest thread in the whole program was the drawing loop, and a page of meters repainting
/// on every reading is most of what it was doing. A reading arrives twenty times a second and
/// moves in the fourth decimal; the bar is a few dozen pixels long. So the question a meter asks
/// is not whether the number changed but whether anybody could see that it had.
/// </remarks>
public sealed class MeterStepTests
{
    /// <summary>The rule under test.</summary>
    private readonly IMeterScale _scale = new MeterScale();

    /// <summary>A meter's length, in pixels, of the order a mixer strip really has.</summary>
    private const double Bar = 120;

    /// <summary>**Two readings a hair apart land on the same pixel, so nothing is redrawn.**</summary>
    [Fact]
    public void A_reading_that_moves_nothing_lands_on_the_same_pixel()
    {
        Assert.Equal(
            _scale.Step(0.5, -60, Bar),
            _scale.Step(0.5001, -60, Bar));
    }

    /// <summary>And two that are really apart do not.</summary>
    [Fact]
    public void A_reading_that_moves_the_bar_lands_somewhere_else()
    {
        Assert.NotEqual(
            _scale.Step(0.2, -60, Bar),
            _scale.Step(0.6, -60, Bar));
    }

    /// <summary>Silence is the floor of the meter.</summary>
    [Fact]
    public void Silence_is_the_bottom()
    {
        Assert.Equal(0, _scale.Step(0, -60, Bar));
    }

    /// <summary>And full scale is the top of it.</summary>
    [Fact]
    public void Full_scale_is_the_top()
    {
        Assert.Equal((int)Bar, _scale.Step(1, -60, Bar));
    }

    /// <summary>A longer meter tells apart readings a shorter one cannot.</summary>
    /// <remarks>
    /// Which is the whole reason the length is asked for rather than a tolerance being picked:
    /// what counts as a visible difference is a fact about the bar on the screen.
    /// </remarks>
    [Fact]
    public void A_longer_meter_sees_more()
    {
        Assert.Equal(_scale.Step(0.5, -60, 8), _scale.Step(0.52, -60, 8));
        Assert.NotEqual(_scale.Step(0.5, -60, 2000), _scale.Step(0.52, -60, 2000));
    }

    /// <summary>A meter with no room on the screen has nothing to redraw for.</summary>
    [Fact]
    public void A_meter_with_no_room_stays_at_nought()
    {
        Assert.Equal(0, _scale.Step(0.8, -60, 0));
        Assert.Equal(0, _scale.Step(0.8, -60, 0.4));
    }

    /// <summary>Nothing that is not a number reaches the arithmetic.</summary>
    [Fact]
    public void A_reading_that_is_not_a_number_stays_at_nought()
    {
        Assert.Equal(0, _scale.Step(double.NaN, -60, Bar));
        Assert.Equal(0, _scale.Step(0.5, -60, double.NaN));
    }

    /// <summary>The floor of the scale decides where a quiet reading lands.</summary>
    [Fact]
    public void The_meters_own_floor_decides()
    {
        Assert.NotEqual(
            _scale.Step(0.02, -60, Bar),
            _scale.Step(0.02, -30, Bar));
    }
}
