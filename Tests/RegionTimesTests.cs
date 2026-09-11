using System;
using JingleBox2.Waveform;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Turning a place on a picture into a time, asked without a picture.
/// </summary>
/// <remarks>
/// The arithmetic is one multiplication and is not what these are about. What they hold is the
/// three ways a reading over somebody's take goes wrong in silence: a file still being read,
/// whose frames and rate are both nought; a handle dragged past the edge of the picture, which
/// lands a hair outside the take; and a region marked out from right to left, whose ends arrive
/// the other way round.
/// </remarks>
public sealed class RegionTimesTests
{
    /// <summary>A minute of audio at the rate a take off this application's own input has.</summary>
    private const long Minute = 44100 * 60;

    /// <summary>And that rate.</summary>
    private const int Rate = 44100;

    /// <summary>The rule under test.</summary>
    private readonly RegionTimes _times = new();

    /// <summary>Halfway along the picture is halfway through the take.</summary>
    [Fact]
    public void The_middle_of_a_minute_is_thirty_seconds()
    {
        Assert.Equal(30, _times.At(0.5, Minute, Rate).TotalSeconds, 3);
    }

    /// <summary>The head of the picture is where the take starts.</summary>
    [Fact]
    public void The_head_is_nought()
    {
        Assert.Equal(TimeSpan.Zero, _times.At(0, Minute, Rate));
    }

    /// <summary>And the tail is the whole of it.</summary>
    [Fact]
    public void The_tail_is_the_whole_take()
    {
        Assert.Equal(60, _times.At(1, Minute, Rate).TotalSeconds, 3);
    }

    /// <summary>A handle dragged off the end of the picture still reads as the end of the take.</summary>
    [Fact]
    public void Past_the_end_is_the_end()
    {
        Assert.Equal(60, _times.At(1.4, Minute, Rate).TotalSeconds, 3);
    }

    /// <summary>And one dragged off the front reads as the front rather than as a time before it.</summary>
    [Fact]
    public void Before_the_start_is_the_start()
    {
        Assert.Equal(TimeSpan.Zero, _times.At(-0.2, Minute, Rate));
    }

    /// <summary>A take whose rate is not known yet is a clock that has not started.</summary>
    [Fact]
    public void A_take_with_no_rate_reads_nought()
    {
        Assert.Equal(TimeSpan.Zero, _times.At(0.5, Minute, 0));
    }

    /// <summary>The same for one holding no frames, which is a file still being read.</summary>
    [Fact]
    public void A_take_with_no_frames_reads_nought()
    {
        Assert.Equal(TimeSpan.Zero, _times.At(0.5, 0, Rate));
    }

    /// <summary>Nothing that is not a number reaches the reading.</summary>
    [Fact]
    public void A_place_that_is_not_a_number_reads_nought()
    {
        Assert.Equal(TimeSpan.Zero, _times.At(double.NaN, Minute, Rate));
    }

    /// <summary>A region is as long as the stretch it covers.</summary>
    [Fact]
    public void A_quarter_of_a_minute_lasts_fifteen_seconds()
    {
        Assert.Equal(15, _times.Between(0.25, 0.5, Minute, Rate).TotalSeconds, 3);
    }

    /// <summary>Marked out right to left it is the same region and lasts the same.</summary>
    [Fact]
    public void Which_end_was_dragged_decides_nothing()
    {
        Assert.Equal(
            _times.Between(0.25, 0.5, Minute, Rate),
            _times.Between(0.5, 0.25, Minute, Rate));
    }

    /// <summary>A region with both ends in one place lasts nothing.</summary>
    [Fact]
    public void A_region_of_no_width_lasts_nothing()
    {
        Assert.Equal(TimeSpan.Zero, _times.Between(0.3, 0.3, Minute, Rate));
    }

    /// <summary>And a length is never negative, whatever it is handed.</summary>
    [Fact]
    public void A_length_is_never_negative()
    {
        Assert.True(_times.Between(1.5, -0.5, Minute, Rate) >= TimeSpan.Zero);
    }
}
