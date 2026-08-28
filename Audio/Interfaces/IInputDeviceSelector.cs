using System.Collections.Generic;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Which capture device to select after the device list is built again.
/// </summary>
/// <remarks>
/// Devices are matched by name and never by number, because the numbers shift the moment
/// hardware appears or disappears: an interface switched on after the application means every
/// device after it in the list moves up one, and a selection kept by number would silently
/// become the device next to the one somebody chose.
/// </remarks>
public interface IInputDeviceSelector
{
    /// <summary>What is picked when there is nothing to pick, which the system then resolves.</summary>
    string Fallback { get; }

    /// <summary>
    /// The device to select: the preferred one when it is still there, otherwise the first
    /// available, otherwise <see cref="Fallback"/>.
    /// </summary>
    /// <param name="devices">What the system says is plugged in now.</param>
    /// <param name="preferred">What was chosen last time, or null when nothing was.</param>
    string Pick(IEnumerable<string> devices, string? preferred);
}
