using System.Collections.Generic;
using JingleBox2.Audio;
using JingleBox2.Audio.Plugins.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A finished take run through RECORD's effect chain.
/// </summary>
/// <remarks>
/// It is arithmetic between two 16 bit buffers with somebody else's code in the middle, which is
/// exactly the shape that goes wrong quietly: a sample scaled by a hair, a channel dropped, a
/// block boundary that plays a frame twice. None of that is audible as a fault, it is audible as
/// a take that sounds slightly wrong and cannot be argued with afterwards, so every one of them
/// is a number here.
/// </remarks>
public class TakeEffectsTests
{
    /// <summary>An effect that leaves what it is given exactly as it found it.</summary>
    private sealed class Untouched : IAudioInsert
    {
        public void Process(float[] buffer, int frames) { }
    }

    /// <summary>An effect that scales what it is given.</summary>
    private sealed class Scaling(float by) : IAudioInsert
    {
        public void Process(float[] buffer, int frames)
        {
            for (int index = 0; index < frames * 2; index++) buffer[index] *= by;
        }
    }

    /// <summary>An effect that writes down every frame it was handed, and how long each block was.</summary>
    private sealed class Watching : IAudioInsert
    {
        public List<int> Blocks { get; } = new();
        public List<float> Left { get; } = new();

        public void Process(float[] buffer, int frames)
        {
            Blocks.Add(frames);

            for (int frame = 0; frame < frames; frame++) Left.Add(buffer[frame * 2]);
        }
    }

    /// <summary>An effect that hands back something that is not a number.</summary>
    private sealed class Poison : IAudioInsert
    {
        public void Process(float[] buffer, int frames)
        {
            for (int index = 0; index < frames * 2; index++) buffer[index] = float.NaN;
        }
    }

    /// <summary>A take of the samples given, laid out for that many channels.</summary>
    private static byte[] Take(int channels, params short[] samples)
    {
        var bytes = new byte[samples.Length * 2];

        for (int at = 0; at < samples.Length; at++)
        {
            bytes[at * 2] = (byte)(samples[at] & 0xFF);
            bytes[at * 2 + 1] = (byte)((samples[at] >> 8) & 0xFF);
        }

        return bytes;
    }

    /// <summary>The samples in a take, read back.</summary>
    private static short[] Samples(byte[] pcm)
    {
        var samples = new short[pcm.Length / 2];

        for (int at = 0; at < samples.Length; at++)
            samples[at] = (short)(pcm[at * 2] | (pcm[at * 2 + 1] << 8));

        return samples;
    }

    /// <summary>
    /// A chain that changes nothing hands back the take it was given, sample for sample.
    /// </summary>
    /// <remarks>
    /// The reason the scaling is 32768 in both directions rather than 32768 out and 32767 back.
    /// With the two different, a take through an empty chain comes back a hair quieter than the
    /// one it went in as, which is a difference nobody could account for later.
    /// </remarks>
    [Fact]
    public void A_chain_that_changes_nothing_gives_the_take_back_unchanged()
    {
        var take = Take(2, 0, 1, -1, 32767, -32768, 12345, -9999, 250);

        byte[] through = new TakeEffects().Through(take, 2, new Untouched(), 4);

        Assert.Equal(take, through);
    }

    /// <summary>What an effect does to the samples is what comes out.</summary>
    [Fact]
    public void What_the_effect_does_is_what_is_written()
    {
        var take = Take(2, 1000, -1000, 2000, -2000);

        short[] through = Samples(new TakeEffects().Through(take, 2, new Scaling(0.5f), 8));

        Assert.Equal(new short[] { 500, -500, 1000, -1000 }, through);
    }

    /// <summary>A take of one channel comes back with two, the same on both sides.</summary>
    /// <remarks>
    /// An effect places things in the stereo field, so narrowing the answer back to one channel
    /// would throw half of what it did away. Mono in is the same sample on both sides, which is
    /// what mono means.
    /// </remarks>
    [Fact]
    public void A_mono_take_comes_back_in_two_channels()
    {
        var take = Take(1, 100, 200, 300);

        var effects = new TakeEffects();

        short[] through = Samples(effects.Through(take, 1, new Untouched(), 8));

        Assert.Equal(2, effects.Channels);
        Assert.Equal(new short[] { 100, 100, 200, 200, 300, 300 }, through);
    }

    /// <summary>A take of more than two channels is read as its first two.</summary>
    [Fact]
    public void A_wide_take_is_read_as_its_first_two_channels()
    {
        var take = Take(4, 10, 20, 30, 40, 50, 60, 70, 80);

        short[] through = Samples(new TakeEffects().Through(take, 4, new Untouched(), 8));

        Assert.Equal(new short[] { 10, 20, 50, 60 }, through);
    }

