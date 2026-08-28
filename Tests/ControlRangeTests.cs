using System;
using JingleBox2.Machines.Ui;
using JingleBox2.Machines.Ui.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The value maths every range control shares, and the two that are about our own parts.
/// </summary>
/// <remarks>
/// A machine's description comes out of a file somebody wrote by hand, so a range that is dead
/// or the wrong way round is an ordinary arrival rather than a fault. A control that sits at
/// the low end is one somebody can see is wrong; an exception on the drawing thread takes the
/// whole panel down.
/// </remarks>
public class ControlRangeTests
{
    private readonly IRangeValue _range = new RangeValue();
    private readonly IKnobMath _dial = new KnobMath();
    private readonly IFaderMath _track = new FaderMath();
    private readonly ITickList _ticks = new TickList();

    /// <summary>Where a value sits, at both ends and in the middle.</summary>
    [Fact]
    public void A_fraction_runs_nought_to_one()
    {
        Assert.Equal(0.0, _range.Fraction(0, 0, 10), 12);
        Assert.Equal(0.5, _range.Fraction(5, 0, 10), 12);
        Assert.Equal(1.0, _range.Fraction(10, 0, 10), 12);
    }

    /// <summary>A value outside the range is held at the end it went past.</summary>
    [Fact]
    public void A_fraction_is_held_inside_its_ends()
    {
        Assert.Equal(0.0, _range.Fraction(-40, 0, 10), 12);
        Assert.Equal(1.0, _range.Fraction(40, 0, 10), 12);
    }

    /// <summary>A range with negative numbers in it works the same way.</summary>
    [Fact]
    public void A_range_may_straddle_nought()
    {
        Assert.Equal(0.5, _range.Fraction(0, -24, 24), 12);
        Assert.Equal(0.0, _range.Fraction(-24, -24, 24), 12);
        Assert.Equal(1.0, _range.Fraction(24, -24, 24), 12);
    }

    /// <summary>A dead range, or one the wrong way round, reads as the bottom.</summary>
    [Fact]
    public void A_range_that_cannot_be_read_is_the_bottom()
    {
        Assert.Equal(0.0, _range.Fraction(5, 10, 10), 12);
        Assert.Equal(0.0, _range.Fraction(5, 10, 0), 12);
        Assert.Equal(0.0, _range.Fraction(double.NaN, 0, 10), 12);
    }

    /// <summary>
    /// The step grid is measured from the minimum, so a range reaches both of its own ends.
    /// </summary>
    /// <remarks>
    /// From nought instead, -24 to 24 in steps of 5 cannot reach either end, and a transpose
    /// knob stops two semitones short at both extremes.
    /// </remarks>
    [Fact]
    public void The_step_grid_is_measured_from_the_minimum()
    {
        Assert.Equal(-24, _range.Quantize(-24, -24, 24, 5), 12);
        Assert.Equal(24, _range.Quantize(24, -24, 24, 5), 12);
        Assert.Equal(-19, _range.Quantize(-20, -24, 24, 5), 12);
    }

    /// <summary>No step at all is a smooth sweep, held inside its ends.</summary>
    [Fact]
    public void No_step_is_a_smooth_sweep()
    {
        Assert.Equal(3.7, _range.Quantize(3.7, 0, 10, 0), 12);
        Assert.Equal(10.0, _range.Quantize(40, 0, 10, 0), 12);
        Assert.Equal(0.0, _range.Quantize(-40, 0, 10, 0), 12);
    }

    /// <summary>A quantized value never leaves its range, whatever the step is.</summary>
    [Fact]
    public void Quantizing_never_leaves_the_range()
    {
        foreach (double step in new[] { 0.0, 0.1, 1.0, 7.0, 100.0 })
            for (double value = -50; value <= 50; value += 0.25)
                Assert.InRange(_range.Quantize(value, -24, 24, step), -24, 24);
    }

    /// <summary>A value that is not a number lands at the bottom rather than staying nonsense.</summary>
    [Fact]
    public void A_value_that_is_not_a_number_lands_at_the_bottom()
    {
        Assert.Equal(-24, _range.Quantize(double.NaN, -24, 24, 1), 12);
        Assert.Equal(10, _range.Quantize(5, 10, 0, 1), 12);
    }

    /// <summary>Dragging up raises the value and dragging down lowers it.</summary>
    [Fact]
    public void Dragging_up_raises_the_value()
    {
        Assert.True(_range.FromDrag(5, 30, 0, 10, 0, 150) > 5);
        Assert.True(_range.FromDrag(5, -30, 0, 10, 0, 150) < 5);
    }

    /// <summary>
    /// A drag is measured from where it began, so going down and back up ends where it started.
    /// </summary>
    [Fact]
    public void A_drag_that_returns_ends_where_it_began()
    {
        Assert.Equal(5.0, _range.FromDrag(5, 0, 0, 10, 0, 150), 12);
    }

