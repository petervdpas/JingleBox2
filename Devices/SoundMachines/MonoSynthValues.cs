using JingleBox2.Rack.Faces;
using JingleBox2.ViewModels;
using System;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Synth.Enums;
using JingleBox2.Tracker;

namespace JingleBox2.Devices.SoundMachines;

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
/// <param name="instrument">
/// Whose new note action it is. On the instrument rather than on the patch, because it is not
/// part of the sound this machine makes: it is what the tracker does with the note before.
/// </param>
public sealed class MonoSynthValues(MonoSynthPatchViewModel patch, TrackerInstrument instrument) : PanelValues
{
    /// <summary>What a new note does to the one the track is still sounding.</summary>
    private const string NewNoteKey = "new_note";

    /// <summary>The oscillator: which shape the wave is.</summary>
    /// <remarks>
    /// The keys are written out one by one, never built from a name or a loop, so every key in
    /// the application can be found by searching for the string that is in the machine's own
    /// file. A key assembled at the call site never appears in the source at all, and both the
    /// tools that hunt for an orphaned key and anybody grepping would miss it.
    /// </remarks>
    private const string WaveKey = "wave";

    /// <summary>How wide the pulse is, which does nothing to the waves that have no pulse.</summary>
    private const string PulseWidthKey = "pulse_width";

    /// <summary>Coarse tuning, in semitones.</summary>
    private const string TuneKey = "tune";

    /// <summary>And fine tuning, in cents.</summary>
    private const string FineKey = "fine";

    /// <summary>How long the pitch takes to slide from the last note to this one.</summary>
    private const string GlideKey = "glide";

    /// <summary>The mixer: how much noise is stirred in with the oscillator.</summary>
    private const string NoiseMixKey = "noise_mix";

    /// <summary>The filter: where it opens to.</summary>
    private const string CutoffKey = "cutoff";

    /// <summary>The same, worded for a panel to print, since a frequency needs its unit.</summary>
    private const string CutoffTextKey = "cutoff_text";

    /// <summary>How much it rings at the corner.</summary>
    private const string ResonanceKey = "resonance";

    /// <summary>Which way round it works: low pass or high pass.</summary>
    private const string FilterModeKey = "filter_mode";

    /// <summary>The amplifier: whether the envelope shapes the level, or the level is flat.</summary>
    private const string EnvelopeToAmpKey = "env_amp";

    /// <summary>How loud it plays.</summary>
    /// <remarks>
    /// The patch's own, not the instrument's, which is the other way round from every other
    /// machine here and is the panel this one follows being obeyed rather than tidied.
    /// </remarks>
    private const string VolumeKey = "volume";

    /// <summary>The envelope: how long it takes to come up.</summary>
    private const string AttackKey = "attack";

    /// <summary>Whether it holds while the key is down, or falls straight through.</summary>
    private const string SustainKey = "sustain";

    /// <summary>And how long the fall takes.</summary>
    private const string DecayKey = "decay";

    /// <summary>The low frequency oscillator: how fast it runs.</summary>
    private const string LfoRateKey = "lfo_rate";

    /// <summary>And what shape it is.</summary>
    private const string LfoWaveKey = "lfo_wave";

    /// <summary>The first route: where what moves the oscillator comes from.</summary>
    private const string VcoSourceKey = "vco_source";

    /// <summary>How much of it there is.</summary>
    private const string VcoAmountKey = "vco_amount";

    /// <summary>And what it lands on, which is the pitch or the pulse width.</summary>
    private const string VcoTargetKey = "vco_target";

    /// <summary>The second route: where what moves the filter comes from.</summary>
    private const string VcfSourceKey = "vcf_source";

    /// <summary>How much of it there is.</summary>
    private const string VcfAmountKey = "vcf_amount";

    /// <summary>And whether it opens the filter or closes it, since there is only one place to land.</summary>
    private const string VcfPolarityKey = "vcf_polarity";

    /// <inheritdoc/>
    /// <remarks>
    /// Switches and flags come back as numbers, because a number is all a described panel deals
    /// in. A key it does not know reads as nought rather than throwing.
    /// </remarks>
    public override double Get(string key) => key switch
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

