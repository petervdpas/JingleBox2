using Avalonia.Media;
using JingleBox2.Views;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The colour a playing pad wears, which is its own walking through the ones beside it.
/// </summary>
/// <remarks>
/// What has to be true is that it is still that pad. A walk that went far enough to reach
/// another pad's colour would say the wrong pad was playing, and one that came home only at the
/// ends would leave a pad wearing something else for most of a show.
/// </remarks>
public class PulseColourTests
{
    /// <summary>The rule under test.</summary>
    private readonly PulseColour _pulse = new();

    /// <summary>A teal, which is what the cyan pad on the palette is.</summary>
    private static readonly Color Teal = Color.Parse("#00ACC1");

    /// <summary>At the start of the cycle a pad is exactly itself.</summary>
    [Fact]
    public void The_walk_starts_at_home()
    {
        Assert.Equal(Teal, _pulse.At(Teal, 0));
    }

    /// <summary>And halfway round, which is what makes it a there and back.</summary>
    [Fact]
    public void And_comes_home_at_the_middle()
    {
        var middle = _pulse.At(Teal, 0.5);

        Assert.InRange(middle.R, Teal.R - 1, Teal.R + 1);
        Assert.InRange(middle.G, Teal.G - 1, Teal.G + 1);
        Assert.InRange(middle.B, Teal.B - 1, Teal.B + 1);
    }

    /// <summary>A quarter of the way round it is somewhere else, or nothing is moving.</summary>
    [Fact]
    public void A_quarter_of_the_way_it_has_moved()
    {
        Assert.NotEqual(Teal, _pulse.At(Teal, 0.25));
    }

    /// <summary>The two quarters go opposite ways, which is what makes it a swing.</summary>
    [Fact]
    public void The_two_halves_go_opposite_ways()
    {
        double up = _pulse.At(Teal, 0.25).ToHsv().H;
        double down = _pulse.At(Teal, 0.75).ToHsv().H;
        double home = Teal.ToHsv().H;

        Assert.True(up > home, "the first quarter walks one way");
        Assert.True(down < home, "the third quarter walks the other");
    }

    /// <summary>It stays near enough that the pad is still the pad.</summary>
    /// <remarks>
    /// Twenty two degrees is about the width of one colour on the palette, so a red pad reaches
    /// towards orange and never arrives: the colours on that palette are forty five apart.
    /// </remarks>
    [Fact]
    public void It_never_reaches_the_next_colour_along()
    {
        double home = Teal.ToHsv().H;

        for (int step = 0; step <= 100; step++)
        {
            double hue = _pulse.At(Teal, step / 100.0).ToHsv().H;

            Assert.True(System.Math.Abs(hue - home) <= 23, "walked " + (hue - home) + " degrees");
        }
    }

    /// <summary>A colour at the top of the wheel walks past nought rather than sticking there.</summary>
    /// <remarks>
    /// Red sits at hue nought, so half its walk is a negative number and half is past 360. Both
    /// have to come back as a hue somebody can see, and the arithmetic that forgets it is the
    /// classic one: a red pad that walked to nought and stopped would flatten at one end of every
    /// cycle.
    /// </remarks>
    [Fact]
    public void Red_walks_across_the_top_of_the_wheel()
    {
        var red = Color.Parse("#E53935");

        var back = _pulse.At(red, 0.75);

        Assert.InRange(back.ToHsv().H, 330, 360);
    }

    /// <summary>A pad with no colour of its own is grey, and still moves.</summary>
    /// <remarks>
    /// Grey has no hue to walk, so the brightness is what says it is playing. Without it those
    /// pads would be the only ones on the bank that said nothing.
    /// </remarks>
    [Fact]
    public void A_grey_pad_still_moves()
    {
        var grey = Color.FromRgb(80, 80, 80);

        var lit = _pulse.At(grey, 0.25);

        Assert.True(lit.R > grey.R, "a grey pad brightens rather than sitting there");
    }

    /// <summary>Black stays black rather than turning into a colour.</summary>
    /// <remarks>
    /// It has no saturation to work with either, so all it can do is lift, which is what an
    /// unlit pad on a dark theme should do.
    /// </remarks>
    [Fact]
    public void Black_only_lifts()
    {
        var black = Color.FromRgb(0, 0, 0);

        var lit = _pulse.At(black, 0.25);

        Assert.Equal(lit.R, lit.G);
        Assert.Equal(lit.G, lit.B);
    }

    /// <summary>What it hands back is opaque, since the pad underneath it is not what shows.</summary>
    [Fact]
    public void It_keeps_the_alpha_it_was_given()
    {
        Assert.Equal(255, _pulse.At(Teal, 0.3).A);
    }
}
