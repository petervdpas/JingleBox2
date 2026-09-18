using System.Diagnostics;
using System.Threading;
using JingleBox2.Audio.Plugins.Bridge;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// How long the mixing waits for a plugin before playing it as silence.
/// </summary>
/// <remarks>
/// Each test clears the deadline behind it, since it is kept per thread and the next test may
/// run on this one.
/// </remarks>
public class MixDeadlineTests
{
    /// <summary>A second in stopwatch ticks, which every figure here is counted in.</summary>
    private static readonly long Second = Stopwatch.Frequency;

    /// <summary>A thread that never said when its cushion runs dry has no deadline at all.</summary>
    /// <remarks>
    /// That is every thread but the one mixing ahead, and a pad or an effect played from one of
    /// those has to go on waiting the way it always did.
    /// </remarks>
    [Fact]
    public void Without_a_cushion_there_is_no_deadline()
    {
        MixDeadline.Clear();

        Assert.Equal(0, MixDeadline.Due(1000, 512));
    }

    /// <summary>A full cushion is waited on until just before it runs dry.</summary>
    [Fact]
    public void A_plugin_is_waited_for_until_just_before_the_cushion_runs_dry()
    {
        try
        {
            long asked = 10 * Second;
            long dryAt = asked + Second / 25;

            MixDeadline.Set(dryAt, 44100);

            long due = MixDeadline.Due(asked, 512);

            Assert.True(due < dryAt);
            Assert.True(due > dryAt - Second / 100);
        }
        finally
        {
            MixDeadline.Clear();
        }
    }

    /// <summary>A cushion nearly empty still gives a plugin its own block's length.</summary>
    /// <remarks>
    /// Otherwise a plugin working at its ordinary speed would be silenced for being asked when
    /// the queue happened to be low, which is worse than the late block it was meant to prevent.
    /// </remarks>
    [Fact]
    public void A_plugin_always_gets_its_own_block_of_time()
    {
        try
        {
            long asked = 10 * Second;

            MixDeadline.Set(asked, 44100);

            Assert.Equal(asked + 512L * Second / 44100, MixDeadline.Due(asked, 512));
        }
        finally
        {
            MixDeadline.Clear();
        }
    }

    /// <summary>The deadline belongs to the thread that set it and to no other.</summary>
    [Fact]
    public void Another_thread_does_not_see_the_deadline()
    {
        try
        {
            MixDeadline.Set(20 * Second, 44100);

            long seen = -1;
            var other = new Thread(() => seen = MixDeadline.Due(10 * Second, 512));

            other.Start();
            other.Join();

            Assert.Equal(0, seen);
        }
        finally
        {
            MixDeadline.Clear();
        }
    }
}
