using System;
using JingleBox2.Audio;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The buffer the sound card keeps in hand, in the two units it has to be in at once.
/// </summary>
/// <remarks>
/// It is chosen in frames, because that is what every other piece of music software calls it and
/// what somebody comparing this with their interface will look for. The sound library wants
/// seconds, and what a person actually feels is the wait. So one number is converted in one
/// place, and this is that conversion and the rule that follows from it: how often the buffer is
/// topped up, which cannot be asked for separately without somebody setting a period that will
/// not keep up with the buffer and getting a dropout with no other explanation.
/// </remarks>
public class OutputBufferTests
{
    /// <summary>The usual sizes, at the usual rates, come to what anybody would expect.</summary>
    [Fact]
    public void Frames_become_the_wait_that_is_felt()
    {
        Assert.Equal(12, TrackerOutput.MillisecondsFor(512, 44100));
        Assert.Equal(11, TrackerOutput.MillisecondsFor(512, 48000));
        Assert.Equal(46, TrackerOutput.MillisecondsFor(2048, 44100));
        Assert.Equal(43, TrackerOutput.MillisecondsFor(2048, 48000));
    }

    /// <summary>
    /// The same size is a different wait at a different rate, which is the whole reason the
    /// milliseconds are shown beside the frames rather than instead of them.
    /// </summary>
    [Fact]
    public void The_same_size_is_a_different_wait_at_a_different_rate()
    {
        Assert.True(TrackerOutput.MillisecondsFor(1024, 44100) > TrackerOutput.MillisecondsFor(1024, 96000));
    }

    /// <summary>
    /// A tiny buffer is a millisecond rather than none. Nought would be read as "no buffer at
    /// all" by whatever is handed it, which is not what a very short one means.
    /// </summary>
    [Fact]
    public void A_tiny_buffer_is_still_a_buffer()
    {
        Assert.Equal(1, TrackerOutput.MillisecondsFor(1, 96000));
        Assert.Equal(1, TrackerOutput.MillisecondsFor(0, 44100));
        Assert.Equal(1, TrackerOutput.MillisecondsFor(-100, 44100));
    }

    /// <summary>And nonsense for a rate does not divide by nought.</summary>
    [Fact]
    public void A_nonsense_rate_is_survived()
    {
        Assert.True(TrackerOutput.MillisecondsFor(512, 0) > 0);
        Assert.True(TrackerOutput.MillisecondsFor(512, -1) > 0);
    }

    /// <summary>
    /// The buffer is topped up four times over rather than at a fixed rate, so a short buffer
    /// is not asked to survive a period most as long as itself.
    /// </summary>
    [Fact]
    public void The_update_period_follows_the_buffer()
    {
        Assert.Equal(10, TrackerOutput.UpdatePeriodFor(60));
        Assert.Equal(10, TrackerOutput.UpdatePeriodFor(46));
        Assert.Equal(5, TrackerOutput.UpdatePeriodFor(12));
        Assert.Equal(5, TrackerOutput.UpdatePeriodFor(2));
    }

    /// <summary>And it is never longer than the buffer it has to keep fed.</summary>
    [Fact]
    public void The_period_is_never_longer_than_the_buffer()
    {
        for (int ms = 1; ms <= 200; ms++)
            Assert.True(TrackerOutput.UpdatePeriodFor(ms) <= Math.Max(5, ms),
                "a period of " + TrackerOutput.UpdatePeriodFor(ms) + " cannot keep " + ms + " ms fed");
    }
}
