using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Tracker.Synth;
using JingleBox2.UI;
using System;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// The editable face of a <see cref="SamplerPatch"/>: the filter and the two envelopes.
/// </summary>
/// <remarks>
/// Every setter clamps through the patch's own limits rather than trusting the control, so a
/// value typed into a box cannot put the machine somewhere it does not go.
///
/// The patch itself is written into rather than copied, because it is the thing the song holds
/// and the voices read: a face with its own numbers would have the panel and the sound
/// disagreeing until something saved.
/// </remarks>
public sealed class SamplerPatchViewModel : ObservableObject
{
    /// <summary>The filter sweep, so a knob position can be checked without a window.</summary>
    private readonly IFrequencyScale _hz = new FrequencyScale();

    /// <summary>The patch the song holds, written into in place.</summary>
    private readonly SamplerPatch _patch;

    /// <summary>Told after every change, which is what marks the song unsaved.</summary>
    private readonly Action _changed;

    /// <summary>Shows one patch. Nothing is copied: the patch handed in is the patch edited.</summary>
    public SamplerPatchViewModel(SamplerPatch patch, Action changed)
    {
        _patch = patch;
        _changed = changed;
    }

    /// <summary>
    /// The patch itself, for whatever plays it and for whatever draws its envelopes.
    /// </summary>
    public SamplerPatch Patch => _patch;

    /// <summary>Bumped on every change, so anything drawing the patch knows to redraw.</summary>
    public int Revision { get; private set; }

    /// <summary>How long the note takes to reach full level, in milliseconds.</summary>
    public double AttackMs
    {
        get => _patch.AttackMs;
        set => Set(v => _patch.AttackMs = v, _patch.AttackMs, value);
    }

    /// <summary>How long it takes to fall from there to the sustain level.</summary>
    public double DecayMs
    {
        get => _patch.DecayMs;
        set => Set(v => _patch.DecayMs = v, _patch.DecayMs, value);
    }

    /// <summary>The level it holds at while the key is down, nought to one.</summary>
    public double Sustain
    {
        get => _patch.Sustain;
        set => Set(v => _patch.Sustain = v, _patch.Sustain, value);
    }

    /// <summary>And how long it takes to go quiet after the key comes up.</summary>
    public double ReleaseMs
    {
        get => _patch.ReleaseMs;
        set => Set(v => _patch.ReleaseMs = v, _patch.ReleaseMs, value);
    }

    /// <summary>
    /// The cutoff as a knob position rather than a frequency, so the dial spends its travel
    /// where the ear does: octaves, not hertz.
    /// </summary>
    /// <remarks>
    /// The patch stores hertz, which is what the filter is worked out from, so the position is
    /// converted both ways rather than kept. The reading beside the dial has to be announced
    /// with it, or the number would go on saying where the knob used to be.
    /// </remarks>
    public double Cutoff
    {
        get => _hz.ToPosition(_patch.CutoffHz);
        set => Set(v => _patch.CutoffHz = _hz.ToHz(v), _hz.ToPosition(_patch.CutoffHz), value,
            nameof(Cutoff), nameof(CutoffText));
    }

    /// <summary>The cutoff in words, in hertz or kilohertz as its size asks for.</summary>
    public string CutoffText => _hz.Text(_patch.CutoffHz);

    /// <summary>How much the filter rings at the cutoff.</summary>
    public double Resonance
    {
        get => _patch.Resonance;
        set => Set(v => _patch.Resonance = v, _patch.Resonance, value);
    }

    /// <summary>How far the filter's own envelope moves the cutoff.</summary>
    public double EnvelopeAmount
    {
        get => _patch.EnvelopeAmount;
        set => Set(v => _patch.EnvelopeAmount = v, _patch.EnvelopeAmount, value);
    }

    /// <summary>
    /// Whether that envelope closes the filter rather than opening it.
    /// </summary>
    /// <remarks>
    /// A flag rather than a signed amount, so the depth knob keeps its whole travel in the
    /// direction that is set instead of spending half of it saying which way round it is.
    /// </remarks>
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

    /// <summary>How far the cutoff follows the note being played up the keyboard.</summary>
    public double KeyFollow
    {
        get => _patch.KeyFollow;
        set => Set(v => _patch.KeyFollow = v, _patch.KeyFollow, value);
    }

    /// <summary>The attack of the filter's own envelope, which is not the amplifier's.</summary>
    public double FilterAttackMs
    {
        get => _patch.FilterAttackMs;
        set => Set(v => _patch.FilterAttackMs = v, _patch.FilterAttackMs, value);
    }

    /// <summary>Its decay.</summary>
    public double FilterDecayMs
    {
        get => _patch.FilterDecayMs;
        set => Set(v => _patch.FilterDecayMs = v, _patch.FilterDecayMs, value);
    }

    /// <summary>Its sustain level.</summary>
    public double FilterSustain
    {
        get => _patch.FilterSustain;
        set => Set(v => _patch.FilterSustain = v, _patch.FilterSustain, value);
    }

    /// <summary>And its release.</summary>
    public double FilterReleaseMs
    {
        get => _patch.FilterReleaseMs;
        set => Set(v => _patch.FilterReleaseMs = v, _patch.FilterReleaseMs, value);
    }

    /// <summary>The patch's own level, before anything the mixer does to it.</summary>
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

    /// <summary>
    /// Writes one value into the patch and lets the patch hold it in range.
    /// </summary>
    /// <remarks>
    /// The clamping is the patch's rather than this class's, so a value arriving from a knob,
    /// from a controller and from a file is bounded by exactly the same rule. A value that is
    /// not a number is dropped rather than written: it can only come from a box somebody is
    /// still typing in, and the floor would be a change nobody asked for.
    ///
    /// The threshold is a thousand millionth, which is there to stop a value being written back
    /// as itself rather than to stop small movements.
    /// </remarks>
    /// <param name="write">Puts the value into the patch, which is the only thing that knows where it goes.</param>
    /// <param name="current">Where the patch stands now, so a value arriving as itself writes nothing.</param>
    /// <param name="value">What is being asked for, before the patch has had a chance to bound it.</param>
    /// <param name="also">
    /// The names that now read differently, when the property is not simply the value: an empty
    /// name means everything, which is what a knob writing straight through wants.
    /// </param>
    private void Set(
        Action<double> write, double current, double value,
        params string[] also)
    {
        if (double.IsNaN(value) || Math.Abs(current - value) < 1e-9) return;

        write(value);
        _patch.Clamp();

        Bump(also.Length > 0 ? also : new[] { "" });
    }

    /// <summary>
    /// Announces a change, moves the revision on, and tells the owner the song has moved.
    /// </summary>
    /// <remarks>
    /// The cutoff's reading is announced every time, whatever moved, because the patch clamps
    /// as a whole: one value going in can move another, and the reading beside the dial is the
    /// one place that shows up.
    /// </remarks>
    private void Bump(params string[] names)
    {
        Revision++;

        foreach (string name in names) OnPropertyChanged(name);

        OnPropertyChanged(nameof(Revision));
        OnPropertyChanged(nameof(CutoffText));

        _changed();
    }
}
