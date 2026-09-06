using JingleBox2.Views;
using JingleBox2.Views.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What a device being dragged along a chain carries, and who is allowed to read it.
/// </summary>
/// <remarks>
/// **A place in a chain means nothing without the chain it is a place in.** The same strip is
/// drawn over a track's chain, the master's, a pad's and the recording input's, so slot 1 is
/// four different devices, and two strips can be on the screen at once. Without the chain
/// travelling beside the number, a device let go over the wrong strip would reorder a chain
/// nobody was dragging, and it would look exactly like a chain reordering itself.
/// </remarks>
public sealed class SlotDragTests
{
    /// <summary>The format under test.</summary>
    private readonly ISlotDrag _drag = new SlotDrag();

    /// <summary>Stands in for a chain, since what is compared is which object it is.</summary>
    private sealed class Chain
    {
    }

    /// <summary>What was picked up comes back on the chain it was picked up from.</summary>
    [Fact]
    public void A_slot_comes_back_on_its_own_chain()
    {
        var chain = new Chain();

        Assert.Equal(2, _drag.IndexFrom(_drag.For(chain, 2), chain));
    }

    /// <summary>The first one included, which is the answer a truthy test would lose.</summary>
    [Fact]
    public void The_first_slot_comes_back_as_the_first()
    {
        var chain = new Chain();

        Assert.Equal(0, _drag.IndexFrom(_drag.For(chain, 0), chain));
    }

    /// <summary>**Another chain is refused**, rather than moving whatever sits at that number.</summary>
    [Fact]
    public void Another_chain_will_not_have_it()
    {
        var mine = new Chain();
        var theirs = new Chain();

        Assert.Equal(-1, _drag.IndexFrom(_drag.For(mine, 1), theirs));
    }

    /// <summary>Two chains holding the same devices are still two chains.</summary>
    /// <remarks>
    /// Compared by which object it is rather than by anything about it, since a track and the
    /// master can perfectly well hold the same effects in the same order.
    /// </remarks>
    [Fact]
    public void Two_chains_that_look_alike_are_not_each_other()
    {
        Assert.Equal(-1, _drag.IndexFrom(_drag.For(new Chain(), 0), new Chain()));
    }

    /// <summary>An empty hand is refused the same way everything else is.</summary>
    [Fact]
    public void Nothing_in_the_hand_is_nothing()
    {
        Assert.Equal(-1, _drag.IndexFrom(null, new Chain()));
    }
}
