using System;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A captured block read as the floats a bus and an effect deal in.
/// </summary>
/// <remarks>
/// Every way this goes wrong is quiet. The two halves of a sample the wrong way round is noise
/// that sounds like a broken cable, an unsigned read is a signal sitting half a scale off nought
/// with no way to see it on a meter, and the wrong divisor is a monitor that is very nearly right
/// and always a hair too loud. None of the three looks like anything in code.
/// </remarks>
public sealed class StereoFloatsTests
{
    /// <summary>The rule under test.</summary>
    private readonly IStereoFloats _floats = new StereoFloats();

    /// <summary>One 16 bit sample as the two bytes a capture hands over.</summary>
    private static byte[] Bytes(params short[] samples)
    {
        var data = new byte[samples.Length * 2];

        for (int i = 0; i < samples.Length; i++)
        {
            data[i * 2] = (byte)(samples[i] & 0xFF);
            data[i * 2 + 1] = (byte)((samples[i] >> 8) & 0xFF);
        }

        return data;
    }

    /// <summary>A stereo block comes through as it stands, sides kept apart.</summary>
    [Fact]
    public void A_stereo_block_keeps_its_sides()
    {
        var into = new float[8];

        int written = _floats.Read(Bytes(16384, -16384, 32767, 0), 8, 2, into);

        Assert.Equal(4, written);
        Assert.Equal(0.5f, into[0], 4);
        Assert.Equal(-0.5f, into[1], 4);
        Assert.Equal(1f, into[2], 3);
        Assert.Equal(0f, into[3], 6);
    }

    /// <summary>A mono block is written to both sides rather than left half a signal.</summary>
    [Fact]
    public void A_mono_block_is_widened()
    {
        var into = new float[4];

        int written = _floats.Read(Bytes(16384, -8192), 4, 1, into);

        Assert.Equal(4, written);
        Assert.Equal(into[0], into[1]);
        Assert.Equal(into[2], into[3]);
        Assert.Equal(0.5f, into[0], 4);
        Assert.Equal(-0.25f, into[2], 4);
    }

    /// <summary>The bottom of the range is exactly minus one, which is what the divisor decides.</summary>
    /// <remarks>
    /// 32768 and not 32767, the same number the other direction multiplies by. Divided by 32767
    /// this reads past minus one, which is a sample outside full scale being handed to an effect.
    /// </remarks>
    [Fact]
    public void The_bottom_of_the_range_is_exactly_minus_one()
    {
        var into = new float[2];

        _floats.Read(Bytes(short.MinValue), 2, 1, into);

        Assert.Equal(-1f, into[0], 6);
    }

    /// <summary>A byte with nothing to pair with is left, since half a sample is not one.</summary>
    [Fact]
    public void An_odd_byte_is_left()
    {
        var into = new float[8];

        int written = _floats.Read(Bytes(16384, -16384), 3, 2, into);

        Assert.Equal(0, written);
    }

    /// <summary>What is claimed past the end of the block is not read.</summary>
    [Fact]
    public void A_length_past_the_block_is_held_to_it()
    {
        var into = new float[8];

        int written = _floats.Read(Bytes(16384), 999, 1, into);

        Assert.Equal(2, written);
    }

    /// <summary>A buffer too small takes what fits rather than throwing on the capture thread.</summary>
    [Fact]
    public void A_small_buffer_takes_what_fits()
    {
        var into = new float[2];

        int written = _floats.Read(Bytes(16384, 16384, 16384, 16384), 8, 2, into);

        Assert.Equal(2, written);
    }

    /// <summary>Nothing at all is nothing, rather than an exception.</summary>
    [Fact]
    public void Nothing_is_nothing()
    {
        var into = new float[4];

        Assert.Equal(0, _floats.Read(Array.Empty<byte>(), 0, 2, into));
        Assert.Equal(0, _floats.Read(Bytes(1), -4, 2, into));
        Assert.Equal(0, _floats.Read(Bytes(1), 2, 1, Array.Empty<float>()));
    }

    /// <summary>How much room a block needs, which is what the caller sizes its buffer by.</summary>
    [Fact]
    public void The_room_a_block_needs_is_two_for_every_frame()
    {
        Assert.Equal(4, _floats.Room(8, 2));
        Assert.Equal(8, _floats.Room(8, 1));
        Assert.Equal(0, _floats.Room(0, 2));
        Assert.Equal(0, _floats.Room(-8, 2));
    }

    /// <summary>A width of nought is read as mono rather than dividing by it.</summary>
    [Fact]
    public void No_width_at_all_is_read_as_one()
    {
        var into = new float[2];

        Assert.Equal(2, _floats.Read(Bytes(16384), 2, 0, into));
        Assert.Equal(0.5f, into[0], 4);
    }
}
