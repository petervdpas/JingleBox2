using System.Collections.Generic;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.Rack.SoundDevices.Faces.Records;
using JingleBox2.ViewModels.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// The Menu's line for what the modulation wheel turns, and the device's controls under it.
/// </summary>
/// <remarks>
/// **One line that opens onto the list rather than the list itself**, which is the whole reason
/// it is here and not on the face. A picker in front of the wheels was drawn and taken out
/// again: it is a box as wide as the longest control name, sitting on the one row of a face that
/// is already the keyboard, and it drops a list as tall as the device has controls over the
/// panel behind it. Twenty five of those lines on the top of the Menu would be the same fault
/// said again, with whatever somebody opened the Menu for three screens down.
///
/// The row in force is marked rather than written differently, since the question a set of
/// choices answers is which one, and a mark is how a menu has always said it.
///
/// Read when the Menu is opened, like every other line here, so a machine edited since offers
/// what it has now and there is nothing to keep in step.
/// </remarks>
/// <param name="wheel">What the instrument's wheel may turn and what it does turn.</param>
/// <param name="said">What the line is called, or nothing for the plain words.</param>
public sealed class WheelMenu(IInstrumentWheel wheel, string said = WheelMenu.Words) : IPanelMenu
{
    /// <summary>What the line says where nobody has named it otherwise.</summary>
    /// <remarks>
    /// Two words, since the lines under it are the answer and a line that asked the whole
    /// question was half as wide again as everything else on the Menu. It is what the wheel it
    /// is about is labelled on the face, which is where somebody has just been looking.
    /// </remarks>
    public const string Words = "Mod wheel";

    /// <inheritdoc/>
    /// <remarks>
    /// A device with nothing a wheel could turn keeps the line and loses the press, which is the
    /// rule the help line already keeps: a line that is not there says the host cannot do it,
    /// and a grey one says this device has nothing to offer. That is a plugin, which owns its
    /// own wheel and is not pointed at from here.
    /// </remarks>
    public IReadOnlyList<PanelMenuItem> Read()
    {
        var names = wheel.Names;

        if (names.Count <= 1)
            return [new PanelMenuItem(said)
            {
                Tip = "This device has no controls a wheel could turn.",
                Option = MenuOptionWords.Wheel,
                Live = false
            }];

        int picked = wheel.Picked;
        var under = new List<PanelMenuItem>();

        for (int at = 0; at < names.Count; at++)
        {
            int row = at;

            under.Add(new PanelMenuItem(names[at])
            {
                Ticked = row == picked,
                Option = MenuOptionWords.Wheel,
                Chosen = () => wheel.Picked = row
            });
        }

        return [new PanelMenuItem(said)
        {
            Tip = "Which control the modulation wheel owns on this instrument.",
            Option = MenuOptionWords.Wheel,
            Lines = under
        }];
    }
}
