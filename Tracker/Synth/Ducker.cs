using System;
using JingleBox2.Tracker.Synth.Interfaces;

namespace JingleBox2.Tracker.Synth;

/// <inheritdoc/>
public sealed class Ducker : IDucker
{
    /// <inheritdoc cref="IDucker.AttackMs"/>
    public const double AttackMs = 5;

    /// <inheritdoc/>
    double IDucker.AttackMs => AttackMs;

    /// <summary>
    /// Below this the duck is inaudible and is treated as gone.
    /// </summary>
    /// <remarks>
    /// A one pole follower approaches nought and never arrives, so left alone it keeps a track
    /// very slightly down for ever, for no reason anyone can hear.
    ///
    /// It applies on the way down only, and that is the whole of what it is for. Applied on the
    /// way up as well it is not a floor but a gate, and a loud one: a single attack step from
    /// nought moves the follower by the target times the coefficient, which at five
    /// milliseconds and 44100 is 0.004525, so every target below 0.0221, about -33 dB, produced
    /// a step smaller than this and was snapped back to nought again on every frame for ever. A
    /// quiet key track never ducked at all, and said nothing about it.
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
    /// <param name="releaseMs">How long the ducked track takes to come back up, as the strip's knob says.</param>
    /// <param name="sampleRate">The rate the mixer runs at, which is what the coefficients are worked out against.</param>
    public Ducker(double releaseMs, int sampleRate)
    {
        _sampleRate = sampleRate <= 0 ? 44100 : sampleRate;
        _attack = CoefficientFor(AttackMs);

        _releaseMs = Clamp(releaseMs);
        _release = CoefficientFor(_releaseMs);
    }

    /// <summary>What the strip's release knob says, kept so the property can tell a real change.</summary>
    private double _releaseMs;

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public double Level => _follower;

    /// <inheritdoc/>
    public double Next(double keyMagnitude)
    {
        double target = double.IsNaN(keyMagnitude) ? 0 : Math.Clamp(Math.Abs(keyMagnitude), 0, 1);
        double coefficient = target > _follower ? _attack : _release;

        _follower += (target - _follower) * coefficient;

        if (_follower < Gone && target <= _follower) _follower = 0;

        return _follower;
    }

    /// <inheritdoc/>
    public void Reset() => _follower = 0;

    /// <inheritdoc cref="IDucker.GainFor"/>
    /// <remarks>
    /// Static as well, so the mixer can ask what a depth and a follower come to without a side
    /// chain of its own and a test can ask without an audio device.
    /// </remarks>
    /// <param name="follower">Where the follower stands, from <see cref="Next"/> or <see cref="Level"/>.</param>
    /// <param name="depth">How far down the strip's knob says the track goes at full scale.</param>
    /// <remarks>
    /// Either argument being not a number at all reads as no ducking. Both are clamped, and
    /// <see cref="Math.Clamp(double, double, double)"/> hands NaN back by design, so one came
    /// out the far end as a gain that was not a number and the track it multiplied was silent
    /// for as long as it lasted. An infinite depth is still held at full scale, since that is
    /// what the clamp is for and infinity is an answer to how far down, if a silly one.
    /// </remarks>
    public static float GainFor(double follower, double depth)
    {
        if (double.IsNaN(depth) || double.IsNaN(follower)) return 1f;

        double amount = Math.Clamp(depth, 0, 1) * Math.Clamp(follower, 0, 1);
        return (float)Math.Clamp(1 - amount, 0, 1);
    }

    /// <inheritdoc/>
    float IDucker.GainFor(double follower, double depth) => GainFor(follower, depth);

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