    /// <summary>
    /// Every frame goes past the effect once, in order, in blocks no longer than it was built for.
    /// </summary>
    /// <remarks>
    /// The block boundary is where an offline pass goes wrong, and the two ways it does are a
    /// frame played twice and a frame skipped. Both leave a take of the right length, so the
    /// order of what the effect actually saw is the only thing that says which happened.
    /// </remarks>
    [Fact]
    public void Every_frame_goes_past_the_effect_once_and_in_order()
    {
        var samples = new short[10 * 2];
        for (int frame = 0; frame < 10; frame++) samples[frame * 2] = (short)((frame + 1) * 1000);

        var watching = new Watching();

        new TakeEffects().Through(Take(2, samples), 2, watching, 4);

        Assert.Equal(new[] { 4, 4, 2 }, watching.Blocks);
        Assert.Equal(10, watching.Left.Count);

        for (int frame = 0; frame < 10; frame++)
            Assert.Equal((frame + 1) * 1000 / 32768f, watching.Left[frame], 6);
    }

    /// <summary>The processed take is exactly as long as the take it came from.</summary>
    /// <remarks>
    /// Deliberate: a delay still ringing at the last frame is cut off with it, so the two takes
    /// lie on top of each other frame for frame, which is what makes keeping both worth having.
    /// </remarks>
    [Fact]
    public void The_processed_take_is_as_long_as_the_one_it_came_from()
    {
        var take = Take(2, new short[64 * 2]);

        byte[] through = new TakeEffects().Through(take, 2, new Scaling(0.5f), 7);

        Assert.Equal(64, through.Length / 4);
    }

    /// <summary>An effect that pushes past full scale is held there rather than wrapped.</summary>
    /// <remarks>
    /// Written as a short, so a value past the top that was cast rather than clamped comes back
    /// as a large negative number: the loudest moment of a take arriving as the quietest, which
    /// is a click at full scale.
    /// </remarks>
    [Fact]
    public void An_effect_that_goes_past_full_scale_is_held_at_it()
    {
        var take = Take(2, 30000, -30000);

        short[] through = Samples(new TakeEffects().Through(take, 2, new Scaling(4f), 8));

        Assert.Equal(new short[] { short.MaxValue, short.MinValue }, through);
    }

    /// <summary>Anything that is not a number is written out as silence.</summary>
    /// <remarks>
    /// This ends in a file rather than at the converters, and a file full of NaN is one that
    /// plays as full scale noise the first time anybody opens it.
    /// </remarks>
    [Fact]
    public void Something_that_is_not_a_number_is_written_as_silence()
    {
        var take = Take(2, 1000, 1000, 1000, 1000);

        short[] through = Samples(new TakeEffects().Through(take, 2, new Poison(), 8));

        Assert.All(through, sample => Assert.Equal(0, sample));
    }

    /// <summary>Nothing to work on is nothing back, rather than a throw.</summary>
    /// <remarks>
    /// Every one of these is reachable: a take stopped the instant it started, a chain that has
    /// gone, a capture that reported no channels. This runs at the end of a take somebody has
    /// just played, so the one thing it may never do is lose the file by throwing.
    /// </remarks>
    [Theory]
    [InlineData(0, 2, 8)]
    [InlineData(4, 0, 8)]
    [InlineData(4, 2, 0)]
    [InlineData(2, 2, 8)]
    public void Nothing_to_work_on_is_nothing_back(int bytes, int channels, int frames)
    {
        byte[] through = new TakeEffects().Through(new byte[bytes], channels, new Untouched(), frames);

        Assert.Empty(through);
    }

    /// <summary>A chain that is not there is nothing back rather than a throw.</summary>
    [Fact]
    public void No_chain_at_all_is_nothing_back()
    {
        Assert.Empty(new TakeEffects().Through(Take(2, 1, 2), 2, null!, 8));
    }

    /// <summary>A part frame at the end is left behind rather than read past.</summary>
    [Fact]
    public void A_part_frame_at_the_end_is_left_behind()
    {
        var take = new byte[2 * 2 * 2 + 3];

        byte[] through = new TakeEffects().Through(take, 2, new Untouched(), 8);

        Assert.Equal(2, through.Length / 4);
    }

    /// <summary>Settling hands the chain that much silence, in blocks it was built for.</summary>
    /// <remarks>
    /// Silence and not the last take: a block that arrived holding whatever was in the buffer
    /// would be the very thing this is meant to flush, played through the chain again.
    /// </remarks>
    [Fact]
    public void Settling_hands_the_chain_silence()
    {
        var watching = new Watching();

        new TakeEffects().Settle(watching, 10, 4);

        Assert.Equal(new[] { 4, 4, 2 }, watching.Blocks);
        Assert.All(watching.Left, sample => Assert.Equal(0f, sample));
    }

    /// <summary>Settling nothing does nothing, rather than throwing.</summary>
    [Theory]
    [InlineData(0, 8)]
    [InlineData(8, 0)]
    public void Settling_nothing_does_nothing(int frames, int block)
    {
        var watching = new Watching();

        new TakeEffects().Settle(watching, frames, block);

        Assert.Empty(watching.Blocks);
    }
}
