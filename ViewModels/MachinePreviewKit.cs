using JingleBox2.Machines;
using JingleBox2.Tracker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using JingleBox2.Machines.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// A kit for the editor's panel to draw: sixteen pads that play nothing.
/// </summary>
/// <remarks>
/// The same reason the pictures on a panel being laid out are real pictures and the takes on it
/// are your real takes. A pad grid drawn against nothing is a grid of no pads, which takes no
/// room, so a machine being laid out around it is laid out around a gap; and one drawn against
/// four made up names is the wrong width for sixteen.
///
/// The names are the notes, because that is what an unfilled kit says on a real machine. What it
/// would say once somebody put drums on it is longer, which is why the caps are sized by the
/// machine rather than by what is written on them.
/// </remarks>
public sealed class MachinePreviewKit : IMachinePads
{
    /// <summary>Which pad is in hand, which is the one thing on the bench that really moves.</summary>
    private int _picked;

    /// <inheritdoc/>
    /// <remarks>Always a full kit, so the grid on the bench is the size it will really be.</remarks>
    public int Count => DrumKit.PadCount;

    /// <inheritdoc/>
    /// <remarks>Nothing is written on them, since nothing has been put on them.</remarks>
    public string Cap(int at) => "";

    /// <inheritdoc/>
    /// <remarks>The note, which is what an unfilled kit says on a real machine.</remarks>
    public string Note(int at) => new Note(DrumKit.FirstSemitone + at).ToString();

    /// <summary>Nothing is sounding: the editor is not playing anything.</summary>
    public bool Lit(int at) => false;

    /// <summary>And nothing is on them, which is the state a machine ships in.</summary>
    public bool Filled(int at) => false;

    /// <inheritdoc/>
    public int Picked
    {
        get => _picked;
        set
        {
            if (value < 0 || value >= Count || value == _picked) return;

            _picked = value;

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Nothing. A pad on a machine being built has no recording to hit.</summary>
    public void Hit(int at) { }

    /// <inheritdoc/>
    /// <remarks>Raised for the pad in hand and nothing else, which is all that can move here.</remarks>
    public event EventHandler? Changed;
}

/// <summary>
/// A recording for the editor's chop control to draw, cut into four.
/// </summary>
/// <remarks>
/// Drawn rather than left empty for the reason the kit is. The control is most of the width of
/// a panel and half its own height is the picture, so a machine laid out around an empty one is
/// laid out around the wrong shape.
///
/// The shape is made rather than read off a file: what it is a picture of does not matter, and
/// a preview that went looking for one of your recordings would put a different machine on the
/// bench depending on what you had recorded.
/// </remarks>
public sealed class MachinePreviewSlices : IMachineSlices
{
    /// <summary>How many peaks the made-up picture has.</summary>
    /// <remarks>
    /// Enough that the shape reads as a recording at any width a panel is likely to be, and few
    /// enough that building it costs nothing when the bench is opened.
    /// </remarks>
    private const int PeakCount = 600;

    /// <inheritdoc/>
    /// <remarks>
    /// Four hits, each a burst that falls away, which is what a drum loop looks like drawn. Made
    /// rather than read off a file, so the bench looks the same whatever you have recorded.
    /// </remarks>
    public float[]? Peaks { get; } =
        Enumerable.Range(0, PeakCount)
            .Select(at => (float)(Math.Abs(Math.Sin(at / 24.0)) * Math.Exp(-(at % 150) / 60.0)))
            .ToArray();

    /// <inheritdoc/>
    /// <remarks>Four even pieces, counting both ends, which is five boundaries.</remarks>
    public ObservableCollection<double> Points { get; } = new(new[] { 0.0, 0.25, 0.5, 0.75, 1.0 });

    /// <inheritdoc/>
    /// <remarks>The second one, so the control shows a piece picked out rather than none.</remarks>
    public int SelectedSlice { get; set; } = 1;

    /// <inheritdoc/>
    public int MaxSlices => DrumKit.PadCount;

    /// <inheritdoc/>
    /// <remarks>Nowhere: nothing on the bench is playing.</remarks>
    public double Playhead => -1;

    /// <inheritdoc/>
    public bool Looping => false;

    /// <inheritdoc/>
    public double LoopStart { get; set; }

    /// <inheritdoc/>
    public double LoopEnd { get; set; } = 1;

    /// <inheritdoc/>
    /// <remarks>Always, so the control draws its full self rather than its folded one.</remarks>
    public bool IsOpen => true;

    /// <inheritdoc/>
    public string TakeText => "a recording";

    /// <inheritdoc/>
    public string CountText => "4 pieces";

    /// <inheritdoc/>
    public double Pieces { get; set; } = SliceEditorViewModel.DefaultPieces;

    /// <inheritdoc/>
    /// <remarks>The real three, so the picker on the bench is the width it will really be.</remarks>
    public IReadOnlyList<string> CutOptions { get; } = new[] { "Hits", "Gaps", "Even" };

    /// <inheritdoc/>
    public string CutBy { get; set; } = "Hits";

    /// <inheritdoc/>
    /// <remarks>The real three, so the picker on the bench is the width it will really be.</remarks>
    public IReadOnlyList<string> LoopNames { get; } = new[] { "Off", "Forward", "Ping-pong" };

    /// <inheritdoc/>
    public string LoopName { get; set; } = "Off";

    /// <summary>Nothing. There is no recording here to cut, only a picture of one.</summary>
    public void Chop() { }

    /// <summary>
    /// Nowhere to subscribe, because none of this moves.
    /// </summary>
    /// <remarks>
    /// The picture is made rather than read off a file, the cuts are fixed, and nothing on the
    /// bench is playing. Holding the handlers would be holding a list that is never read.
    /// </remarks>
    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add { }
        remove { }
    }
}

/// <summary>
/// A map for the editor's panel to draw: three zones across the keyboard, playing nothing.
/// </summary>
/// <remarks>
/// The same reason the kit is a real kit of sixteen. A map drawn against nothing is one lane of
/// empty board, which is the wrong height, so a machine being laid out around it is laid out
/// around a gap that will not be there once somebody puts an instrument on it.
///
/// Three rather than one, because what a map is for is telling you whether the keyboard is
/// covered, and one zone across the whole of it never shows that. They can be dragged: the panel
/// being laid out is a real panel, and a boundary moved here moves nothing but this.
/// </remarks>
public sealed class MachinePreviewMap : IMachineZones
{
    /// <summary>Three zones covering the keyboard, each with its root in the middle of it.</summary>
    /// <remarks>
    /// Movable, since the panel being laid out is a real panel and a boundary dragged here moves
    /// nothing but this.
    /// </remarks>
    private readonly (int Low, int High, int Root)[] _zones =
    {
        (0, 39, 20),
        (40, 79, 60),
        (80, 119, 100),
    };

