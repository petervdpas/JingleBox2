using System;

namespace JingleBox2.Rack.SoundDevices.Faces.Records;

/// <summary>
/// One line of what a sound device's Menu part drops down.
/// </summary>
/// <remarks>
/// Deliberately not a menu item. What is drawn is the host's business, and a panel drawn from a
/// description has no business naming a toolkit's types: a sound device says it wants this part and
/// the host decides what a menu looks like on the screen it is drawing. It also means the whole
/// of what a sound device offers can be put a question to without a window.
///
/// Flat, with nothing hanging under it. What the part offers is a list of the control surfaces
/// there is a layout for, and one line to start learning, and none of that is a tree: a menu
/// that has to be walked into is a menu somebody has to work out before they can use it.
/// </remarks>
/// <param name="Said">What the line says.</param>
public sealed record PanelMenuItem(string Said)
{
    /// <summary>
    /// The longer version, for resting on it. Nothing where the line explains itself.
    /// </summary>
    public string Tip { get; init; } = "";

    /// <summary>False for a line worth showing and not worth pressing.</summary>
    public bool Live { get; init; } = true;

    /// <summary>What pressing it does.</summary>
    public Action? Chosen { get; init; }

    /// <summary>
    /// Which of the Menu's options this line belongs to. See <see cref="MenuOptionWords"/>.
    /// </summary>
    /// <remarks>
    /// The host offers everything it has and the sound device's own file says which options its
    /// Menu carries, so this is what lets the two meet without either knowing what the other holds.
    /// A line belonging to no option is carried whatever the sound device asked for, which is what
    /// a line that is not part of an option is: something the Menu always says.
    /// </remarks>
    public string Option { get; init; } = "";

    /// <summary>
    /// Which part of the Menu the line is drawn in, where a divider parts one part from the next.
    /// </summary>
    /// <remarks>
    /// A Menu does several different jobs, the device's own page, its presets, and the hardware
    /// pointed at it, and a divider between them is how every menu says so without a heading.
    /// Nothing said means the line's own option, which is the ordinary case; a line that belongs
    /// with another option's lines says that option, the way learning a control belongs with the
    /// control surfaces it is learned on.
    /// </remarks>
    public string Section
    {
        get => _section ?? Option;
        init => _section = value;
    }

    /// <summary>What was said about the part, or nothing for the line's own option.</summary>
    private readonly string? _section;
}
