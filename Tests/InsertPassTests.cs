using System;
using System.Runtime.InteropServices;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Plugins.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A channel's block through the effect on it, which is what a pad's chain and the recording
/// input's chain both are.
/// </summary>
/// <remarks>
/// It was written inside the pad path and had no tests of its own, which is how the two things
/// it protects against could both have come back. **A block passed over is the start of every
/// pad playing dry**, since the first block BASS asks for is the whole playback buffer and is far
/// longer than the working buffer; and a NaN written back reaches the card, which is the one
/// fault here that is a room full of noise rather than a message on a status bar.
///
/// The buffer is pinned rather than faked, because what this does is arithmetic through a
/// pointer and a test over an array would be testing something else.
/// </remarks>
public sealed class InsertPassTests
{
    /// <summary>The pass under test.</summary>
    private readonly IInsertPass _pass = new InsertPass();

    /// <summary>An effect that writes down what it was given and can be told to misbehave.</summary>
    private sealed class Watching : IAudioInsert
    {
        /// <summary>How many frames it was handed, one entry per call.</summary>
        public System.Collections.Generic.List<int> Blocks { get; } = new();

        /// <summary>What every sample becomes, or nothing to leave them alone.</summary>
        public Func<float, float>? Bend { get; set; }

        /// <summary>Whether it falls over rather than working.</summary>
        public bool Throws { get; set; }

        /// <inheritdoc/>
        public void Process(float[] buffer, int frames)
        {
            Blocks.Add(frames);

            if (Throws) throw new InvalidOperationException("fell over");

            if (Bend == null) return;

            for (int i = 0; i < frames * 2; i++) buffer[i] = Bend(buffer[i]);
        }
    }

    /// <summary>Runs a block through, over a pinned array, and hands back what came out.</summary>
    private float[] Through(IAudioInsert insert, float[] audio, int channels, int scratchFrames = 256)
    {
        var copy = (float[])audio.Clone();
        var handle = GCHandle.Alloc(copy, GCHandleType.Pinned);

        try
        {
            _pass.Run(insert, new float[scratchFrames * 2], handle.AddrOfPinnedObject(), copy.Length * sizeof(float), channels);
        }
        finally
        {
            handle.Free();
        }

        return copy;
    }

    /// <summary>A stereo block goes through as it stands.</summary>
    [Fact]
    public void A_stereo_block_goes_through_the_effect()
    {
        var insert = new Watching { Bend = v => v * 0.5f };

        float[] answer = Through(insert, new[] { 1f, -1f, 0.5f, 0.25f }, 2);

        Assert.Equal(new[] { 0.5f, -0.5f, 0.25f, 0.125f }, answer);
        Assert.Equal(new[] { 2 }, insert.Blocks);
    }

    /// <summary>A mono channel is widened for the effect and folded back afterwards.</summary>
    [Fact]
    public void A_mono_block_is_widened_and_folded()
    {
        var insert = new Watching();

        float[] answer = Through(insert, new[] { 0.25f, -0.5f }, 1);

        Assert.Equal(new[] { 0.25f, -0.5f }, answer);
        Assert.Equal(new[] { 2 }, insert.Blocks);
    }

    /// <summary>What the effect did reaches a mono channel, folded back onto its one side.</summary>
    /// <remarks>
    /// Half of full scale rather than a number that would be bent on the way out, since what is
    /// asked here is whether the fold carries the effect's work and not what the curve does.
    /// </remarks>
    [Fact]
    public void A_mono_block_hears_what_the_effect_did()
    {
        var insert = new Watching { Bend = v => v + 0.5f };

        float[] answer = Through(insert, new[] { 0f, 0f }, 1);

        Assert.Equal(new[] { 0.5f, 0.5f }, answer);
    }

    /// <summary>
    /// **A block longer than the working buffer is worked through in pieces and never skipped.**
    /// </summary>
    [Fact]
    public void A_long_block_is_worked_through_in_pieces()
    {
        var insert = new Watching { Bend = v => 1f };
        var audio = new float[1000 * 2];

        float[] answer = Through(insert, audio, 2, scratchFrames: 256);

        Assert.Equal(new[] { 256, 256, 256, 232 }, insert.Blocks);
        Assert.All(answer, v => Assert.True(v > 0.9f, "a frame was passed over and came out at " + v));
    }

    /// <summary>**Nothing that is not a number is written back**, whatever the effect handed over.</summary>
    [Fact]
    public void What_is_not_a_number_is_silenced()
    {
        var insert = new Watching { Bend = _ => float.NaN };

        float[] answer = Through(insert, new[] { 0.5f, 0.5f }, 2);

        Assert.All(answer, v => Assert.Equal(0f, v));
    }

    /// <summary>What is merely too loud is bent rather than left past full scale.</summary>
    [Fact]
    public void What_is_too_loud_is_bent()
    {
        var insert = new Watching { Bend = _ => 4f };

        float[] answer = Through(insert, new[] { 0f, 0f }, 2);

        Assert.All(answer, v => Assert.True(v <= 1f && v > 0.5f, "a sample left at " + v));
    }

    /// <summary>An effect that falls over costs the rest of that block and no more.</summary>
    [Fact]
    public void An_effect_that_throws_is_survived()
    {
        var insert = new Watching { Throws = true };

        float[] answer = Through(insert, new[] { 0.5f, -0.5f }, 2);

        Assert.Equal(new[] { 0.5f, -0.5f }, answer);
    }

    /// <summary>And it stops there rather than being asked for every remaining piece.</summary>
    [Fact]
    public void An_effect_that_throws_is_not_asked_again()
    {
        var insert = new Watching { Throws = true };

        Through(insert, new float[1000 * 2], 2, scratchFrames: 256);

        Assert.Single(insert.Blocks);
    }

    /// <summary>No effect at all leaves the audio exactly as it was.</summary>
    [Fact]
    public void No_effect_leaves_the_block_alone()
    {
        float[] answer = Through(null!, new[] { 0.5f, -0.5f }, 2);

        Assert.Equal(new[] { 0.5f, -0.5f }, answer);
    }

    /// <summary>Nothing to work on is nothing to do, rather than a fault on the audio thread.</summary>
    [Fact]
    public void Nothing_to_work_on_does_nothing()
    {
        var insert = new Watching();

        _pass.Run(insert, new float[16], IntPtr.Zero, 64, 2);
        _pass.Run(insert, new float[16], new IntPtr(1), 0, 2);
        _pass.Run(insert, new float[16], new IntPtr(1), -4, 2);
        _pass.Run(insert, Array.Empty<float>(), new IntPtr(1), 64, 2);

        Assert.Empty(insert.Blocks);
    }

    /// <summary>A block too short to hold one frame is left rather than half read.</summary>
    [Fact]
    public void A_block_short_of_a_frame_is_left()
    {
        var insert = new Watching();

        float[] answer = Through(insert, new[] { 0.5f }, 2);

        Assert.Equal(new[] { 0.5f }, answer);
        Assert.Empty(insert.Blocks);
    }
}
