using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// Which of the two shapes the oscillator makes.
/// </summary>
/// <remarks>
/// Two rather than six, because this is one oscillator into a filter and the filter is where
/// the tone comes from. The numbers are written down in songs, so they are fixed rather than
/// implied by the order.
/// </remarks>
public enum MonoSynthWave
{
    /// <summary>A ramp: everything the harmonic series has, which is what a filter wants.</summary>
    Saw = 0,

    /// <summary>A square whose two halves are uneven, and can be moved while the note sounds.</summary>
    Pulse = 1
}

/// <summary>Where a modulation comes from.</summary>
public enum ModSource
{
    /// <summary>The note's own envelope, so the modulation has the shape of the note.</summary>
    Envelope = 0,

    /// <summary>The low frequency oscillator, so it keeps going as long as the note does.</summary>
    Lfo = 1
}

/// <summary>What the oscillator's modulation lands on.</summary>
public enum VcoModTarget
{
    /// <summary>The pitch, which is vibrato at a low rate and something else entirely at a high one.</summary>
    Frequency = 0,

    /// <summary>How wide the pulse is, which moves the tone without moving the note.</summary>
    PulseWidth = 1
}

/// <summary>Which end of the filter is open.</summary>
public enum FilterMode
{
    /// <summary>Everything below the cutoff, which is what a filter usually means.</summary>
    LowPass = 0,

    /// <summary>Everything above it, for taking the body out of something.</summary>
    HighPass = 1
}

/// <summary>The shape the low frequency oscillator makes.</summary>
public enum LfoWave
{
    /// <summary>Up and down evenly, which is a wobble.</summary>
    Triangle = 0,

    /// <summary>One end or the other and nothing between, which is a trill.</summary>
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
///
/// Plain data, held by the panel that edits it, by any scope drawing it and by every voice in
/// the air, which is why a preset lands on the patch that is already there rather than
/// replacing it: see <see cref="CopyFrom"/>.
/// </remarks>
public sealed class MonoSynthPatch
{
    /// <summary>Neither envelope stage can be negative; nought is a jump rather than a length.</summary>
    public const double MinTimeMs = 0;

    /// <summary>Four seconds, which is a pad rather than a bass line.</summary>
    public const double MaxAttackMs = 4000;

    /// <summary>Eight seconds, long enough for a note to fall away across a whole pattern.</summary>
    public const double MaxDecayMs = 8000;

    /// <summary>Nought is glide switched off, which is every note starting at its own pitch.</summary>
    public const double MinGlideMs = 0;

    /// <summary>Two seconds, by which point the slide is the part anybody notices.</summary>
    public const double MaxGlideMs = 2000;

    /// <summary>As far closed as the filter is allowed to go.</summary>
    public const double MinCutoffHz = 20;

    /// <summary>Wide open: at the top of the range the filter is out of the way.</summary>
    public const double MaxCutoffHz = 20000;

    /// <summary>Two octaves down.</summary>
    public const double MinTuneSemitones = -24;

    /// <summary>Two octaves up.</summary>
    public const double MaxTuneSemitones = 24;

    /// <summary>
    /// A hundredth of a cycle a second, which is a sweep lasting a minute and a half.
    /// </summary>
    /// <remarks>
    /// Not nought, unlike the other machine's rate. Here the LFO is one of two things a
    /// modulation route can be pointed at, and a route pointed at an oscillator that never
    /// moves is a route that silently does nothing; the amount knob is where it is switched off.
    /// </remarks>
    public const double MinLfoRateHz = 0.01;

    /// <summary>A hundred a second, which is well into audio and is the point of allowing it.</summary>
    public const double MaxLfoRateHz = 100;

    /// <summary>The oscillator's shape, which with one oscillator is most of the character.</summary>
    public MonoSynthWave Wave { get; set; } = MonoSynthWave.Saw;

    /// <summary>How wide the pulse is, nought to one. Only heard on the pulse wave.</summary>
    public double PulseWidth { get; set; } = 0.5;

    /// <summary>Whole semitones added to every note this instrument plays.</summary>
    public double TuneSemitones { get; set; }

    /// <summary>The last hundredth of a semitone, for sitting a voice against another.</summary>
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

