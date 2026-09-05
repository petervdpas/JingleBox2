using System;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;

namespace JingleBox2.SoundDevices.SoundEffects;

/// <inheritdoc/>
public sealed class LoudnessMakeup : ILoudnessMakeup
{
    /// <summary>
    /// How long the followers take to answer, in seconds.
    /// </summary>
    /// <remarks>
    /// Long enough to read programme level rather than the waveform, short enough that a passage
    /// getting louder is followed within a note. Under a cycle of the lowest note anybody plays it
    /// would be measuring the wave itself, and a gain moving at the rate of the wave is distortion
    /// rather than a correction.
    /// </remarks>
    public const double FollowSeconds = 0.05;

    /// <summary>The quietest mean square either follower is believed at.</summary>
    public const double Faintest = 1e-9;

    /// <summary>How far the makeup is allowed to reach downwards.</summary>
    public const double Quietest = 0.05;

    /// <summary>And upwards.</summary>
    public const double Loudest = 4.0;

    /// <summary>How much of each follower one sample carries over, worked out once.</summary>
    private readonly double _following;

    /// <summary>The mean square going into the curve.</summary>
    private double _dry;

    /// <summary>And the mean square coming out of it.</summary>
    private double _wet;

    /// <summary>Builds one at the rate it is about to be handed audio at.</summary>
    /// <param name="sampleRate">What the host is running at.</param>
    public LoudnessMakeup(int sampleRate)
    {
        double rate = sampleRate <= 0 ? 44100 : sampleRate;

        _following = 1.0 - Math.Exp(-1.0 / (FollowSeconds * rate));
    }

    /// <inheritdoc/>
    public void Saw(double dry, double wet)
    {
        if (!double.IsFinite(dry) || !double.IsFinite(wet)) return;

        _dry += (dry * dry - _dry) * _following;
        _wet += (wet * wet - _wet) * _following;
    }

    /// <inheritdoc/>
    public double Makeup
    {
        get
        {
            if (_dry < Faintest || _wet < Faintest) return 1;

            double makeup = Math.Sqrt(_dry / _wet);

            return double.IsFinite(makeup) ? Math.Clamp(makeup, Quietest, Loudest) : 1;
        }
    }
}
