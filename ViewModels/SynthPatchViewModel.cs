using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Tracker.Synth;
using JingleBox2.UI;
using System;
using JingleBox2.Tracker.Synth.Enums;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// The editable face of a <see cref="SynthPatch"/>. The patch itself stays plain data that
/// serializes with the song; this wraps it for the sliders and keeps every value in range.
/// </summary>
public sealed class SynthPatchViewModel : ObservableObject
{
    /// <summary>The filter sweep, so a knob position can be checked without a window.</summary>
    private readonly IFrequencyScale _hz = new FrequencyScale();

    /// <summary>The patch the song holds, written into in place rather than copied.</summary>
    private readonly SynthPatch _patch;

    /// <summary>Told after every change, which is what marks the song unsaved.</summary>
    private readonly Action _changed;

    /// <summary>Shows one patch. Nothing is copied: the patch handed in is the patch edited.</summary>
    public SynthPatchViewModel(SynthPatch patch, Action changed)
    {
        _patch = patch;
        _changed = changed;
    }

    /// <summary>The patch itself, for whatever plays it and whatever draws its shape.</summary>
    public SynthPatch Patch => _patch;

    /// <summary>
    /// Bumped on every change. The scopes read the patch itself, which is plain data and says
    /// nothing when it changes, so this is what tells them to redraw.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>
    /// Every wave there is, for the picker, read off the enum rather than listed here so a wave
    /// added later appears without anybody remembering this line.
    /// </summary>
    public SynthWave[] Waves { get; } = Enum.GetValues<SynthWave>();

