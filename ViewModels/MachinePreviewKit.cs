using JingleBox2.Machines;
using JingleBox2.Tracker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

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
    private int _picked;

    public int Count => DrumKit.PadCount;

    public string Cap(int at) => "";

    public string Note(int at) => new Note(DrumKit.FirstSemitone + at).ToString();

    /// <summary>Nothing is sounding: the editor is not playing anything.</summary>
    public bool Lit(int at) => false;

    /// <summary>And nothing is on them, which is the state a machine ships in.</summary>
    public bool Filled(int at) => false;

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
    public float[]? Peaks { get; } =
        Enumerable.Range(0, 600)
            .Select(at => (float)(Math.Abs(Math.Sin(at / 24.0)) * Math.Exp(-(at % 150) / 60.0)))
            .ToArray();

    public ObservableCollection<double> Points { get; } = new(new[] { 0.0, 0.25, 0.5, 0.75, 1.0 });

    public int SelectedSlice { get; set; } = 1;

    public int MaxSlices => DrumKit.PadCount;

    public double Playhead => -1;

    public bool Looping => false;

    public double LoopStart { get; set; }

    public double LoopEnd { get; set; } = 1;

    public bool IsOpen => true;

    public string TakeText => "a recording";

    public string CountText => "4 pieces";

    public double Pieces { get; set; } = SliceEditorViewModel.DefaultPieces;

    public IReadOnlyList<string> CutOptions { get; } = new[] { "Hits", "Gaps", "Even" };

    public string CutBy { get; set; } = "Hits";

    public IReadOnlyList<string> LoopNames { get; } = new[] { "Off", "Forward", "Ping-pong" };

    public string LoopName { get; set; } = "Off";

    /// <summary>Nothing. There is no recording here to cut, only a picture of one.</summary>
    public void Chop() { }

    /// <summary>Never raised: none of this moves.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
}
