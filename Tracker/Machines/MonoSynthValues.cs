using JingleBox2.Machines;
using JingleBox2.Tracker.Synth;
using JingleBox2.ViewModels;
using System;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// The mono synth's panel, wired to a real patch.
/// </summary>
/// <remarks>
/// One voice, one set of settings, and no map or grid in front of them, so like
/// <see cref="SynthValues"/> every key here is about the whole instrument. What makes it a
/// different machine rather than the same one with different numbers is the modulation: two
/// routes, each saying where it comes from, how much, and what it lands on, instead of a knob
/// per destination. Half the keys below are those two routes.
///
/// The level is the patch's own and not the instrument's, which is the other way round from
/// every other machine here. The panel this follows has its volume at the end of the signal
/// path with the rest of the sound, and moving it onto the instrument to match the others would
/// change what a preset means.
///
/// A key it does not know reads as zero and swallows the write, for the reason the others do: a
/// machine.json written by a later version has to open on an older app rather than take it down.
/// </remarks>
/// <param name="patch">The oscillator, the filter, the envelope and the two routes.</param>
public sealed class MonoSynthValues(OuroborosPatchViewModel patch) : IMachineValues
{
    // Written out one by one, never built from a name or a loop, so every key in the app can be
    // found by searching for the string that is in the file.

    // ---- the oscillator ---------------------------------------------------

    private const string WaveKey = "wave";
    private const string PulseWidthKey = "pulse_width";
    private const string TuneKey = "tune";
    private const string FineKey = "fine";
    private const string GlideKey = "glide";

    // ---- the mixer --------------------------------------------------------

    private const string NoiseMixKey = "noise_mix";

    // ---- the filter -------------------------------------------------------

    private const string CutoffKey = "cutoff";
    private const string CutoffTextKey = "cutoff_text";
    private const string ResonanceKey = "resonance";
    private const string FilterModeKey = "filter_mode";

    // ---- the amplifier ----------------------------------------------------

    private const string EnvelopeToAmpKey = "env_amp";
    private const string VolumeKey = "volume";

    // ---- the envelope -----------------------------------------------------

    private const string AttackKey = "attack";
    private const string SustainKey = "sustain";
    private const string DecayKey = "decay";

    // ---- the low frequency oscillator -------------------------------------

    private const string LfoRateKey = "lfo_rate";
    private const string LfoWaveKey = "lfo_wave";

    // ---- what moves the oscillator ----------------------------------------

    private const string VcoSourceKey = "vco_source";
    private const string VcoAmountKey = "vco_amount";
    private const string VcoTargetKey = "vco_target";

    // ---- what moves the filter --------------------------------------------

    private const string VcfSourceKey = "vcf_source";
    private const string VcfAmountKey = "vcf_amount";
    private const string VcfPolarityKey = "vcf_polarity";

    /// <summary>Told when something moved, for saving the song and redrawing what else shows it.</summary>
    public Action? Changed { get; set; }

    public double Get(string key) => key switch
    {
        WaveKey => (double)patch.Wave,
        PulseWidthKey => patch.PulseWidth,
        TuneKey => patch.TuneSemitones,
        FineKey => patch.FineCents,
        GlideKey => patch.GlideMs,

        NoiseMixKey => patch.NoiseMix,

        CutoffKey => patch.CutoffHz,
        ResonanceKey => patch.Resonance,
        FilterModeKey => (double)patch.FilterMode,

        EnvelopeToAmpKey => patch.EnvelopeToAmp ? 1 : 0,
        VolumeKey => patch.Volume,

        AttackKey => patch.AttackMs,
        SustainKey => patch.Sustain ? 1 : 0,
        DecayKey => patch.DecayMs,

        LfoRateKey => patch.LfoRateHz,
        LfoWaveKey => (double)patch.LfoWave,

        VcoSourceKey => (double)patch.VcoModSource,
        VcoAmountKey => patch.VcoModAmount,
        VcoTargetKey => (double)patch.VcoModTarget,

        VcfSourceKey => (double)patch.VcfModSource,
        VcfAmountKey => patch.VcfModAmount,
        VcfPolarityKey => patch.VcfModInverted ? 1 : 0,

        _ => 0,
    };

