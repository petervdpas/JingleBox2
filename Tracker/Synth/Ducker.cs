using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// One track pushing another down: the kick keying the bass, the way a desk does it with a
/// compressor and a side chain. Follows the key track's level and turns that into a gain.
/// </summary>
/// <remarks>
/// Deliberately not a compressor. There is no threshold, no ratio and no knee: the depth knob
/// says how far down the track goes when the key is at full scale, and the track follows the
/// key in proportion below that. It is the effect people actually reach for, with two controls
/// instead of five.
///
/// The attack is fixed and fast. A slow duck attack leaves the first part of the key note
/// fighting the track it is meant to be clearing room for, which is the one thing this is for.
///
/// One per strip, and <see cref="Next"/> runs on the audio thread once per frame. Nothing here
/// allocates or takes a lock; <see cref="ReleaseMs"/> is written from the block that is about
/// to render, which is why it is a property that works its coefficient out on the spot rather
/// than something the mixer has to rebuild.
/// </remarks>
public sealed class Ducker
{
    /// <summary>Fast enough to be out of the way before a kick has finished its click.</summary>
    public const double AttackMs = 5;

    /// <summary>
    /// Below this the duck is inaudible and is treated as gone.
    /// </summary>
    /// <remarks>
    /// A one pole follower approaches nought and never arrives, so left alone it keeps a track
    /// very slightly down for ever, for no reason anyone can hear.
    /// </remarks>
    private const double Gone = 0.0001;

    private readonly double _sampleRate;
    private readonly double _attack;

    /// <summary>The release, as a share of the distance moved per sample.</summary>
    private double _release;

    /// <summary>Where the follower stands: what the key track is doing, smoothed.</summary>
    private double _follower;

    /// <summary>
    /// Sets up a side chain at the strip's release, for a mixer running at that rate.
    /// </summary>
    /// <remarks>
    /// The release is set outright rather than through the property. A value that happens to
    /// match the one the field starts on is not a change, and the coefficient would stay at
    /// nought, which is a duck that goes down and never comes back up.
    /// </remarks>
    public Ducker(double releaseMs, int sampleRate)
    {
        _sampleRate = sampleRate <= 0 ? 44100 : sampleRate;
        _attack = CoefficientFor(AttackMs);

        _releaseMs = Clamp(releaseMs);
        _release = CoefficientFor(_releaseMs);
    }

    /// <summary>What the strip's release knob says, kept so the property can tell a real change.</summary>
    private double _releaseMs;

    /// <summary>How long the ducked track takes to come back up. Settable while it runs.</summary>
    public double ReleaseMs
    {
        get => _releaseMs;
        set
        {
            double clamped = Clamp(value);
            if (Math.Abs(_releaseMs - clamped) < 0.001) return;

            _releaseMs = clamped;
            _release = CoefficientFor(clamped);
        }
    }

    /// <summary>Where the follower is: 0 when the key is silent, 1 when it is at full scale.</summary>
    public double Level => _follower;

    /// <summary>
    /// Takes one frame of the key track and moves the follower towards it. Up quickly, down
    /// slowly, which is what makes a duck breathe rather than chatter.
    /// </summary>
    public double Next(double keyMagnitude)
    {
        double target = double.IsNaN(keyMagnitude) ? 0 : Math.Clamp(Math.Abs(keyMagnitude), 0, 1);
        double coefficient = target > _follower ? _attack : _release;

        _follower += (target - _follower) * coefficient;

        if (_follower < Gone) _follower = 0;

        return _follower;
    }

    /// <summary>Back to no ducking at all, for a transport stop.</summary>
    public void Reset() => _follower = 0;

    /// <summary>What to multiply the ducked track by, given where the follower is.</summary>
    /// <remarks>
    /// Static, and holding nothing, so the mixer can ask what a depth and a follower come to
    /// without a side chain of its own and a test can ask without an audio device.
    /// </remarks>
    public static float GainFor(double follower, double depth)
    {
        double amount = Math.Clamp(depth, 0, 1) * Math.Clamp(follower, 0, 1);
        return (float)Math.Clamp(1 - amount, 0, 1);
    }

    /// <summary>A release that is not a number at all reads as the strip's default.</summary>
    private static double Clamp(double milliseconds) =>
        double.IsNaN(milliseconds)
            ? TrackMix.DefaultDuckReleaseMs
            : Math.Clamp(milliseconds, TrackMix.MinDuckReleaseMs, TrackMix.MaxDuckReleaseMs);

    /// <summary>A one pole coefficient: how much of the way it moves in a single sample.</summary>
    private double CoefficientFor(double milliseconds)
    {
        double samples = Math.Max(1, milliseconds / 1000.0 * _sampleRate);
        return 1 - Math.Exp(-1.0 / samples);
    }
}
