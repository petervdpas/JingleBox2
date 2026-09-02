using System.Collections.Generic;
using JingleBox2.Rack.Faces.Interfaces;

namespace JingleBox2.Rack.Faces;

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
public sealed class Panel
{
    /// <summary>What the panel is, from the outside in. Usually a grid holding groups.</summary>
    public PanelElement Root { get; set; } = new() { Element = ElementKinds.Grid };
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
public sealed class PanelElement
{
    /// <summary>Which kind of thing this is. See <see cref="ElementKinds"/>.</summary>
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
    public List<PanelElement> Children { get; set; } = new();
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
///
/// Two more belong to every control: <c>when</c> names another parameter and <c>is</c> the value
/// it has to be at, and together they say when this control does anything. A pulse's duty cycle
/// is the case: it means nothing on a sawtooth, so on every other wave it greys out rather than
/// sitting there looking live. Greyed and not taken away, because a panel that grows and shrinks
/// depending on what it is set to is a different panel every time you look at it.
/// </remarks>
public static class ElementKinds
{
    /// <summary>Rows and columns. Properties: columns, rows, and per child column, row, span.</summary>
    /// <remarks>
    /// Properties: rows and columns, each a comma separated list of sizes, and gap. What it
    /// holds says row, column and span.
    /// </remarks>
    public const string Grid = "Grid";

    /// <summary>A framed group with a heading. Properties: caption, gap, equal, width, height.</summary>
    /// <remarks>
    /// A group given a size smaller than what it holds draws over whatever is under it, which is
    /// what the same group does written by hand. It is not clipped: a frame sits exactly on its
    /// own boundary, so clipping a group's contents shaves a pixel off every picture in it, and
    /// clipping the group takes the corners off its own frame.
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

    /// <summary>
    /// A dial. Properties: dial, ticks, headroom, lines, display.
    /// </summary>
    /// <remarks>
    /// The first four are what it looks like rather than what it does: how wide the dial is, how
    /// many marks are set round it, how much air is left above the name and how many lines that
    /// name may take. They are worth saying where a machine stands its knobs on a strip, because
    /// a row of dials that are not the same size does not line up with the row under it.
    ///
    /// <c>display</c> names a text setting the knob writes under itself instead of its own
    /// number, for the control that turns one thing and says another: a filter's dial turns a
    /// position and reads out hertz, and only the machine knows how one becomes the other.
    /// </remarks>
    public const string Knob = "Knob";
    /// <remarks>
    /// Properties: track, which is the length of the throw, and ticks, a comma separated list of
    /// values to mark on the scale. The throw is not the height: a fader also draws its name
    /// above and its value below, so a machine asking for a throw of 96 wants a control taller
    /// than that.
    ///
    /// Say nothing and the fader takes the standard throw, which is what nearly every machine
    /// should do: one length, so a panel does not end up with a different fader in each of its
    /// boxes, and so raising it raises them all. Nought means the other thing, take whatever
    /// height you are given, for a strip that fills its panel.
    /// </remarks>
    public const string Fader = "Fader";

    /// <summary>
    /// One of two positions. Properties: on and off, the wording at each end.
    /// </summary>
    /// <remarks>
    /// The wording is the panel's, not the parameter's: what the two ends of a switch are called
    /// is a matter of what it does on this machine, and a switch nobody has worded says on and
    /// off, which is true of every switch ever made and useful on none of them.
    ///
    /// <c>lines</c> and <c>headroom</c> are the knob's, and here for the same reason: a switch
    /// standing in a row of dials has to sit on the line they sit on.
    ///
    /// <c>options</c> makes it more than two: a comma separated list of positions, and the
    /// parameter holds which one is chosen, counting from zero. That is the same number a
    /// <see cref="Choice"/> holds, and the difference is only what it looks like. A list of six
    /// waves belongs on a switch you can see the position of, not behind a dropdown that has to
    /// be opened to find out what the machine is set to.
    /// </remarks>
    public const string Switch = "Switch";

    /// <summary>
    /// A value typed or stepped rather than turned. No properties of its own beyond the ones
    /// every element has.
    /// </summary>
    /// <remarks>
    /// The range, the step and the wording all come from the parameter, so a number field says
    /// nothing a dial does not; what it buys is that a value can be read exactly and set exactly.
    /// For a tempo, a transpose or a count, where somebody knows the number they want and
    /// hunting for it with a dial is the wrong instrument.
    /// </remarks>
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
    /// number that could stand for either. See <see cref="PanelActions"/>.
    /// </remarks>
    public const string Button = "Button";

