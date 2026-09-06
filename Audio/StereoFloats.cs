using System;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class StereoFloats : IStereoFloats
{
    /// <summary>
    /// What a 16 bit sample is divided by, which is the same number the other direction
    /// multiplies by.
    /// </summary>
    /// <remarks>
    /// 32768 and not 32767, matching <see cref="SixteenBit.FullScale"/>. The two have to be the
    /// same number or a take heard on the way in and written on the way out would differ by a
    /// hair that nobody could account for later, which is the trap
    /// <c>Tests/TakeEffectsTests.cs</c> already pins for the round trip through a chain.
    /// </remarks>
    private const float FullScale = 32768f;

    /// <inheritdoc/>
    public int Room(int bytes, int channels) => Frames(bytes, channels) * 2;

    /// <inheritdoc/>
    public int Read(byte[] data, int bytes, int channels, float[] into)
    {
        if (data == null || into == null) return 0;

        int wide = Math.Max(1, channels);
        int frames = Frames(Math.Min(bytes, data.Length), wide);

        if (frames <= 0) return 0;

        frames = Math.Min(frames, into.Length / 2);

        if (frames <= 0) return 0;

        for (int frame = 0; frame < frames; frame++)
        {
            int at = frame * wide * 2;

            float left = Sample(data, at);
            float right = wide > 1 ? Sample(data, at + 2) : left;

            into[frame * 2] = left;
            into[frame * 2 + 1] = right;
        }

        return frames * 2;
    }

    /// <summary>How many whole frames a block holds.</summary>
    /// <param name="bytes">How many bytes are real.</param>
    /// <param name="channels">How wide the capture is.</param>
    private static int Frames(int bytes, int channels)
    {
        int wide = Math.Max(1, channels);

        if (bytes <= 0) return 0;

        return bytes / (2 * wide);
    }

    /// <summary>One sample, read as signed with the little end first.</summary>
    /// <param name="data">The block.</param>
    /// <param name="at">Which byte the sample starts on.</param>
    private static float Sample(byte[] data, int at) =>
        (short)(data[at] | (data[at + 1] << 8)) / FullScale;
}
