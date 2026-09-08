using JingleBox2.Audio;
using JingleBox2.Audio.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Reading a block down into what everything above the capture meets.
/// </summary>
/// <remarks>
/// The two ways in have to agree exactly, since one of them is what a take off the input holds
/// and the other is what a take off the recorder's bus holds, and a difference between them
/// would be two recordings of one performance that are not the same file.
/// </remarks>
public sealed class SixteenBitTests
{
    /// <summary>Floats, which is what a bus hands over.</summary>
    private static readonly CaptureFormat Floats = new(44100, 2, 32, true);

    /// <summary>One block of floats, at the levels the ends of the range are made of.</summary>
    private static byte[] Block(params float[] samples)
    {
        var bytes = new byte[samples.Length * 4];

        for (int at = 0; at < samples.Length; at++)
            System.BitConverter.GetBytes(samples[at]).CopyTo(bytes, at * 4);

        return bytes;
    }

    /// <summary>
    /// Written into a buffer is the same answer as handed back as one.
    /// </summary>
    /// <remarks>
    /// The whole reason the second way exists is that the mixing thread may not allocate, and
    /// the whole risk of it is that it becomes a second spelling of the arithmetic. This is what
    /// says it is not.
    /// </remarks>
    [Fact]
    public void Into_a_buffer_reads_exactly_as_handed_back()
    {
        var reader = new SixteenBit();
        byte[] block = Block(0f, 0.5f, -0.5f, 1f, -1f, 2f, float.NaN, -0.000030517578125f);

        byte[] made = reader.Down(block, block.Length, Floats);

        var into = new byte[reader.Room(block.Length, Floats)];

        Assert.Equal(made.Length, reader.Down(block, block.Length, Floats, into));
        Assert.Equal(made, into);
    }

    /// <summary>A buffer with no room for the block is refused rather than half filled.</summary>
    /// <remarks>
    /// Half a block written into a take is a hole, and a hole is not something anybody would hear
    /// as a fault in the recorder.
    /// </remarks>
    [Fact]
    public void A_buffer_with_no_room_is_refused()
    {
        var reader = new SixteenBit();
        byte[] block = Block(0.25f, 0.25f, 0.25f, 0.25f);

        Assert.Equal(0, reader.Down(block, block.Length, Floats, new byte[2]));
    }

    /// <summary>How much room a block needs is what reading it writes.</summary>
    [Fact]
    public void The_room_is_what_gets_written()
    {
        var reader = new SixteenBit();

        foreach (var shape in new[]
        {
            Floats,
            new CaptureFormat(44100, 2, 16, false),
            new CaptureFormat(44100, 2, 32, false),
            new CaptureFormat(44100, 2, 24, false)
        })
        {
            var block = new byte[48];

            Assert.Equal(reader.Room(block.Length, shape), reader.Down(block, block.Length, shape).Length);
        }
    }
}
