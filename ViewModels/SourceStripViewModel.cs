using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace JingleBox2.ViewModels;

/// <summary>
/// One strip on the mixer over something that is not a track: the recording input, the take
/// being auditioned, or the pads.
/// </summary>
/// <remarks>
/// A track's strip is <see cref="TrackStripViewModel"/> and has a pan, a mute, a solo, ducking
/// and a place in the song. None of that is true here. What these three have is a level and a
/// meter, so that is what this is, and the three differ only in what the level writes to and
/// where the meter reads from, which are handed in.
///
/// Handed in rather than subclassed, because there is no behaviour to override: the recording
/// input's level goes into the recorder's gain and never reaches the output bus at all, while
/// the pads' and the take's are bus levels. Three classes would be three copies of a fader.
///
/// The meter is written from outside by whatever is polling, exactly as a track's is, since what
/// the meters are about is whether anything is sounding at all and that is a question about the
/// page rather than about one strip.
/// </remarks>
public sealed partial class SourceStripViewModel : ObservableObject
{
    /// <summary>Reads where the level stands.</summary>
    private readonly Func<double> _read;

    /// <summary>Writes where the level has been put.</summary>
    private readonly Action<double> _write;

    /// <summary>What the meter should show, as two peaks from 0 to 1.</summary>
    private readonly Func<(float Left, float Right)> _meter;

    /// <summary>Told after a solo moves, so the whole row can be worked out again.</summary>
    /// <remarks>
    /// Solo is not a fact about one strip: it means only this, which is a statement about every
    /// source at once. So the strip records what was pressed and hands the answering to whatever
    /// knows the whole row.
    /// </remarks>
    private readonly Action? _soloed;

    /// <summary>The bus this strip is over, or nothing where there is none.</summary>
    /// <remarks>
    /// Only a strip over a bus has a pan and a mute. The recording input has neither: panning
    /// what is being recorded is not something the recorder does, and a mute there would mean
    /// quietly recording nothing, which is a way to lose a take rather than a control.
    /// </remarks>
    private readonly Audio.Interfaces.IOutputBus? _bus;

    /// <summary>Builds a strip over one source.</summary>
    /// <param name="label">What the badge says, which is what the thing is called.</param>
    /// <param name="tip">The longer version, for resting on the badge.</param>
    /// <param name="minimum">The bottom of the fader, in decibels.</param>
    /// <param name="maximum">The top of it.</param>
    /// <param name="read">Where the level stands now.</param>
    /// <param name="write">Where to put it when the fader moves.</param>
    /// <param name="meter">What is going through, for the meter.</param>
    /// <param name="bus">
    /// The bus underneath, which is what gives the strip a pan and a mute. Nothing for a strip
    /// that is over something else, which is the recording input.
    /// </param>
    /// <param name="soloed">Told after the solo moves, so the whole row can be worked out again.</param>
    /// <param name="source">
    /// What this strip is fed from, where that is a thing anybody can choose. Only the recording
    /// input has one; the pads and a take being auditioned are fed by this program.
    /// </param>
    public SourceStripViewModel(
        string label,
        string tip,
        double minimum,
        double maximum,
        Func<double> read,
        Action<double> write,
        Func<(float Left, float Right)> meter,
        Audio.Interfaces.IOutputBus? bus = null,
        Action? soloed = null,
        Interfaces.IInputSource? source = null)
    {
        _bus = bus;
        _soloed = soloed;

        Source = source;

        Label = label;
        Tip = tip;
        Minimum = minimum;
        Maximum = maximum;

        _read = read;
        _write = write;
        _meter = meter;
    }

    /// <summary>What the badge says.</summary>
    public string Label { get; }

    /// <summary>The longer version, for resting on it.</summary>
    public string Tip { get; }

    /// <summary>The bottom of the fader, in decibels.</summary>
    public double Minimum { get; }

    /// <summary>The top of the fader, in decibels.</summary>
    public double Maximum { get; }

