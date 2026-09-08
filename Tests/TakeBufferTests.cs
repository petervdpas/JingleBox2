using System;
using System.Threading;
using System.Threading.Tasks;
using JingleBox2.Audio;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What the capture has heard, and the moment a take stops.
/// </summary>
/// <remarks>
/// **This exists because of a fault that destroyed takes and said nothing.** Stopping used to be
/// two acts: a flag saying the take was over, and the audio still sitting in the buffer. A block
/// arriving between the two was read as monitoring and trimmed the buffer back to its last fifth
/// of a second, so a four second performance was saved as 200 milliseconds of near silence,
/// under the right name, with a waveform that drew a flat line. Nothing was logged, because from
/// the inside nothing had gone wrong.
///
/// So what is asked here is never "does it keep the audio". It is what happens at the seam, and
/// it is asked with two threads, because one thread cannot fail the way this failed.
/// </remarks>
public class TakeBufferTests
{
    /// <summary>A block of a given length, filled so it can be told from silence.</summary>
    private static byte[] Block(int bytes, byte with = 7)
    {
        var block = new byte[bytes];
        Array.Fill(block, with);
        return block;
    }

    /// <summary>Everything heard during a take is what the take is.</summary>
    [Fact]
    public void A_take_is_everything_heard_while_it_was_being_made()
    {
        var buffer = new TakeBuffer();

        buffer.Start();

        for (int at = 0; at < 40; at++) buffer.Add(Block(4096));

        byte[] take = buffer.Stop();

        Assert.Equal(40 * 4096, take.Length);
        Assert.Equal(take, buffer.Take);
    }

    /// <summary>Past the monitor's length, so a trim would be visible.</summary>
    /// <remarks>
    /// Forty blocks of four kilobytes is 160 KB against the monitor's 35, so anything that trims
    /// while a take is being made shows up as a take five times too short rather than as a
    /// rounding.
    /// </remarks>
    [Fact]
    public void A_take_is_kept_whole_however_much_longer_it_is_than_the_monitor()
    {
        var buffer = new TakeBuffer();

        buffer.Start();

        for (int at = 0; at < 40; at++) buffer.Add(Block(4096));

        Assert.True(buffer.Stop().Length > TakeBuffer.MonitorBytes * 4);
    }

    /// <summary>With no take being made, only the last moment is kept.</summary>
    [Fact]
    public void Watching_the_input_keeps_only_the_last_moment()
    {
        var buffer = new TakeBuffer();

        for (int at = 0; at < 40; at++) buffer.Add(Block(4096));

        Assert.True(buffer.Recent(int.MaxValue, 4).Length <= TakeBuffer.MonitorBytes);
    }

    /// <summary>
    /// A block arriving at the very moment a take stops does not take the take with it.
    /// </summary>
    /// <remarks>
    /// **The fault, said in a test.** One thread is the capture callback, adding as fast as it
    /// can; the other stops the take. The take has to be everything that was added before the
    /// stop and nothing later, and above all it may not come back as the monitor's last fifth of
    /// a second. Run a hundred times, because a race that fires one time in twenty passes a test
    /// that runs it once.
    /// </remarks>
    [Fact]
    public async Task A_block_arriving_as_the_take_stops_does_not_shorten_it()
    {
        for (int run = 0; run < 100; run++)
        {
            var buffer = new TakeBuffer();
            var going = true;

            buffer.Start();

            for (int at = 0; at < 40; at++) buffer.Add(Block(4096));

            var capture = Task.Run(() =>
            {
                while (Volatile.Read(ref going)) buffer.Add(Block(64));
            });

            await Task.Delay(1);

            byte[] take = buffer.Stop();

            Volatile.Write(ref going, false);
            await capture;

            Assert.True(
                take.Length >= 40 * 4096,
                "run " + run + " lost the take: " + take.Length + " bytes, and the monitor keeps "
                    + TakeBuffer.MonitorBytes);
        }
    }

