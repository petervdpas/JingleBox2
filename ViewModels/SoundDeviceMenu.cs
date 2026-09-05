using System;
using System.Collections.Generic;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.Rack.SoundDevices.Faces.Records;
using JingleBox2.SoundDevices.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// What a device's Menu offers: its own page, and then whatever else the host has to say.
/// </summary>
/// <remarks>
/// The links half of a Menu is <see cref="Midi.ControlMenu"/> and is about hardware pointed at
/// this box, which has nothing to do with what the box is. So the page is added here rather than
/// there: one wrapper over whatever menu a device was already being given, in the one order that
/// makes sense, which is that what this thing is comes before which knob is driving it.
///
/// A device with no page keeps the line and loses the press, rather than losing the line. A line
/// that is not there says the host cannot show help at all; a line that is there and grey says
/// this device's author wrote none, which is the true answer and is also the nudge to go and
/// write one.
///
/// How the page is shown is handed in, defaulted to the window that shows it. Not because
/// anything else will ever want another way, but because a menu whose one interesting line can
/// only be tested by opening a window is a menu nobody tests.
/// </remarks>
public sealed class SoundDeviceMenu : IPanelMenu
{
    /// <summary>The menu this one is wrapped round, whose lines come after the page.</summary>
    private readonly IPanelMenu _inner;

    /// <summary>
    /// Which device this menu is about, asked each time rather than held.
    /// </summary>
    /// <remarks>
    /// A panel is shown a different box as somebody works, and the rack rereads its folders
    /// whenever what is registered changes, so anything holding one box would be holding the one
    /// that was open when the window was built.
    /// </remarks>
    private readonly Func<IRackProject?> _device;

    /// <summary>What opening the page does.</summary>
    private readonly Action<IRackProject> _open;

    /// <summary>Wraps a menu so the device's own page is the first thing on it.</summary>
    /// <param name="inner">What the host was already offering, drawn under the page.</param>
    /// <param name="device">Which device this is about, or nothing where none is open.</param>
    /// <param name="open">
    /// How the page is shown. Left out, a window of the device's own, which is what everything
    /// in the application wants; handed in by a test, which has no windows.
    /// </param>
    public SoundDeviceMenu(IPanelMenu inner, Func<IRackProject?> device, Action<IRackProject>? open = null)
    {
        _inner = inner;
        _device = device;
        _open = open ?? (box => Views.SoundDeviceHelpWindow.Show(box, Views.ActiveWindow.Now));
    }

    /// <inheritdoc/>
    public IReadOnlyList<PanelMenuItem> Read()
    {
        var box = _device();

        var lines = new List<PanelMenuItem> { Page(box) };

        lines.AddRange(_inner.Read());

        return lines;
    }

    /// <summary>The line that opens the device's page, live only where there is one.</summary>
    /// <param name="box">The device this menu is about, or nothing.</param>
    private PanelMenuItem Page(IRackProject? box) =>
        box is { Help.Length: > 0 }
            ? new PanelMenuItem("Help")
            {
                Tip = "What " + box.Name + "'s own author wrote about it.",
                Option = MenuOptionWords.Help,
                Chosen = () => _open(box)
            }
            : new PanelMenuItem("Help")
            {
                Tip = box == null
                    ? "Nothing is open to have a page."
                    : box.Name + " carries no help page. One is written in DESIGNER, on the Helptext tab.",
                Option = MenuOptionWords.Help,
                Live = false
            };
}
