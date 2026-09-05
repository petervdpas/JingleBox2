using JingleBox2.Rack.Controls;
using JingleBox2.Rack.Controls.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The clip light: when it comes on, how long it stays, and how it goes out.
/// </summary>
/// <remarks>
/// A clip is an instant and a light nobody sees is a light that is not there, so the whole of
/// this is about time. The moment is handed in rather than read off a clock, which is what lets
/// two seconds of holding be asked about without waiting two seconds.
/// </remarks>
public class ClipHoldTests
{
    /// <summary>The rule under test.</summary>
    private readonly IClipHold _clip = new ClipHold();

    /// <summary>Ordinary levels light nothing, however many of them there are.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(0.99)]
    public void Music_under_full_scale_lights_nothing(double level)
    {
        Assert.False(_clip.Saw(level, 0));
        Assert.False(_clip.Saw(level, 1));
        Assert.False(_clip.Saw(level, 10));
    }

    /// <summary>Full scale is clipping: there is no more room past one.</summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.4)]
    [InlineData(9)]
    public void Full_scale_lights_it(double level)
    {
        Assert.True(_clip.Saw(level, 0));
    }

    /// <summary>
    /// It stays lit after the moment has passed, which is the whole point of it.
    /// </summary>
    /// <remarks>
    /// One sample over can be a single hit in a whole take. A light that showed it for one frame
    /// would be a light nobody ever saw.
    /// </remarks>
    [Fact]
    public void It_holds_after_the_moment()
    {
        _clip.Saw(1.2, 0);

        Assert.True(_clip.Saw(0, 0.5));
        Assert.True(_clip.Saw(0, 1.9));
    }

    /// <summary>And goes out on its own, so a light still on is about something recent.</summary>
    [Fact]
    public void And_goes_out_on_its_own()
    {
        _clip.Saw(1.2, 0);

        Assert.False(_clip.Saw(0, 2.0));
        Assert.False(_clip.Saw(0, 30));
    }

    /// <summary>A second clip while it is lit starts the hold again.</summary>
    [Fact]
    public void A_second_clip_holds_it_longer()
    {
        _clip.Saw(1.2, 0);
        _clip.Saw(1.2, 1.5);

        Assert.True(_clip.Saw(0, 3.0), "still lit, since it clipped again at 1.5");
        Assert.False(_clip.Saw(0, 3.6));
    }

    /// <summary>Somebody who has seen it can put it out.</summary>
    [Fact]
    public void It_can_be_put_out_by_hand()
    {
        _clip.Saw(1.2, 0);

        _clip.Clear();

        Assert.False(_clip.Saw(0, 0.1));
    }

    /// <summary>
    /// A reading that is not a number lights it.
    /// </summary>
    /// <remarks>
    /// Nothing should ever hand one here, and if something does it is a fault worth a light
    /// rather than one worth hiding. It would otherwise be the one reading that can never light
    /// anything, since every comparison against a NaN is false.
    /// </remarks>
    [Fact]
    public void Nonsense_lights_it_too()
    {
        Assert.True(_clip.Saw(double.NaN, 0));
    }

    /// <summary>
    /// A clock that jumps backwards puts it out rather than sticking it on for ever.
    /// </summary>
    /// <remarks>
    /// Which is what a machine's clock doing something unusual looks like from in here. A light
    /// that could never go out is worse than one that never comes on, since after it every
    /// session looks like it clipped.
    /// </remarks>
    [Fact]
    public void A_clock_that_goes_backwards_does_not_strand_it()
    {
        _clip.Saw(1.2, 100);

        Assert.False(_clip.Saw(0, 5));
    }
}