    /// <summary>What the capture goes on hearing after a take does not reach the take.</summary>
    /// <remarks>
    /// The other half of the same seam. The input is left open while the meter is being watched,
    /// so blocks keep arriving after a take is over, and they belong to the monitor rather than
    /// to what is about to be written down.
    /// </remarks>
    [Fact]
    public void What_arrives_after_a_take_does_not_join_it()
    {
        var buffer = new TakeBuffer();

        buffer.Start();
        buffer.Add(Block(4096));

        byte[] take = buffer.Stop();

        for (int at = 0; at < 40; at++) buffer.Add(Block(4096, 9));

        Assert.Equal(4096, buffer.Take.Length);
        Assert.Equal(take, buffer.Take);
        Assert.All(buffer.Take, one => Assert.Equal(7, one));
    }

    /// <summary>Stopping without a take being made hands back nothing rather than the monitor.</summary>
    /// <remarks>
    /// A stop is answered by the recorder before it reaches here, but the buffer may not depend
    /// on that: handing back the monitor's last moment would write a file nobody asked for.
    /// </remarks>
    [Fact]
    public void Stopping_when_nothing_was_being_recorded_is_nothing()
    {
        var buffer = new TakeBuffer();

        for (int at = 0; at < 10; at++) buffer.Add(Block(4096));

        Assert.Empty(buffer.Stop());
        Assert.Empty(buffer.Take);
    }

    /// <summary>Starting a take throws away the take before it.</summary>
    [Fact]
    public void Starting_a_take_throws_the_last_one_away()
    {
        var buffer = new TakeBuffer();

        buffer.Start();
        buffer.Add(Block(4096));
        buffer.Stop();

        buffer.Start();

        Assert.Empty(buffer.Take);
    }

    /// <summary>Reset leaves nothing behind, since it runs before an input that may not open.</summary>
    [Fact]
    public void Reset_leaves_nothing_behind()
    {
        var buffer = new TakeBuffer();

        buffer.Start();
        buffer.Add(Block(4096));
        buffer.Stop();

        buffer.Reset();

        Assert.False(buffer.Recording);
        Assert.Empty(buffer.Take);
        Assert.Empty(buffer.Recent(int.MaxValue, 4));
    }

    /// <summary>The last moment is a whole number of frames, however much was asked for.</summary>
    /// <remarks>
    /// A meter reading a part frame has its channels the wrong way round from there on, which
    /// draws as the two sides swapping.
    /// </remarks>
    [Theory]
    [InlineData(1000, 4)]
    [InlineData(999, 4)]
    [InlineData(1001, 6)]
    public void The_last_moment_is_whole_frames(int asked, int frame)
    {
        var buffer = new TakeBuffer();

        buffer.Add(Block(8192));

        Assert.Equal(0, buffer.Recent(asked, frame).Length % frame);
    }

    /// <summary>Nothing sensible asked for is nothing back, rather than a throw.</summary>
    [Theory]
    [InlineData(0, 4)]
    [InlineData(10, 0)]
    [InlineData(2, 4)]
    public void Nothing_sensible_asked_for_is_nothing_back(int asked, int frame)
    {
        var buffer = new TakeBuffer();

        buffer.Add(Block(8192));

        Assert.Empty(buffer.Recent(asked, frame));
    }

    /// <summary>A block that is not there is ignored rather than thrown at.</summary>
    [Fact]
    public void A_block_that_is_not_there_is_ignored()
    {
        var buffer = new TakeBuffer();

        buffer.Start();
        buffer.Add(null!);
        buffer.Add(Array.Empty<byte>());
        buffer.Add(Block(64));

        Assert.Equal(64, buffer.Stop().Length);
    }

