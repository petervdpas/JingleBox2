using System.Collections.Generic;

namespace JingleBox2.Machines;

/// <summary>
/// How a machine's face is put together.
/// </summary>
/// <remarks>
/// A machine is its parameters, but a list of parameters is not a panel. Which control turns
/// which parameter, what stands beside what, and what is grouped under one heading is a thing
/// the person who built the machine knows and the host does not, so the machine says it here
/// and the host draws what it is told.
///
/// One tree of elements rather than a fixed shape of sections and controls: a grid is an
/// element, a group is an element, and a knob is an element, so a group inside a grid inside a
/// group needs no new idea and adding a control to the library is adding one name. Everything
/// is described rather than drawn: no pixels, no colours. A panel pinned to coordinates would
/// be wrong at the first different window width, and a panel that had to be compiled would mean
/// shipping a binary to move a knob.
/// </remarks>
public sealed class MachinePanel
{
    /// <summary>What the panel is, from the outside in. Usually a grid holding groups.</summary>
    public MachineElement Root { get; set; } = new() { Element = MachineElementKinds.Grid };
}

/// <summary>
/// One thing on a panel: a container, a control, or a label.
/// </summary>
/// <remarks>
/// Deliberately one type for all of them. A designer that drags things onto other things needs
/// every thing to be the same kind of thing, and a format with a class per control would have
/// to be extended, and every reader of it updated, before a machine could use a new one.
///
/// What an element is called is a string rather than an enum for the same reason: a host that
/// meets an element it does not know draws nothing and carries on, which is what lets a machine
/// built against a later library still open in an earlier host.
/// </remarks>
public sealed class MachineElement
{
    /// <summary>Which kind of thing this is. See <see cref="MachineElementKinds"/>.</summary>
    public string Element { get; set; } = "";

    /// <summary>
    /// The parameter this control turns, by key, or empty for something that turns nothing.
    /// </summary>
    /// <remarks>
    /// A container has none. A control that names a parameter the machine does not have is
    /// skipped rather than drawn dead, since a knob wired to nothing is worse than no knob.
    /// </remarks>
    public string Parameter { get; set; } = "";

    /// <summary>What is written on it. Empty means use the parameter's own name.</summary>
    public string Label { get; set; } = "";

    /// <summary>
    /// Everything else about it, as plain text.
    /// </summary>
    /// <remarks>
    /// Where it sits, how big it is, how many columns a grid has: things that differ per kind
    /// of element and would otherwise mean a property on this class for every control ever
    /// written. A reader takes the keys it understands and leaves the rest alone, so a machine
    /// saved by a later designer still opens.
    /// </remarks>
    public Dictionary<string, string> Properties { get; set; } = new();

    /// <summary>What is inside it, for the elements that hold other elements.</summary>
    public List<MachineElement> Children { get; set; } = new();
}

/// <summary>
/// The elements a panel can be built out of.
/// </summary>
/// <remarks>
/// Written out as constants rather than an enum so a machine can name one this host has never
/// heard of without the file failing to read. These are the ones the library draws today.
/// </remarks>
public static class MachineElementKinds
{
    /// <summary>Rows and columns. Properties: columns, rows, and per child column, row, span.</summary>
    public const string Grid = "Grid";

    /// <summary>A framed group with a heading. Property: caption.</summary>
    public const string Group = "Group";

    /// <summary>Things laid side by side, wrapping when they run out of room.</summary>
    public const string Row = "Row";

    /// <summary>Things laid one under the other.</summary>
    public const string Column = "Column";

    /// <summary>
    /// A run of panel cells things stand on, so that rows line up down the panel.
    /// </summary>
    /// <remarks>
    /// Properties: orientation, cell, gap, columns, and per child span. Not a row with a gap
    /// set on it: everything on a strip is placed against the same grid of cells, which is
    /// what makes two strips agree with each other when nothing in them is the same size.
    /// </remarks>
    public const string Strip = "Strip";

    public const string Knob = "Knob";
    public const string Fader = "Fader";
    public const string Switch = "Switch";
    public const string Number = "Number";

    /// <summary>
    /// A button that is down while it is held. Properties: cap, lamp.
    /// </summary>
    /// <remarks>
    /// Held, the parameter reads its top; let go, its bottom. A machine that wants a thing to
    /// happen watches for the top rather than being handed an event, so a button is still just
    /// a number and a song saved while one was held is a song with a number in it.
    /// </remarks>
    public const string Button = "Button";

    /// <summary>
    /// A lamp. Read only: lit when the parameter is past the middle of its range. Properties:
    /// size, colour.
    /// </summary>
    public const string Led = "Led";

    /// <summary>
    /// How loud something is, from the parameter's place in its range. Read only. Properties:
    /// orientation, stereo, width, height.
    /// </summary>
    public const string Meter = "Meter";

    /// <summary>
    /// The keyboard, and the octave it is showing. Properties: keys, caption.
    /// </summary>
    /// <remarks>
    /// The parameter is the octave on show and nothing else. Which keys are sounding is not a
    /// setting and cannot be one, so it is not described here.
    /// </remarks>
    public const string Keys = "Keys";

    /// <summary>
    /// The recording's shape. Turns nothing. Properties: width, height, placeholder.
    /// </summary>
    public const string Wave = "Wave";

    /// <summary>
    /// One of a list, by number. Property: options, a comma separated list.
    /// </summary>
    /// <remarks>
    /// The parameter holds which one is chosen, counting from zero, so a choice is a number
    /// like everything else and the words are only what the panel says out loud.
    /// </remarks>
    public const string Choice = "Choice";

    /// <summary>A line of text, for a panel that wants to say something.</summary>
    public const string Label = "Label";

    /// <summary>Room left deliberately empty.</summary>
    public const string Spacer = "Spacer";
}
