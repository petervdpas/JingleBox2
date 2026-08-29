using JingleBox2.UI;
using JingleBox2.UI.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The two scales a fader and a filter knob are marked in, and the round trip through each.
/// </summary>
/// <remarks>
/// Both are the same shape of thing: a reading a person understands on one side, and what the
/// audio actually multiplies or sweeps by on the other. Both were static and untested, and both
/// are the sort of maths that is wrong by a factor of two for a year without anybody being able
/// to say which end is at fault.
/// </remarks>
public class ScaleTests
{
    private readonly IGainScale _gain = new GainScale();
    private readonly IFrequencyScale _hz = new FrequencyScale();

    /// <summary>Unity on the fader is unity in the engine, which is the whole point of the marking.</summary>
    [Fact]
    public void ZeroDecibelsIsUnity()
    {
        Assert.Equal(1.0, _gain.ToAmplitude(0), 10);
        Assert.Equal(0.0, _gain.ToDecibels(1), 10);
    }

    /// <summary>Six decibels is very nearly twice the amplitude, which is where the headroom stops.</summary>
    [Fact]
    public void SixDecibelsIsNearlyTwice()
    {
        Assert.Equal(1.995262, _gain.ToAmplitude(_gain.MaximumDecibels), 5);
    }

    /// <summary>The bottom of the travel is silence, not a very small amplitude.</summary>
    /// <remarks>
    /// A fader pulled all the way down has to be off. Left as a tiny multiplier it would leak a
    /// track nobody can hear into a mix nobody can find it in.
    /// </remarks>
    [Fact]
    public void TheBottomOfTheTravelIsOff()
    {
        Assert.Equal(0, _gain.ToAmplitude(_gain.MinimumDecibels));
        Assert.Equal(0, _gain.ToAmplitude(_gain.MinimumDecibels - 20));
        Assert.Equal(0, _gain.ToAmplitude(double.NaN));
    }

    /// <summary>Above the top of the travel the fader stops rather than carrying on.</summary>
    [Fact]
    public void AboveTheTopItStops()
    {
        Assert.Equal(_gain.ToAmplitude(_gain.MaximumDecibels), _gain.ToAmplitude(_gain.MaximumDecibels + 30), 10);
    }

    /// <summary>A reading put through both ways comes back where it started.</summary>
    [Theory]
    [InlineData(-48)]
    [InlineData(-24)]
    [InlineData(-6)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    public void AGainRoundTripComesBackWhereItStarted(double decibels)
    {
        Assert.Equal(decibels, _gain.ToDecibels(_gain.ToAmplitude(decibels)), 8);
    }

    /// <summary>Nothing and a negative amplitude both read as the bottom rather than as an error.</summary>
    [Fact]
    public void NoAmplitudeReadsAsTheBottom()
    {
        Assert.Equal(_gain.MinimumDecibels, _gain.ToDecibels(0));
        Assert.Equal(_gain.MinimumDecibels, _gain.ToDecibels(-1));
        Assert.Equal(_gain.MinimumDecibels, _gain.ToDecibels(double.NaN));
    }

    /// <summary>The two ends of the filter sweep sit at the two ends of the knob.</summary>
    [Fact]
    public void TheSweepEndsAtTheKnobEnds()
    {
        Assert.Equal(0, _hz.ToPosition(_hz.MinHz), 10);
        Assert.Equal(1, _hz.ToPosition(_hz.MaxHz), 10);
        Assert.Equal(_hz.MinHz, _hz.ToHz(0), 8);
        Assert.Equal(_hz.MaxHz, _hz.ToHz(1), 8);
    }

    /// <summary>The sweep is logarithmic, so each octave takes the same amount of knob.</summary>
    /// <remarks>
    /// The reason it is not linear: on a linear knob everything below a kilohertz, which is most
    /// of what a filter is ever set to, lives in the first twentieth of the travel.
    /// </remarks>
    [Fact]
    public void EachOctaveTakesTheSameTravel()
    {
        double first = _hz.ToPosition(80) - _hz.ToPosition(40);
        double later = _hz.ToPosition(8000) - _hz.ToPosition(4000);

        Assert.Equal(first, later, 8);
    }

    /// <summary>A position put through both ways comes back where it started.</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    public void AFrequencyRoundTripComesBackWhereItStarted(double position)
    {
        Assert.Equal(position, _hz.ToPosition(_hz.ToHz(position)), 8);
    }

    /// <summary>Off the ends, both directions clamp rather than running away.</summary>
    [Fact]
    public void OffTheEndsItClamps()
    {
        Assert.Equal(0, _hz.ToPosition(1), 10);
        Assert.Equal(1, _hz.ToPosition(50000), 10);
        Assert.Equal(_hz.MinHz, _hz.ToHz(-2), 8);
        Assert.Equal(_hz.MaxHz, _hz.ToHz(9), 8);
    }

    /// <summary>A filter wide open says so in words rather than printing twenty kilohertz.</summary>
    [Fact]
    public void WideOpenReadsAsOff()
    {
        Assert.Equal("off", _hz.Text(_hz.MaxHz));
        Assert.Equal("off", _hz.Text(_hz.MaxHz + 1));
        Assert.Equal("-", _hz.Text(double.NaN));
    }

    /// <summary>Below a kilohertz it reads in hertz, and above it in kilohertz.</summary>
    [Theory]
    [InlineData(20, "20 Hz")]
    [InlineData(440, "440 Hz")]
    [InlineData(999, "999 Hz")]
    [InlineData(1000, "1.0 kHz")]
    [InlineData(4500, "4.5 kHz")]
    public void ItChangesUnitAtAKilohertz(double hz, string reads)
    {
        Assert.Equal(reads, _hz.Text(hz));
    }
}
