using System;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Plugins.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// One block buffer per call rather than one kept on the instance, because this runs once at the
/// end of a take on a thread nobody is waiting on: there is nothing here to be careful about, and
/// a buffer kept would be a buffer two takes could be inside at once.
/// </remarks>
public sealed class TakeEffects : ITakeEffects
{
    /// <summary>How wide a chain works, and therefore how wide what comes out of one is.</summary>
    public const int Stereo = 2;

    /// <summary>Two bytes to a sample, which is what 16 bit means.</summary>
    private const int BytesPerSample = 2;

    /// <summary>
    /// What a sample is scaled by in both directions.
    /// </summary>
    /// <remarks>
    /// 32768 rather than 32767, and the same number each way, so a value read out and written
    /// straight back is the number it started as. With 32767 on the way back a chain with nothing
    /// on it would hand back a take a hair quieter than the one it was given, which is the sort of
    /// difference that is impossible to account for afterwards.
    /// </remarks>
    private const float FullScale = 32768f;

    /// <inheritdoc/>
    public int Channels => Stereo;

    /// <inheritdoc/>
    public byte[] Through(byte[] pcm, int channels, IAudioInsert effect, int maxFrames)
    {
        if (pcm == null || effect == null || channels < 1 || maxFrames < 1) return Array.Empty<byte>();

        int wide = channels * BytesPerSample;
        int frames = pcm.Length / wide;

        if (frames < 1) return Array.Empty<byte>();

        var block = new float[maxFrames * Stereo];
        var made = new byte[frames * Stereo * BytesPerSample];

        for (int at = 0; at < frames; at += maxFrames)
        {
            int count = Math.Min(maxFrames, frames - at);

            Read(pcm, channels, at, count, block);

            effect.Process(block, count);

            Written(block, count, made, at);
        }

        return made;
    }

    /// <inheritdoc/>
    public void Settle(IAudioInsert effect, int frames, int maxFrames)
    {
        if (effect == null || frames < 1 || maxFrames < 1) return;

        var block = new float[maxFrames * Stereo];

        for (int at = 0; at < frames; at += maxFrames)
        {
            int count = Math.Min(maxFrames, frames - at);

            Array.Clear(block, 0, count * Stereo);

            effect.Process(block, count);
        }
    }

    /// <summary>Reads part of a take into a block, as the two sides a chain works on.</summary>
    /// <remarks>
    /// A take of one channel is the same sample on both sides, which is what mono means and is
    /// what every other host does with it. A take of more than two is read as its first two.
    /// </remarks>
    /// <param name="pcm">The take.</param>
    /// <param name="channels">How wide it is.</param>
    /// <param name="from">The first frame to read.</param>
    /// <param name="count">How many frames to read.</param>
    /// <param name="into">The block to fill.</param>
    private static void Read(byte[] pcm, int channels, int from, int count, float[] into)
    {
        int wide = channels * BytesPerSample;

        for (int frame = 0; frame < count; frame++)
        {
            int at = (from + frame) * wide;

            short left = (short)(pcm[at] | (pcm[at + 1] << 8));
            short right = channels > 1 ? (short)(pcm[at + 2] | (pcm[at + 3] << 8)) : left;

            into[frame * Stereo] = left / FullScale;
            into[frame * Stereo + 1] = right / FullScale;
        }
    }

    /// <summary>Writes a worked-on block back into the take being built.</summary>
    /// <param name="block">What the chain left behind.</param>
    /// <param name="count">How many frames of it are this block's.</param>
    /// <param name="into">The take being built.</param>
    /// <param name="from">The frame that block starts at.</param>
    private static void Written(float[] block, int count, byte[] into, int from)
    {
        for (int index = 0; index < count * Stereo; index++)
        {
            float value = block[index];

            int sample = float.IsNaN(value) ? 0 : (int)MathF.Round(value * FullScale);

            if (sample > short.MaxValue) sample = short.MaxValue;
            else if (sample < short.MinValue) sample = short.MinValue;

            int at = (from * Stereo + index) * BytesPerSample;

            into[at] = (byte)(sample & 0xFF);
            into[at + 1] = (byte)((sample >> 8) & 0xFF);
        }
    }
}
