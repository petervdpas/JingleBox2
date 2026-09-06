using System.Collections.Generic;
using JingleBox2.UI.Enums;
using JingleBox2.UI.Records;

namespace JingleBox2.UI.Interfaces;

/// <summary>
/// What may be joined to what on a patchbay, and how the channels line up when it is.
/// </summary>
/// <remarks>
/// **One place, because three things ask it.** The picture asks so it can draw a cable per wire,
/// the hand asks so a drag that cannot land says so before it is let go, and whatever really
/// does the wiring asks so it makes the same pairs the picture drew. Written out three times
/// those would drift, and the way that fails is a cable on the screen that is not a link on the
/// machine.
/// </remarks>
public interface IPatchWiring
{
    /// <summary>
    /// Whether a cable may run from one port to the other.
    /// </summary>
    /// <remarks>
    /// Audio flows one way, so one end has to be an output and the other an input; the order the
    /// two are handed over does not matter, since a hand drags a cable in whichever direction it
    /// likes. A block cannot be joined to itself, which is feedback and is the one connection
    /// that can be made by accident.
    ///
    /// **A fixed point refuses everything**, which is how the picture can show how this
    /// application is wired inside itself without offering to take it apart: the pads reach the
    /// mixer because that is what a mixer is, not because somebody patched them.
    /// </remarks>
    /// <param name="from">One end of the cable.</param>
    /// <param name="to">The other end.</param>
    bool Allowed(PatchPort from, PatchPort to);

    /// <summary>
    /// Which channel of the output feeds which channel of the input.
    /// </summary>
    /// <remarks>
    /// Stereo to stereo is the pair it looks like. **A mono output feeds both sides of a stereo
    /// input**, rather than one side, because a source that only lands on the left is a recording
    /// somebody has to fix afterwards and is never what anybody meant: this is the case a Bluetooth
    /// headset in its telephone profile produces, and it is the one that used to be refused in
    /// silence. A stereo output into a mono input arrives on the one channel, both sides summed,
    /// which is what a port does with two things arriving at it anyway.
    /// </remarks>
    /// <param name="from">What the output carries.</param>
    /// <param name="to">What the input takes.</param>
    /// <returns>Pairs of channel numbers, counting from nought.</returns>
    IReadOnlyList<(int From, int To)> Pairs(PatchChannels from, PatchChannels to);
}
