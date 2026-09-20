using System;

namespace JingleBox2.Rack.SoundDevices.Faces.Interfaces;

/// <summary>
/// Where the two wheels beside a keyboard are being held, for a panel to draw them.
/// </summary>
/// <remarks>
/// Beside <see cref="IPanelKeys"/> and supplied the same way, because a drawn wheel is the same
/// kind of thing a drawn keyboard is: where a hand has left something on the device.
///
/// **This half is the reading and nothing else**, which is not the same as the wheel being read
/// only: a hand on a drawn wheel says so through the command the control carries, and the host
/// takes that to the same place the hardware's goes. So what is read here is wherever the last
/// hand to touch either of them left it, and a described panel's wheels are exactly as true as
/// its keyboard's: they follow whatever is playing the device, wherever the page it is on
/// happens to be.
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
