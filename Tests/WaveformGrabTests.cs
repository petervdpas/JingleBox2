using JingleBox2.Rack.Controls;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which of the four lines on a picture of a recording a press takes hold of.
/// </summary>
/// <remarks>
/// The case this exists for is a loop lying on the window's own ends, which is what a fresh loop
/// does. Deciding by the nearest pixel there takes the loop as often as the window, so the picture
/// is split: the top half takes the window's lines, the bottom half the loop's.
/// </remarks>
public sealed class WaveformGrabTests
{
    /// <summary>The rule under test.</summary>
    private readonly WaveformGrab _grab = new();

    /// <summary>How tall the picture is here.</summary>
    private const double Height = 100;

    /// <summary>How close a press has to be.</summary>
    private const double Reach = 12;

    /// <summary>Asks about a picture with the window from 10 to 300 and the loop lying on it.</summary>
    private int Pressed(double x, double y, bool loop = true, double loopStart = 10, double loopEnd = 300) =>
        _grab.Grabbed(x, y, Height, 10, 300, loopStart, loopEnd, loop, Reach);

    /// <summary>On lines lying on each other, the top half takes the window's end and the bottom half the loop's.</summary>
    [Fact]
    public void Lines_on_each_other_are_told_apart_by_the_half_pressed()
    {
        Assert.Equal(1, Pressed(301, 20));
        Assert.Equal(3, Pressed(301, 80));
        Assert.Equal(0, Pressed(9, 20));
        Assert.Equal(2, Pressed(9, 80));
    }

    /// <summary>A loop line a pixel nearer does not take the press away from the window in the top half.</summary>
    [Fact]
    public void A_nearer_loop_line_does_not_steal_the_top_half()
    {
        Assert.Equal(1, Pressed(296, 10, loopEnd: 295));
    }

    /// <summary>Where the half pressed has nothing in reach, the other half's line is taken.</summary>
    [Fact]
    public void A_press_in_the_wrong_half_still_takes_the_only_line_in_reach()
    {
        Assert.Equal(1, Pressed(300, 90, loopEnd: 150));
        Assert.Equal(3, Pressed(150, 10, loopEnd: 150));
    }

    /// <summary>With no loop showing, the loop's lines are never taken, whichever half.</summary>
    [Fact]
    public void With_no_loop_its_lines_are_never_taken()
    {
        Assert.Equal(1, Pressed(301, 90, loop: false));
        Assert.Equal(-1, Pressed(150, 90, loop: false, loopEnd: 150));
    }

    /// <summary>Nothing in reach takes nothing, and nonsense takes nothing.</summary>
    [Fact]
    public void Nothing_in_reach_and_nonsense_take_nothing()
    {
        Assert.Equal(-1, Pressed(150, 20));
        Assert.Equal(-1, Pressed(double.NaN, 20));
        Assert.Equal(-1, _grab.Grabbed(10, 20, Height, 10, 300, 10, 300, true, 0));
        Assert.Equal(1, Pressed(301, double.NaN));
    }

    /// <summary>A window closed right down to one pixel takes the start first.</summary>
    [Fact]
    public void A_closed_window_takes_the_start_first()
    {
        Assert.Equal(0, _grab.Grabbed(50, 10, Height, 50, 50, 50, 50, true, Reach));
        Assert.Equal(2, _grab.Grabbed(50, 90, Height, 50, 50, 50, 50, true, Reach));
    }
}
