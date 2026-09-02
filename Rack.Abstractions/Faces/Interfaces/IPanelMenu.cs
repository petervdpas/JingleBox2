using System.Collections.Generic;
using JingleBox2.Rack.Faces.Records;

namespace JingleBox2.Rack.Faces.Interfaces;

/// <summary>
/// What a machine's Menu part drops down.
/// </summary>
/// <remarks>
/// A machine can be pointed at by a knob, and what it is pointed at with is a fact about the room
/// the machine is being played in rather than about the machine. So the machine says where on its
/// face that belongs, by putting a Menu part there, and the host says what is in it: a panel
/// drawn from a description has no way of reaching a MIDI port, a settings file or a folder of
/// saved layouts, and should not have one.
///
/// The same arrangement <see cref="IPanelPresets"/> and <see cref="JingleBox2.Rack.Machines.Interfaces.IMachineZones"/> already
/// keep, and for the same reason. Which controllers there are and what has been kept for them are
/// none of them settings, cannot be written into a song, and change while the panel is on screen.
///
/// <see cref="Read"/> is asked each time the part is worked rather than being a list held here,
/// because what it answers moves under it: a layout saved a moment ago on another page would be
/// missing from anything read earlier.
/// </remarks>
public interface IPanelMenu
{
    /// <summary>What is on offer now, as lines to draw.</summary>
    /// <remarks>
    /// Nothing at all is a legitimate answer and is drawn as nothing, but it is rarely the right
    /// one: an empty menu and a broken part look the same from the outside.
    /// </remarks>
    IReadOnlyList<PanelMenuItem> Read();
}