    /// <summary>
    /// Nought is all oscillator, one is all noise, and between them is both.
    /// </summary>
    /// <remarks>
    /// A mixer rather than a choice of wave. A kick wants a sine body with a noise transient
    /// over it, and a snare wants mostly noise with a little tone underneath; neither is
    /// possible when picking noise means giving the oscillator up.
    /// </remarks>
    public double NoiseMix { get; set; }

    /// <summary>Which end of the filter is open. Low pass unless somebody says otherwise.</summary>
    public FilterMode FilterMode { get; set; } = FilterMode.LowPass;

    /// <summary>Where it turns over. High enough by default to be heard as open rather than dull.</summary>
    public double CutoffHz { get; set; } = 12000;

    /// <summary>How hard it rings at the cutoff. Nought is a plain roll off.</summary>
    public double Resonance { get; set; }

    /// <summary>How long the note takes to reach full. Two milliseconds is a plucked start.</summary>
    public double AttackMs { get; set; } = 2;

    /// <summary>How long it takes to fall away, and how long the tail is after a note off.</summary>
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

    /// <summary>How fast the low frequency oscillator runs, for whichever route is pointed at it.</summary>
    public double LfoRateHz { get; set; } = 5;

    /// <summary>Its shape: a wobble or a trill.</summary>
    public LfoWave LfoWave { get; set; } = LfoWave.Triangle;

    /// <summary>Where the oscillator's modulation comes from.</summary>
    public ModSource VcoModSource { get; set; } = ModSource.Lfo;

    /// <summary>How much, nought to one.</summary>
    public double VcoModAmount { get; set; }

    /// <summary>What it lands on: the pitch, or how wide the pulse is.</summary>
    public VcoModTarget VcoModTarget { get; set; } = VcoModTarget.Frequency;

    /// <summary>Where the filter's modulation comes from. The envelope, which is the usual answer.</summary>
    public ModSource VcfModSource { get; set; } = ModSource.Envelope;

    /// <summary>How far it moves the cutoff, nought to one, and nought is the route switched off.</summary>
    public double VcfModAmount { get; set; }

    /// <summary>False opens the filter, true closes it. The polarity switch.</summary>
    public bool VcfModInverted { get; set; }

    /// <summary>
    /// How loud it comes out. Half by default: a raw saw is a full scale wave and arriving at
    /// full scale means arriving already clipping against everything else in the mix.
    /// </summary>
    public double Volume { get; set; } = 0.5;

    /// <summary>How far the pitch modulation reaches when it is pointed at frequency.</summary>
    public const double PitchModSemitones = 24;

    /// <summary>A copy that shares nothing, for a voice that must not feel an edit mid note.</summary>
    public MonoSynthPatch Clone() => new()
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

    /// <summary>
    /// Takes on another patch's settings without becoming another object, for a preset landing
    /// on the patch the panel and any sounding voice are already holding.
    /// </summary>
    public void CopyFrom(MonoSynthPatch other)
    {
        if (other is null || ReferenceEquals(other, this)) return;

        Wave = other.Wave;
        PulseWidth = other.PulseWidth;
        TuneSemitones = other.TuneSemitones;
        FineCents = other.FineCents;
        GlideMs = other.GlideMs;
        NoiseMix = other.NoiseMix;
        FilterMode = other.FilterMode;
        CutoffHz = other.CutoffHz;
        Resonance = other.Resonance;
        AttackMs = other.AttackMs;
        DecayMs = other.DecayMs;
        Sustain = other.Sustain;
        EnvelopeToAmp = other.EnvelopeToAmp;
        LfoRateHz = other.LfoRateHz;
        LfoWave = other.LfoWave;
        VcoModSource = other.VcoModSource;
        VcoModAmount = other.VcoModAmount;
        VcoModTarget = other.VcoModTarget;
        VcfModSource = other.VcfModSource;
        VcfModAmount = other.VcfModAmount;
        VcfModInverted = other.VcfModInverted;
        Volume = other.Volume;

        Clamp();
    }

    /// <summary>Brings a patch read off disk back into range, whatever was in the file.</summary>
    public void Clamp()
    {
        if (!Enum.IsDefined(Wave)) Wave = MonoSynthWave.Saw;
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

    /// <summary>A value that is not a number at all reads as the low end rather than poisoning the voice.</summary>
    private static double Clamp(double value, double low, double high) =>
        double.IsNaN(value) ? low : Math.Clamp(value, low, high);
}