    /// <summary>Which zone is in hand.</summary>
    private int _picked;

    /// <inheritdoc/>
    public int Count => _zones.Length;

    /// <summary>Nothing. A zone on a machine being built has no recording to be called after.</summary>
    public string Cap(int at) => "";

    /// <inheritdoc/>
    public int Low(int at) => _zones[at].Low;

    /// <inheritdoc/>
    public int High(int at) => _zones[at].High;

    /// <inheritdoc/>
    public int Root(int at) => _zones[at].Root;

    /// <summary>Drawn as filled, so the chip shows a zone rather than a gap.</summary>
    public bool Filled(int at) => true;

    /// <inheritdoc/>
    public int Picked
    {
        get => _picked;
        set
        {
            if (value < 0 || value >= Count || value == _picked) return;

            _picked = value;

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    public void Move(int at, int low, int high, int root)
    {
        if (at < 0 || at >= Count) return;

        _zones[at] = (low, high, root);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public event EventHandler? Changed;
}

/// <summary>
/// A wave for the editor's panel to draw: one the machine being built is not making.
/// </summary>
/// <remarks>
/// The same reason the kit is a real kit and the map a real map. A picture drawn against nothing
/// is an empty frame, and a machine laid out around an empty frame is laid out around the wrong
/// thing once there is a sound behind it.
///
/// A sawtooth, because it is the shape that reads as a wave at a glance: a sine could be a
/// squiggle and a square could be a mistake.
/// </remarks>
public sealed class MachinePreviewScope : IMachineScope
{
    /// <inheritdoc/>
    /// <remarks>
    /// A sawtooth, whatever is asked for. Nothing on the bench is sounding, so the shape is a
    /// picture of what the control draws rather than of what any machine is making.
    /// </remarks>
    public void Trace(double[] into, double cycles, double seconds, bool running)
    {
        for (int at = 0; at < into.Length; at++)
        {
            double across = into.Length == 1 ? 0 : at / (into.Length - 1.0);
            double phase = across * cycles % 1.0;

            into[at] = phase * 2.0 - 1.0;
        }
    }

    /// <summary>
    /// Nowhere to subscribe, because nothing here is playing.
    /// </summary>
    event EventHandler? IMachineScope.Changed
    {
        add { }
        remove { }
    }
}

/// <summary>
/// A track for the editor's panel to count: one nothing is really playing.
/// </summary>
/// <remarks>
/// The same reason the kit is a real kit. A row of lamps laid out against no pattern is a row
/// of eight dark dots, and how much room the page buttons want cannot be judged from that: it
/// is the number of them and the width of what is written on them that decides it.
///
/// A pattern of the usual length, stopped part way through, so the chip shows a lit lamp and a
/// picked page as well as the dark ones.
/// </remarks>
public sealed class MachinePreviewLocation : IMachineLocation
{
    /// <summary>Four pages of eight lines, which is a pattern of the usual length.</summary>
    private static readonly string[] Runs = { "0-7", "8-15", "16-23", "24-31" };

    /// <summary>Not live, so the panel being designed shows the row as the rack shows it.</summary>
    public bool Live => false;

    /// <inheritdoc/>
    public int Lamps => 8;

    /// <inheritdoc/>
    /// <remarks>Part way along, so the row shows a lit lamp beside the dark ones.</remarks>
    public int Lit => 3;

    /// <inheritdoc/>
    public int FirstNumber => 8;

    /// <inheritdoc/>
    public IReadOnlyList<string> Pages => Runs;

    /// <inheritdoc/>
    /// <remarks>Not the first, so the row shows a page picked out as well as the rest.</remarks>
    public int Page => 1;

    /// <summary>Nothing. There is no playhead here to move.</summary>
    public void Show(int page) { }

    /// <summary>
    /// Nowhere to subscribe, because nothing here is playing.
    /// </summary>
    event EventHandler? IMachineLocation.Changed
    {
        add { }
        remove { }
    }
}
