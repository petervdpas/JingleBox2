using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <param name="devices">
    /// The ports worth greeting, asked each time rather than held, since what is plugged in
    /// changes. Every one of them is offered to every screen, and a port no screen claims is
    /// left alone.
    /// </param>
    /// <param name="screens">The protocols this build knows how to write.</param>
    public ControllerScreens(Func<IEnumerable<string>>? devices, params IControllerScreen[] screens)
    {
        _devices = devices;
        _screens = screens ?? Array.Empty<IControllerScreen>();
    }

    /// <summary>Which of them speaks for that device, or none.</summary>
    private IControllerScreen? For(string? device) =>
        device is null ? null : _screens.FirstOrDefault(one => one.Writes(device));

    /// <inheritdoc/>
    public bool Writes(string? device) => For(device) is not null;

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
    public void Say(string device, string first, string second) => For(device)?.Say(device, first, second);

    /// <inheritdoc/>
    public void Moved(string device, ScreenKind kind, double fraction, string what, string reads, bool hide = true) =>
        For(device)?.Moved(device, kind, fraction, what, reads, hide);

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
