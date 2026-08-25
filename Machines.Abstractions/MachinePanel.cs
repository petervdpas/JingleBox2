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
///
/// Three properties belong to every kind rather than to any of them: <c>width</c> and
/// <c>height</c> in pixels, and <c>margin</c>, which is one number for all four sides or a list
/// of them the way a Thickness is written. A container leaves a gap between the things it holds
/// on its own, so a margin is for the one element that wants to stand apart from the rest.
/// </remarks>
public static class MachineElementKinds
{
    /// <summary>Rows and columns. Properties: columns, rows, and per child column, row, span.</summary>
    /// <remarks>
    /// Properties: rows and columns, each a comma separated list of sizes, and gap. What it
    /// holds says row, column and span.
    /// </remarks>
    public const string Grid = "Grid";

    /// <summary>A framed group with a heading. Properties: caption, gap, equal, width, height.</summary>
    /// <remarks>
    /// Nothing inside it draws outside it. A group given a size smaller than what it holds is a
    /// group somebody meant to be that size, and a heading with a knob hanging out from under it
    /// reads as a mistake in the panel rather than a mistake in the description.
    /// </remarks>
    /// <remarks>
    /// Properties: caption and inset, the air between the frame and what is in it. Where a
    /// section sits in a row, and whether it shares the row's height, is the row's business.
    /// Room left over inside a section is shared above and below its contents, which is what a
    /// rack looks like and is not worth a knob of its own.
    /// </remarks>
    public const string Group = "Group";

    /// <summary>
    /// Things laid side by side, wrapping when they run out of room. Properties: gap, equal.
    /// </summary>
    /// <remarks>
    /// Properties: gap, and heights, which is <c>match</c> (the default) or <c>own</c>. Matched,
    /// every section in the row is as tall as the tallest of them; their own, each is as tall as
    /// what is in it. Either way the height comes from the contents.
    /// </remarks>
    public const string Row = "Row";

    /// <summary>Things laid one under the other. Properties: gap, equal.</summary>
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
    /// <remarks>
    /// Properties: track, which is the length of the throw, and ticks, a comma separated list of
    /// values to mark on the scale. The throw is not the height: a fader also draws its name
    /// above and its value below, so a machine asking for a throw of 96 wants a control taller
    /// than that.
    /// </remarks>
    public const string Fader = "Fader";

    /// <summary>
    /// One of two positions. Properties: on and off, the wording at each end.
    /// </summary>
    /// <remarks>
    /// The wording is the panel's, not the parameter's: what the two ends of a switch are called
    /// is a matter of what it does on this machine, and a switch nobody has worded says on and
    /// off, which is true of every switch ever made and useful on none of them.
    /// </remarks>
    public const string Switch = "Switch";

    public const string Number = "Number";

    /// <summary>
    /// A button that is down while it is held. Properties: cap, lamp, action.
    /// </summary>
    /// <remarks>
    /// Held, the parameter reads its top; let go, its bottom. A machine that wants a thing to
    /// happen watches for the top rather than being handed an event, so a button is still just
    /// a number and a song saved while one was held is a song with a number in it.
    ///
    /// Unless it names an <c>action</c>, which is the other kind of button: one that asks the
    /// host to do something rather than setting anything. Taking the recording off a pad and
    /// loading a folder of samples onto a kit are neither of them settings, and there is no
    /// number that could stand for either. See <see cref="MachineActions"/>.
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
    /// The parameter is the octave on show and nothing else. Which keys are sounding, which have
    /// something on them and which one is in hand are not settings and cannot be: they come from
    /// whoever is showing the panel, through <see cref="IMachineKeys"/>, the same way the pads
    /// do. On a kit they are the pads: the grid and the keyboard are two pictures of one thing,
    /// which is why picking a pad outlines its key.
    /// </remarks>
    public const string Keys = "Keys";

    /// <summary>
    /// The recording's shape, with what plays of it marked on the picture.
    /// </summary>
    /// <remarks>
    /// The parameter is the text setting holding the take, so the picture is of whatever the
    /// machine is set to play rather than of nothing. Properties: width, height, placeholder,
    /// showMarkers, showLoop, and start, end, loopStart and loopEnd, each naming a parameter
    /// holding a fraction of the file. Those four are the only part of this that is a value, and
    /// they go both ways: dragging a handle on the picture writes the parameter it names.
    /// </remarks>
    public const string Wave = "Wave";

    /// <summary>
    /// The envelope as a curve, with a playhead that runs along it while a note sounds.
    /// </summary>
    /// <remarks>
    /// Properties: attack, decay, sustain and release, each naming the parameter that holds it,
    /// and width and height. It turns nothing itself: the four faders beside it do that, and
    /// this is the picture of what they add up to, which is a thing four faders cannot show.
    ///
    /// The four are named rather than assumed, because a machine is free to call its envelope
    /// whatever it likes and some machines have two.
    /// </remarks>
    public const string Envelope = "Envelope";

