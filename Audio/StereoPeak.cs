using System;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class StereoPeak : IStereoPeak
{
    /// <inheritdoc/>
    public (float Left, float Right) Of(float[]? block, int floats)
    {
        if (block == null) return (0, 0);

        int read = Math.Min(floats, block.Length);

        float left = 0;
        float right = 0;

        for (int i = 0; i + 1 < read; i += 2)
        {
            float l = Math.Abs(block[i]);
            float r = Math.Abs(block[i + 1]);

            if (l > left) left = l;
            if (r > right) right = r;
        }

        return (left > 1f ? 1f : left, right > 1f ? 1f : right);
    }
}
