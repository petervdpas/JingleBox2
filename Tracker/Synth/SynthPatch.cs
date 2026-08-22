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

    public const double MinDrive = 1;
    public const double MaxDrive = 10;

    public const double MinTuneSemitones = -24;
    public const double MaxTuneSemitones = 24;

    public const double MinFineCents = -100;
    public const double MaxFineCents = 100;

    public const double MinCutoffHz = ToneFilter.MinHz;

    /// <summary>Wide open. A patch at the top of the range is not filtered at all.</summary>
    public const double MaxCutoffHz = ToneFilter.OpenHz;

    public const double MinResonance = ToneFilter.MinResonance;
    public const double MaxResonance = ToneFilter.MaxResonance;

    public SynthWave Wave { get; set; } = SynthWave.Square;

    /// <summary>How much of the pulse wave's cycle is high. Ignored by the other waves.</summary>
    public double Duty { get; set; } = 0.5;

    public double AttackMs { get; set; } = 2;
    public double DecayMs { get; set; } = 40;
    public double Sustain { get; set; } = 0.6;
    public double ReleaseMs { get; set; } = 80;

    /// <summary>Whole semitones added to every note this instrument plays.</summary>
    public double TuneSemitones { get; set; }

    /// <summary>The last hundredth of a semitone, for sitting a voice against another.</summary>
    public double FineCents { get; set; }

    /// <summary>
    /// How hard the voice is pushed into its saturation. One is untouched; above that the tone
    /// fills out and squares off without getting louder, since the drive is levelled out again.
    /// </summary>
    public double Drive { get; set; } = 1;

    public double VibratoRateHz { get; set; }
    public double VibratoDepthCents { get; set; }

    public double TremoloRateHz { get; set; }
    public double TremoloDepth { get; set; }

    /// <summary>How far the pitch starts away from the note, in semitones. Negative drops into it.</summary>
    public double PitchEnvSemitones { get; set; }

    /// <summary>How long that pitch offset takes to reach the note.</summary>
    public double PitchEnvMs { get; set; } = 60;

    /// <summary>Where the low pass starts taking the top off. Wide open by default.</summary>
    public double FilterCutoffHz { get; set; } = MaxCutoffHz;

    /// <summary>How much the filter rings at its cutoff. Zero is a plain roll off.</summary>
    public double FilterResonance { get; set; }

    public SynthPatch Clone() => new()
    {
        Wave = Wave,
        Duty = Duty,
        AttackMs = AttackMs,
        DecayMs = DecayMs,
        Sustain = Sustain,
        ReleaseMs = ReleaseMs,
        TuneSemitones = TuneSemitones,
        FineCents = FineCents,
        Drive = Drive,
        VibratoRateHz = VibratoRateHz,
        VibratoDepthCents = VibratoDepthCents,
        TremoloRateHz = TremoloRateHz,
        TremoloDepth = TremoloDepth,
        PitchEnvSemitones = PitchEnvSemitones,
        PitchEnvMs = PitchEnvMs,
        FilterCutoffHz = FilterCutoffHz,
        FilterResonance = FilterResonance
    };

    /// <summary>
    /// Takes on another patch's settings without becoming another object.
    /// </summary>
    /// <remarks>
    /// A preset lands on the patch that is already there rather than replacing it, because the
    /// panel, the scopes and any voice in the air are all holding this one. Swap the object and
    /// they would go on showing and sounding the patch nobody can reach any more.
    /// </remarks>
    public void CopyFrom(SynthPatch other)
    {
        if (other is null || ReferenceEquals(other, this)) return;

        Wave = other.Wave;
        Duty = other.Duty;
        AttackMs = other.AttackMs;
        DecayMs = other.DecayMs;
        Sustain = other.Sustain;
        ReleaseMs = other.ReleaseMs;
        TuneSemitones = other.TuneSemitones;
        FineCents = other.FineCents;
        Drive = other.Drive;
        VibratoRateHz = other.VibratoRateHz;
        VibratoDepthCents = other.VibratoDepthCents;
        TremoloRateHz = other.TremoloRateHz;
        TremoloDepth = other.TremoloDepth;
        PitchEnvSemitones = other.PitchEnvSemitones;
        PitchEnvMs = other.PitchEnvMs;
        FilterCutoffHz = other.FilterCutoffHz;
        FilterResonance = other.FilterResonance;

        Clamp();
    }

    /// <summary>Pulls every value back into range, for anything read off disk.</summary>
    public void Clamp()
    {
        if (!Enum.IsDefined(Wave)) Wave = SynthWave.Square;

        Duty = Clamp(Duty, MinDuty, MaxDuty);
        AttackMs = Clamp(AttackMs, MinTimeMs, MaxAttackMs);
        DecayMs = Clamp(DecayMs, MinTimeMs, MaxDecayMs);
        Sustain = Clamp(Sustain, MinSustain, MaxSustain);
        ReleaseMs = Clamp(ReleaseMs, MinTimeMs, MaxReleaseMs);
        Drive = Clamp(Drive, MinDrive, MaxDrive);
        TuneSemitones = Clamp(TuneSemitones, MinTuneSemitones, MaxTuneSemitones);
        FineCents = Clamp(FineCents, MinFineCents, MaxFineCents);

        VibratoRateHz = Clamp(VibratoRateHz, MinRateHz, MaxRateHz);
        VibratoDepthCents = Clamp(VibratoDepthCents, MinVibratoDepthCents, MaxVibratoDepthCents);

        TremoloRateHz = Clamp(TremoloRateHz, MinRateHz, MaxRateHz);
        TremoloDepth = Clamp(TremoloDepth, MinTremoloDepth, MaxTremoloDepth);

        PitchEnvSemitones = Clamp(PitchEnvSemitones, MinPitchEnvSemitones, MaxPitchEnvSemitones);
        PitchEnvMs = Clamp(PitchEnvMs, MinTimeMs, MaxPitchEnvMs);

        // A patch written before the filter existed has no cutoff at all, which reads as zero
        // and would silence it. Nothing is a filter that is not there, so it opens up.
        FilterCutoffHz = FilterCutoffHz <= MinCutoffHz ? MaxCutoffHz : Clamp(FilterCutoffHz, MinCutoffHz, MaxCutoffHz);
        FilterResonance = Clamp(FilterResonance, MinResonance, MaxResonance);
    }

    /// <summary>A value that is not a number at all reads as the low end rather than poisoning the voice.</summary>
    private static double Clamp(double value, double min, double max) =>
        double.IsNaN(value) ? min : Math.Clamp(value, min, max);
}