    /// <summary>
    /// A picture the machine carries: a logo, a badge, a strip of trim.
    /// </summary>
    /// <remarks>
    /// Properties: file, width, height, and fit. It turns no parameter and never will. A badge
    /// is not a setting, nothing about it is worth writing into a song, and a machine that wants
    /// its picture to change with what it is doing wants a control and not this.
    ///
    /// <c>file</c> names the picture inside the machine's own folder, and names nothing else. A
    /// machine travels as that folder, so a picture in it arrives with it, and a name is the
    /// only thing about a file that survives being copied, zipped and opened on somebody else's
    /// disc. A host reading a name that climbs out of the folder draws nothing: the description
    /// came from whoever built the machine, and where it may read from did not.
    ///
    /// It may be a drawing rather than a photograph: a file ending in .svg is drawn at whatever
    /// size the panel gives it, so a logo stays sharp where a picture made of pixels is stretched
    /// up from the size it was saved at. The name is what says which it is, and a host that
    /// cannot read one draws the empty frame it draws for any file it cannot open.
    ///
    /// <c>fit</c> is how the picture takes the room it is given. "uniform", the default, keeps
    /// its shape; "fill" stretches it to the corners; "none" draws it at the size it was made.
    /// </remarks>
    public const string Image = "Image";

    /// <summary>
    /// Which recording the machine plays. Property: caption.
    /// </summary>
    /// <remarks>
    /// The parameter is the text setting the take is kept in, and pressing this asks whoever is
    /// showing the panel for a new one. It has to be asked: the shelf the takes are kept on is
    /// the host's and a panel drawn from a description has no way of reaching it, which is the
    /// same reason the picture is handed its shape rather than fetching one.
    /// </remarks>
    public const string Take = "Take";

    /// <summary>
    /// Where the machine is started from. Properties: caption, width.
    /// </summary>
    /// <remarks>
    /// The picker at the top of a panel: one of the machine's own presets, or on the Recording
    /// machine one of your takes. It turns no parameter, since what a preset does is write the
    /// lot of them at once, and it is not a Choice either: a Choice is a number the machine
    /// keeps and this is not kept anywhere. The list comes from whoever is showing the panel,
    /// through <see cref="IMachinePresets"/>, which also says what to call it.
    /// </remarks>
    public const string Preset = "Preset";

    /// <summary>
    /// One of a list, by number. Property: options, a comma separated list.
    /// </summary>
    /// <remarks>
    /// The parameter holds which one is chosen, counting from zero, so a choice is a number
    /// like everything else and the words are only what the panel says out loud.
    /// </remarks>
    public const string Choice = "Choice";

    /// <summary>
    /// A line of text, for a panel that wants to say something.
    /// </summary>
    /// <remarks>
    /// Its own wording, unless it names a text setting, in which case it says what that setting
    /// says. A machine that has just been pointed at a recording wants to write the name of it
    /// somewhere, and a label is the thing on a panel whose whole job is words.
    /// </remarks>
    public const string Label = "Label";

    /// <summary>
    /// A line the panel can be typed into. The parameter is a text setting.
    /// </summary>
    /// <remarks>
    /// The other half of <see cref="Label"/>, which says what a text setting says and cannot
    /// change it. What a pad on a kit is called is the player's word and not the machine's, so
    /// somewhere on the panel it has to be typed.
    /// </remarks>
    public const string Text = "Text";

    /// <summary>
    /// Which pad the controls beside the grid are about, as a list to pick from.
    /// </summary>
    /// <remarks>
    /// The grid says it too, and both are wanted: the grid is how a hand picks a pad, and this
    /// is how you read off which one is in hand without counting squares. It shows what the pads
    /// show, so a pad renamed is renamed here.
    /// </remarks>
    public const string PadPicker = "PadPicker";

    /// <summary>
    /// The pads of a kit: a grid of buttons. Properties: columns, cap, capHeight, gap, colour.
    /// </summary>
    /// <remarks>
    /// It holds one <see cref="Pad"/> for each button, so how many pads a machine has is a thing
    /// the machine says rather than a number built into the program. That is what makes a pad
    /// reachable on its own: it has a name of its own, a key of its own, and a line of its own in
    /// every preset.
    ///
    /// What is on a pad is still not described. Which recording it plays and what it is called
    /// belong to the preset or the song; whether it is lit is what the machine is doing this
    /// instant. Those come from the host through <see cref="IMachinePads"/>, by the same position
    /// the buttons are declared in.
    ///
    /// A Pads holding no buttons falls back to however many the host has, which is what every
    /// machine written before the buttons existed expects.
    /// </remarks>
    public const string Pads = "Pads";

    /// <summary>
    /// One button of a pad grid. The parameter is its name; the key is what it answers to.
    /// </summary>
    /// <remarks>
    /// Named, and that is the point of it. A preset says what is on "kick" rather than what is
    /// on the fourth thing in a list, so reordering the grid does not silently move every drum
    /// along one, and a preset written for a machine is legible without counting.
    ///
    /// The key is the note that fires it in a pattern. Written on the button and left alone by
    /// everything else: which recording answers which key is the whole of what a kit is.
    /// </remarks>
    public const string Pad = "Pad";

    /// <summary>
    /// The recording the machine holds, cut into pieces: the picture, the boundaries, and what
    /// it takes to make more or fewer of them.
    /// </summary>
    /// <remarks>
    /// A machine that fills itself from one recording rather than many puts this on its face. It
    /// turns no parameter: where the cuts are is read back off the pieces, so the pieces are the
    /// truth and this is the picture of them. Supplied through <see cref="IMachineSlices"/> by
    /// whoever is showing the panel, for the same reason the pads are.
    /// </remarks>
    public const string Slices = "Slices";

    /// <summary>Room left deliberately empty.</summary>
    public const string Spacer = "Spacer";
}
