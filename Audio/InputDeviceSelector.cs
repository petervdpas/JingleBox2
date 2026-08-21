using System.Collections.Generic;
using System.Linq;

namespace JingleBox2.Audio;

/// <summary>
/// Picks which capture device to select after the device list is (re)built.
/// Devices are matched by name because indexes shift when hardware appears or disappears.
/// </summary>
public static class InputDeviceSelector
{
    public const string Fallback = "Default";

    /// <summary>
    /// Returns <paramref name="preferred"/> when it is still present, otherwise the first
    /// available device, otherwise <see cref="Fallback"/>.
    /// </summary>
    public static string Pick(IEnumerable<string> devices, string? preferred)
    {
        var list = devices as IList<string> ?? devices.ToList();

        if (!string.IsNullOrEmpty(preferred) && list.Contains(preferred))
            return preferred;

        return list.FirstOrDefault() ?? Fallback;
    }
}
