using JingleBox2.UI;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Lengths and places held to whole pixels of whatever screen is really being drawn on.
/// </summary>
/// <remarks>
/// **The rule exists for the scalings nobody develops at.** Every test here is written at more
/// than one of them for that reason: a row of 18 comes out whole at 100% and at 150% and lands
/// between two pixels at 125%, so a test taken at one scaling reports that everything is fine
/// and says nothing whatever about the machine the fault was reported on.
/// </remarks>
public sealed class DevicePixelsTests
{
    /// <summary>A row that already lands on pixels is left exactly where it is.</summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void A_row_that_lands_on_pixels_is_left_alone(double scaling) =>
        Assert.Equal(18, new DevicePixels().Whole(18, scaling), 9);

    /// <summary>And one that falls between two of them is held to the nearer.</summary>
    [Fact]
    public void A_row_between_two_pixels_is_held_to_one()
    {
        double held = new DevicePixels().Whole(18, 1.25);

        Assert.Equal(18.4, held, 9);
        Assert.Equal(23, held * 1.25, 9);
    }

    /// <summary>
    /// **Which is the whole of what it is for: every step is the same distance.**
    /// </summary>
    /// <remarks>
    /// The fault said as a test. Twenty rows at 125%: unheld they are 22.5 pixels apart, so the
    /// pattern moves 22 and then 23 under the playhead for ever and every glyph on it is drawn
    /// at a different fraction of a pixel each time. Held, every step is 23.
    /// </remarks>
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    public void Every_step_down_the_pattern_is_the_same_whole_number_of_pixels(double scaling)
    {
        double row = new DevicePixels().Whole(18, scaling);
        double step = row * scaling;

        Assert.Equal(step, System.Math.Round(step), 9);

        for (int line = 0; line < 20; line++)
            Assert.Equal(step, (line + 1) * row * scaling - line * row * scaling, 9);
    }

    /// <summary>A row of no height is a pattern nobody can see, so a length is never nought.</summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void A_row_is_never_nothing(double scaling)
    {
        var pixels = new DevicePixels();

        Assert.Equal(1 / scaling, pixels.Whole(0, scaling), 9);
        Assert.Equal(1 / scaling, pixels.Whole(0.2, scaling), 9);
    }

    /// <summary>The top of a pattern is nought and stays nought, which a length may not.</summary>
    [Fact]
    public void The_top_of_a_pattern_stays_the_top() =>
        Assert.Equal(0, new DevicePixels().Lands(0, 1.25), 9);

    /// <summary>A place between two pixels lands on one of them.</summary>
    [Fact]
    public void A_place_between_two_pixels_lands_on_one()
    {
        double landed = new DevicePixels().Lands(10.3, 1.25);

        Assert.Equal(13, landed * 1.25, 9);
    }

    /// <summary>
    /// Half a pixel goes the same way every time, which is what makes the rows equal.
    /// </summary>
    /// <remarks>
    /// To the even pixel, 22.5 and 23.5 answer 22 and 24, so two rows of the same height would
    /// come out a pixel apart depending only on where each happened to fall.
    /// </remarks>
    [Fact]
    public void Half_a_pixel_always_goes_the_same_way()
    {
        var pixels = new DevicePixels();

        Assert.Equal(23, pixels.Whole(18, 1.25) * 1.25, 9);
        Assert.Equal(24, pixels.Whole(18.8, 1.25) * 1.25, 9);
    }

    /// <summary>A screen nobody has asked about yet changes nothing at all.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1.5)]
    [InlineData(double.NaN)]
    public void A_screen_nobody_has_asked_about_changes_nothing(double scaling)
    {
        var pixels = new DevicePixels();

        Assert.Equal(18.3, pixels.Whole(18.3, scaling), 9);
        Assert.Equal(18.3, pixels.Lands(18.3, scaling), 9);
    }

    /// <summary>And a length that is not a number cannot be put on a pixel.</summary>
    [Fact]
    public void A_length_that_is_not_a_number_is_handed_back()
    {
        var pixels = new DevicePixels();

        Assert.Equal(double.NaN, pixels.Whole(double.NaN, 1.25));
        Assert.Equal(double.PositiveInfinity, pixels.Lands(double.PositiveInfinity, 1.25));
    }
}
