using System;

namespace JingleBox2.Rack.Machines.Interfaces;

/// <summary>
/// The map behind a panel's zones: what each one covers, what is on it, and which is in hand.
/// </summary>
/// <remarks>
/// Not the pads by another name. A pad answers to one key and the machine says which, so the
/// keys are declared on the panel and never move. A zone answers to a stretch of keyboard and
/// there is no saying in advance how many stretches an instrument is: a piano sampled every
/// fourth key is thirteen of them and the same piano sampled once is one. So the map is the
/// host's, and the panel draws whatever it is handed.
///
/// Which is also why the ranges are here and not among the settings. A range is edited by
/// dragging it on the picture, the way it is on every sampler ever made, and a drag is three
/// numbers moving together: dragging a zone bodily up the keyboard carries its root along with
/// it, and a low edge dragged past its own high edge is a zone that answers to nothing.
/// <see cref="Move"/> takes all three so the map is never seen half way through one.
///
/// Adding a zone, taking one away and spreading them out are not here. They are things asked of
/// the host, the way loading samples onto a kit is, and they arrive through
/// <see cref="JingleBox2.Rack.Faces.PanelActions"/> like every other thing a button asks for.
/// </remarks>
public interface IMachineZones
{
    /// <summary>How many zones the map holds.</summary>
    int Count { get; }

    /// <summary>What that zone is called: its name, or the recording's, or the keys it covers.</summary>
    string Cap(int at);

    /// <summary>The lowest key it answers to.</summary>
    int Low(int at);

    /// <summary>The highest key it answers to.</summary>
    int High(int at);

    /// <summary>The key at which its recording plays untouched.</summary>
    int Root(int at);

    /// <summary>Whether anything is on it, so an empty zone can be drawn as an empty zone.</summary>
    bool Filled(int at);

    /// <summary>Which one the settings beside the map are about. Written when one is pressed.</summary>
    int Picked { get; set; }

    /// <summary>Puts that zone where a drag has left it: the two edges and the root at once.</summary>
    void Move(int at, int low, int high, int root);

    /// <summary>Told when any of the above has moved.</summary>
    event EventHandler? Changed;
}
