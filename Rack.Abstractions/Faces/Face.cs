using System;
using System.Collections.Generic;

namespace JingleBox2.Rack.Faces;

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
///
/// A class and not a record, which matters more than it looks. The panel redraws when it is
/// handed a different machine, and "different" has to mean a different handing over rather than
/// different contents: the ordinary case is the same machine with a new recording behind it,
/// where every field is equal and the picture is stale. A record compares by value, so handing
/// the same face over again would be no change at all and nothing would be redrawn.
/// </remarks>
public sealed class Face
{
    /// <summary>Puts the three halves of a machine together into the one thing a panel is handed.</summary>
    /// <param name="panel">What it looks like.</param>
    /// <param name="parameters">What its controls stand for.</param>
    /// <param name="folder">
    /// Where it is kept, empty for a machine that is not on disc and so can name no picture and
    /// no sound of its own.
    /// </param>
    public Face(Panel panel, IReadOnlyList<Parameter> parameters, string folder = "")
    {
        Panel = panel;
        Parameters = parameters;
        Folder = folder;
    }

    /// <summary>What it looks like.</summary>
    public Panel Panel { get; }

    /// <summary>What it can be set to, which is what the controls stand for.</summary>
    public IReadOnlyList<Parameter> Parameters { get; }

    /// <summary>
    /// Where it is kept, which is what a picture or a sound of its own is named relative to.
    /// </summary>
    /// <remarks>
    /// Empty for a machine that is not on disc, which is a machine that can have neither.
    /// </remarks>
    public string Folder { get; }

    /// <summary>The same machine, handed over again, for when what is behind it has changed.</summary>
    public Face Again() => new(Panel, Parameters, Folder);

    /// <summary>A machine with nothing on it, for a panel that has been handed none.</summary>
    public static readonly Face None = new(new Panel(), Array.Empty<Parameter>());
}
