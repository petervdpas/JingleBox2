using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// Everything that makes a synth instrument sound the way it does: one oscillator, an ADSR
/// envelope, and a little modulation. Plain data, so it serializes with the song and saves
/// as a preset without any of the audio code coming along.
/// </summary>
public sealed class SynthPatch
{
    public const double MinTimeMs = 0;
    public const double MaxAttackMs = 2000;
    public const double MaxDecayMs = 5000;
    public const double MaxReleaseMs = 5000;
    public const double MaxPitchEnvMs = 2000;

    public const double MinSustain = 0;
    public const double MaxSustain = 1;

    public const double MinRateHz = 0;
    public const double MaxRateHz = 20;

    public const double MinVibratoDepthCents = 0;
    public const double MaxVibratoDepthCents = 200;

    public const double MinTremoloDepth = 0;
    public const double MaxTremoloDepth = 1;

    public const double MinPitchEnvSemitones = -24;
    public const double MaxPitchEnvSemitones = 24;

    public const double MinDuty = 0.05;
    public const double MaxDuty = 0.95;

    public SynthWave Wave { get; set; } = SynthWave.Square;

    /// <summary>How much of the pulse wave's cycle is high. Ignored by the other waves.</summary>
    public double Duty { get; set; } = 0.5;

    public double AttackMs { get; set; } = 2;
    public double DecayMs { get; set; } = 40;
    public double Sustain { get; set; } = 0.6;
    public double ReleaseMs { get; set; } = 80;

    public double VibratoRateHz { get; set; }
    public double VibratoDepthCents { get; set; }

    public double TremoloRateHz { get; set; }
    public double TremoloDepth { get; set; }

    /// <summary>How far the pitch starts away from the note, in semitones. Negative drops into it.</summary>
    public double PitchEnvSemitones { get; set; }

    /// <summary>How long that pitch offset takes to reach the note.</summary>
    public double PitchEnvMs { get; set; } = 60;

    public SynthPatch Clone() => new()
    {
        Wave = Wave,
        Duty = Duty,
        AttackMs = AttackMs,
        DecayMs = DecayMs,
        Sustain = Sustain,
        ReleaseMs = ReleaseMs,
        VibratoRateHz = VibratoRateHz,
        VibratoDepthCents = VibratoDepthCents,
        TremoloRateHz = TremoloRateHz,
        TremoloDepth = TremoloDepth,
        PitchEnvSemitones = PitchEnvSemitones,
        PitchEnvMs = PitchEnvMs
    };

    /// <summary>Pulls every value back into range, for anything read off disk.</summary>
    public void Clamp()
    {
        if (!Enum.IsDefined(Wave)) Wave = SynthWave.Square;

        Duty = Clamp(Duty, MinDuty, MaxDuty);
        AttackMs = Clamp(AttackMs, MinTimeMs, MaxAttackMs);
        DecayMs = Clamp(DecayMs, MinTimeMs, MaxDecayMs);
        Sustain = Clamp(Sustain, MinSustain, MaxSustain);
        ReleaseMs = Clamp(ReleaseMs, MinTimeMs, MaxReleaseMs);

        VibratoRateHz = Clamp(VibratoRateHz, MinRateHz, MaxRateHz);
        VibratoDepthCents = Clamp(VibratoDepthCents, MinVibratoDepthCents, MaxVibratoDepthCents);

        TremoloRateHz = Clamp(TremoloRateHz, MinRateHz, MaxRateHz);
        TremoloDepth = Clamp(TremoloDepth, MinTremoloDepth, MaxTremoloDepth);

        PitchEnvSemitones = Clamp(PitchEnvSemitones, MinPitchEnvSemitones, MaxPitchEnvSemitones);
        PitchEnvMs = Clamp(PitchEnvMs, MinTimeMs, MaxPitchEnvMs);
    }

    /// <summary>A value that is not a number at all reads as the low end rather than poisoning the voice.</summary>
    private static double Clamp(double value, double min, double max) =>
        double.IsNaN(value) ? min : Math.Clamp(value, min, max);
}