        NewNoteKey => (double)instrument.NewNoteAction,

        _ => 0,
    };

    /// <inheritdoc/>
    /// <remarks>
    /// Every switch goes through <see cref="Picked"/> and every flag through
    /// <see cref="Flagged"/>, so a machine file naming a position this build has not got picks
    /// one that exists rather than casting itself into an enum value nothing handles.
    /// </remarks>
    protected override bool Write(string key, double value)
    {
        return key switch
        {
            WaveKey => Picked((int)patch.Wave, value, (int)MonoSynthWave.Pulse,
                at => patch.Wave = (MonoSynthWave)at),
            PulseWidthKey => Moved(patch.PulseWidth, value, () => patch.PulseWidth = value),
            TuneKey => Moved(patch.TuneSemitones, value, () => patch.TuneSemitones = value),
            FineKey => Moved(patch.FineCents, value, () => patch.FineCents = value),
            GlideKey => Moved(patch.GlideMs, value, () => patch.GlideMs = value),

            NoiseMixKey => Moved(patch.NoiseMix, value, () => patch.NoiseMix = value),

            CutoffKey => Moved(patch.CutoffHz, value, () => patch.CutoffHz = value),
            ResonanceKey => Moved(patch.Resonance, value, () => patch.Resonance = value),
            FilterModeKey => Picked((int)patch.FilterMode, value, (int)FilterMode.HighPass,
                at => patch.FilterMode = (FilterMode)at),

            EnvelopeToAmpKey => Flagged(patch.EnvelopeToAmp, value, on => patch.EnvelopeToAmp = on),
            VolumeKey => Moved(patch.Volume, value, () => patch.Volume = value),

            AttackKey => Moved(patch.AttackMs, value, () => patch.AttackMs = value),
            SustainKey => Flagged(patch.Sustain, value, on => patch.Sustain = on),
            DecayKey => Moved(patch.DecayMs, value, () => patch.DecayMs = value),

            LfoRateKey => Moved(patch.LfoRateHz, value, () => patch.LfoRateHz = value),
            LfoWaveKey => Picked((int)patch.LfoWave, value, (int)LfoWave.Square,
                at => patch.LfoWave = (LfoWave)at),

            VcoSourceKey => Picked((int)patch.VcoModSource, value, (int)ModSource.Lfo,
                at => patch.VcoModSource = (ModSource)at),
            VcoAmountKey => Moved(patch.VcoModAmount, value, () => patch.VcoModAmount = value),
            VcoTargetKey => Picked((int)patch.VcoModTarget, value, (int)VcoModTarget.PulseWidth,
                at => patch.VcoModTarget = (VcoModTarget)at),

            VcfSourceKey => Picked((int)patch.VcfModSource, value, (int)ModSource.Lfo,
                at => patch.VcfModSource = (ModSource)at),
            VcfAmountKey => Moved(patch.VcfModAmount, value, () => patch.VcfModAmount = value),
            VcfPolarityKey => Flagged(patch.VcfModInverted, value, on => patch.VcfModInverted = on),

            NewNoteKey => Moved((int)instrument.NewNoteAction, value, 0, (int)VoiceEnding.Sustain,
                at => instrument.NewNoteAction = (VoiceEnding)at),

            _ => false,
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The cutoff is the only one worded, since a frequency without its unit reads as a number
    /// nobody can place.
    /// </remarks>
    public override string GetText(string key) => key switch
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
    /// <param name="current">Where the switch is now.</param>
    /// <param name="value">Where the panel says it should be.</param>
    /// <param name="last">The highest position this build has.</param>
    /// <param name="write">What to do once it is known to have moved.</param>
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
    /// <param name="current">Whether it is on now.</param>
    /// <param name="value">What the panel says, which is a number either side of a half.</param>
    /// <param name="write">What to do once it is known to have moved.</param>
    private static bool Flagged(bool current, double value, Action<bool> write)
    {
        bool on = value >= 0.5;

        if (on == current) return false;

        write(on);

        return true;
    }
}