    /// <summary>Holding shift makes the same drag cover a quarter as much.</summary>
    [Fact]
    public void A_fine_drag_covers_less()
    {
        double coarse = _range.FromDrag(5, 30, 0, 10, 0, 150) - 5;
        double fine = _range.FromDrag(5, 30, 0, 10, 0, 150, fine: true) - 5;

        Assert.Equal(coarse * _range.FineFactor, fine, 12);
    }

    /// <summary>A drag with nothing to drag over lands at the bottom rather than dividing by nought.</summary>
    [Fact]
    public void A_drag_with_no_room_lands_at_the_bottom()
    {
        Assert.Equal(0.0, _range.FromDrag(5, 30, 0, 10, 0, 0), 12);
        Assert.Equal(10.0, _range.FromDrag(5, 30, 10, 10, 0, 150), 12);
    }

    /// <summary>The pointer sweeps three quarters of a circle, seven o'clock to five o'clock.</summary>
    [Fact]
    public void The_pointer_sweeps_the_whole_dial()
    {
        Assert.Equal(_dial.StartDegrees, _dial.AngleFor(0, 0, 10), 12);
        Assert.Equal(_dial.StartDegrees + _dial.SweepDegrees, _dial.AngleFor(10, 0, 10), 12);
        Assert.Equal(0.0, _dial.AngleFor(5, 0, 10), 12);
    }

    /// <summary>Twelve o'clock is straight up, in screen coordinates where y grows downwards.</summary>
    [Fact]
    public void Twelve_oclock_is_straight_up()
    {
        var (x, y) = _dial.PointAt(100, 100, 10, 0);

        Assert.Equal(100, x, 9);
        Assert.Equal(90, y, 9);
    }

    /// <summary>Three o'clock is to the right, and the radius is honoured.</summary>
    [Fact]
    public void Three_oclock_is_to_the_right()
    {
        var (x, y) = _dial.PointAt(100, 100, 10, 90);

        Assert.Equal(110, x, 9);
        Assert.Equal(100, y, 9);
    }

    /// <summary>The fader's track runs bottom to top, where a fader's zero belongs.</summary>
    [Fact]
    public void The_track_runs_bottom_to_top()
    {
        Assert.Equal(0.0, _track.ValueAt(200, 100, 100, 0, 10, 0), 9);
        Assert.Equal(10.0, _track.ValueAt(100, 100, 100, 0, 10, 0), 9);
        Assert.Equal(5.0, _track.ValueAt(150, 100, 100, 0, 10, 0), 9);
    }

    /// <summary>The cap and the pointer agree, which is what stops a grabbed fader jumping.</summary>
    [Fact]
    public void The_cap_and_the_pointer_agree()
    {
        for (double value = 0; value <= 10; value += 0.5)
        {
            double y = _track.CapCenterY(value, 100, 100, 0, 10);

            Assert.Equal(value, _track.ValueAt(y, 100, 100, 0, 10, 0), 9);
        }
    }

    /// <summary>A track with no length reads as the bottom rather than dividing by nought.</summary>
    [Fact]
    public void A_track_with_no_length_reads_as_the_bottom()
    {
        Assert.Equal(0.0, _track.ValueAt(150, 100, 0, 0, 10, 0), 12);
        Assert.Equal(10.0, _track.ValueAt(150, 100, 100, 10, 10, 0), 12);
    }

    /// <summary>The marks are read as they are written, in order.</summary>
    [Fact]
    public void The_marks_are_read_as_written()
    {
        Assert.Equal(new[] { 6.0, 0.0, -6.0, -12.0 }, _ticks.Parse("6,0,-6,-12"));
        Assert.Equal(new[] { 6.0, 0.0 }, _ticks.Parse("  6 , 0  "));
    }

    /// <summary>A typo costs the mark rather than the page.</summary>
    [Fact]
    public void Junk_costs_only_the_mark()
    {
        Assert.Equal(new[] { 6.0, -6.0 }, _ticks.Parse("6,nonsense,-6"));
        Assert.Empty(_ticks.Parse("nonsense"));
        Assert.Empty(_ticks.Parse(null));
        Assert.Empty(_ticks.Parse(""));
        Assert.Empty(_ticks.Parse("   "));
        Assert.Empty(_ticks.Parse(",,,"));
    }

    /// <summary>
    /// A decimal point is a point, whatever the machine's own locale says.
    /// </summary>
    /// <remarks>
    /// These come out of a machine's file, which is the same file on everybody's computer.
    /// </remarks>
    [Fact]
    public void The_marks_are_read_the_same_everywhere()
    {
        Assert.Equal(new[] { 1.5, -0.5 }, _ticks.Parse("1.5,-0.5"));
    }
}
