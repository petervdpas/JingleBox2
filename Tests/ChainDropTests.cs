using System.Collections.Generic;
using JingleBox2.UI;
using JingleBox2.UI.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Where a device let go of over a chain lands.
/// </summary>
/// <remarks>
/// Order is the whole point of a chain, so a drop that is out by one is a compressor after the
/// delay rather than before it, and there is nothing on the screen that says which of the two
/// you asked for. **The gap is counted with the device still in the row and the chain counts
/// without it**, which is the trap: dragging rightwards is out by one and dragging leftwards is
/// not, so a version with that wrong works perfectly half the time.
/// </remarks>
public sealed class ChainDropTests
{
    /// <summary>The rule under test.</summary>
    private readonly IChainDrop _drop = new ChainDrop();

    /// <summary>Three blocks a hundred wide with a gap of ten between them.</summary>
    private static List<(double Left, double Right)> Three() =>
        new() { (0, 100), (110, 210), (220, 320) };

    /// <summary>The near half of a block means in front of it.</summary>
    [Fact]
    public void The_near_half_of_a_block_is_in_front_of_it()
    {
        Assert.Equal(0, _drop.Place(Three(), 10));
        Assert.Equal(1, _drop.Place(Three(), 120));
        Assert.Equal(2, _drop.Place(Three(), 230));
    }

    /// <summary>And the far half means after it.</summary>
    [Fact]
    public void The_far_half_of_a_block_is_after_it()
    {
        Assert.Equal(1, _drop.Place(Three(), 90));
        Assert.Equal(2, _drop.Place(Three(), 200));
        Assert.Equal(3, _drop.Place(Three(), 310));
    }

    /// <summary>A point in the gap between two blocks belongs to the one it is in front of.</summary>
    [Fact]
    public void A_point_between_two_blocks_is_in_front_of_the_later_one()
    {
        Assert.Equal(1, _drop.Place(Three(), 105));
        Assert.Equal(2, _drop.Place(Three(), 215));
    }

    /// <summary>Before the row is the start of it, which is the only way to say "first".</summary>
    [Fact]
    public void Before_the_row_is_the_start()
    {
        Assert.Equal(0, _drop.Place(Three(), -40));
    }

    /// <summary>Past the row is the end, since dragging to the end means the end.</summary>
    [Fact]
    public void Past_the_row_is_the_end()
    {
        Assert.Equal(3, _drop.Place(Three(), 900));
    }

    /// <summary>An empty row is one place, and asking about nothing is not a fault.</summary>
    [Fact]
    public void An_empty_row_has_one_place()
    {
        Assert.Equal(0, _drop.Place(new List<(double, double)>(), 40));
        Assert.Equal(0, _drop.Place(null!, 40));
    }

    /// <summary>**Dragging rightwards lands one short of the gap**, which is the whole trap.</summary>
    [Fact]
    public void Moving_rightwards_lands_one_short_of_the_gap()
    {
        Assert.Equal(1, _drop.Landing(moving: 0, place: 2));
        Assert.Equal(2, _drop.Landing(moving: 0, place: 3));
    }

    /// <summary>Dragging leftwards lands on the gap itself.</summary>
    [Fact]
    public void Moving_leftwards_lands_on_the_gap()
    {
        Assert.Equal(0, _drop.Landing(moving: 2, place: 0));
        Assert.Equal(1, _drop.Landing(moving: 2, place: 1));
    }

    /// <summary>Either side of where it already is leaves it where it is.</summary>
    [Fact]
    public void Either_side_of_itself_is_where_it_already_is()
    {
        Assert.Equal(1, _drop.Landing(moving: 1, place: 1));
        Assert.Equal(1, _drop.Landing(moving: 1, place: 2));
    }

    /// <summary>
    /// The whole gesture, over three devices, read as the order that comes out.
    /// </summary>
    /// <remarks>
    /// The two rules only mean anything together, and this is what a hand actually does: drop
    /// the first one past the last, and it is last.
    /// </remarks>
    [Fact]
    public void The_first_dropped_past_the_last_is_last()
    {
        int place = _drop.Place(Three(), 900);

        Assert.Equal(2, _drop.Landing(moving: 0, place: place));
    }

    /// <summary>And the last dropped in front of the first is first.</summary>
    [Fact]
    public void The_last_dropped_before_the_first_is_first()
    {
        int place = _drop.Place(Three(), -10);

        Assert.Equal(0, _drop.Landing(moving: 2, place: place));
    }
}
