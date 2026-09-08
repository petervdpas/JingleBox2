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
        int room = Room(block, ref count, from);

        if (room <= 0) return Array.Empty<byte>();

        var made = new byte[room];

        Down(block, count, from, made);

        return made;
    }

    /// <inheritdoc/>
    public int Down(byte[]? block, int count, CaptureFormat from, byte[]? into)
    {
        int room = Room(block, ref count, from);

        if (room <= 0 || into == null || into.Length < room) return 0;

        if (from.Bits == 16 && !from.Floats) Whole(block!, count, 2, into);
        else if (from.Bits == 32 && from.Floats) Floats(block!, count, into);
        else if (from.Bits == 32) Narrow(block!, count, 4, 2, into);
        else Narrow(block!, count, 3, 1, into);

        return room;
    }

    /// <inheritdoc/>
    public int Room(int count, CaptureFormat from)
    {
        int width = Width(from);

        return width == 0 || count <= 0 ? 0 : count / width * 2;
    }

    /// <summary>
    /// How much room a block needs, and how much of it will be read.
    /// </summary>
    /// <remarks>
    /// Both answers at once because they are the same arithmetic, and the count is held to what
    /// the block really has here rather than in each of the two ways in: a caller that says more
    /// bytes than it handed over is what a capture looks like when a device is closing.
    /// </remarks>
    /// <param name="block">The audio as it arrived.</param>
    /// <param name="count">How many bytes are claimed, held to what is there.</param>
    /// <param name="from">What those bytes are made of.</param>
    private static int Room(byte[]? block, ref int count, CaptureFormat from)
    {
        if (block == null || count <= 0) return 0;

        count = Math.Min(count, block.Length);

        int width = Width(from);

        return width == 0 ? 0 : count / width * 2;
    }

    /// <summary>How wide one sample of that shape is, or nought for one nothing here reads.</summary>
    private static int Width(CaptureFormat from)
    {
        if (from.Bits == 16 && !from.Floats) return 2;
        if (from.Bits == 32) return 4;
        if (from.Bits == 24 && !from.Floats) return 3;

        return 0;
    }

    /// <summary>The block as it stands, trimmed to whole samples.</summary>
    /// <remarks>
    /// Handed back as its own array rather than the one that arrived, since the capture goes on
    /// using its buffer for the next block and everything above keeps what it is given.
    /// </remarks>
    private static void Whole(byte[] block, int count, int width, byte[] into) =>
        Buffer.BlockCopy(block, 0, into, 0, count - (count % width));

    /// <summary>Floating point samples, held at full scale and silenced where they are not numbers.</summary>
    private static void Floats(byte[] block, int count, byte[] into)
    {
        int samples = count / 4;

        for (int sample = 0; sample < samples; sample++)
        {
            float value = BitConverter.ToSingle(block, sample * 4);

            int written = float.IsNaN(value)
                ? 0
                : Math.Clamp((int)MathF.Round(value * FullScale), Floor, Ceiling);

            into[sample * 2] = (byte)(written & 0xFF);
            into[(sample * 2) + 1] = (byte)((written >> 8) & 0xFF);
        }
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
    /// <param name="into">Where the samples go.</param>
    private static void Narrow(byte[] block, int count, int width, int skip, byte[] into)
    {
        int samples = count / width;

        for (int sample = 0; sample < samples; sample++)
        {
            int at = (sample * width) + skip;

            into[sample * 2] = block[at];
            into[(sample * 2) + 1] = block[at + 1];
        }
    }
}
