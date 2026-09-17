using System.Runtime.InteropServices;
using JingleBox2.Audio.Plugins;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The shape of the struct a plugin is told the time in.
/// </summary>
/// <remarks>
/// A plugin reads this by offset, out of a pointer, in another language. Nothing checks that the
/// struct written here matches the one Steinberg published: put a field in the wrong place and
/// the plugin does not fail, it reads a tempo out of the middle of a timestamp and plays at some
/// speed nobody asked for. These are the numbers from the SDK's own header, written down so that
/// a field added or moved has to be admitted to rather than merely done.
/// </remarks>
public class ProcessContextLayoutTests
{
    /// <summary>What the SDK's struct comes to on a sixty four bit build.</summary>
    [Fact]
    public void It_is_the_size_the_sdk_says()
    {
        Assert.Equal(112, Marshal.SizeOf<ProcessContext>());
    }

    /// <summary>Every field, where the SDK puts it.</summary>
    /// <remarks>
    /// The whole struct rather than the few this application fills in, because the ones it leaves
    /// alone are what hold the rest in place.
    /// </remarks>
    [Theory]
    [InlineData("State", 0)]
    [InlineData("SampleRate", 8)]
    [InlineData("ProjectTimeSamples", 16)]
    [InlineData("SystemTime", 24)]
    [InlineData("ContinousTimeSamples", 32)]
    [InlineData("ProjectTimeMusic", 40)]
    [InlineData("BarPositionMusic", 48)]
    [InlineData("CycleStartMusic", 56)]
    [InlineData("CycleEndMusic", 64)]
    [InlineData("Tempo", 72)]
    [InlineData("TimeSigNumerator", 80)]
    [InlineData("TimeSigDenominator", 84)]
    [InlineData("SmpteOffsetSubframes", 92)]
    [InlineData("SamplesToNextClock", 104)]
    public void Every_field_is_where_the_sdk_puts_it(string field, int offset)
    {
        Assert.Equal(offset, (int)Marshal.OffsetOf<ProcessContext>(field));
    }

    /// <summary>
    /// The flags, which are what say whether the rest is worth reading.
    /// </summary>
    /// <remarks>
    /// A wrong bit here is the quietest fault of the lot: the numbers are all correct and the
    /// plugin ignores them, because the flag that would have vouched for them was never set.
    /// </remarks>
    [Fact]
    public void The_flags_are_the_bits_the_sdk_says()
    {
        Assert.Equal(1u << 1, Vst3Abi.ContextPlaying);
        Assert.Equal(1u << 9, Vst3Abi.ContextProjectTimeMusicValid);
        Assert.Equal(1u << 10, Vst3Abi.ContextTempoValid);
        Assert.Equal(1u << 11, Vst3Abi.ContextBarPositionValid);
        Assert.Equal(1u << 13, Vst3Abi.ContextTimeSigValid);
        Assert.Equal(1u << 17, Vst3Abi.ContextContTimeValid);
    }

    /// <summary>
    /// CLAP says where the song is in a different shape, and it is read by offset just the same.
    /// </summary>
    /// <remarks>
    /// Its positions are fixed point with thirty one bits under the point, so a field in the
    /// wrong place here does not read as a slightly wrong tempo: it reads as a song two billion
    /// beats from where it is.
    /// </remarks>
    [Theory]
    [InlineData("Header", 0)]
    [InlineData("Flags", 16)]
    [InlineData("SongPositionBeats", 24)]
    [InlineData("SongPositionSeconds", 32)]
    [InlineData("Tempo", 40)]
    [InlineData("TempoIncrement", 48)]
    [InlineData("LoopStartBeats", 56)]
    [InlineData("LoopEndBeats", 64)]
    [InlineData("LoopStartSeconds", 72)]
    [InlineData("LoopEndSeconds", 80)]
    [InlineData("BarStart", 88)]
    [InlineData("BarNumber", 96)]
    [InlineData("TimeSignatureNumerator", 100)]
    [InlineData("TimeSignatureDenominator", 102)]
    public void The_clap_transport_is_the_shape_clap_says(string field, int offset)
    {
        Assert.Equal(offset, (int)Marshal.OffsetOf<ClapEventTransport>(field));
    }

    /// <summary>And CLAP's own flags, which do the same job as VST3's.</summary>
    [Fact]
    public void The_clap_flags_are_the_bits_clap_says()
    {
        Assert.Equal(1u << 0, ClapAbi.TransportHasTempo);
        Assert.Equal(1u << 1, ClapAbi.TransportHasBeats);
        Assert.Equal(1u << 2, ClapAbi.TransportHasSeconds);
        Assert.Equal(1u << 3, ClapAbi.TransportHasTimeSignature);
        Assert.Equal(1u << 4, ClapAbi.TransportIsPlaying);
        Assert.Equal(9, ClapAbi.TransportEvent);
        Assert.Equal(1L << 31, ClapAbi.BeatTimeFactor);
    }

    /// <summary>And the context itself is where the plugin looks for it inside the call.</summary>
    /// <remarks>
    /// The pointer to it is the last field of ProcessData, which is the struct actually handed
    /// across. It sitting anywhere else is a plugin reading the time out of an event list.
    /// </remarks>
    [Fact]
    public void The_context_is_the_last_thing_in_a_process_call()
    {
        Assert.Equal(Marshal.SizeOf<ProcessData>() - System.IntPtr.Size,
                     (int)Marshal.OffsetOf<ProcessData>("ProcessContext"));
    }
}
