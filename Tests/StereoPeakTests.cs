using JingleBox2.Audio;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// How loud a block was, which is what every meter on the desk and on the patchbay reads.
/// </summary>
/// <remarks>
/// Worth its own file because a meter is the one control nobody can check by reading it: a bar
/// that sits still looks exactly like a bus with nothing on it, which is how a dead meter went
/// unnoticed on one platform for as long as it did.
/// </remarks>
public class StereoPeakTests
{
    /// <summary>The two sides are answered apart, from the same block.</summary>
    [Fact]
    public void The_two_sides_are_answered_apart()
    {
        var peak = new StereoPeak();

        var (left, right) = peak.Of(new[] { 0.25f, 0.5f, 0.125f, 0.75f }, 4);

        Assert.Equal(0.25f, left);
        Assert.Equal(0.75f, right);
    }

    /// <summary>A trough counts as much as a crest, since a peak is a magnitude.</summary>
    [Fact]
    public void A_trough_counts_as_much_as_a_crest()
    {
        var peak = new StereoPeak();

        var (left, right) = peak.Of(new[] { -0.8f, -0.4f, 0.1f, 0.2f }, 4);

        Assert.Equal(0.8f, left);
        Assert.Equal(0.4f, right);
    }

    /// <summary>Past full scale is still full scale, since a bar runs from nought to one.</summary>
    [Fact]
    public void Past_full_scale_reads_full_scale()
    {
        var peak = new StereoPeak();

        var (left, right) = peak.Of(new[] { 4.2f, -9f }, 2);

        Assert.Equal(1f, left);
        Assert.Equal(1f, right);
    }

    /// <summary>
    /// A sample that is not a number is passed over rather than pinning the meter for ever.
    /// </summary>
    [Fact]
    public void A_sample_that_is_not_a_number_is_passed_over()
    {
        var peak = new StereoPeak();

        var (left, right) = peak.Of(new[] { float.NaN, float.NaN, 0.3f, 0.6f }, 4);

        Assert.Equal(0.3f, left);
        Assert.Equal(0.6f, right);
    }

    /// <summary>And a block that is nothing else reads as silence rather than as not a number.</summary>
    [Fact]
    public void A_block_of_nothing_but_that_reads_silent()
    {
        var peak = new StereoPeak();

        var (left, right) = peak.Of(new[] { float.NaN, float.PositiveInfinity }, 2);

        Assert.Equal(0f, left);
        Assert.Equal(1f, right);
    }

    /// <summary>A float on the end with nothing to pair with is left, since half a frame is not one.</summary>
    [Fact]
    public void A_float_with_nothing_to_pair_with_is_left()
    {
        var peak = new StereoPeak();

        var (left, right) = peak.Of(new[] { 0.2f, 0.3f, 0.9f }, 3);

        Assert.Equal(0.2f, left);
        Assert.Equal(0.3f, right);
    }

    /// <summary>Only what was said to be real is read, so one buffer serves every block.</summary>
    [Fact]
    public void Only_what_was_said_to_be_real_is_read()
    {
        var peak = new StereoPeak();

        var (left, right) = peak.Of(new[] { 0.1f, 0.1f, 0.95f, 0.95f }, 2);

        Assert.Equal(0.1f, left);
        Assert.Equal(0.1f, right);
    }

    /// <summary>A count past the end of the block is held to the block rather than throwing.</summary>
    [Fact]
    public void A_count_past_the_end_is_held_to_the_block()
    {
        var peak = new StereoPeak();

        var (left, right) = peak.Of(new[] { 0.4f, 0.5f }, 4096);

        Assert.Equal(0.4f, left);
        Assert.Equal(0.5f, right);
    }

    /// <summary>Nothing at all is silence rather than an answer nobody can use.</summary>
    [Fact]
    public void Nothing_at_all_is_silence()
    {
        var peak = new StereoPeak();

        Assert.Equal((0f, 0f), peak.Of(null, 16));
        Assert.Equal((0f, 0f), peak.Of(System.Array.Empty<float>(), 0));
        Assert.Equal((0f, 0f), peak.Of(new[] { 0.5f, 0.5f }, -1));
    }
}
