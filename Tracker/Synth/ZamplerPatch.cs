using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// What Zampler does to a recording once it has been read: a filter, and two envelopes.
/// </summary>
/// <remarks>
/// The zone map decides which recording sounds and how fast it is read. This decides what
/// happens to it afterwards, and on the machine this is named for that was the whole of the
/// instrument: eight voices, each landing in a four pole resonant low pass with an envelope of
/// its own, so a sample was raw material rather than a finished sound.
///
/// Two envelopes rather than one, as the Emulator had: the loudness and the brightness are not
/// the same shape. A string that fades slowly while its top goes almost at once is two
/// envelopes and cannot be one.
/// </remarks>
public sealed class ZamplerPatch
{
    public const double MinTimeMs = 0;
    public const double MaxAttackMs = 4000;
    public const double MaxDecayMs = 8000;
    public const double MaxReleaseMs = 8000;

    public const double MinCutoffHz = 20;
    public const double MaxCutoffHz = 20000;

    /// <summary>How far the filter envelope can move the cutoff, in octaves at full amount.</summary>
    public const double EnvelopeOctaves = 6;

    // ---- the amplifier ----------------------------------------------------

    public double AttackMs { get; set; } = 2;
    public double DecayMs { get; set; } = 200;
    public double Sustain { get; set; } = 1;
    public double ReleaseMs { get; set; } = 160;

    // ---- the filter -------------------------------------------------------

    /// <summary>Where the four pole low pass turns over. Wide open by default.</summary>
    public double CutoffHz { get; set; } = MaxCutoffHz;

    /// <summary>How hard it rings at the cutoff. Zero is a plain roll off.</summary>
    public double Resonance { get; set; }

    /// <summary>How far the filter's own envelope moves the cutoff. Nothing at zero.</summary>
    public double EnvelopeAmount { get; set; }

    /// <summary>True to have the envelope close the filter rather than open it.</summary>
    public bool EnvelopeInverted { get; set; }

    /// <summary>
    /// How much the cutoff follows the keyboard, so high notes stay as bright as low ones.
    /// </summary>
    /// <remarks>
    /// Nothing at zero, which lets the top of the keyboard go dull. One is the cutoff rising a
    /// semitone for every semitone above the zone's root, which keeps the tone even across a
    /// zone at the cost of it changing character between zones.
    /// </remarks>
    public double KeyFollow { get; set; }

    // ---- the filter's own envelope ----------------------------------------

    public double FilterAttackMs { get; set; } = 2;
    public double FilterDecayMs { get; set; } = 300;
    public double FilterSustain { get; set; } = 0.5;
    public double FilterReleaseMs { get; set; } = 200;

    /// <summary>0-1 gain for the whole instrument, under the cell's volume column.</summary>
    public double Volume { get; set; } = 1.0;

    public ZamplerPatch Clone() => new()
    {
        AttackMs = AttackMs,
        DecayMs = DecayMs,
        Sustain = Sustain,
        ReleaseMs = ReleaseMs,
        CutoffHz = CutoffHz,
        Resonance = Resonance,
        EnvelopeAmount = EnvelopeAmount,
        EnvelopeInverted = EnvelopeInverted,
        KeyFollow = KeyFollow,
        FilterAttackMs = FilterAttackMs,
        FilterDecayMs = FilterDecayMs,
        FilterSustain = FilterSustain,
        FilterReleaseMs = FilterReleaseMs,
        Volume = Volume
    };

    /// <summary>
    /// Takes on another patch's settings without becoming another object, for a preset landing
    /// on the patch the panel and any sounding voice are already holding.
    /// </summary>
    public void CopyFrom(ZamplerPatch other)
    {
        if (other is null || ReferenceEquals(other, this)) return;

        AttackMs = other.AttackMs;
        DecayMs = other.DecayMs;
        Sustain = other.Sustain;
        ReleaseMs = other.ReleaseMs;
        CutoffHz = other.CutoffHz;
        Resonance = other.Resonance;
        EnvelopeAmount = other.EnvelopeAmount;
        EnvelopeInverted = other.EnvelopeInverted;
        KeyFollow = other.KeyFollow;
        FilterAttackMs = other.FilterAttackMs;
        FilterDecayMs = other.FilterDecayMs;
        FilterSustain = other.FilterSustain;
        FilterReleaseMs = other.FilterReleaseMs;
        Volume = other.Volume;

        Clamp();
    }

    /// <summary>Brings a patch read off disk back into range, whatever was in the file.</summary>
    public void Clamp()
    {
        AttackMs = Hold(AttackMs, MinTimeMs, MaxAttackMs, 2);
        DecayMs = Hold(DecayMs, MinTimeMs, MaxDecayMs, 200);
        Sustain = Hold(Sustain, 0, 1, 1);
        ReleaseMs = Hold(ReleaseMs, MinTimeMs, MaxReleaseMs, 160);

        // A patch written before the filter existed has no cutoff at all, which reads as zero
        // and would silence it. Nothing is a filter that is not there, so it opens up.
        CutoffHz = CutoffHz <= MinCutoffHz ? MaxCutoffHz : Hold(CutoffHz, MinCutoffHz, MaxCutoffHz, MaxCutoffHz);

        Resonance = Hold(Resonance, 0, 0.98, 0);
        EnvelopeAmount = Hold(EnvelopeAmount, 0, 1, 0);
        KeyFollow = Hold(KeyFollow, 0, 1, 0);

        FilterAttackMs = Hold(FilterAttackMs, MinTimeMs, MaxAttackMs, 2);
        FilterDecayMs = Hold(FilterDecayMs, MinTimeMs, MaxDecayMs, 300);
        FilterSustain = Hold(FilterSustain, 0, 1, 0.5);
        FilterReleaseMs = Hold(FilterReleaseMs, MinTimeMs, MaxReleaseMs, 200);

        Volume = Hold(Volume, 0, 1, 1);
    }

    private static double Hold(double value, double min, double max, double whenLost) =>
        double.IsNaN(value) ? whenLost : Math.Clamp(value, min, max);

    /// <summary>
    /// Where the filter actually sits for one note, once the envelope and the keyboard have
    /// had their say.
    /// </summary>
    /// <remarks>
    /// Both in octaves rather than hertz, because that is how the ear hears a filter move and
    /// how the machine's own controls behaved. Multiplying is what keeps a sweep sounding the
    /// same whether it starts at eighty hertz or eight hundred.
    /// </remarks>
    public double CutoffFor(double envelope, int semitone, int root)
    {
        double octaves = EnvelopeAmount * envelope * EnvelopeOctaves * (EnvelopeInverted ? -1 : 1);

        octaves += KeyFollow * (semitone - root) / 12.0;

        return Math.Clamp(CutoffHz * Math.Pow(2, octaves), MinCutoffHz, MaxCutoffHz);
    }
}
