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
    /// Returns <paramref name="preferred"/> when it is still present, otherwise null.
    /// For device kinds where "nothing selected" is a valid state and picking an arbitrary
    /// replacement would be wrong, such as MIDI input.
    /// </summary>
    public static string? Preserve(IEnumerable<string> devices, string? preferred)
    {
        if (string.IsNullOrEmpty(preferred)) return null;

        var list = devices as IList<string> ?? devices.ToList();
        return list.Contains(preferred) ? preferred : null;
    }

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
