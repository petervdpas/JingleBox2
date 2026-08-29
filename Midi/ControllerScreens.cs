using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Controllers.Interfaces;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <summary>
/// Every screen on the desk, as one screen.
/// </summary>
/// <remarks>
/// <inheritdoc cref="IControllerScreen"/>
///
/// The application says "JingleBox2 and the song's name" once and this decides who hears it, so
/// nothing above here knows that Arturia's screens and a Mackie display are two different things,
/// and nothing below here has to be asked whether it is the right one.
///
/// A device with two screens is not a thing anybody makes, so the first protocol that claims a
/// device gets it and the rest are not asked. A device no protocol claims is written to by
/// nobody, which is the whole point: it used to be written to by everybody.
/// </remarks>
public sealed class ControllerScreens : IControllerScreen
{
    private readonly IReadOnlyList<IControllerScreen> _screens;
    private readonly Func<IEnumerable<string>>? _devices;
    private readonly IControllerProfiles? _profiles;

    /// <param name="devices">
    /// The ports worth greeting, asked each time rather than held, since what is plugged in
    /// changes. Every one of them is offered to every screen, and a port no screen claims is
    /// left alone.
    /// </param>
    /// <param name="profiles">
    /// Which ports belong to the same controller, for <see cref="Where"/>. Without one, a reading
    /// only ever reaches a screen on the very port the control was turned on.
    /// </param>
    /// <param name="screens">The protocols this build knows how to write.</param>
    public ControllerScreens(
        Func<IEnumerable<string>>? devices,
        IControllerProfiles? profiles,
        params IControllerScreen[] screens)
    {
        _devices = devices;
        _profiles = profiles;
        _screens = screens ?? Array.Empty<IControllerScreen>();
    }

    /// <summary>Which of them speaks for that device, or none.</summary>
    private IControllerScreen? For(string? device) =>
        device is null ? null : _screens.FirstOrDefault(one => one.Writes(device));

    /// <inheritdoc/>
    public bool Writes(string? device) => Where(device) is not null;

    /// <summary>
    /// Which port to write to, for something that happened on this one.
    /// </summary>
    /// <remarks>
    /// **A screen belongs to a controller, not to a port.** A KeyLab mkII is two ports and its
    /// knobs arrive on one of them while its screen is on the other, so a reading written back to
    /// the port it came from reaches nothing at all. That is not a detail: turning a knob and
    /// being told what it is doing is most of what a screen is for, and without this the KeyLab
    /// has a screen that can only ever say hello.
    ///
    /// The port it came from is tried first, since for a MiniLab 3 that is the answer and costs
    /// one comparison. Otherwise the ports of the same controller are looked through for one that
    /// has a screen.
    ///
    /// Two of the same controller on one desk is the case that decides how: the ports are asked
    /// how much of their name they share with the one the control was turned on, and the closest
    /// wins. `KeyLab mkII 49 MIDI` and `KeyLab mkII 49 DAW` share everything up to the last word,
    /// and a second unit's ports carry a number that breaks the match earlier. It is a heuristic
    /// and it is the only thing available: an operating system does not say which ports are one
    /// controller, and the identity a device answers with is the same for both of them.
    /// </remarks>
    /// <param name="device">The port something happened on.</param>
    private string? Where(string? device)
    {
        if (string.IsNullOrWhiteSpace(device)) return null;
        if (For(device) is not null) return device;
        if (_profiles is null) return null;

        string mine = _profiles.Called(device);

        if (mine.Length == 0 || string.Equals(mine, device, StringComparison.OrdinalIgnoreCase))
            return null;

        string? best = null;
        int shared = -1;

        foreach (string other in Devices())
        {
            if (string.Equals(other, device, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(_profiles.Called(other), mine, StringComparison.Ordinal)) continue;
            if (For(other) is null) continue;

            int same = Alike(device!, other);

            if (same <= shared) continue;

            shared = same;
            best = other;
        }

        return best;
    }

    /// <summary>How many characters two port names begin with in common.</summary>
    private static int Alike(string one, string other)
    {
        int n = 0;

        while (n < one.Length && n < other.Length
               && char.ToUpperInvariant(one[n]) == char.ToUpperInvariant(other[n]))
            n++;

        return n;
    }

    /// <summary>The top line as it stands, so a device plugged in later is greeted the same.</summary>
    private string _first = "";

    /// <summary>The line under it.</summary>
    private string _second = "";

    /// <inheritdoc/>
    /// <remarks>
    /// Told to each protocol, so each remembers it, and then put on every device that has a
    /// screen. Both halves are needed: the remembering is for a device that arrives later, and
    /// the writing is for the ones already here, which is every device at the moment this is
    /// first called.
    /// </remarks>
    public void Standing(string first, string second)
    {
        _first = first ?? "";
        _second = second ?? "";

        foreach (var screen in _screens) screen.Standing(_first, _second);

        foreach (string device in Devices()) Say(device, _first, _second);
    }

    /// <inheritdoc/>
    public void Say(string device, string first, string second)
    {
        if (Where(device) is not { } port) return;

        For(port)?.Say(port, first, second);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The reading goes to the controller's screen, which need not be on the port the control was
    /// turned on. See <see cref="Where"/>.
    /// </remarks>
    public void Moved(string device, ScreenKind kind, double fraction, string what, string reads, bool hide = true)
    {
        if (Where(device) is not { } port) return;

        For(port)?.Moved(port, kind, fraction, what, reads, hide);
    }

    /// <inheritdoc/>
    public void Gone(string device)
    {
        foreach (var screen in _screens) screen.Gone(device);
    }

    /// <inheritdoc/>
    public void Again()
    {
        foreach (string device in Devices())
            foreach (var screen in _screens)
                screen.Gone(device);

        Standing(_first, _second);
    }

    /// <summary>The ports to try, and nothing thrown if asking fails.</summary>
    private IEnumerable<string> Devices()
    {
        try
        {
            return _devices?.Invoke() ?? Array.Empty<string>();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }
}