    /// <summary>Whether this strip has a pan and a mute, which a strip over a bus has.</summary>
    public bool OnABus => _bus != null;

    /// <summary>
    /// What this strip is fed from, or nothing where that is not a question anybody answers.
    /// </summary>
    /// <remarks>
    /// **A strip says what a thing is doing, and where it comes from is the first half of that**,
    /// which is why the picker is at the foot of this strip rather than on another page. The pads
    /// and a take have none: what feeds them is this program.
    /// </remarks>
    public Interfaces.IInputSource? Source { get; }

    /// <summary>Whether to draw the source picker, which only a strip with a source does.</summary>
    public bool TakesSource => Source != null;

    /// <summary>Where it sits between the speakers, -1 hard left to 1 hard right.</summary>
    public double Pan
    {
        get => _bus?.Pan ?? 0;
        set
        {
            if (_bus == null || Math.Abs(_bus.Pan - value) < 0.0001) return;

            _bus.Pan = value;

            OnPropertyChanged();
        }
    }

    /// <summary>Backing field for <see cref="Solo"/>.</summary>
    private bool solo;

    /// <summary>
    /// Whether this is the only thing being heard.
    /// </summary>
    /// <remarks>
    /// Recorded here and answered elsewhere: what a solo does is pause every source that is not
    /// soloed, which only the output bus knows how to do because only it knows them all.
    ///
    /// Which also means a solo here is nothing like a track's. A track's solo is the song's, is
    /// saved in the song file, and picks among the tracks; this picks among the things the
    /// application is playing, so soloing the pads silences the whole song rather than part of it.
    /// </remarks>
    public bool Solo
    {
        get => solo;
        set
        {
            if (solo == value) return;

            solo = value;

            OnPropertyChanged();

            _soloed?.Invoke();
        }
    }

    /// <summary>Backing field for <see cref="CanSolo"/>.</summary>
    private bool canSolo;

    /// <summary>
    /// Whether soloing is possible at all, which is only while there is one output stream.
    /// </summary>
    /// <remarks>
    /// A solo is worked out by the bus, so with the bus off there is nothing that knows every
    /// source and nothing that could pause the others. The button is left where it is and goes
    /// grey rather than coming and going: a control that vanishes takes the strip's layout with
    /// it, and one that is dark and says why is the same answer without the movement.
    /// </remarks>
    public bool CanSolo
    {
        get => canSolo;
        set => SetProperty(ref canSolo, value);
    }

    /// <summary>Whether it is silenced, with its fader left where it stands.</summary>
    public bool Mute
    {
        get => _bus?.Mute ?? false;
        set
        {
            if (_bus == null || _bus.Mute == value) return;

            _bus.Mute = value;

            OnPropertyChanged();
        }
    }

    /// <summary>Where the level stands, in decibels.</summary>
    public double VolumeDecibels
    {
        get => _read();
        set
        {
            if (Math.Abs(_read() - value) < 0.0001) return;

            _write(value);

            OnPropertyChanged();
        }
    }

    /// <summary>Backing field for <see cref="Left"/>.</summary>
    private double left;

    /// <summary>Backing field for <see cref="Right"/>.</summary>
    private double right;

    /// <summary>What is going through on the left, for the meter.</summary>
    public double Left
    {
        get => left;
        private set => SetProperty(ref left, value);
    }

    /// <summary>And on the right.</summary>
    public double Right
    {
        get => right;
        private set => SetProperty(ref right, value);
    }

    /// <summary>
    /// Reads the meter again, called by whatever is polling the page.
    /// </summary>
    /// <remarks>
    /// The reading is taken once and put into both, rather than the meter being asked twice: the
    /// two sides of one reading have to come from the same moment or a mono source drawn as
    /// stereo flickers between them.
    /// </remarks>
    public void ReadMeter()
    {
        var (l, r) = _meter();

        Left = l;
        Right = r;
    }

    /// <summary>Says the level moved somewhere else, so the fader follows.</summary>
    public void Reread() => OnPropertyChanged(nameof(VolumeDecibels));
}
