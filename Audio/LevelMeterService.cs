using ManagedBass;
using System;

namespace JingleBox2.Audio;

/// <summary>The loudest sample seen on each side, 0 to 1.</summary>
public readonly record struct StereoLevel(float Left, float Right)
{
    public static readonly StereoLevel Silent = new(0, 0);

    /// <summary>The louder of the two, for anything that shows a single bar.</summary>
    public float Peak => Math.Max(Left, Right);
}

public interface ILevelMeterService
{
    float GetLevelFromBytes(byte[]? data);
    float GetLevelFromHandle(int channelHandle);

    /// <summary>
    /// Both sides of interleaved 16 bit audio. A mono signal reports the same level twice, so
    /// the caller does not have to care which it was handed.
    /// </summary>
    StereoLevel GetStereoFromBytes(byte[]? data, int channels);

    StereoLevel GetStereoFromHandle(int channelHandle);
}

public sealed class LevelMeterService : ILevelMeterService
{
    public float GetLevelFromBytes(byte[]? data)
    {
        if (data == null || data.Length < 4) return 0;

        float maxLevel = 0;
        for (int i = 0; i < data.Length; i += 2)
        {
            if (i + 1 < data.Length)
            {
                short sample = (short)((data[i + 1] << 8) | data[i]);
                // Widen before Abs: Math.Abs(short.MinValue) throws OverflowException.
                float normalized = Math.Abs((int)sample) / 32768f;
                if (normalized > maxLevel) maxLevel = normalized;
            }
        }
        return Math.Clamp(maxLevel, 0, 1);
    }

    public StereoLevel GetStereoFromBytes(byte[]? data, int channels)
    {
        if (data == null || data.Length < 4) return StereoLevel.Silent;
        if (channels < 2) return Both(GetLevelFromBytes(data));

        float left = 0;
        float right = 0;

        // One frame is a sample per channel, side by side. Anything past the second channel is
        // not something a two bar meter can show, so it is stepped over.
        int bytesPerFrame = channels * 2;

        for (int frame = 0; frame + bytesPerFrame <= data.Length; frame += bytesPerFrame)
        {
            left = Math.Max(left, Sample(data, frame));
            right = Math.Max(right, Sample(data, frame + 2));
        }

        return new StereoLevel(Math.Clamp(left, 0, 1), Math.Clamp(right, 0, 1));
    }

    public StereoLevel GetStereoFromHandle(int channelHandle)
    {
        if (channelHandle == 0) return StereoLevel.Silent;

        int level = Bass.ChannelGetLevel(channelHandle);
        if (level < 0 || level == -1) return StereoLevel.Silent;

        // BASS packs the two sides into one value: the left in the high word, the right in the low.
        float left = ((level >> 16) & 0xFFFF) / 32768f;
        float right = (level & 0xFFFF) / 32768f;

        return new StereoLevel(Math.Clamp(left, 0, 1), Math.Clamp(right, 0, 1));
    }

    private static StereoLevel Both(float level) => new(level, level);

    /// <summary>One 16 bit sample as a level, widened before Abs: Abs(short.MinValue) throws.</summary>
    private static float Sample(byte[] data, int index)
    {
        short sample = (short)((data[index + 1] << 8) | data[index]);
        return Math.Abs((int)sample) / 32768f;
    }

    public float GetLevelFromHandle(int channelHandle)
    {
        if (channelHandle == 0) return 0;

        int level = Bass.ChannelGetLevel(channelHandle);
        if (level >= 0 && level != -1)
        {
            int left = (level >> 16) & 0xFFFF;
            int right = level & 0xFFFF;
            float peak = Math.Max(left, right) / 32768f;
            return Math.Clamp(peak, 0, 1);
        }
        return 0;
    }
}
