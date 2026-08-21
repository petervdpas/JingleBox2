using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Tracker.Synth;
using System;

namespace JingleBox2.ViewModels;

/// <summary>
/// The editable face of a <see cref="SynthPatch"/>. The patch itself stays plain data that
/// serializes with the song; this wraps it for the sliders and keeps every value in range.
/// </summary>
public sealed class SynthPatchViewModel : ObservableObject
{
    private readonly SynthPatch _patch;
    private readonly Action _changed;

    public SynthPatchViewModel(SynthPatch patch, Action changed)
    {
        _patch = patch;
        _changed = changed;
    }

    public SynthPatch Patch => _patch;

    public SynthWave[] Waves { get; } = Enum.GetValues<SynthWave>();

    public SynthWave Wave
    {
        get => _patch.Wave;
        set
        {
            if (_patch.Wave == value) return;

            _patch.Wave = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPulse));
            _changed();
        }
    }

    /// <summary>Duty only means anything to the pulse wave, so the row hides for the others.</summary>
    public bool IsPulse => _patch.Wave == SynthWave.Pulse;

    public double Duty
    {
        get => _patch.Duty;
        set => Set(v => _patch.Duty = v, _patch.Duty, value, SynthPatch.MinDuty, SynthPatch.MaxDuty,
            nameof(Duty));
    }

    public double AttackMs
    {
        get => _patch.AttackMs;
        set => Set(v => _patch.AttackMs = v, _patch.AttackMs, value, SynthPatch.MinTimeMs, SynthPatch.MaxAttackMs,
            nameof(AttackMs));
    }

    public double DecayMs
    {
        get => _patch.DecayMs;
        set => Set(v => _patch.DecayMs = v, _patch.DecayMs, value, SynthPatch.MinTimeMs, SynthPatch.MaxDecayMs,
            nameof(DecayMs));
    }

    public double Sustain
    {
        get => _patch.Sustain;
        set => Set(v => _patch.Sustain = v, _patch.Sustain, value, SynthPatch.MinSustain, SynthPatch.MaxSustain,
            nameof(Sustain));
    }

    public double ReleaseMs
    {
        get => _patch.ReleaseMs;
        set => Set(v => _patch.ReleaseMs = v, _patch.ReleaseMs, value, SynthPatch.MinTimeMs, SynthPatch.MaxReleaseMs,
            nameof(ReleaseMs));
    }

    public double VibratoRateHz
    {
        get => _patch.VibratoRateHz;
        set => Set(v => _patch.VibratoRateHz = v, _patch.VibratoRateHz, value,
            SynthPatch.MinRateHz, SynthPatch.MaxRateHz, nameof(VibratoRateHz));
    }

    public double VibratoDepthCents
    {
        get => _patch.VibratoDepthCents;
        set => Set(v => _patch.VibratoDepthCents = v, _patch.VibratoDepthCents, value,
            SynthPatch.MinVibratoDepthCents, SynthPatch.MaxVibratoDepthCents, nameof(VibratoDepthCents));
    }

    public double TremoloRateHz
    {
        get => _patch.TremoloRateHz;
        set => Set(v => _patch.TremoloRateHz = v, _patch.TremoloRateHz, value,
            SynthPatch.MinRateHz, SynthPatch.MaxRateHz, nameof(TremoloRateHz));
    }

    public double TremoloDepth
    {
        get => _patch.TremoloDepth;
        set => Set(v => _patch.TremoloDepth = v, _patch.TremoloDepth, value,
            SynthPatch.MinTremoloDepth, SynthPatch.MaxTremoloDepth, nameof(TremoloDepth));
    }

    public double PitchEnvSemitones
    {
        get => _patch.PitchEnvSemitones;
        set => Set(v => _patch.PitchEnvSemitones = v, _patch.PitchEnvSemitones, value,
            SynthPatch.MinPitchEnvSemitones, SynthPatch.MaxPitchEnvSemitones, nameof(PitchEnvSemitones));
    }

    public double PitchEnvMs
    {
        get => _patch.PitchEnvMs;
        set => Set(v => _patch.PitchEnvMs = v, _patch.PitchEnvMs, value,
            SynthPatch.MinTimeMs, SynthPatch.MaxPitchEnvMs, nameof(PitchEnvMs));
    }

    /// <summary>Called after a preset lands on top of the patch: every value may have moved.</summary>
    public void RefreshAll() => OnPropertyChanged(string.Empty);

    private void Set(Action<double> assign, double current, double value, double min, double max, params string[] changed)
    {
        double clamped = double.IsNaN(value) ? min : Math.Clamp(value, min, max);
        if (Math.Abs(current - clamped) < 0.0001) return;

        assign(clamped);

        foreach (var name in changed)
            OnPropertyChanged(name);

        _changed();
    }
}
