using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What the audio is sized at when nobody has chosen, and what a stored nought means.
/// </summary>
/// <remarks>
/// Both platforms are asked on whichever machine is running the tests, which is the whole reason
/// the rule takes the platform rather than looking it up: a rule that reads the operating system
/// inside itself can only ever be checked on one.
/// </remarks>
public class AudioDefaultsTests
{
    private readonly IAudioDefaults _defaults = new AudioDefaults();

    /// <summary>The default is the nearest size to the sixty milliseconds it ran as a constant.</summary>
    [Fact]
    public void Linux_gets_the_size_nearest_what_the_constant_was()
    {
        var sizes = _defaults.For(windows: false);

        Assert.Equal(2048, sizes.BufferFrames);
        Assert.Equal(10, sizes.UpdatePeriodMs);
    }

    /// <summary>Windows is answered too, whichever machine is asking.</summary>
    [Fact]
    public void Windows_is_answered_from_here()
    {
        var sizes = _defaults.For(windows: true);

        Assert.True(sizes.BufferFrames > 0);
        Assert.True(sizes.UpdatePeriodMs > 0);
    }

    /// <summary>Nothing chosen leaves the sound library on its own thread count.</summary>
    [Fact]
    public void Nobody_is_given_a_thread_count_by_default()
    {
        Assert.Equal(0, _defaults.For(windows: false).UpdateThreads);
        Assert.Equal(0, _defaults.For(windows: true).UpdateThreads);
    }

    /// <summary>
    /// A settings file with nought in it sounds exactly as it did before there was a setting.
    /// </summary>
    /// <remarks>
    /// This is the one that matters: every file written before these existed holds nought, and
    /// nought has to mean "whatever suits this machine" rather than nought milliseconds of buffer.
    /// </remarks>
    [Fact]
    public void Nought_means_the_default_rather_than_nothing()
    {
        var chosen = _defaults.Chosen(new AudioSizes(0, 0, 0));

        Assert.Equal(_defaults.Here.BufferFrames, chosen.BufferFrames);
        Assert.Equal(_defaults.Here.UpdatePeriodMs, chosen.UpdatePeriodMs);
    }

    /// <summary>And a number that was chosen is kept.</summary>
    [Fact]
    public void What_was_chosen_is_kept()
    {
        var chosen = _defaults.Chosen(new AudioSizes(512, 20, 3));

        Assert.Equal(512, chosen.BufferFrames);
        Assert.Equal(20, chosen.UpdatePeriodMs);
        Assert.Equal(3, chosen.UpdateThreads);
    }

    /// <summary>One chosen and the others not is answered one at a time.</summary>
    [Fact]
    public void Each_of_the_three_falls_back_on_its_own()
    {
        var chosen = _defaults.Chosen(new AudioSizes(0, 20, 0));

        Assert.Equal(_defaults.Here.BufferFrames, chosen.BufferFrames);
        Assert.Equal(20, chosen.UpdatePeriodMs);
        Assert.Equal(_defaults.Here.UpdateThreads, chosen.UpdateThreads);
    }
}
