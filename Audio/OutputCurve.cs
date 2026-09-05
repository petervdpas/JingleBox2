using System;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class OutputCurve : IOutputCurve
{
    /// <summary>Under this nothing is touched, so ordinary music leaves exactly as it was made.</summary>
    /// <remarks>
    /// Bending from nought up would quietly reshape every sample in the song on its way out,
    /// which is not this rule's business. Only what is loud enough to be a problem is touched.
    /// </remarks>
    public const float Knee = 0.7f;

    /// <inheritdoc/>
    public float Bend(float value)
    {
        if (!float.IsFinite(value)) return 0;

        float magnitude = MathF.Abs(value);

        if (magnitude <= Knee) return value;

        float over = (magnitude - Knee) / (1 - Knee);
        float shaped = Knee + (1 - Knee) * MathF.Tanh(over);

        return value < 0 ? -shaped : shaped;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Held to what the buffer can really take, the same rule everything on this path keeps: a
    /// caller claiming more than it handed over is what writing past the end of somebody's array
    /// looks like from in here.
    /// </remarks>
    public void Bend(float[] buffer, int samples)
    {
        if (buffer == null) return;

        int count = Math.Min(samples, buffer.Length);

        for (int at = 0; at < count; at++) buffer[at] = Bend(buffer[at]);
    }
}
