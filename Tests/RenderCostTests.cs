using System.Threading;
using JingleBox2.Audio;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What a block of mixing cost against the time it had.
/// </summary>
/// <remarks>
/// The whole point of the measurement is telling expensive from late, so what is pinned here is
/// that the ratio is worked out from the block's own length rather than from anything fixed: the
/// same three milliseconds is a quarter of one block and twice another.
/// </remarks>
public class RenderCostTests
{
    /// <summary>The keeper under test.</summary>
    private readonly RenderCost _cost = new();

    /// <summary>The rate every case here is worked out at.</summary>
    private const int Rate = 48000;

    /// <summary>512 frames at 48 kHz is 10.67 ms, so a third of it is a third.</summary>
    [Fact]
    public void A_block_is_measured_against_its_own_length()
    {
        _cost.Took(512, 512 * 1000.0 / Rate / 3, Rate);

        Assert.Equal(1, _cost.Blocks);
        Assert.InRange(_cost.Worst, 0.32, 0.34);
    }

    /// <summary>The same time in a shorter block is a bigger share of it.</summary>
    [Fact]
    public void The_same_time_costs_more_in_a_shorter_block()
    {
        _cost.Took(1024, 5, Rate);
        double big = _cost.Worst;

        _cost.Fresh();
        _cost.Took(256, 5, Rate);

        Assert.True(_cost.Worst > big);
    }

    /// <summary>A block that took longer than it had is over one.</summary>
    [Fact]
    public void A_block_over_its_budget_reads_over_one()
    {
        _cost.Took(256, 256 * 1000.0 / Rate * 1.5, Rate);

        Assert.True(_cost.Worst > 1);
    }

    /// <summary>The worst stands, whatever comes after it.</summary>
    [Fact]
    public void The_worst_is_the_worst_and_not_the_last()
    {
        _cost.Took(512, 9, Rate);
        _cost.Took(512, 1, Rate);

        Assert.InRange(_cost.Worst, 0.83, 0.85);
    }

    /// <summary>Nothing is said until the stretch is up.</summary>
    [Fact]
    public void Nothing_is_said_for_a_single_block()
    {
        Assert.Null(_cost.Took(512, 1, Rate));
    }

    /// <summary>And when it is, the line names the blocks, the worst and the mean.</summary>
    /// <remarks>
    /// Five seconds is the stretch, which is too long to sit through, so what is waited on here
    /// is the clock the keeper reads rather than real mixing: the blocks are handed to it in no
    /// time at all and the line arrives once the wall clock has moved past the stretch.
    /// </remarks>
    [Fact]
    public void The_line_says_the_blocks_the_worst_and_the_mean()
    {
        string? line = null;

        for (int at = 0; at < 200 && line == null; at++)
        {
            line = _cost.Took(512, 512 * 1000.0 / Rate / 2, Rate);

            if (line == null) Thread.Sleep(30);
        }

        Assert.NotNull(line);
        Assert.Contains("render:", line);
        Assert.Contains("512 frames", line);
        Assert.Contains("worst 50%", line);
        Assert.Contains("mean 50%", line);
        Assert.Contains("none over", line);
    }

    /// <summary>The line also says what the runtime collected over the stretch.</summary>
    /// <remarks>
    /// The other half of the answer, and the half the block timings cannot give: a mean that is
    /// low with a worst that is over its budget is a pause rather than slow code, and only the
    /// collections say whether there were any. Which of the two sentences comes out depends on
    /// whether anything was collected while the test ran, so what is pinned is that one of them
    /// is there rather than which.
    /// </remarks>
    [Fact]
    public void The_line_says_what_was_collected()
    {
        string? line = null;

        for (int at = 0; at < 200 && line == null; at++)
        {
            line = _cost.Took(512, 1, Rate);

            if (line == null) Thread.Sleep(30);
        }

        Assert.NotNull(line);
        Assert.True(line.Contains("nothing was collected") || line.Contains("collections (gen 0/1/2)"));
    }

    /// <summary>Saying it starts the next stretch, so a line is about what happened since.</summary>
    [Fact]
    public void Saying_it_starts_the_next_stretch()
    {
        string? line = null;

        for (int at = 0; at < 200 && line == null; at++)
        {
            line = _cost.Took(512, 1, Rate);

            if (line == null) Thread.Sleep(30);
        }

        Assert.NotNull(line);
        Assert.Equal(0, _cost.Blocks);
        Assert.Equal(0, _cost.Worst);
    }

    /// <summary>What is not a block is ignored rather than counted as a free one.</summary>
    /// <remarks>
    /// A rate of nought would divide by it, and a block of no frames averaged in as nought would
    /// report the mixing cheaper than it is, which is the one direction this measurement must
    /// never be wrong in.
    /// </remarks>
    [Theory]
    [InlineData(0, 1.0, Rate)]
    [InlineData(-512, 1.0, Rate)]
    [InlineData(512, 1.0, 0)]
    [InlineData(512, -1.0, Rate)]
    [InlineData(512, double.NaN, Rate)]
    public void What_is_not_a_block_is_not_counted(int frames, double milliseconds, int rate)
    {
        Assert.Null(_cost.Took(frames, milliseconds, rate));

        Assert.Equal(0, _cost.Blocks);
        Assert.Equal(0, _cost.Worst);
    }

    /// <summary>A block that took no time at all is a block and is counted.</summary>
    [Fact]
    public void A_free_block_still_counts()
    {
        _cost.Took(512, 0, Rate);

        Assert.Equal(1, _cost.Blocks);
        Assert.Equal(0, _cost.Worst);
    }

    /// <summary>Forgetting the stretch leaves nothing behind.</summary>
    [Fact]
    public void A_fresh_stretch_holds_nothing()
    {
        _cost.Took(512, 9, Rate);
        _cost.Fresh();

        Assert.Equal(0, _cost.Blocks);
        Assert.Equal(0, _cost.Worst);
    }
}