    /// <summary>Which wave the oscillator runs. Changing it can hide or show the duty row.</summary>
    public SynthWave Wave
    {
        get => _patch.Wave;
        set
        {
            if (_patch.Wave == value) return;

            _patch.Wave = value;
            Bump();

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPulse));
            _changed();
        }
    }

    /// <summary>Duty only means anything to the pulse wave, so the row hides for the others.</summary>
    public bool IsPulse => _patch.Wave == SynthWave.Pulse;

    /// <summary>How wide the pulse is, which only the pulse wave hears.</summary>
    public double Duty
    {
        get => _patch.Duty;
        set => Set(v => _patch.Duty = v, _patch.Duty, value, SynthPatch.MinDuty, SynthPatch.MaxDuty,
            nameof(Duty));
    }

    /// <summary>How long the note takes to reach full level, in milliseconds.</summary>
    public double AttackMs
    {
        get => _patch.AttackMs;
        set => Set(v => _patch.AttackMs = v, _patch.AttackMs, value, SynthPatch.MinTimeMs, SynthPatch.MaxAttackMs,
            nameof(AttackMs));
    }

    /// <summary>How long it takes to fall from there to the sustain level.</summary>
    public double DecayMs
    {
        get => _patch.DecayMs;
        set => Set(v => _patch.DecayMs = v, _patch.DecayMs, value, SynthPatch.MinTimeMs, SynthPatch.MaxDecayMs,
            nameof(DecayMs));
    }

    /// <summary>The level it holds at while the key is down.</summary>
    public double Sustain
    {
        get => _patch.Sustain;
        set => Set(v => _patch.Sustain = v, _patch.Sustain, value, SynthPatch.MinSustain, SynthPatch.MaxSustain,
            nameof(Sustain));
    }

    /// <summary>And how long it takes to go quiet after the key comes up.</summary>
    public double ReleaseMs
    {
        get => _patch.ReleaseMs;
        set => Set(v => _patch.ReleaseMs = v, _patch.ReleaseMs, value, SynthPatch.MinTimeMs, SynthPatch.MaxReleaseMs,
            nameof(ReleaseMs));
    }

    /// <summary>The patch's own transposition, in whole semitones.</summary>
    public double TuneSemitones
    {
        get => _patch.TuneSemitones;
        set => Set(v => _patch.TuneSemitones = v, _patch.TuneSemitones, value,
            SynthPatch.MinTuneSemitones, SynthPatch.MaxTuneSemitones, nameof(TuneSemitones));
    }

    /// <summary>And the rest of it, in cents, for a detune two voices can beat against.</summary>
    public double FineCents
    {
        get => _patch.FineCents;
        set => Set(v => _patch.FineCents = v, _patch.FineCents, value,
            SynthPatch.MinFineCents, SynthPatch.MaxFineCents, nameof(FineCents));
    }

    /// <summary>
    /// How hard the wave is pushed before it leaves the voice. Not in MappoGraph's set: it is
    /// the one control here that the chiptune synth this patch mirrors never had.
    /// </summary>
    public double Drive
    {
        get => _patch.Drive;
        set => Set(v => _patch.Drive = v, _patch.Drive, value, SynthPatch.MinDrive, SynthPatch.MaxDrive,
            nameof(Drive));
    }

    /// <summary>How fast the pitch wobbles, in hertz.</summary>
    public double VibratoRateHz
    {
        get => _patch.VibratoRateHz;
        set => Set(v => _patch.VibratoRateHz = v, _patch.VibratoRateHz, value,
            SynthPatch.MinRateHz, SynthPatch.MaxRateHz, nameof(VibratoRateHz));
    }

    /// <summary>And how far it wobbles, in cents, so the depth means the same at every pitch.</summary>
    public double VibratoDepthCents
    {
        get => _patch.VibratoDepthCents;
        set => Set(v => _patch.VibratoDepthCents = v, _patch.VibratoDepthCents, value,
            SynthPatch.MinVibratoDepthCents, SynthPatch.MaxVibratoDepthCents, nameof(VibratoDepthCents));
    }

    /// <summary>How fast the level wobbles, in hertz.</summary>
    public double TremoloRateHz
    {
        get => _patch.TremoloRateHz;
        set => Set(v => _patch.TremoloRateHz = v, _patch.TremoloRateHz, value,
            SynthPatch.MinRateHz, SynthPatch.MaxRateHz, nameof(TremoloRateHz));
    }

    /// <summary>And how far, nought being none.</summary>
    public double TremoloDepth
    {
        get => _patch.TremoloDepth;
        set => Set(v => _patch.TremoloDepth = v, _patch.TremoloDepth, value,
            SynthPatch.MinTremoloDepth, SynthPatch.MaxTremoloDepth, nameof(TremoloDepth));
    }

    /// <summary>How far the note is bent at the start, in semitones, which is what a kick is.</summary>
    public double PitchEnvSemitones
    {
        get => _patch.PitchEnvSemitones;
        set => Set(v => _patch.PitchEnvSemitones = v, _patch.PitchEnvSemitones, value,
            SynthPatch.MinPitchEnvSemitones, SynthPatch.MaxPitchEnvSemitones, nameof(PitchEnvSemitones));
    }

    /// <summary>How long that bend takes to arrive at the note itself.</summary>
    public double PitchEnvMs
    {
        get => _patch.PitchEnvMs;
        set => Set(v => _patch.PitchEnvMs = v, _patch.PitchEnvMs, value,
            SynthPatch.MinTimeMs, SynthPatch.MaxPitchEnvMs, nameof(PitchEnvMs));
    }

    /// <summary>
    /// The cutoff in hertz, which is what the filter is worked out from and what is stored.
    /// </summary>
    /// <remarks>
    /// It announces the knob's position and the reading beside it as well as itself, since all
    /// three are the same number said three ways and a knob left reading the old value is the
    /// way that goes wrong.
    /// </remarks>
    public double FilterCutoffHz
    {
        get => _patch.FilterCutoffHz;
        set => Set(v => _patch.FilterCutoffHz = v, _patch.FilterCutoffHz, value,
            SynthPatch.MinCutoffHz, SynthPatch.MaxCutoffHz,
            nameof(FilterCutoffHz), nameof(FilterCutoff), nameof(FilterCutoffText));
    }

    /// <summary>
    /// The cutoff as a knob position rather than a frequency, so the dial spends its travel
    /// where the ear does: octaves, not hertz.
    /// </summary>
    public double FilterCutoff
    {
        get => _hz.ToPosition(_patch.FilterCutoffHz);
        set => FilterCutoffHz = _hz.ToHz(value);
    }

    /// <summary>The cutoff in words, in hertz or kilohertz as its size asks for.</summary>
    public string FilterCutoffText => _hz.Text(_patch.FilterCutoffHz);

    /// <summary>How much the filter rings at its cutoff.</summary>
    public double FilterResonance
    {
        get => _patch.FilterResonance;
        set => Set(v => _patch.FilterResonance = v, _patch.FilterResonance, value,
            SynthPatch.MinResonance, SynthPatch.MaxResonance, nameof(FilterResonance));
    }

    /// <summary>Called after a preset lands on top of the patch: every value may have moved.</summary>
    public void RefreshAll()
    {
        Bump();
        OnPropertyChanged(string.Empty);
    }

    /// <summary>
    /// Moves the revision on and says so, which is how anything drawing the patch is told to
    /// read it again: the patch is plain data and says nothing about itself.
    /// </summary>
    private void Bump()
    {
        Revision++;
        OnPropertyChanged(nameof(Revision));
    }

    /// <summary>
    /// Writes one value into the patch, held inside its own range, and only when it moved.
    /// </summary>
    /// <remarks>
    /// A value that is not a number is read as the floor rather than written through: a NaN
    /// reaching a voice is silence at best, and it can arrive from a box somebody emptied. The
    /// bounds are the patch's own constants, so a value arriving from a knob, a controller or a
    /// file is held by exactly the same rule. The threshold below which nothing is announced is
    /// a tenth of a thousandth, which is finer than any of these is drawn or heard.
    /// </remarks>
    /// <param name="assign">Puts the value into the patch, which is the only thing that knows where it goes.</param>
    /// <param name="current">Where the patch stands now, so a value arriving as itself announces nothing.</param>
    /// <param name="value">What is being asked for, before it has been bounded.</param>
    /// <param name="min">The bottom of the parameter's own range, and where a NaN lands.</param>
    /// <param name="max">The top of the parameter's own range.</param>
    /// <param name="changed">Every name that now reads differently, since one number can be several.</param>
    private void Set(Action<double> assign, double current, double value, double min, double max, params string[] changed)
    {
        double clamped = double.IsNaN(value) ? min : Math.Clamp(value, min, max);
        if (Math.Abs(current - clamped) < 0.0001) return;

        assign(clamped);
        Bump();

        foreach (var name in changed)
            OnPropertyChanged(name);

        _changed();
    }
}