    /// <summary>
    /// A lamp. Read only: lit when the parameter is past the middle of its range. Properties:
    /// size, colour, blink.
    /// </summary>
    /// <remarks>
    /// <c>blink</c> says the parameter is a rate in hertz rather than something to be past the
    /// middle of, and the lamp goes round at it. For the lamp beside a low frequency oscillator's
    /// rate knob, which is not reporting the rate so much as being it.
    /// </remarks>
    public const string Led = "Led";

    /// <summary>
    /// How loud something is, from the parameter's place in its range. Read only. Properties:
    /// orientation, stereo, width, height.
    /// </summary>
    public const string Meter = "Meter";

    /// <summary>
    /// Where the track playing this instrument has got to. Properties: caption, pages, colour,
    /// size, gap, width.
    /// </summary>
    /// <remarks>
    /// A button per page of rows over the lamps that count them, and it turns nothing: the
    /// pattern is the tracker's and this watches it. Read only in the sense that matters, since
    /// the one thing the buttons do is choose which run of rows the lamps show.
    ///
    /// <c>pages</c> is false for the lamps alone, on a machine with room for one and not the
    /// other. It names no parameter: where a song has got to is not a setting of the machine,
    /// so the host hands it over the way it hands over the keyboard.
    /// </remarks>
    public const string Location = "Location";

    /// <summary>
    /// The keyboard, and the octave it is showing. Properties: keys, caption.
    /// </summary>
    /// <remarks>
    /// The parameter is the octave on show and nothing else. Which keys are sounding, which have
    /// something on them and which one is in hand are not settings and cannot be: they come from
    /// whoever is showing the panel, through <see cref="JingleBox2.Rack.Machines.Interfaces.IMachineKeys"/>, the same way the pads
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
    /// The shape the machine is making, drawn. Properties: width, height, cycles.
    /// </summary>
    /// <remarks>
    /// For the machine that generates its sound rather than playing one back: it has no recording
    /// to show, and a row of knobs does not tell anybody what a wave with the duty at a fifth and
    /// the drive at four looks like.
    ///
    /// It turns nothing. The curve comes from the machine's own engine through
    /// <see cref="IPanelScope"/>, because what a wave is and what drive does to it are the
    /// machine's business, and a picture drawn from anything but the real thing would be a
    /// drawing of a machine rather than of this one.
    ///
    /// <c>cycles</c> names the setting saying how much of the wave is shown. It is the one thing
    /// about this that anybody turns, and on the machine it came from that knob is marked as no
    /// part of the sound: see <see cref="Parameter.Saved"/>.
    /// </remarks>
    public const string Scope = "Scope";

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
    /// Where the machine is started from. Properties: source, caption, width.
    /// </summary>
    /// <remarks>
    /// The picker at the top of a panel. It turns no parameter, since what a preset does is write
    /// the lot of them at once, and it is not a Choice either: a Choice is a number the machine
    /// keeps and this is not kept anywhere. The list comes from whoever is showing the panel,
    /// through <see cref="IPanelPresets"/>.
    ///
    /// There are two of these and they are not the same control. <c>source</c> says which:
    /// <see cref="PanelStarts.Presets"/> offers the machine's own presets, which are a handful
    /// shipped in its folder and have nothing to file, so the picker is one control wide.
    /// <see cref="PanelStarts.Takes"/> offers your recordings, which run to hundreds and are
    /// filed under categories, so it comes with a category list in front of it and is a different
    /// width and a different shape.
    ///
    /// On the object, because that is where it is true. It used to be a fact about the machine,
    /// which meant a machine could not carry one of each and the answer had to be found by
    /// reading the presets folder to see whether anything in it said so. A machine that says
    /// nothing here still falls back to that, so every machine written before this reads as it
    /// always did.
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
    /// The pads of a kit: a grid of buttons. Properties: rows, columns, cap, capHeight, gap, colour.
    /// </summary>
    /// <remarks>
    /// Rows and columns say what shape it is, because that is what a grid is and it cannot be
    /// worked out from the buttons: sixteen of them is four by four, two by eight or sixteen by
    /// one. It holds one <see cref="Pad"/> for each button, so how many pads a machine has, and
    /// what each of them answers to, is a thing the machine says rather than a number built into
    /// the program. That is what makes a pad
    /// reachable on its own: it has a name of its own, a key of its own, and a line of its own in
    /// every preset.
    ///
    /// What is on a pad is still not described. Which recording it plays and what it is called
    /// belong to the preset or the song; whether it is lit is what the machine is doing this
    /// instant. Those come from the host through <see cref="JingleBox2.Rack.Machines.Interfaces.IMachinePads"/>, by the same position
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
    /// The map of a sampler: every zone drawn as the stretch of keyboard it answers to.
    /// Properties: lane, gap, font.
    /// </summary>
    /// <remarks>
    /// Drawn and not listed, because a table of ranges says nothing about whether the keyboard
    /// is covered and the gaps are the first thing you need to see. It is edited on the picture
    /// too: an edge dragged resizes, the middle dragged slides the whole zone along, and the
    /// white line says which key the recording was made at. Nobody dials a zone's edges.
    ///
    /// It turns no parameter and holds no children. How many zones there are is not the
    /// machine's to say, unlike the pads of a kit: a zone is added and taken away while the
    /// instrument is being built, so the map comes from the host through
    /// <see cref="JingleBox2.Rack.Machines.Interfaces.IMachineZones"/> and this draws what it is handed.
    /// </remarks>
    public const string Zones = "Zones";

