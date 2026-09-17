using JingleBox2.Audio.Plugins.Bridge;
using JingleBox2.Audio.Plugins.Bridge.Enums;
using JingleBox2.Rack.SoundDevices.Timing;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Where the song is, crossing into the process the plugin lives in.
/// </summary>
/// <remarks>
/// It rides in the spare bytes of the shared block's header, which means offsets written by hand
/// on one side and read by hand on the other. Nothing checks that those two agree except this:
/// get one of them wrong and a plugin is handed a tempo of nought or a transport that is always
/// stopped, and the only sign is that it plays at the wrong speed.
/// </remarks>
public class BridgeTransportTests
{
    /// <summary>Enough frames for a block to be made at all, which is all this needs.</summary>
    private const int Frames = 64;

    /// <summary>Makes a block, does something with it, and takes it away again.</summary>
    private static void WithBlock(System.Action<BridgeBlock> doing)
    {
        var block = BridgeBlock.Create(Frames, out _);

        try
        {
            doing(block);
        }
        finally
        {
            block.Dispose();
        }
    }

    /// <summary>What went in comes out, every field of it.</summary>
    [Fact]
    public void A_transport_written_is_the_one_read_back()
    {
        WithBlock(block =>
        {
            block.Transport = new Transport(true, 137.5, 42.25, 7, 8);

            var back = block.Transport;

            Assert.True(back.Playing);
            Assert.Equal(137.5, back.Bpm);
            Assert.Equal(42.25, back.Beats);
            Assert.Equal(7, back.Numerator);
            Assert.Equal(8, back.Denominator);
        });
    }

    /// <summary>A stopped transport crosses as a stopped one rather than as nothing at all.</summary>
    [Fact]
    public void A_stopped_transport_crosses_as_stopped()
    {
        WithBlock(block =>
        {
            block.Transport = new Transport(false, 90.0, 12.0);

            var back = block.Transport;

            Assert.False(back.Playing);
            Assert.Equal(90.0, back.Bpm);
            Assert.Equal(12.0, back.Beats);
        });
    }

    /// <summary>
    /// A block nobody has written to answers with a transport that stands still, rather than with
    /// a tempo of nought that something downstream would divide by.
    /// </summary>
    [Fact]
    public void A_block_nobody_wrote_to_is_still()
    {
        WithBlock(block =>
        {
            var back = block.Transport;

            Assert.False(back.Playing);
            Assert.Equal(Transport.Still.Bpm, back.Bpm);
        });
    }

    /// <summary>
    /// And one written with nonsense answers the same way, since a plugin can no more use a
    /// tempo of nought than it could use no tempo at all.
    /// </summary>
    [Fact]
    public void Nonsense_crosses_as_stillness()
    {
        WithBlock(block =>
        {
            block.Transport = new Transport(true, 0.0, 5.0);

            Assert.Equal(Transport.Still.Bpm, block.Transport.Bpm);
        });
    }

    /// <summary>
    /// The transport does not land on anything else in the header.
    /// </summary>
    /// <remarks>
    /// The header also holds how many frames a block carries and how many events have been
    /// queued, and those are what the whole crossing runs on. A transport written over either of
    /// them would not be a plugin at the wrong tempo, it would be a plugin that stops working.
    /// </remarks>
    [Fact]
    public void It_does_not_write_over_the_rest_of_the_header()
    {
        WithBlock(block =>
        {
            block.Transport = new Transport(true, 200.0, 999.0, 5, 4);

            Assert.Equal(Frames, block.MaxFrames);
        });
    }

    /// <summary>
    /// And the events still cross with a transport sitting beside them, which is the other half
    /// of the same worry: the queue's own counters live in the header too.
    /// </summary>
    [Fact]
    public void Events_still_cross()
    {
        WithBlock(block =>
        {
            block.Transport = new Transport(true, 200.0, 999.0);

            block.Queue(BridgeEvent.NoteOn, 60, 100f);

            Assert.Single(block.Take());
            Assert.Equal(200.0, block.Transport.Bpm);
        });
    }
}
