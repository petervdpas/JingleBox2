using System;
using JingleBox2.Tracker.Synth.Enums;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// Everything that makes a synth instrument sound the way it does: one oscillator, an ADSR
/// envelope, and a little modulation. Plain data, so it serializes with the song and saves
/// as a preset without any of the audio code coming along.
/// </summary>
/// <remarks>
/// Held by the panel that edits it, by the scopes that draw it and by every voice in the air,
/// which is why a preset lands on the patch that is already there rather than replacing it:
/// see <see cref="CopyFrom"/>.
///
/// The ends of every control live here rather than on the panel, because a patch read off disc
/// has to be brought back inside them and the panel is not involved in that.
/// </remarks>
public sealed class SynthPatch
{
    /// <summary>No envelope stage can be negative; nought is a jump rather than a length.</summary>
    public const double MinTimeMs = 0;

    /// <summary>Two seconds, which is longer than anything anybody has asked a tracker for.</summary>
    public const double MaxAttackMs = 2000;

    /// <summary>Five seconds, long enough for a pad to fall away across several lines.</summary>
    public const double MaxDecayMs = 5000;

    /// <summary>Five seconds, the same as the decay: a tail is a tail either way.</summary>
    public const double MaxReleaseMs = 5000;

    /// <summary>Two seconds, which is far longer than a pitch drop is ever useful for.</summary>
    public const double MaxPitchEnvMs = 2000;

    /// <summary>Nothing held at all, so the note ends on its decay.</summary>
    public const double MinSustain = 0;

    /// <summary>Held at full, so the decay does nothing.</summary>
    public const double MaxSustain = 1;

    /// <summary>Nought is the modulation switched off rather than a very slow one.</summary>
    public const double MinRateHz = 0;

    /// <summary>Twenty a second, which is where a wobble stops being a wobble and becomes a tone.</summary>
    public const double MaxRateHz = 20;

    /// <summary>No vibrato.</summary>
    public const double MinVibratoDepthCents = 0;

    /// <summary>Two hundred cents, which is a whole tone either side.</summary>
    public const double MaxVibratoDepthCents = 200;

    /// <summary>No tremolo.</summary>
    public const double MinTremoloDepth = 0;

    /// <summary>All the way down at the bottom of the modulation, which is silence at the trough.</summary>
    public const double MaxTremoloDepth = 1;

    /// <summary>Two octaves down, which is a note dropping into pitch from below.</summary>
    public const double MinPitchEnvSemitones = -24;

    /// <summary>Two octaves up, which is the same trick the other way round.</summary>
    public const double MaxPitchEnvSemitones = 24;

    /// <summary>Narrow, but not so narrow that the pulse disappears into a click.</summary>
    public const double MinDuty = 0.05;

    /// <summary>Wide, and the same distance from the end as the minimum is from its own.</summary>
    public const double MaxDuty = 0.95;

    /// <summary>One is untouched, which is what a patch with no drive on it holds.</summary>
    public const double MinDrive = 1;

    /// <summary>Ten, by which point the wave is very nearly a square whatever went in.</summary>
    public const double MaxDrive = 10;

    /// <summary>Two octaves down.</summary>
    public const double MinTuneSemitones = -24;

    /// <summary>Two octaves up.</summary>
    public const double MaxTuneSemitones = 24;

    /// <summary>A whole semitone flat, which is where the fine control meets the coarse one.</summary>
    public const double MinFineCents = -100;

    /// <summary>A whole semitone sharp.</summary>
    public const double MaxFineCents = 100;

    /// <summary>As far closed as the filter itself allows.</summary>
    public const double MinCutoffHz = ToneFilter.MinHz;

    /// <summary>Wide open. A patch at the top of the range is not filtered at all.</summary>
    public const double MaxCutoffHz = ToneFilter.OpenHz;

    /// <summary>No ringing, which is a plain roll off.</summary>
    public const double MinResonance = ToneFilter.MinResonance;

    /// <summary>As far as the filter will ring without oscillating on its own.</summary>
    public const double MaxResonance = ToneFilter.MaxResonance;

    /// <summary>The oscillator's shape. Square by default, which is the tracker sound.</summary>
    public SynthWave Wave { get; set; } = SynthWave.Square;

    /// <summary>How much of the pulse wave's cycle is high. Ignored by the other waves.</summary>
    public double Duty { get; set; } = 0.5;

    /// <summary>How long the note takes to reach full. Two milliseconds is a plucked start.</summary>
    public double AttackMs { get; set; } = 2;

