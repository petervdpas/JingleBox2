using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Tracker.Synth;
using System;

namespace JingleBox2.ViewModels;

/// <summary>
/// The editable face of an <see cref="MonoSynthPatch"/>. The patch stays plain data that
/// serializes with the song; this is what the panel's knobs and switches are bound to.
/// </summary>
/// <remarks>
/// Every setter clamps through the patch's own limits rather than trusting the control, so a
/// value typed into a box cannot put the machine somewhere it does not go.
/// </remarks>
public sealed class MonoSynthPatchViewModel : ObservableObject
{
    private readonly MonoSynthPatch _patch;
    private readonly Action _changed;

    public MonoSynthPatchViewModel(MonoSynthPatch patch, Action changed)
    {
        _patch = patch;
        _changed = changed;

        StartLamp();
    }

    public MonoSynthPatch Patch => _patch;

    /// <summary>Bumped on every change, so a scope watching plain data knows to redraw.</summary>
    public int Revision { get; private set; }

    /// <summary>Called after a preset lands on top of the patch: every value may have moved.</summary>
    public void RefreshAll()
    {
        Revision++;
        OnPropertyChanged(string.Empty);
    }

    /// <summary>
    /// The low frequency oscillator's lamp, going round at whatever rate is dialled in.
    /// </summary>
    /// <remarks>
    /// The machine this follows has a lamp beside its rate knob for the same reason: a rate in
    /// hertz is a number, and a light going round at it is the rate itself, which is the thing
    /// you are actually setting. It runs off the patch rather than off a playing voice, so it
    /// shows the rate whether or not anything is sounding, which is when you are setting it.
    ///
    /// It lives here rather than on whatever is hosting the panel, so the tab and the window
    /// onto a track both get it without either of them knowing about it.
    /// </remarks>
    public bool LfoLit
    {
        get => _lit;
        private set
        {
            if (_lit == value) return;

            _lit = value;
            OnPropertyChanged();
        }
    }

    private bool _lit;
    private readonly System.Timers.Timer _lamp = new() { AutoReset = true, Interval = 100 };

    private void StartLamp()
    {
        _lamp.Elapsed += (_, _) =>
        {
            // Half a turn per tick, so it is lit for half of every cycle. Clamped, because a
            // lamp at forty hertz is a lamp that is simply on, and one at a fiftieth is a
            // timer that fires once a minute.
            double half = 500.0 / Math.Clamp(_patch.LfoRateHz, 0.2, 20);

            if (Math.Abs(_lamp.Interval - half) > 1) _lamp.Interval = half;

            Avalonia.Threading.Dispatcher.UIThread.Post(() => LfoLit = !LfoLit);
        };

        _lamp.Start();
    }

    /// <summary>Stops the lamp. A panel put away should not go on ticking.</summary>
    public void Close() => _lamp.Stop();

    // ---- the lists the switches are drawn from ---------------------------

    public MonoSynthWave[] Waves { get; } = Enum.GetValues<MonoSynthWave>();
    public FilterMode[] FilterModes { get; } = Enum.GetValues<FilterMode>();
    public LfoWave[] LfoWaves { get; } = Enum.GetValues<LfoWave>();
    public ModSource[] ModSources { get; } = Enum.GetValues<ModSource>();
    public VcoModTarget[] VcoModTargets { get; } = Enum.GetValues<VcoModTarget>();

    // ---- oscillator -------------------------------------------------------

    public MonoSynthWave Wave
    {
        get => _patch.Wave;
        set => Set(value, _patch.Wave, v => _patch.Wave = v);
    }

    public double PulseWidth
    {
        get => _patch.PulseWidth;
        set => Set(value, _patch.PulseWidth, v => _patch.PulseWidth = v);
    }

    public double TuneSemitones
    {
        get => _patch.TuneSemitones;
        set => Set(value, _patch.TuneSemitones, v => _patch.TuneSemitones = v);
    }

    public double FineCents
    {
        get => _patch.FineCents;
        set => Set(value, _patch.FineCents, v => _patch.FineCents = v);
    }

