using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Tracker.Synth;
using JingleBox2.UI;
using System;

namespace JingleBox2.ViewModels;

/// <summary>
/// The editable face of a <see cref="ZamplerPatch"/>: the filter and the two envelopes.
/// </summary>
/// <remarks>
/// Every setter clamps through the patch's own limits rather than trusting the control, so a
/// value typed into a box cannot put the machine somewhere it does not go.
/// </remarks>
public sealed class ZamplerPatchViewModel : ObservableObject
{
    private readonly ZamplerPatch _patch;
    private readonly Action _changed;

    public ZamplerPatchViewModel(ZamplerPatch patch, Action changed)
    {
        _patch = patch;
        _changed = changed;
    }

    public ZamplerPatch Patch => _patch;

    /// <summary>Bumped on every change, so anything drawing the patch knows to redraw.</summary>
    public int Revision { get; private set; }

    // ---- the amplifier ----------------------------------------------------

    public double AttackMs
    {
        get => _patch.AttackMs;
        set => Set(v => _patch.AttackMs = v, _patch.AttackMs, value);
    }

    public double DecayMs
    {
        get => _patch.DecayMs;
        set => Set(v => _patch.DecayMs = v, _patch.DecayMs, value);
    }

    public double Sustain
    {
        get => _patch.Sustain;
        set => Set(v => _patch.Sustain = v, _patch.Sustain, value);
    }

    public double ReleaseMs
    {
        get => _patch.ReleaseMs;
        set => Set(v => _patch.ReleaseMs = v, _patch.ReleaseMs, value);
    }

    // ---- the filter -------------------------------------------------------

    /// <summary>
    /// The cutoff as a knob position rather than a frequency, so the dial spends its travel
    /// where the ear does: octaves, not hertz.
    /// </summary>
    public double Cutoff
    {
        get => FrequencyScale.ToPosition(_patch.CutoffHz);
        set => Set(v => _patch.CutoffHz = FrequencyScale.ToHz(v), FrequencyScale.ToPosition(_patch.CutoffHz), value,
            nameof(Cutoff), nameof(CutoffText));
    }

    public string CutoffText => FrequencyScale.Text(_patch.CutoffHz);

    public double Resonance
    {
        get => _patch.Resonance;
        set => Set(v => _patch.Resonance = v, _patch.Resonance, value);
    }

    public double EnvelopeAmount
    {
        get => _patch.EnvelopeAmount;
        set => Set(v => _patch.EnvelopeAmount = v, _patch.EnvelopeAmount, value);
    }

    public bool EnvelopeInverted
    {
        get => _patch.EnvelopeInverted;
        set
        {
            if (_patch.EnvelopeInverted == value) return;

            _patch.EnvelopeInverted = value;
            Bump(nameof(EnvelopeInverted));
        }
    }

    public double KeyFollow
    {
        get => _patch.KeyFollow;
        set => Set(v => _patch.KeyFollow = v, _patch.KeyFollow, value);
    }

    // ---- the filter's own envelope ----------------------------------------

    public double FilterAttackMs
    {
        get => _patch.FilterAttackMs;
        set => Set(v => _patch.FilterAttackMs = v, _patch.FilterAttackMs, value);
    }

    public double FilterDecayMs
    {
        get => _patch.FilterDecayMs;
        set => Set(v => _patch.FilterDecayMs = v, _patch.FilterDecayMs, value);
    }

    public double FilterSustain
    {
        get => _patch.FilterSustain;
        set => Set(v => _patch.FilterSustain = v, _patch.FilterSustain, value);
    }

    public double FilterReleaseMs
    {
        get => _patch.FilterReleaseMs;
        set => Set(v => _patch.FilterReleaseMs = v, _patch.FilterReleaseMs, value);
    }

    public double Volume
    {
        get => _patch.Volume;
        set => Set(v => _patch.Volume = v, _patch.Volume, value);
    }

    /// <summary>Called after a preset lands on top of the patch: every value may have moved.</summary>
    public void RefreshAll()
    {
        Revision++;
        OnPropertyChanged(string.Empty);
    }

    private void Set(
        Action<double> write, double current, double value,
        params string[] also)
    {
        if (double.IsNaN(value) || Math.Abs(current - value) < 1e-9) return;

        write(value);
        _patch.Clamp();

        Bump(also.Length > 0 ? also : new[] { "" });
    }

    private void Bump(params string[] names)
    {
        Revision++;

        foreach (string name in names) OnPropertyChanged(name);

        OnPropertyChanged(nameof(Revision));
        OnPropertyChanged(nameof(CutoffText));

        _changed();
    }
}
