using JingleBox2.Waveform;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Drawing a region on a take, which is the gesture the editor was missing.
/// </summary>
/// <remarks>
/// Only the two handles could be moved, so selecting the middle of a recording meant dragging one
/// end all the way in and then the other. The rules worth pinning are that a drag works in either
/// direction and that it can never leave a region too narrow to take hold of again.
/// </remarks>
public class TrimSelectionTests
{
    /// <summary>The narrowest a region may be in these tests.</summary>
    private const double Gap = 0.01;

    /// <summary>A drag left to right is the region between the two.</summary>
    [Fact]
    public void A_drag_makes_the_region()
    {
        var trim = new TrimSelection();

        trim.Select(0.25, 0.75, Gap);

        Assert.Equal(0.25, trim.Start, 6);
        Assert.Equal(0.75, trim.End, 6);
    }

    /// <summary>And a drag the other way is the same region.</summary>
    [Fact]
    public void Backwards_is_the_same_region()
    {
        var trim = new TrimSelection();

        trim.Select(0.75, 0.25, Gap);

        Assert.Equal(0.25, trim.Start, 6);
        Assert.Equal(0.75, trim.End, 6);
    }

    /// <summary>A drag that goes nowhere still leaves something that can be grabbed.</summary>
    [Fact]
    public void A_drag_of_nothing_leaves_the_smallest_region()
    {
        var trim = new TrimSelection();

        trim.Select(0.5, 0.5, Gap);

        Assert.True(trim.End - trim.Start >= Gap);
    }

    /// <summary>At the very end there is no room to the right, so it takes it from the left.</summary>
    [Fact]
    public void At_the_end_the_room_is_taken_from_the_other_side()
    {
        var trim = new TrimSelection();

        trim.Select(1, 1, Gap);

        Assert.True(trim.End - trim.Start >= Gap);
        Assert.True(trim.End <= 1);
        Assert.True(trim.Start >= 0);
    }

    /// <summary>A drag off either edge is held to the take.</summary>
    [Fact]
    public void A_drag_past_the_edges_is_clamped()
    {
        var trim = new TrimSelection();

        trim.Select(-3, 4, Gap);

        Assert.Equal(0, trim.Start, 6);
        Assert.Equal(1, trim.End, 6);
    }
}
