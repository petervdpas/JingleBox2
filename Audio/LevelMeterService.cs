using ManagedBass;
using System;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class LevelMeterService : ILevelMeterService
{
    /// <inheritdoc/>
    public float GetLevelFromBytes(byte[]? data)
    {
        if (data == null || data.Length < 4) return 0;

        float maxLevel = 0;
        for (int i = 0; i < data.Length; i += 2)
        {
            if (i + 1 < data.Length)
            {
                float normalized = Sample(data, i);
                if (normalized > maxLevel) maxLevel = normalized;
            }
        }
        return Math.Clamp(maxLevel, 0, 1);
    }

    /// <inheritdoc/>
    public StereoLevel GetStereoFromBytes(byte[]? data, int channels)
    {
        if (data == null || data.Length < 4) return StereoLevel.Silent;
        if (channels < 2) return Both(GetLevelFromBytes(data));

        float left = 0;
        float right = 0;

        int bytesPerFrame = channels * 2;

        for (int frame = 0; frame + bytesPerFrame <= data.Length; frame += bytesPerFrame)
        {
            left = Math.Max(left, Sample(data, frame));
            right = Math.Max(right, Sample(data, frame + 2));
        }

        return new StereoLevel(Math.Clamp(left, 0, 1), Math.Clamp(right, 0, 1));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// BASS packs the two sides into one value, the left in the high word and the right in the
    /// low, and answers -1 for a channel that is not playing.
    /// </remarks>
    public StereoLevel GetStereoFromHandle(int channelHandle)
    {
        if (channelHandle == 0) return StereoLevel.Silent;

        int level = Bass.ChannelGetLevel(channelHandle);
        if (level < 0 || level == -1) return StereoLevel.Silent;

        float left = ((level >> 16) & 0xFFFF) / 32768f;
        float right = (level & 0xFFFF) / 32768f;

        return new StereoLevel(Math.Clamp(left, 0, 1), Math.Clamp(right, 0, 1));
    }

    /// <inheritdoc/>
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

    /// <summary>One level said twice, which is what a mono signal reads as on a two bar meter.</summary>
    private static StereoLevel Both(float level) => new(level, level);

    /// <summary>One 16 bit sample as a level.</summary>
    /// <remarks>
    /// Widened to an int before Abs, because Abs(short.MinValue) has no answer in a short and
    /// throws rather than saturating.
    /// </remarks>
    private static float Sample(byte[] data, int index)
    {
        short sample = (short)((data[index + 1] << 8) | data[index]);
        return Math.Abs((int)sample) / 32768f;
    }
}
