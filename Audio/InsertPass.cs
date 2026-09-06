using System;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class InsertPass : IInsertPass
{
    /// <summary>
    /// The curve everything leaving this way goes through, the same one the master uses.
    /// </summary>
    /// <remarks>
    /// A pad's audio and what is coming in on the input never touch the tracker's mixer, so the
    /// guard on the master reaches neither of them.
    /// </remarks>
    private static readonly IOutputCurve Leaving = new OutputCurve();

    /// <inheritdoc/>
    public void Run(Plugins.Interfaces.IAudioInsert? insert, float[] scratch, IntPtr buffer, int length, int channels)
    {
        if (insert == null || scratch == null || buffer == IntPtr.Zero || length <= 0) return;

        int wide = Math.Max(1, channels);

        int samples = length / sizeof(float);
        if (samples <= 0) return;

        int frames = samples / wide;
        if (frames <= 0) return;

        int most = scratch.Length / 2;
        if (most <= 0) return;

        for (int start = 0; start < frames; start += most)
        {
            int take = Math.Min(most, frames - start);

            if (!Piece(insert, scratch, buffer, start, take, wide)) return;
        }
    }

    /// <summary>
    /// One piece of a block: out of the channel's buffer, through the effect, and back in.
    /// </summary>
    /// <remarks>
    /// More than two channels is unusual and the ones past the second are left alone rather than
    /// folded, since there is nothing here that says what a third channel means.
    /// </remarks>
    /// <param name="insert">The effect.</param>
    /// <param name="scratch">The stereo buffer to work in.</param>
    /// <param name="buffer">The channel's samples.</param>
    /// <param name="start">Which frame of them this piece begins at.</param>
    /// <param name="frames">How many frames this piece holds.</param>
    /// <param name="channels">How many channels the stream carries.</param>
    /// <returns>False where the effect fell over, which costs the rest of that block only.</returns>
    private static unsafe bool Piece(
        Plugins.Interfaces.IAudioInsert insert,
        float[] scratch,
        IntPtr buffer,
        int start,
        int frames,
        int channels)
    {
        float* audio = (float*)buffer + start * channels;

        if (channels == 1)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                scratch[frame * 2] = audio[frame];
                scratch[frame * 2 + 1] = audio[frame];
            }
        }
        else
        {
            for (int frame = 0; frame < frames; frame++)
            {
                scratch[frame * 2] = audio[frame * channels];
                scratch[frame * 2 + 1] = audio[frame * channels + 1];
            }
        }

        try
        {
            insert.Process(scratch, frames);
        }
        catch (Exception)
        {
            return false;
        }

        Leaving.Bend(scratch, frames * 2);

        if (channels == 1)
        {
            for (int frame = 0; frame < frames; frame++)
                audio[frame] = (scratch[frame * 2] + scratch[frame * 2 + 1]) * 0.5f;
        }
        else
        {
            for (int frame = 0; frame < frames; frame++)
            {
                audio[frame * channels] = scratch[frame * 2];
                audio[frame * channels + 1] = scratch[frame * 2 + 1];
            }
        }

        return true;
    }
}