    /// <summary>How long it takes to fall from full to the sustain level.</summary>
    public double DecayMs { get; set; } = 40;

    /// <summary>Where it holds while the note is held, nought to one.</summary>
    public double Sustain { get; set; } = 0.6;

    /// <summary>How long the tail is once the note is let go of.</summary>
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

    /// <summary>How fast the pitch wobbles. Nought is no vibrato at all.</summary>
    public double VibratoRateHz { get; set; }

    /// <summary>How far it wobbles, in hundredths of a semitone.</summary>
    public double VibratoDepthCents { get; set; }

    /// <summary>How fast the level wobbles. Nought is no tremolo at all.</summary>
    public double TremoloRateHz { get; set; }

    /// <summary>How far down the trough goes, nought to one.</summary>
    public double TremoloDepth { get; set; }

    /// <summary>How far the pitch starts away from the note, in semitones. Negative drops into it.</summary>
    public double PitchEnvSemitones { get; set; }

    /// <summary>How long that pitch offset takes to reach the note.</summary>
    public double PitchEnvMs { get; set; } = 60;

    /// <summary>Where the low pass starts taking the top off. Wide open by default.</summary>
    public double FilterCutoffHz { get; set; } = MaxCutoffHz;

    /// <summary>How much the filter rings at its cutoff. Zero is a plain roll off.</summary>
    public double FilterResonance { get; set; }

    /// <summary>
    /// True when the drive holds its loudness rather than its peak, so the knob changes the tone
    /// and not the level.
    /// </summary>
    /// <remarks>
    /// False is what the drive has always done and is therefore the default: the makeup maps full
    /// scale to full scale, which holds the height of the wave and says nothing about its area. A
    /// saw driven hard is nearly a square, the square is the same height and far fuller, and the
    /// measured cost on a real patch is 5.6 dB of loudness added by a control this class's own
    /// summary says gets no louder.
    ///
    /// A setting rather than a repair, and false rather than true, because every song already
    /// written was made against the old behaviour and is entitled to sound exactly as it did.
    /// It travels with the patch, so it is in the song, in the preset and in the zip, which is
    /// the whole reason it is here and not a tick box in SETTINGS.
    /// </remarks>
    public bool EvenDrive { get; set; }

    /// <summary>True when the filter runs before the drive rather than after it.</summary>
    /// <remarks>
    /// False is what the voice has always done and is the default for the same reason as
    /// <see cref="EvenDrive"/>.
    ///
    /// This is a tone control and not only a repair. Drive into filter and filter into drive are
    /// two different instruments, which is why real synths put the choice on the front panel: the
    /// first squares the wave up and then takes the top off it, and the second shapes the wave and
    /// then rounds what is left. What it also does is stop a resonant peak being applied to a wave
    /// that has already been squared off, which is what pushed this machine's presets past full
    /// scale: the same patch peaks 0.866 with the resonance down and 1.057 with it at 0.30.
    /// </remarks>
    public bool FilterFirst { get; set; }

    /// <summary>A copy that shares nothing, for a voice that must not feel an edit mid note.</summary>
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
        FilterResonance = FilterResonance,
        EvenDrive = EvenDrive,
        FilterFirst = FilterFirst
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
        EvenDrive = other.EvenDrive;
        FilterFirst = other.FilterFirst;

        Clamp();
    }

    /// <summary>
    /// Pulls every value back into range, for anything read off disk.
    /// </summary>
    /// <remarks>
    /// The cutoff is the awkward one. A patch written before the filter existed has no cutoff
    /// at all, which reads as nought and would silence it, so nothing opens the filter up.
    /// Nothing, and not merely low: a cutoff sitting on the bottom of its own range is a filter
    /// somebody has turned all the way down, and it stays where they put it. The two were one
    /// test once, and the bottom of the range is exactly the minimum, so a filter closed by
    /// hand sprang wide open at the instant it arrived: through the beginning and out at the
    /// end. Nothing else in the application could do that, which is why it took so long to
    /// find; the knob and the wire were both innocent.
    /// </remarks>
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

        FilterCutoffHz = FilterCutoffHz <= 0 || double.IsNaN(FilterCutoffHz)
            ? MaxCutoffHz
            : Clamp(FilterCutoffHz, MinCutoffHz, MaxCutoffHz);
        FilterResonance = Clamp(FilterResonance, MinResonance, MaxResonance);
    }

    /// <summary>A value that is not a number at all reads as the low end rather than poisoning the voice.</summary>
    private static double Clamp(double value, double min, double max) =>
        double.IsNaN(value) ? min : Math.Clamp(value, min, max);
}
