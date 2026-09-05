using JingleBox2.Audio.Plugins.Bridge;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What a crossing to a plugin's own process is said to have cost.
/// </summary>
/// <remarks>
/// The arithmetic rather than the plumbing, which is the half that can be wrong quietly: a share
/// worked out against the wrong denominator reads as a plausible percentage and sends whoever
/// believes it at the wrong half of the problem.
/// </remarks>
public class BridgeCostTests
{
    /// <summary>How long 512 frames at 44100 have, which is what a share is taken against.</summary>
    private const double Had = 512 * 1000.0 / 44100;

    /// <summary>Nothing is said until a stretch is up, which is almost every crossing.</summary>
    /// <remarks>
    /// The line is built only when there is one to build. This is called once per plugin per
    /// block on the thread the sound card is waiting on, so an ordinary crossing has to cost the
    /// arithmetic and nothing else.
    /// </remarks>
    [Fact]
    public void An_ordinary_crossing_says_nothing()
    {
        var cost = new BridgeCost("Nothing");

        for (int at = 0; at < 500; at++)
            Assert.Null(cost.Crossed(512, 1.0, 44100));
    }

    /// <summary>A crossing that is not one is passed over rather than counted as a free one.</summary>
    /// <remarks>
    /// A rate of nought would divide by it, and a crossing counted at no cost would report the
    /// bridge as cheaper than it is, which is the direction that ends an investigation early.
    /// </remarks>
    [Fact]
    public void A_crossing_that_is_not_one_is_not_counted()
    {
        var cost = new BridgeCost("Nothing");

        cost.Crossed(0, 1.0, 44100);
        cost.Crossed(512, 1.0, 0);
        cost.Crossed(512, -1.0, 44100);
        cost.Crossed(512, double.NaN, 44100);

        Assert.Equal(0, cost.Crossings);
        Assert.Equal(0.0, cost.Worst);
    }

    /// <summary>The share is against the time that audio had, not against anything else.</summary>
    /// <remarks>
    /// 512 frames at 44100 have 11.61 milliseconds, so a crossing of 1.161 is a tenth of them.
    /// Pinned as a number rather than compared with another reading, since two readings that have
    /// both moved agree just as well as two that are right.
    /// </remarks>
    [Fact]
    public void A_crossing_is_measured_against_the_time_its_audio_had()
    {
        var cost = new BridgeCost("Nothing");

        cost.Crossed(512, Had / 10, 44100);

        Assert.Equal(0.1, cost.Worst, 6);
        Assert.Equal(1, cost.Crossings);
    }

    /// <summary>The worst is the worst and is not disturbed by what came after it.</summary>
    [Fact]
    public void The_worst_crossing_stands()
    {
        var cost = new BridgeCost("Nothing");

        cost.Crossed(512, Had / 4, 44100);
        cost.Crossed(512, Had / 100, 44100);
        cost.Crossed(512, Had / 50, 44100);

        Assert.Equal(0.25, cost.Worst, 6);
        Assert.Equal(3, cost.Crossings);
    }
}