    public void Set(string key, double value)
    {
        bool moved = key switch
        {
            WaveKey => Picked((int)patch.Wave, value, (int)OuroborosWave.Pulse,
                at => patch.Wave = (OuroborosWave)at),
            PulseWidthKey => MachineSetting.Moved(patch.PulseWidth, value, () => patch.PulseWidth = value),
            TuneKey => MachineSetting.Moved(patch.TuneSemitones, value, () => patch.TuneSemitones = value),
            FineKey => MachineSetting.Moved(patch.FineCents, value, () => patch.FineCents = value),
            GlideKey => MachineSetting.Moved(patch.GlideMs, value, () => patch.GlideMs = value),

            NoiseMixKey => MachineSetting.Moved(patch.NoiseMix, value, () => patch.NoiseMix = value),

            CutoffKey => MachineSetting.Moved(patch.CutoffHz, value, () => patch.CutoffHz = value),
            ResonanceKey => MachineSetting.Moved(patch.Resonance, value, () => patch.Resonance = value),
            FilterModeKey => Picked((int)patch.FilterMode, value, (int)FilterMode.HighPass,
                at => patch.FilterMode = (FilterMode)at),

            EnvelopeToAmpKey => Flagged(patch.EnvelopeToAmp, value, on => patch.EnvelopeToAmp = on),
            VolumeKey => MachineSetting.Moved(patch.Volume, value, () => patch.Volume = value),

            AttackKey => MachineSetting.Moved(patch.AttackMs, value, () => patch.AttackMs = value),
            SustainKey => Flagged(patch.Sustain, value, on => patch.Sustain = on),
            DecayKey => MachineSetting.Moved(patch.DecayMs, value, () => patch.DecayMs = value),

            LfoRateKey => MachineSetting.Moved(patch.LfoRateHz, value, () => patch.LfoRateHz = value),
            LfoWaveKey => Picked((int)patch.LfoWave, value, (int)LfoWave.Square,
                at => patch.LfoWave = (LfoWave)at),

            VcoSourceKey => Picked((int)patch.VcoModSource, value, (int)ModSource.Lfo,
                at => patch.VcoModSource = (ModSource)at),
            VcoAmountKey => MachineSetting.Moved(patch.VcoModAmount, value, () => patch.VcoModAmount = value),
            VcoTargetKey => Picked((int)patch.VcoModTarget, value, (int)VcoModTarget.PulseWidth,
                at => patch.VcoModTarget = (VcoModTarget)at),

            VcfSourceKey => Picked((int)patch.VcfModSource, value, (int)ModSource.Lfo,
                at => patch.VcfModSource = (ModSource)at),
            VcfAmountKey => MachineSetting.Moved(patch.VcfModAmount, value, () => patch.VcfModAmount = value),
            VcfPolarityKey => Flagged(patch.VcfModInverted, value, on => patch.VcfModInverted = on),

            _ => false,
        };

        if (moved) Changed?.Invoke();
    }

    public string GetText(string key) => key switch
    {
        CutoffTextKey => patch.CutoffText,
        _ => "",
    };

    /// <summary>
    /// One of a switch's positions, by its place in the list rather than by its name.
    /// </summary>
    /// <remarks>
    /// The switch on the panel holds a number, the way every setting here does, and the machine
    /// says what the positions are called. Clamped rather than trusted: a machine.json listing
    /// three waves on a build that has two must pick one that exists.
    /// </remarks>
    private static bool Picked(int current, double value, int last, Action<int> write)
    {
        int wanted = (int)Math.Clamp(Math.Round(value), 0, last);

        if (wanted == current) return false;

        write(wanted);

        return true;
    }

    /// <summary>
    /// A switch of two positions, arriving as a number because that is all a panel has.
    /// </summary>
    /// <remarks>
    /// Half way up is on, so a control that sweeps rather than clicks still lands somewhere
    /// definite.
    /// </remarks>
    private static bool Flagged(bool current, double value, Action<bool> write)
    {
        bool on = value >= 0.5;

        if (on == current) return false;

        write(on);

        return true;
    }
}
