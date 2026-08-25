using System;
using System.Collections.Generic;

namespace JingleBox2.Machines;

/// <summary>
/// A machine, as the thing that draws it needs it: one body of work rather than three parcels.
/// </summary>
/// <remarks>
/// The panel, what its controls are worth, and where the machine lives are one machine and have
/// to travel as one. Handing them over separately means every place that shows a machine has to
/// remember all three, and the one that forgets does not fail: it draws a panel with a frame
/// where the logo should be, because a picture is named inside the machine's folder and nobody
/// said which folder. That happened, which is why this exists.
///
/// Not the settings. Which recording is on it and where its knobs are standing belong to an
/// instrument in a song, and two instruments made from one machine have different ones. The
/// machine is what they have in common.
/// </remarks>
/// <param name="Panel">What it looks like.</param>
/// <param name="Parameters">What it can be set to, which is what the controls stand for.</param>
/// <param name="Folder">
/// Where it is kept, which is what a picture or a sound of its own is named relative to. Empty
/// for a machine that is not on disc, which is a machine that can have neither.
/// </param>
public sealed record MachineFace(
    MachinePanel Panel,
    IReadOnlyList<MachineParameter> Parameters,
    string Folder = "")
{
    /// <summary>A machine with nothing on it, for a panel that has been handed none.</summary>
    public static readonly MachineFace None = new(new MachinePanel(), Array.Empty<MachineParameter>());
}
