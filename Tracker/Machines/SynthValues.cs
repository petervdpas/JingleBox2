using JingleBox2.Machines;
using JingleBox2.Tracker.Synth;
using JingleBox2.UI;
using JingleBox2.ViewModels;
using System;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// The oscillator machine's panel, wired to a real patch.
/// </summary>
/// <remarks>
/// The plainest of these adapters, and the machine everything else was built out of: one voice,
/// one set of settings, and no map or grid in front of them. Every key here is about the whole
/// instrument, so unlike a kit's (<see cref="KitValues"/>) or a sampler's
/// (<see cref="SamplerValues"/>) there is nothing for a key to be about but the one thing.
///
/// Two of them are not the patch's. The level is the instrument's, on top of whatever the wave
/// comes out at, which is why it reads in decibels and every other machine's does too. And how
/// much of the wave the picture shows is nobody's: it is where you happen to be looking, kept
/// here so the knob that sets it and the picture that obeys it are reading one number.
///
/// A key it does not know reads as zero and swallows the write, for the reason the others do: a
/// machine.json written by a later version has to open on an older app rather than take it down.
/// </remarks>
/// <param name="patch">The wave, the envelope, the filter and the modulation.</param>
/// <param name="instrument">Whose level it is, which is not the patch's.</param>
public sealed class SynthValues(SynthPatchViewModel patch, TrackerInstrument instrument) : MachineValues
{
    // Written out one by one, never built from a name or a loop, so every key in the app can be
    // found by searching for the string that is in the file.

    // ---- the oscillator ---------------------------------------------------

    private const string WaveKey = "wave";
    private const string DutyKey = "duty";
    private const string TuneKey = "tune";
    private const string FineKey = "fine";
    private const string PitchEnvKey = "pitch_env";
    private const string PitchTimeKey = "pitch_time";

    // ---- the amplifier ----------------------------------------------------

    private const string AttackKey = "attack";
    private const string DecayKey = "decay";
    private const string SustainKey = "sustain";
    private const string ReleaseKey = "release";
    private const string DriveKey = "drive";
    private const string LevelKey = "level";

    // ---- the filter and what moves it -------------------------------------

    private const string CutoffKey = "cutoff";
    private const string CutoffTextKey = "cutoff_text";
    private const string ResonanceKey = "resonance";
    private const string VibratoRateKey = "vib_rate";
    private const string VibratoDepthKey = "vib_depth";
    private const string TremoloRateKey = "trem_rate";
    private const string TremoloDepthKey = "trem_depth";

    /// <summary>
    /// How much of the wave the picture shows, which is no part of the sound.
    /// </summary>
    /// <remarks>
    /// A knob on the face like any other, and the machine marks it as one nothing writes down.
    /// See <see cref="MachineParameter.Saved"/>.
    /// </remarks>
    private const string CyclesKey = "cycles";

    /// <summary>Where the view setting is kept, since the instrument is no place for it.</summary>
    private double _cycles = 2;

    /// <summary>What the picture is set to show, for whoever is drawing it.</summary>
    public double Cycles => _cycles;

    public override double Get(string key) => key switch
    {
        WaveKey => (double)patch.Wave,
        DutyKey => patch.Duty,
        TuneKey => patch.TuneSemitones,
        FineKey => patch.FineCents,
        PitchEnvKey => patch.PitchEnvSemitones,
        PitchTimeKey => patch.PitchEnvMs,

        AttackKey => patch.AttackMs,
        DecayKey => patch.DecayMs,
        SustainKey => patch.Sustain,
        ReleaseKey => patch.ReleaseMs,
        DriveKey => patch.Drive,

        // Decibels, because that is what a level fader is marked in and what the ear reads. A
        // fader on the raw amplitude does nothing for three quarters of its travel.
        LevelKey => GainScale.ToDecibels(instrument.Volume),

        CutoffKey => patch.FilterCutoff,
        ResonanceKey => patch.FilterResonance,
        VibratoRateKey => patch.VibratoRateHz,
        VibratoDepthKey => patch.VibratoDepthCents,
        TremoloRateKey => patch.TremoloRateHz,
        TremoloDepthKey => patch.TremoloDepth,

        CyclesKey => _cycles,

        _ => 0,
    };

    protected override bool Write(string key, double value)
    {
        return key switch
        {
            WaveKey => Wave(value),
            DutyKey => Moved(patch.Duty, value, () => patch.Duty = value),
            TuneKey => Moved(patch.TuneSemitones, value, () => patch.TuneSemitones = value),
            FineKey => Moved(patch.FineCents, value, () => patch.FineCents = value),
            PitchEnvKey => Moved(
                patch.PitchEnvSemitones, value, () => patch.PitchEnvSemitones = value),
            PitchTimeKey => Moved(patch.PitchEnvMs, value, () => patch.PitchEnvMs = value),

            AttackKey => Moved(patch.AttackMs, value, () => patch.AttackMs = value),
            DecayKey => Moved(patch.DecayMs, value, () => patch.DecayMs = value),
            SustainKey => Moved(patch.Sustain, value, () => patch.Sustain = value),
            ReleaseKey => Moved(patch.ReleaseMs, value, () => patch.ReleaseMs = value),
            DriveKey => Moved(patch.Drive, value, () => patch.Drive = value),

            LevelKey => Moved(
                GainScale.ToDecibels(instrument.Volume), value,
                () => instrument.Volume = GainScale.ToAmplitude(
                    Math.Clamp(value, GainScale.MinimumDecibels, GainScale.MaximumDecibels))),

            CutoffKey => Moved(patch.FilterCutoff, value, () => patch.FilterCutoff = value),
            ResonanceKey => Moved(
                patch.FilterResonance, value, () => patch.FilterResonance = value),
            VibratoRateKey => Moved(patch.VibratoRateHz, value, () => patch.VibratoRateHz = value),
            VibratoDepthKey => Moved(
                patch.VibratoDepthCents, value, () => patch.VibratoDepthCents = value),
            TremoloRateKey => Moved(patch.TremoloRateHz, value, () => patch.TremoloRateHz = value),
            TremoloDepthKey => Moved(patch.TremoloDepth, value, () => patch.TremoloDepth = value),

            // Nobody saves it, so nobody is told it moved either. The picture reads it back on
            // the next frame, which is sixteen milliseconds away.
            CyclesKey => Zoomed(value),

            _ => false,
        };
    }

    public override string GetText(string key) => key switch
    {
        CutoffTextKey => patch.FilterCutoffText,
        _ => "",
    };

    /// <summary>
    /// Which wave, by its place in the list rather than by its name.
    /// </summary>
    /// <remarks>
    /// The switch on the panel holds a number, the way every setting here does, and the machine
    /// says what the positions are called. Clamped rather than trusted: a machine.json listing
    /// seven waves on a build that has six must pick a wave that exists.
    /// </remarks>
    private bool Wave(double value)
    {
        var wanted = (SynthWave)(int)Math.Clamp(Math.Round(value), 0, (int)SynthWave.Noise);

        if (patch.Wave == wanted) return false;

        patch.Wave = wanted;

        return true;
    }

    /// <summary>Moves the view, and says it did not change the machine.</summary>
    private bool Zoomed(double value)
    {
        _cycles = Math.Clamp(value, 1, 8);

        return false;
    }
}
