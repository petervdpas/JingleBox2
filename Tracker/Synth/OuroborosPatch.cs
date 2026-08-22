using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>Which of the two shapes the oscillator makes.</summary>
public enum OuroborosWave
{
    Saw = 0,
    Pulse = 1
}

/// <summary>Where a modulation comes from.</summary>
public enum ModSource
{
    Envelope = 0,
    Lfo = 1
}

/// <summary>What the oscillator's modulation lands on.</summary>
public enum VcoModTarget
{
    Frequency = 0,
    PulseWidth = 1
}

/// <summary>Which end of the filter is open.</summary>
public enum FilterMode
{
    LowPass = 0,
    HighPass = 1
}

/// <summary>The shape the low frequency oscillator makes.</summary>
public enum LfoWave
{
    Triangle = 0,
    Square = 1
}

/// <summary>
/// Ouroboros: one oscillator, one filter, one envelope, and two places to send modulation.
/// </summary>
/// <remarks>
/// Modelled on the Moog Mother-32, which is a good fit for a tracker without meaning to be:
/// it is monophonic, and this engine has always been one voice to a track. What it adds that
/// nothing here had is glide, and a mixer, so noise is something the oscillator is blended
/// with rather than something it is replaced by.
///
/// The modulation is the part worth copying. Rather than a fixed vibrato knob and a fixed
/// tremolo knob, there are two routes, and each says where it comes from, how much, and where
/// it goes. Two knobs and two switches reach further than four knobs that each do one thing,
/// and a third destination later is a value in an enum rather than another pair of knobs.
/// </remarks>
public sealed class OuroborosPatch
{
    public const double MinTimeMs = 0;
    public const double MaxAttackMs = 4000;
    public const double MaxDecayMs = 8000;

    public const double MinGlideMs = 0;
    public const double MaxGlideMs = 2000;

    public const double MinCutoffHz = 20;
    public const double MaxCutoffHz = 20000;

    public const double MinTuneSemitones = -24;
    public const double MaxTuneSemitones = 24;

    public const double MinLfoRateHz = 0.01;
    public const double MaxLfoRateHz = 100;

    // ---- oscillator -------------------------------------------------------

    public OuroborosWave Wave { get; set; } = OuroborosWave.Saw;

    /// <summary>How wide the pulse is, nought to one. Only heard on the pulse wave.</summary>
    public double PulseWidth { get; set; } = 0.5;

    public double TuneSemitones { get; set; }

    public double FineCents { get; set; }

    /// <summary>
    /// How long a note takes to slide from the last one to its own pitch. Zero is off.
    /// </summary>
    /// <remarks>
    /// The thing this machine has that nothing here had. A tracker is one voice to a track, so
    /// every note follows the one before it, which is exactly the arrangement glide was made
    /// for: a bass line slides rather than steps.
    /// </remarks>
    public double GlideMs { get; set; }

    // ---- mixer ------------------------------------------------------------

    /// <summary>
    /// Nought is all oscillator, one is all noise, and between them is both.
    /// </summary>
    /// <remarks>
    /// A mixer rather than a choice of wave. A kick wants a sine body with a noise transient
    /// over it, and a snare wants mostly noise with a little tone underneath; neither is
    /// possible when picking noise means giving the oscillator up.
    /// </remarks>
    public double NoiseMix { get; set; }

    // ---- filter -----------------------------------------------------------

    public FilterMode FilterMode { get; set; } = FilterMode.LowPass;

    public double CutoffHz { get; set; } = 12000;

    public double Resonance { get; set; }

    // ---- envelope ---------------------------------------------------------

    public double AttackMs { get; set; } = 2;

    public double DecayMs { get; set; } = 300;

    /// <summary>
    /// On, the note holds at full until it is let go. Off, it decays away and stays gone.
    /// </summary>
    /// <remarks>
    /// A switch rather than a level, which is what the machine this follows does. Off is a
    /// drum or a pluck, on is anything held; the two cover more than a sustain knob suggests
    /// and there is nothing to set wrong.
    /// </remarks>
    public bool Sustain { get; set; }

