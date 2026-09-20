using System;

namespace JingleBox2.Rack.SoundDevices.Faces.Interfaces;

/// <summary>
/// Where the two wheels beside a keyboard are being held, for a panel to draw them.
/// </summary>
/// <remarks>
/// Beside <see cref="IPanelKeys"/> and supplied the same way, because a drawn wheel is the same
/// kind of thing a drawn keyboard is: a picture of what a hand is doing to the device, rather
/// than a control on its face. Which is why there is nothing here to write. A wheel on the
/// screen that could be dragged would be a second wheel disagreeing with the one under the hand,
/// and it would jump as soon as the hardware moved again.
///
/// So it is read only in the sense that matters, and that makes a described panel's wheels
/// exactly as true as the keyboard's: they follow whatever is playing the device, wherever the
/// page it is on happens to be.
///
/// Whoever is showing the panel answers it. A device has no way of knowing what is plugged in,
/// and a wheel is a fact about the room rather than about the device.
/// </remarks>
public interface IPanelWheels
{
    /// <summary>
    /// Where the pitch wheel is, minus one for all the way down to one for all the way up.
    /// </summary>
    /// <remarks>Nought is the wheel at rest, which is a note at the pitch it was played at.</remarks>
    double Lean { get; }

    /// <summary>And how far up the modulation wheel is, nought to one.</summary>
    double Amount { get; }

    /// <summary>Told when either of them moves, so the picture can follow.</summary>
    event EventHandler? Moved;
}
