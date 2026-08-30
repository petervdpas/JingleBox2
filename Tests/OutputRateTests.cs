using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What rate the card is opened at, which has to be the rate the mixer works at.
/// </summary>
/// <remarks>
/// The card was opened at a literal 44100 while the mixer read the setting. They agree at 44100
/// and nowhere else, and where they disagree nothing says so: the sound is resampled down to the
/// card and back up by the system mixer, and the middle conversion throws away everything above
/// half the card's rate for good.
/// </remarks>
public class OutputRateTests
{
    private readonly IOutputRate _rate = new OutputRate();

    /// <summary>Nothing chosen is the same default the tracker's output falls back to.</summary>
    [Fact]
    public void Nothing_chosen_is_the_default()
    {
        Assert.Equal(TrackerOutput.DefaultSampleRate, _rate.Chosen(0));
    }

    /// <summary>A rate that was chosen is the rate, whichever it is.</summary>
    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    [InlineData(96000)]
    public void What_was_chosen_is_used(int chosen)
    {
        Assert.Equal(chosen, _rate.Chosen(chosen));
    }

    /// <summary>A nonsense rate reads as nothing chosen rather than being passed to the card.</summary>
    [Fact]
    public void A_negative_rate_is_the_default()
    {
        Assert.Equal(TrackerOutput.DefaultSampleRate, _rate.Chosen(-1));
    }
}