    /// <summary>
    /// Only the part of a reused buffer that is this block is kept.
    /// </summary>
    /// <remarks>
    /// The one that reads the recorder's bus keeps its own buffer, as long as the longest block
    /// it has ever had, so what sits past a shorter block is the tail of an older one. Written
    /// into a take that is a moment of something that really happened, repeated, which is the
    /// hardest kind of fault to hear as a fault rather than as a performance.
    /// </remarks>
    [Fact]
    public void Only_this_much_of_a_reused_buffer_is_kept()
    {
        var buffer = new TakeBuffer();

        buffer.Start();

        buffer.Add(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 8);
        buffer.Add(new byte[] { 9, 10, 3, 4, 5, 6, 7, 8 }, 2);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, buffer.Stop());
    }

    /// <summary>A count past the end of the buffer is held to what is really there.</summary>
    [Fact]
    public void More_than_there_is_keeps_what_there_is()
    {
        var buffer = new TakeBuffer();

        buffer.Start();

        buffer.Add(new byte[] { 1, 2 }, 64);

        Assert.Equal(new byte[] { 1, 2 }, buffer.Stop());
    }

    /// <summary>
    /// A take that runs over many blocks comes back in order, byte for byte.
    /// </summary>
    /// <remarks>
    /// The whole risk of keeping a take in blocks rather than in one array is the joins, and
    /// every way a join goes wrong leaves a take of exactly the right length: a block copied
    /// twice, one dropped, or two swapped. So it is checked by content and not by length, with a
    /// pattern that says where each byte came from.
    /// </remarks>
    [Fact]
    public void A_take_over_many_blocks_comes_back_in_order()
    {
        var buffer = new TakeBuffer();
        var written = new System.Collections.Generic.List<byte>();

        buffer.Start();

        for (int round = 0; round < 40; round++)
        {
            var block = new byte[3000];

            for (int at = 0; at < block.Length; at++) block[at] = (byte)((round * 7) + at);

            written.AddRange(block);
            buffer.Add(block);
        }

        byte[] take = buffer.Stop();

        Assert.True(take.Length > TakeBuffer.ChunkBytes * 3, "the take was too short to cross the joins");
        Assert.Equal(written.ToArray(), take);
    }

    /// <summary>A block longer than one of them is kept whole rather than to the first join.</summary>
    [Fact]
    public void A_block_longer_than_one_of_them_is_kept_whole()
    {
        var buffer = new TakeBuffer();
        var block = new byte[(TakeBuffer.ChunkBytes * 2) + 17];

        for (int at = 0; at < block.Length; at++) block[at] = (byte)at;

        buffer.Start();
        buffer.Add(block);

        Assert.Equal(block, buffer.Stop());
    }

    /// <summary>
    /// Monitoring holds the last moment and no more of it, and it really is the last.
    /// </summary>
    /// <remarks>
    /// Blocks are dropped off the front to keep it, and dropping whole blocks would be easier and
    /// would leave the meter reading a moment that is up to one block longer than it says. What is
    /// pinned here is both halves: how much is held, and that it is the end of what went in.
    /// </remarks>
    [Fact]
    public void Monitoring_holds_the_last_moment_exactly()
    {
        var buffer = new TakeBuffer();
        byte written = 0;

        for (int round = 0; round < 60; round++)
        {
            var block = new byte[4000];

            for (int at = 0; at < block.Length; at++) block[at] = written++;

            buffer.Add(block);
        }

        byte[] recent = buffer.Recent(int.MaxValue, 4);

        Assert.Equal(TakeBuffer.MonitorBytes, recent.Length);
        Assert.Equal((byte)(written - 1), recent[^1]);

        for (int at = 1; at < recent.Length; at++)
            Assert.Equal((byte)(recent[at - 1] + 1), recent[at]);
    }

    /// <summary>What was heard while nobody was recording is not in the take that follows.</summary>
    [Fact]
    public void The_last_moment_is_not_the_start_of_the_next_take()
    {
        var buffer = new TakeBuffer();

        buffer.Add(new byte[8000]);
        buffer.Start();
        buffer.Add(new byte[] { 1, 2, 3, 4 });

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, buffer.Stop());
    }
}