    /// <summary>
    /// Which zone the settings beside the map are about, as a list to pick from.
    /// </summary>
    /// <remarks>
    /// What <see cref="PadPicker"/> is to a kit. The map says it too, and both are wanted: the
    /// map is how a hand picks a zone out of a picture, and this is how you read off which one
    /// is in hand and step through them in order.
    /// </remarks>
    public const string ZonePicker = "ZonePicker";

    /// <summary>
    /// The recording the machine holds, cut into pieces: the picture, the boundaries, and what
    /// it takes to make more or fewer of them.
    /// </summary>
    /// <remarks>
    /// A machine that fills itself from one recording rather than many puts this on its face. It
    /// turns no parameter: where the cuts are is read back off the pieces, so the pieces are the
    /// truth and this is the picture of them. Supplied through <see cref="JingleBox2.Rack.Machines.Interfaces.IMachineSlices"/> by
    /// whoever is showing the panel, for the same reason the pads are.
    /// </remarks>
    public const string Slices = "Slices";

    /// <summary>
    /// The control surfaces there is a layout for on this machine. Properties: corner, caption,
    /// cap, capHeight.
    /// </summary>
    /// <remarks>
    /// A knob on a controller can be pointed at this machine, and this is where the machine says
    /// that belongs on its face: pressing it lists the desks a layout has been kept for, and one
    /// line more that starts learning, which is the same mode Ctrl+Shift+M turns over.
    ///
    /// It turns no parameter and never will. Which desk is plugged in and what has been kept for
    /// it are facts about the room the machine is being played in, not about the machine, so none
    /// of it can be written into a song and none of it is the machine's to know. What is on offer
    /// comes from whoever is showing the panel, through <see cref="IPanelMenu"/>, the same way
    /// the presets and the map do.
    ///
    /// <b>It goes in a corner and it may not go anywhere else.</b> This is the one place the
    /// program itself speaks on somebody's front panel, so it keeps out of the way of the things
    /// that are the machine, and it never stretches to fill what is holding it however that is
    /// laid out. <c>corner</c> is which one: topRight, which is the default and where every
    /// program puts this button, or topLeft, bottomRight or bottomLeft.
    ///
    /// <c>caption</c> is what is written on it, and says nothing by default, which draws the
    /// three bars every program uses for a menu. A machine with room for a word may use one.
    /// </remarks>
    public const string Menu = "Menu";

    /// <summary>
    /// The instrument's name in the song, as a badge. Properties: width.
    /// </summary>
    /// <remarks>
    /// The instrument's own name, which belongs to the song and not to the machine: a machine is
    /// called what the machine is called, and an instrument off it is yours to call anything. So
    /// it turns no parameter and cannot: it comes from whoever is showing the panel, through
    /// <see cref="JingleBox2.Rack.Machines.Interfaces.IInstrumentName"/>, the same way the presets and the map do, and it is read only
    /// where what is being shown is the machine itself rather than an instrument.
    ///
    /// It is a part so that the machine says where it goes. This program used to draw it in a
    /// corner over every panel, which is the one thing a machine's face is never supposed to
    /// have done to it: a machine that had never asked for it grew one, and a machine with
    /// something of its own in that corner had the two drawn on top of each other.
    ///
    /// A machine with no InstrumentName on it shows no name, which is a machine saying its face is
    /// its own.
    /// Nothing is lost by that: the window is titled with it, the rack lists it, and the song's
    /// instrument list renames it.
    /// </remarks>
    public const string InstrumentName = "InstrumentName";

    /// <summary>Room left deliberately empty.</summary>
    public const string Spacer = "Spacer";
}