    /// <summary>
    /// On, the envelope opens the amplifier. Off, a note is simply on or off at full.
    /// </summary>
    public bool EnvelopeToAmp { get; set; } = true;

    // ---- low frequency oscillator ----------------------------------------

    public double LfoRateHz { get; set; } = 5;

    public LfoWave LfoWave { get; set; } = LfoWave.Triangle;

    // ---- the two modulation routes ---------------------------------------

    public ModSource VcoModSource { get; set; } = ModSource.Lfo;

    /// <summary>How much, nought to one.</summary>
    public double VcoModAmount { get; set; }

    public VcoModTarget VcoModTarget { get; set; } = VcoModTarget.Frequency;

    public ModSource VcfModSource { get; set; } = ModSource.Envelope;

    public double VcfModAmount { get; set; }

    /// <summary>False opens the filter, true closes it. The polarity switch.</summary>
    public bool VcfModInverted { get; set; }

    // ---- output -----------------------------------------------------------

    /// <summary>
    /// How loud it comes out. Half by default: a raw saw is a full scale wave and arriving at
    /// full scale means arriving already clipping against everything else in the mix.
    /// </summary>
    public double Volume { get; set; } = 0.5;

    /// <summary>How far the pitch modulation reaches when it is pointed at frequency.</summary>
    public const double PitchModSemitones = 24;

    public OuroborosPatch Clone() => new()
    {
        Wave = Wave,
        PulseWidth = PulseWidth,
        TuneSemitones = TuneSemitones,
        FineCents = FineCents,
        GlideMs = GlideMs,
        NoiseMix = NoiseMix,
        FilterMode = FilterMode,
        CutoffHz = CutoffHz,
        Resonance = Resonance,
        AttackMs = AttackMs,
        DecayMs = DecayMs,
        Sustain = Sustain,
        EnvelopeToAmp = EnvelopeToAmp,
        LfoRateHz = LfoRateHz,
        LfoWave = LfoWave,
        VcoModSource = VcoModSource,
        VcoModAmount = VcoModAmount,
        VcoModTarget = VcoModTarget,
        VcfModSource = VcfModSource,
        VcfModAmount = VcfModAmount,
        VcfModInverted = VcfModInverted,
        Volume = Volume
    };

    /// <summary>Brings a patch read off disk back into range, whatever was in the file.</summary>
    public void Clamp()
    {
        if (!Enum.IsDefined(Wave)) Wave = OuroborosWave.Saw;
        if (!Enum.IsDefined(FilterMode)) FilterMode = FilterMode.LowPass;
        if (!Enum.IsDefined(LfoWave)) LfoWave = LfoWave.Triangle;
        if (!Enum.IsDefined(VcoModSource)) VcoModSource = ModSource.Lfo;
        if (!Enum.IsDefined(VcfModSource)) VcfModSource = ModSource.Envelope;
        if (!Enum.IsDefined(VcoModTarget)) VcoModTarget = VcoModTarget.Frequency;

        PulseWidth = Clamp(PulseWidth, 0.02, 0.98);
        TuneSemitones = Clamp(TuneSemitones, MinTuneSemitones, MaxTuneSemitones);
        FineCents = Clamp(FineCents, -100, 100);
        GlideMs = Clamp(GlideMs, MinGlideMs, MaxGlideMs);
        NoiseMix = Clamp(NoiseMix, 0, 1);
        CutoffHz = Clamp(CutoffHz, MinCutoffHz, MaxCutoffHz);
        Resonance = Clamp(Resonance, 0, 0.98);
        AttackMs = Clamp(AttackMs, MinTimeMs, MaxAttackMs);
        DecayMs = Clamp(DecayMs, MinTimeMs, MaxDecayMs);
        LfoRateHz = Clamp(LfoRateHz, MinLfoRateHz, MaxLfoRateHz);
        VcoModAmount = Clamp(VcoModAmount, 0, 1);
        VcfModAmount = Clamp(VcfModAmount, 0, 1);
        Volume = Clamp(Volume, 0, 2);
    }

    private static double Clamp(double value, double low, double high) =>
        double.IsNaN(value) ? low : Math.Clamp(value, low, high);
}
