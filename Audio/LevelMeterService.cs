using ManagedBass;
using System;

namespace JingleBox2.Audio;

public interface ILevelMeterService
{
    float GetLevelFromBytes(byte[]? data);
    float GetLevelFromHandle(int channelHandle);
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
