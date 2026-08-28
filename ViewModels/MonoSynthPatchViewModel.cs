using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Tracker.Synth;
using System;
using JingleBox2.Tracker.Synth.Enums;

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
    /// <summary>The patch itself, written straight through rather than copied.</summary>
    private readonly MonoSynthPatch _patch;

    /// <summary>Told after every move, which is how the song learns it has been changed.</summary>
    private readonly Action _changed;

    /// <summary>Binds to one patch and starts the lamp beside the rate knob.</summary>
    public MonoSynthPatchViewModel(MonoSynthPatch patch, Action changed)
    {
        _patch = patch;
        _changed = changed;

        StartLamp();
    }

    /// <summary>The patch underneath, for anything that wants the data rather than the knobs.</summary>
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

    /// <inheritdoc cref="LfoLit"/>
    private bool _lit;

    /// <summary>What turns the lamp over, at half the dialled rate.</summary>
    /// <remarks>
    /// The starting interval is a stand-in for one tick only: the first elapse works out the real
    /// one from the patch.
    /// </remarks>
    private readonly System.Timers.Timer _lamp = new() { AutoReset = true, Interval = 100 };

    /// <summary>Half a turn of the lamp, in milliseconds.</summary>
    /// <remarks>
    /// The timer fires twice per cycle so the lamp is lit for half of every one, which is what
    /// makes it read as a rate rather than a flicker.
    /// </remarks>
    private const double HalfTurnMs = 500.0;

    /// <summary>The slowest and fastest the lamp is allowed to go, whatever the knob says.</summary>
    /// <remarks>
    /// A lamp at forty hertz is a lamp that is simply on, and one at a fiftieth of a hertz is a
    /// timer that fires once a minute. Neither says anything about the rate, so the lamp stops
    /// following the knob outside these two and the number beside it goes on being the truth.
    /// </remarks>
    private const double SlowestLampHz = 0.2;

    /// <inheritdoc cref="SlowestLampHz"/>
    private const double FastestLampHz = 20;

    /// <summary>Starts the lamp, which then works out its own interval on every tick.</summary>
    /// <remarks>
    /// Read off the patch each time rather than reset when the rate knob moves, because the rate
    /// can be changed from a controller, from automation and from a preset landing on top of the
    /// whole patch, and only one of those goes through a setter here.
    /// </remarks>
    private void StartLamp()
    {
        _lamp.Elapsed += (_, _) =>
        {
            double half = HalfTurnMs / Math.Clamp(_patch.LfoRateHz, SlowestLampHz, FastestLampHz);

            if (Math.Abs(_lamp.Interval - half) > 1) _lamp.Interval = half;

            Avalonia.Threading.Dispatcher.UIThread.Post(() => LfoLit = !LfoLit);
        };

        _lamp.Start();
    }

    /// <summary>Stops the lamp. A panel put away should not go on ticking.</summary>
    public void Close() => _lamp.Stop();

    /// <summary>
    /// What each of the five switches has to offer, read off the enumerations themselves.
    /// </summary>
    /// <remarks>
    /// Asked of the type rather than written out in XAML, so a wave added to the synth turns up on
    /// its switch without anybody remembering to add it in two places.
    /// </remarks>
    public MonoSynthWave[] Waves { get; } = Enum.GetValues<MonoSynthWave>();

    /// <inheritdoc cref="Waves"/>
    public FilterMode[] FilterModes { get; } = Enum.GetValues<FilterMode>();

    /// <inheritdoc cref="Waves"/>
    public LfoWave[] LfoWaves { get; } = Enum.GetValues<LfoWave>();

    /// <inheritdoc cref="Waves"/>
    public ModSource[] ModSources { get; } = Enum.GetValues<ModSource>();

    /// <inheritdoc cref="Waves"/>
    public VcoModTarget[] VcoModTargets { get; } = Enum.GetValues<VcoModTarget>();

    /// <summary>The shape the oscillator makes.</summary>
    public MonoSynthWave Wave
    {
        get => _patch.Wave;
        set => Set(value, _patch.Wave, v => _patch.Wave = v);
    }

    /// <summary>How lopsided the pulse is, which does nothing on the shapes that are not one.</summary>
    public double PulseWidth
    {
        get => _patch.PulseWidth;
        set => Set(value, _patch.PulseWidth, v => _patch.PulseWidth = v);
    }

    /// <summary>The oscillator's own transpose, in semitones.</summary>
    public double TuneSemitones
    {
        get => _patch.TuneSemitones;
        set => Set(value, _patch.TuneSemitones, v => _patch.TuneSemitones = v);
    }

    /// <summary>And the fine part of it, in cents.</summary>
    public double FineCents
    {
        get => _patch.FineCents;
        set => Set(value, _patch.FineCents, v => _patch.FineCents = v);
    }

    /// <summary>How long the pitch takes to slide from one note to the next.</summary>
    public double GlideMs
    {
        get => _patch.GlideMs;
        set => Set(value, _patch.GlideMs, v => _patch.GlideMs = v);
    }

    /// <summary>How much noise is mixed in beside the oscillator.</summary>
    public double NoiseMix
    {
        get => _patch.NoiseMix;
        set => Set(value, _patch.NoiseMix, v => _patch.NoiseMix = v);
    }

    /// <summary>Which way round the filter works: what it lets past and what it holds back.</summary>
    public FilterMode FilterMode
    {
        get => _patch.FilterMode;
        set => Set(value, _patch.FilterMode, v => _patch.FilterMode = v);
    }

    /// <summary>Where the filter is set, in hertz.</summary>
    public double CutoffHz
    {
        get => _patch.CutoffHz;
        set => Set(value, _patch.CutoffHz, v => _patch.CutoffHz = v);
    }

    /// <summary>The cutoff as anybody would say it: hertz below a thousand, kilohertz above.</summary>
    public string CutoffText => _patch.CutoffHz >= 1000
        ? (_patch.CutoffHz / 1000).ToString("0.0") + " kHz"
        : _patch.CutoffHz.ToString("0") + " Hz";

    /// <summary>How much the filter lifts what is right at the cutoff.</summary>
    public double Resonance
    {
        get => _patch.Resonance;
        set => Set(value, _patch.Resonance, v => _patch.Resonance = v);
    }

    /// <summary>How long the envelope takes to come up.</summary>
    public double AttackMs
    {
        get => _patch.AttackMs;
        set => Set(value, _patch.AttackMs, v => _patch.AttackMs = v);
    }

    /// <summary>And how long it takes to fall away.</summary>
    public double DecayMs
    {
        get => _patch.DecayMs;
        set => Set(value, _patch.DecayMs, v => _patch.DecayMs = v);
    }

    /// <summary>Whether it holds while the key is down, or falls away regardless.</summary>
    public bool Sustain
    {
        get => _patch.Sustain;
        set => Set(value, _patch.Sustain, v => _patch.Sustain = v);
    }

    /// <summary>Whether the envelope works the level as well as whatever else it is pointed at.</summary>
    public bool EnvelopeToAmp
    {
        get => _patch.EnvelopeToAmp;
        set => Set(value, _patch.EnvelopeToAmp, v => _patch.EnvelopeToAmp = v);
    }

    /// <summary>How fast the low frequency oscillator goes round, which is what the lamp shows.</summary>
    public double LfoRateHz
    {
        get => _patch.LfoRateHz;
        set => Set(value, _patch.LfoRateHz, v => _patch.LfoRateHz = v);
    }

    /// <summary>The shape it goes round in.</summary>
    public LfoWave LfoWave
    {
        get => _patch.LfoWave;
        set => Set(value, _patch.LfoWave, v => _patch.LfoWave = v);
    }

    /// <summary>What is moving the oscillator, on the first of the two modulation routes.</summary>
    public ModSource VcoModSource
    {
        get => _patch.VcoModSource;
        set => Set(value, _patch.VcoModSource, v => _patch.VcoModSource = v);
    }

    /// <summary>How far it moves it.</summary>
    public double VcoModAmount
    {
        get => _patch.VcoModAmount;
        set => Set(value, _patch.VcoModAmount, v => _patch.VcoModAmount = v);
    }

    /// <summary>And what about the oscillator it moves.</summary>
    public VcoModTarget VcoModTarget
    {
        get => _patch.VcoModTarget;
        set => Set(value, _patch.VcoModTarget, v => _patch.VcoModTarget = v);
    }

    /// <summary>What is moving the filter, on the second route.</summary>
    public ModSource VcfModSource
    {
        get => _patch.VcfModSource;
        set => Set(value, _patch.VcfModSource, v => _patch.VcfModSource = v);
    }

    /// <summary>How far it moves it.</summary>
    public double VcfModAmount
    {
        get => _patch.VcfModAmount;
        set => Set(value, _patch.VcfModAmount, v => _patch.VcfModAmount = v);
    }

    /// <summary>Whether that route works the other way round.</summary>
    public bool VcfModInverted
    {
        get => _patch.VcfModInverted;
        set => Set(value, _patch.VcfModInverted, v => _patch.VcfModInverted = v);
    }

    /// <summary>How loud the machine is before it reaches the track.</summary>
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