    public double GlideMs
    {
        get => _patch.GlideMs;
        set => Set(value, _patch.GlideMs, v => _patch.GlideMs = v);
    }

    public double NoiseMix
    {
        get => _patch.NoiseMix;
        set => Set(value, _patch.NoiseMix, v => _patch.NoiseMix = v);
    }

    // ---- filter -----------------------------------------------------------

    public FilterMode FilterMode
    {
        get => _patch.FilterMode;
        set => Set(value, _patch.FilterMode, v => _patch.FilterMode = v);
    }

    public double CutoffHz
    {
        get => _patch.CutoffHz;
        set => Set(value, _patch.CutoffHz, v => _patch.CutoffHz = v);
    }

    /// <summary>The cutoff as anybody would say it: hertz below a thousand, kilohertz above.</summary>
    public string CutoffText => _patch.CutoffHz >= 1000
        ? (_patch.CutoffHz / 1000).ToString("0.0") + " kHz"
        : _patch.CutoffHz.ToString("0") + " Hz";

    public double Resonance
    {
        get => _patch.Resonance;
        set => Set(value, _patch.Resonance, v => _patch.Resonance = v);
    }

    // ---- envelope ---------------------------------------------------------

    public double AttackMs
    {
        get => _patch.AttackMs;
        set => Set(value, _patch.AttackMs, v => _patch.AttackMs = v);
    }

    public double DecayMs
    {
        get => _patch.DecayMs;
        set => Set(value, _patch.DecayMs, v => _patch.DecayMs = v);
    }

    public bool Sustain
    {
        get => _patch.Sustain;
        set => Set(value, _patch.Sustain, v => _patch.Sustain = v);
    }

    public bool EnvelopeToAmp
    {
        get => _patch.EnvelopeToAmp;
        set => Set(value, _patch.EnvelopeToAmp, v => _patch.EnvelopeToAmp = v);
    }

    // ---- low frequency oscillator ----------------------------------------

    public double LfoRateHz
    {
        get => _patch.LfoRateHz;
        set => Set(value, _patch.LfoRateHz, v => _patch.LfoRateHz = v);
    }

    public LfoWave LfoWave
    {
        get => _patch.LfoWave;
        set => Set(value, _patch.LfoWave, v => _patch.LfoWave = v);
    }

    // ---- the two modulation routes ---------------------------------------

    public ModSource VcoModSource
    {
        get => _patch.VcoModSource;
        set => Set(value, _patch.VcoModSource, v => _patch.VcoModSource = v);
    }

    public double VcoModAmount
    {
        get => _patch.VcoModAmount;
        set => Set(value, _patch.VcoModAmount, v => _patch.VcoModAmount = v);
    }

    public VcoModTarget VcoModTarget
    {
        get => _patch.VcoModTarget;
        set => Set(value, _patch.VcoModTarget, v => _patch.VcoModTarget = v);
    }

    public ModSource VcfModSource
    {
        get => _patch.VcfModSource;
        set => Set(value, _patch.VcfModSource, v => _patch.VcfModSource = v);
    }

    public double VcfModAmount
    {
        get => _patch.VcfModAmount;
        set => Set(value, _patch.VcfModAmount, v => _patch.VcfModAmount = v);
    }

    public bool VcfModInverted
    {
        get => _patch.VcfModInverted;
        set => Set(value, _patch.VcfModInverted, v => _patch.VcfModInverted = v);
    }

    // ---- output -----------------------------------------------------------

    public double Volume
    {
        get => _patch.Volume;
        set => Set(value, _patch.Volume, v => _patch.Volume = v);
    }

    /// <summary>
    /// Writes a value, clamps the whole patch, and tells whoever is listening. One path for
    /// every control, so no knob can be the one that forgot to say it had moved.
    /// </summary>
    private void Set<T>(T value, T current, Action<T> write, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (Equals(value, current)) return;

        write(value);
        _patch.Clamp();

        Revision++;

        OnPropertyChanged(name);
        OnPropertyChanged(nameof(Revision));
        OnPropertyChanged(nameof(CutoffText));

        _changed();
    }
}
