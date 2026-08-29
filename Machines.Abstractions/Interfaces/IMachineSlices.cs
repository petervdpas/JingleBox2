using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace JingleBox2.Machines.Interfaces;

/// <summary>
/// One recording and where it is cut, for the machines that fill themselves from a single take.
/// </summary>
/// <remarks>
/// Wider than the other things a host supplies a panel, and unavoidably: the others are a
/// question and an answer, and this is a control's whole surface. Chopping is one act with a
/// picture, a count, a way of choosing where to cut and a piece in hand, and none of it can be
/// left out without leaving the control unusable.
///
/// What it is not is a machine. Nothing here loads a recording, because the recording is
/// whichever one the machine already plays, and there is one place to put a sample on a machine.
/// Nothing here knows what a piece becomes either: a stretch of keyboard on one machine and one
/// key on another, settled by whoever supplied this.
///
/// Where the cuts are is read back off the pieces rather than stored, so the pieces are the
/// truth. Move a boundary and the two pieces either side of it are what changed.
/// </remarks>
public interface IMachineSlices : INotifyPropertyChanged
{
    /// <summary>The shape of the whole take, or nothing when there is none on the machine.</summary>
    float[]? Peaks { get; }

    /// <summary>Where it is cut, as fractions of the whole, including both ends.</summary>
    ObservableCollection<double> Points { get; }

    /// <summary>The piece in hand, or -1 for none. Written when one is clicked.</summary>
    int SelectedSlice { get; set; }

    /// <summary>The most pieces this machine can hold, which is how many places it has for one.</summary>
    int MaxSlices { get; }

    /// <summary>How far through the take the machine has got, or -1 while nothing is sounding.</summary>
    double Playhead { get; }

    /// <summary>Whether the piece in hand repeats, which is what draws the dashed handles.</summary>
    bool Looping { get; }

    /// <summary>Where that repeat starts, inside the piece. Written by dragging its handle.</summary>
    double LoopStart { get; set; }

    /// <summary>Where it ends, the other of the two handles.</summary>
    double LoopEnd { get; set; }

    /// <summary>Whether there is a recording to cut at all.</summary>
    bool IsOpen { get; }

    /// <summary>What the take is called, for the line above the picture.</summary>
    string TakeText { get; }

    /// <summary>How many pieces there are, said in words.</summary>
    string CountText { get; }

    /// <summary>How many to aim for the next time it is cut.</summary>
    double Pieces { get; set; }

    /// <summary>The ways of deciding where to cut, and which is chosen.</summary>
    IReadOnlyList<string> CutOptions { get; }

    /// <summary>Which of those is chosen, by its own name.</summary>
    string CutBy { get; set; }

    /// <summary>Whether the piece in hand loops, offered as words rather than a switch.</summary>
    IReadOnlyList<string> LoopNames { get; }

    /// <summary>Which of those is chosen, by its own name.</summary>
    string LoopName { get; set; }

    /// <summary>Cuts it, throwing away where it is cut now.</summary>
    void Chop();
}
