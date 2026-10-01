using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>Reading the sound server's cycle out of what PipeWire says about its own settings.</summary>
/// <remarks>
/// The reading is what decides the buffer on a machine nobody has set up, so it has to be right
/// about the forced values, which win over the ordinary ones when somebody has set them, and it
/// has to say it does not know rather than guess when the text is not what it expects.
/// </remarks>
public class SoundServerClockTests
{
    /// <summary>The rule under test.</summary>
    private readonly ISoundServerClock _clock = new SoundServerClock();

    /// <summary>What PipeWire printed on the machine this was written on.</summary>
    private const string Ordinary =
        "Found \"settings\" metadata 38\n" +
        "update: id:0 key:'log.level' value:'2' type:''\n" +
        "update: id:0 key:'clock.rate' value:'48000' type:''\n" +
        "update: id:0 key:'clock.allowed-rates' value:'[ 48000 ]' type:''\n" +
        "update: id:0 key:'clock.quantum' value:'1024' type:''\n" +
        "update: id:0 key:'clock.min-quantum' value:'32' type:''\n" +
        "update: id:0 key:'clock.max-quantum' value:'2048' type:''\n" +
        "update: id:0 key:'clock.force-quantum' value:'0' type:''\n" +
        "update: id:0 key:'clock.force-rate' value:'0' type:''\n";

    /// <summary>The rate and the quantum are read off the ordinary lines.</summary>
    [Fact]
    public void The_rate_and_the_quantum_are_read()
    {
        Assert.Equal(new ServerClock(48000, 1024), _clock.FromPipeWire(Ordinary));
    }

    /// <summary>A forced quantum and a forced rate win over the ordinary ones.</summary>
    [Fact]
    public void Forced_values_win()
    {
        string forced = Ordinary
            .Replace("clock.force-quantum' value:'0'", "clock.force-quantum' value:'256'")
            .Replace("clock.force-rate' value:'0'", "clock.force-rate' value:'44100'");

        Assert.Equal(new ServerClock(44100, 256), _clock.FromPipeWire(forced));
    }

    /// <summary>Text with no clock in it is not knowing, not a guess.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("Found \"settings\" metadata 38\n")]
    [InlineData("update: id:0 key:'clock.rate' value:'48000' type:''\n")]
    [InlineData("update: id:0 key:'clock.rate' value:'fast' type:''\nupdate: id:0 key:'clock.quantum' value:'1024' type:''\n")]
    public void Text_without_a_clock_is_not_knowing(string text)
    {
        Assert.Null(_clock.FromPipeWire(text));
    }

    /// <summary>Asking the running system answers without throwing, whatever the machine has.</summary>
    [Fact]
    public void Asking_the_system_answers_without_throwing()
    {
        var read = _clock.Read();

        Assert.True(read is null || (read.Value.Rate > 0 && read.Value.Quantum > 0));
    }
}
