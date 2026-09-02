using System.Collections.Generic;

namespace JingleBox2.Rack.SoundDevices.Faces.Interfaces;

/// <summary>
/// The order a panel reads in.
/// </summary>
/// <remarks>
/// Depth first through the tree, which is the order somebody's eye goes over the face: a grid's
/// first row before its second, and everything in a group before whatever stands after it. So
/// "the third knob" means the third knob you would point at, without anybody numbering them.
///
/// It exists so that a controller nobody has written a file for is still useful the moment it
/// is plugged in: encoder three drives the third parameter on whatever machine is in front of
/// you, on every machine, including one written next year. See docs/hardware-integration.md.
///
/// A parameter named twice on one panel, which happens where a value is shown beside the knob
/// that turns it, counts once and keeps the place of the first.
/// </remarks>
public interface IPanelOrder
{
    /// <summary>The parameters a panel turns, in the order it reads.</summary>
    IReadOnlyList<string> Of(Panel? panel);

    /// <summary>The parameter at that place, or nothing when the panel has fewer than that.</summary>
    string At(Panel? panel, int ordinal);
}
