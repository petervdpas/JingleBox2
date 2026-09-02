using System;

namespace JingleBox2.Rack.SoundDevices.Faces.Records;

/// <summary>
/// One line of what a machine's Menu part drops down.
/// </summary>
/// <remarks>
/// Deliberately not a menu item. What is drawn is the host's business, and a panel drawn from a
/// description has no business naming a toolkit's types: a machine says it wants this part and
/// the host decides what a menu looks like on the screen it is drawing. It also means the whole
/// of what a machine offers can be put a question to without a window.
///
/// Flat, with nothing hanging under it. What the part offers is a list of the control surfaces
/// there is a layout for, and one line to start learning, and none of that is a tree: a menu
/// that has to be walked into is a menu somebody has to work out before they can use it.
/// </remarks>
/// <param name="Said">What the line says.</param>
public sealed record PanelMenuItem(string Said)
{
    /// <summary>The longer version, for resting on it. Nothing where the line explains itself.</summary>
    public string Tip { get; init; } = "";

    /// <summary>False for a line worth showing and not worth pressing.</summary>
    public bool Live { get; init; } = true;

    /// <summary>What pressing it does.</summary>
    public Action? Chosen { get; init; }

    /// <summary>
    /// Which of the Menu's options this line belongs to. See <see cref="MenuOptionWords"/>.
    /// </summary>
    /// <remarks>
    /// The host offers everything it has and the machine's own file says which options its Menu
    /// carries, so this is what lets the two meet without either knowing what the other holds.
    /// A line belonging to no option is carried whatever the machine asked for, which is what a
    /// line that is not part of an option is: something the Menu always says.
    /// </remarks>
    public string Option { get; init; } = "";
}
