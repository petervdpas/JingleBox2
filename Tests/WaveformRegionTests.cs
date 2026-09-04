using JingleBox2.Rack.Controls;
using JingleBox2.Rack.Controls.Interfaces;
using JingleBox2.Rack.Controls.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Marking a stretch of a take: dragging one out, and moving either end of it afterwards.
/// </summary>
/// <remarks>
/// The rule the trim dialog kept for itself while it drew its own waveform. It is the control's
/// now, so a machine drawing its own face marks a stretch by the same arithmetic the application
/// does, and there is one answer to how narrow a region may be rather than two.
///
/// What matters is that a drag works in either direction and that nothing can leave a region too
/// narrow to take hold of again, since a region with its two handles on the same pixel is one
/// nobody can undo by hand.
/// </remarks>
public class WaveformRegionTests
{
    /// <summary>The rule under test.</summary>
    private readonly IWaveformRegion _region = new WaveformRegion();

    /// <summary>The narrowest a region may be in these tests.</summary>
    private const double Gap = 0.01;

    /// <summary>A drag left to right is the stretch between the two.</summary>
    [Fact]
    public void A_drag_makes_the_region()
    {
        var drawn = _region.Drawn(0.25, 0.75, Gap);

        Assert.Equal(0.25, drawn.Start, 6);
        Assert.Equal(0.75, drawn.End, 6);
    }

    /// <summary>And a drag the other way is the same stretch.</summary>
    [Fact]
    public void Backwards_is_the_same_region()
    {
        var drawn = _region.Drawn(0.75, 0.25, Gap);

        Assert.Equal(0.25, drawn.Start, 6);
        Assert.Equal(0.75, drawn.End, 6);
    }

    /// <summary>A drag that goes nowhere still leaves something that can be grabbed.</summary>
    [Fact]
    public void A_drag_of_nothing_leaves_the_smallest_region()
    {
        var drawn = _region.Drawn(0.5, 0.5, Gap);

        Assert.True(drawn.End - drawn.Start >= Gap);
    }

    /// <summary>At the very end there is no room to the right, so it takes it from the left.</summary>
    [Fact]
    public void At_the_end_the_room_is_taken_from_the_other_side()
    {
        var drawn = _region.Drawn(1, 1, Gap);

        Assert.True(drawn.End - drawn.Start >= Gap);
        Assert.True(drawn.End <= 1);
        Assert.True(drawn.Start >= 0);
    }

    /// <summary>A drag off either edge is held to the take.</summary>
    [Fact]
    public void A_drag_past_the_edges_is_clamped()
    {
        var drawn = _region.Drawn(-3, 4, Gap);

        Assert.Equal(0, drawn.Start, 6);
        Assert.Equal(1, drawn.End, 6);
    }

    /// <summary>The start never crosses the end, nor comes closer to it than the gap.</summary>
    [Theory]
    [InlineData(0.2, 0.2)]
    [InlineData(0.9, 0.69)]
    [InlineData(-1, 0)]
    public void The_start_stops_short_of_the_end(double at, double lands)
    {
        Assert.Equal(lands, _region.Started(at, new Region(0.1, 0.7), Gap), 6);
    }

    /// <summary>And the end never crosses the start.</summary>
    [Theory]
    [InlineData(0.9, 0.9)]
    [InlineData(0.05, 0.11)]
    [InlineData(4, 1)]
    public void The_end_stops_short_of_the_start(double at, double lands)
    {
        Assert.Equal(lands, _region.Ended(at, new Region(0.1, 0.7), Gap), 6);
    }

    /// <summary>
    /// A region already narrower than the gap is not widened by dragging an end.
    /// </summary>
    /// <remarks>
    /// Which is what happens when somebody zooms in after marking: the gap is a share of what
    /// is on screen, so it grows as the picture does, and the stretch that was legal a moment
    /// ago is now narrower than the rule. Held where it is rather than pushed about, since a
    /// handle that jumped away from the hand would read as the control fighting back.
    /// </remarks>
    [Fact]
    public void A_region_narrower_than_the_gap_is_left_alone()
    {
        var tight = new Region(0.5, 0.502);

        Assert.True(_region.Started(0.5, tight, Gap) <= tight.End);
        Assert.True(_region.Ended(0.502, tight, Gap) >= tight.Start);
    }
}
