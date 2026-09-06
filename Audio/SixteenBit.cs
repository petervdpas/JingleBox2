using System;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class SixteenBit : ISixteenBit
{
    /// <summary>What full scale is on the way down, and the same number both ways.</summary>
    /// <remarks>
    /// 32768 rather than 32767, which is the rule <c>ITakeEffects</c> already keeps and for the
    /// same reason: two different numbers for the two directions make a take come back a hair
    /// quieter than it went in, which is a difference nobody can account for a week later.
    /// </remarks>
    private const float FullScale = 32768f;

    /// <summary>The loudest and quietest a sample can be written as.</summary>
    private const int Ceiling = 32767;

    /// <inheritdoc cref="Ceiling"/>
    private const int Floor = -32768;

    /// <inheritdoc/>
    public byte[] Down(byte[]? block, int count, CaptureFormat from)
    {
        if (block == null || count <= 0) return Array.Empty<byte>();

        count = Math.Min(count, block.Length);

        if (from.Bits == 16 && !from.Floats) return Whole(block, count, 2);
        if (from.Bits == 32 && from.Floats) return Floats(block, count);
        if (from.Bits == 32 && !from.Floats) return Narrow(block, count, 4, 2);
        if (from.Bits == 24 && !from.Floats) return Narrow(block, count, 3, 1);

        return Array.Empty<byte>();
    }

    /// <summary>The block as it stands, trimmed to whole samples.</summary>
    /// <remarks>
    /// Handed back as its own array rather than the one that arrived, since the capture goes on
    /// using its buffer for the next block and everything above keeps what it is given.
    /// </remarks>
    private static byte[] Whole(byte[] block, int count, int width)
    {
        int bytes = count - (count % width);
        var same = new byte[bytes];

        Buffer.BlockCopy(block, 0, same, 0, bytes);

        return same;
    }

    /// <summary>Floating point samples, held at full scale and silenced where they are not numbers.</summary>
    private static byte[] Floats(byte[] block, int count)
    {
        int samples = count / 4;
        var sixteen = new byte[samples * 2];

        for (int sample = 0; sample < samples; sample++)
        {
            float value = BitConverter.ToSingle(block, sample * 4);

            int written = float.IsNaN(value)
                ? 0
                : Math.Clamp((int)MathF.Round(value * FullScale), Floor, Ceiling);

            sixteen[sample * 2] = (byte)(written & 0xFF);
            sixteen[(sample * 2) + 1] = (byte)((written >> 8) & 0xFF);
        }

        return sixteen;
    }

    /// <summary>
    /// Wider integer samples, by keeping their top two bytes.
    /// </summary>
    /// <remarks>
    /// Which is a truncation rather than a rounding, and is what every converter does with the
    /// bits it has no room for: the error is under one step of what is being written and is
    /// below the noise of anything that was captured.
    /// </remarks>
    /// <param name="block">The audio as it arrived.</param>
    /// <param name="count">How many bytes of it are real.</param>
    /// <param name="width">How wide one sample is in it.</param>
    /// <param name="skip">How many of its low bytes to drop.</param>
    private static byte[] Narrow(byte[] block, int count, int width, int skip)
    {
        int samples = count / width;
        var sixteen = new byte[samples * 2];

        for (int sample = 0; sample < samples; sample++)
        {
            int at = (sample * width) + skip;

            sixteen[sample * 2] = block[at];
            sixteen[(sample * 2) + 1] = block[at + 1];
        }

        return sixteen;
    }
}
