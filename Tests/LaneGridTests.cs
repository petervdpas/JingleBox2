using JingleBox2.UI;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>The value grid of an automation lane: its marked lines, and where a drawn point lands.</summary>
public class LaneGridTests
{
    /// <summary>The rule.</summary>
    private readonly LaneGrid _grid = new();

    /// <summary>A tempo from 20 to 400 is marked every 50 beats a minute.</summary>
    [Fact]
    public void A_tempo_is_marked_every_fifty()
    {
        Assert.Equal(new[] { 50.0, 100, 150, 200, 250, 300, 350, 400 }, _grid.Lines(20, 400));
    }

    /// <summary>A pan from minus one to one is marked every half, nought included exactly.</summary>
    [Fact]
    public void A_pan_is_marked_every_half()
    {
        Assert.Equal(new[] { -1.0, -0.5, 0, 0.5, 1 }, _grid.Lines(-1, 1));
    }

    /// <summary>A range of nothing, backwards or not a number has no lines.</summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 1)]
    [InlineData(double.NaN, 1)]
    public void A_range_of_nothing_has_no_lines(double min, double max)
    {
        Assert.Empty(_grid.Lines(min, max));
    }

    /// <summary>Close to a line, a point lands on it.</summary>
    [Fact]
    public void Close_to_a_line_it_lands_on_it()
    {
        Assert.Equal(100, _grid.Snap(104, 20, 400, 6));
        Assert.Equal(150, _grid.Snap(146.2, 20, 400, 6));
    }

    /// <summary>Away from a line, a tempo lands on a whole beat a minute rather than wherever the pointer was.</summary>
    [Fact]
    public void Away_from_a_line_a_tempo_lands_on_a_whole_number()
    {
        Assert.Equal(127, _grid.Snap(127.259, 20, 400, 6));
        Assert.Equal(100, _grid.Snap(99.846, 20, 400, 0));
    }

    /// <summary>With no reach nothing is pulled to a line, only rounded.</summary>
    [Fact]
    public void No_reach_only_rounds()
    {
        Assert.Equal(104, _grid.Snap(104.2, 20, 400, 0));
    }

    /// <summary>Past either end lands on the end, and not a number on the bottom.</summary>
    [Theory]
    [InlineData(9999, 400)]
    [InlineData(-5, 20)]
    [InlineData(double.NaN, 20)]
    public void Past_the_ends_lands_on_them(double value, double expected)
    {
        Assert.Equal(expected, _grid.Snap(value, 20, 400, 6));
    }

    /// <summary>A line outside the range is never landed on: the point is rounded instead.</summary>
    [Fact]
    public void A_line_outside_the_range_is_not_landed_on()
    {
        Assert.Equal(21, _grid.Snap(21.3, 20, 400, 40));
    }
}
