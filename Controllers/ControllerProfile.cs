using System.Collections.Generic;

namespace JingleBox2.Controllers;

/// <summary>
/// What a controller is, as a file. The counterpart of <c>machine.json</c> for hardware.
/// </summary>
/// <remarks>
/// A description and never a negotiation. Everything here is a fact about the model of device
/// rather than about the one on this desk, so a file written by somebody in another country
/// with the same controller is right here too.
///
/// What it must not hold is what any control drives. That is the user's, and a file carrying it
/// would be a manufacturer deciding somebody else's desk.
///
/// Nothing in the application requires one. A controller with no profile works exactly as it
/// always did and its controls are called `CC 89 ch 1` instead of `Encoder 3`, which is the only
/// difference a profile is ever allowed to make.
/// </remarks>
public sealed class ControllerProfile
{
    /// <summary>What to call the device, in SETTINGS and in a list of links.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The port names this is about, as patterns.
    /// </summary>
    /// <remarks>
    /// Patterns rather than names because a port is not called the same thing on two operating
    /// systems. Identity is the better key and does not depend on any of this; see
    /// <see cref="Identity"/>.
    /// </remarks>
    public List<string> Matches { get; set; } = new();

    /// <summary>
    /// What the device answers a universal identity request with.
    /// </summary>
    /// <remarks>
    /// The one identifier that travels. A MiniLab 3 is `00 20 6B` family `0002` member `0404` on
    /// every operating system, through any hub, with three of them plugged in, while its port is
    /// called something different on each. Recorded here and not yet used to match: reading the
    /// reply needs system exclusive read back off the wire, which is not built. See
    /// docs/hardware-integration.md.
    /// </remarks>
    public ControllerIdentity? Identity { get; set; }

    /// <summary>
    /// Which port does what, for a device that presents more than one.
    /// </summary>
    /// <remarks>
    /// The most useful thing in the file, and the one nobody expects to need. A MiniLab 3 shows
    /// up as four ports with four nearly identical names, three of them wrong for anything you
    /// would guess, and the only way to find out which is which is a manual most people do not
    /// have. A profile that says so turns a list of four mysteries into a list of four jobs.
    /// </remarks>
    public List<ControllerPort> Ports { get; set; } = new();

    /// <summary>
    /// The controls that send the same thing whatever mode the device is in.
    /// </summary>
    /// <remarks>
    /// A modulation strip is a modulation strip in every program. Kept apart from the programs
    /// because a control common to all of them says nothing about which one is running, and
    /// counting it would make the guess below worse.
    /// </remarks>
    public List<ControllerControl> Controls { get; set; } = new();

    /// <summary>
    /// The device's modes, and what its controls send in each.
    /// </summary>
    /// <remarks>
    /// The reason profiles exist. A MiniLab 3 has seven of these and switching between them
    /// rearranges every number the device sends, with nothing said and no way to ask from
    /// outside. Two of them are the manufacturer's and five are whatever the owner built.
    /// </remarks>
    public List<ControllerProgram> Programs { get; set; } = new();

    /// <summary>
    /// The screen it has, if it has one.
    /// </summary>
    /// <remarks>
    /// A fact about the device in the same way its ports are, and it has to be written down for
    /// the same reason: there is no way to ask. A controller cannot be sent "have you a screen",
    /// and a screen that is not there swallows whatever is sent to it without a word, so a
    /// program guessing gets no feedback whether it guessed right or wrong.
    ///
    /// Nothing here for a device with no screen, which is most of them, and nothing for a device
    /// whose screen nobody has worked out how to write to yet. Both mean the same thing to the
    /// application, which is that it writes nothing.
    /// </remarks>
    public ControllerScreen? Screen { get; set; }

    /// <summary>
    /// The control surface protocol it speaks, if it speaks one.
    /// </summary>
    /// <remarks>
    /// Mackie Control needs no file, which is the whole reason it is worth reading: the protocol
    /// says what every control on it is, so a surface nobody has written anything about works on
    /// arrival. This is not that promise being withdrawn. It is the other half of it.
    ///
    /// A device with no file is still read as a Mackie surface when it is ticked for the
    /// transport, exactly as before. A device with a file that never mentions Mackie is not, and
    /// that is a statement rather than an omission: a file lists what the device sends, and one
    /// that lists fifty one plain controllers and no protocol is saying there is no protocol.
    ///
    /// The case it exists for cost an evening. Mackie's eight V-pots are continuous controllers
    /// 0x10 to 0x17, which is 16 to 23, and a nanoKONTROL2's eight knobs are 16 to 23 as well.
    /// Ticking Transport for it, which is the only way to reach its transport buttons, turned on
    /// the Mackie reading of the very same numbers, so every knob was decoded twice: once as the
    /// position it is and once as a count of notches it is not. The second reading won often
    /// enough to be unusable, and from a hand on the desk it reads as a knob that is far too
    /// sensitive rather than as two things answering one message. An MPD218 has the same
    /// collision on 16 to 21 and would have had the same evening.
    /// </remarks>
    public ControllerSurface? Surface { get; set; }

    /// <summary>Anything a person opening the file should know. Read by nobody.</summary>
    public string Note { get; set; } = "";
}

/// <summary>What control surface protocol a device speaks, and on which of its ports.</summary>
/// <remarks>
/// Two facts and not one, for the reason a screen's are two: a device that speaks Mackie rarely
/// speaks it on the port its knobs are on. A MiniLab 3 keeps it on a port named MCU beside three
/// others, and a KeyLab mkII on the one named DAW, and only while Global Settings, DAW Map says
/// Standard MCU. Naming the port is what stops the protocol being read off the port that carries
/// the plain notes.
/// </remarks>
public sealed class ControllerSurface
{
    /// <summary>
    /// Which protocol: <c>mackie</c>, and nothing else is read here yet.
    /// </summary>
    /// <remarks>
    /// A name rather than a flag, the same as a screen's, so the day a second surface protocol
    /// arrives a file written today still names something. A name nothing here implements reads
    /// as a device with no surface, which is deliberately not an error.
    /// </remarks>
    public string Protocol { get; set; } = "";

    /// <summary>The port it is spoken on, as a pattern, matched the way every other port is.</summary>
    public string Port { get; set; } = "";
}

/// <summary>What kind of screen a device has, and which of its ports it is on.</summary>
/// <remarks>
/// Two facts and not one, because the port is not guessable either. A MiniLab 3 has a port named
/// for Analog Lab that looks like the obvious place and is not: its screen is written on the main
/// port. A KeyLab mkII's screen is on neither of the two ports by name; it is on whichever one
/// carries the DAW protocol, because the screen is part of that protocol rather than a thing of
/// its own.
/// </remarks>
public sealed class ControllerScreen
{
    /// <summary>
    /// Which protocol writes to it: <c>arturia</c> or <c>mackie</c>.
    /// </summary>
    /// <remarks>
    /// A name rather than a number, so a file written today still names something the day a third
    /// protocol arrives. A name nothing here implements is a device with no screen, which is the
    /// same answer as saying nothing at all and is deliberately not an error: a file may describe
    /// more of a device than this build knows what to do with.
    /// </remarks>
    public string Protocol { get; set; } = "";

    /// <summary>The port it is written to, as a pattern, matched the way every other port is.</summary>
    public string Port { get; set; } = "";

    /// <summary>
    /// Whether it has to be switched on before it will take anything.
    /// </summary>
    /// <remarks>
    /// A MiniLab 3 does: nothing appears on it until it has been sent
    /// <c>F0 00 20 6B 7F 42 02 02 40 6A 21 F7</c> once, which despite the name is not a wake at
    /// all. It is Arturia's ordinary write-a-setting, preset 02, param 40, control 6A, value 21,
    /// so it is switching something on rather than rousing anything.
    ///
    /// A KeyLab mkII is the exact opposite and that is why this is in the file. Sent it, the
    /// screen takes nothing afterwards; not sent it, the same text appears at once. Which is what
    /// you would expect of a setting written into a device it was never meant for, and it is the
    /// second time this one message has been caught doing damage: our own remarks already
    /// suspected it of being why a MiniLab stops speaking Mackie Control once it has had one.
    ///
    /// Off unless a file asks for it, because it is a write into somebody's hardware and the
    /// device that needs it can say so.
    /// </remarks>
    public bool Wake { get; set; }

}

/// <summary>What a device says when it is asked who it is.</summary>
public sealed class ControllerIdentity
{
    /// <summary>One or three bytes, as hex: "00 20 6B".</summary>
    public string Manufacturer { get; set; } = "";

    /// <summary>The family, as hex: two bytes, least significant first, as the device sends them.</summary>
    public string Family { get; set; } = "";

    /// <summary>And the model within that family, the same way.</summary>
    public string Member { get; set; } = "";
}

/// <summary>One of a device's ports, and what it is for.</summary>
public sealed class ControllerPort
{
    /// <summary>The port's name, as a pattern.</summary>
    public string Match { get; set; } = "";

    /// <summary>controls, transport, screen, thru. What the device intends it for.</summary>
    public string Role { get; set; } = "";

    /// <summary>What to tell somebody looking at a list of four of these.</summary>
    public string Note { get; set; } = "";
}

/// <summary>One of a device's modes.</summary>
public sealed class ControllerProgram
{
    /// <summary>What the device calls this mode, which is what a log line and a link list say.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// How this program's controls report themselves: absolute or relative.
    /// </summary>
    /// <remarks>
    /// A property of the program rather than of the control, because it is the program that
    /// decides. The same eight knobs on a MiniLab 3 walk smoothly through a range in its DAW
    /// program and count notches in its Arturia one. Same knobs, same hand, different kind of
    /// control, and no way to tell from outside except by watching what they send.
    /// </remarks>
    public string Sends { get; set; } = "";

    /// <summary>What each control sends while this program is the one running.</summary>
    /// <remarks>
    /// Which program is running is worked out from these: they do not overlap, so one number is
    /// usually enough to say which mode the device is in. A number that appears in two programs
    /// says nothing and is passed over.
    /// </remarks>
    public List<ControllerControl> Controls { get; set; } = new();
}

/// <summary>One thing on the front of the device.</summary>
public sealed class ControllerControl
{
    /// <summary>What is printed beside it, or as near as makes no difference.</summary>
    public string Name { get; set; } = "";

    /// <summary>Which continuous controller it sends. Below nought for one that sends none.</summary>
    public int Cc { get; set; } = -1;

    /// <summary>1 to 16, or nought for any, which is what almost all of them are.</summary>
    public int Channel { get; set; }

    /// <summary>encoder, fader, pad, button, strip. What it is, not what it does.</summary>
    public string Kind { get; set; } = "";

    /// <summary>
    /// Which of the transport's keys this button is: <c>play</c>, <c>stop</c>, <c>record</c>,
    /// or nothing for a button that is not one of them.
    /// </summary>
    /// <remarks>
    /// The legend printed on the button, which is a fact about the hardware in the way its
    /// controller number is, and it is here because otherwise one device in five is worse off
    /// than the rest for no reason anybody could explain.
    ///
    /// A MiniLab 3 and a KeyLab mkII need none of this: their transport buttons speak Mackie
    /// Control, plain controllers or machine control, so ticking Transport in SETTINGS makes
    /// them work and nothing has to be pointed anywhere. A nanoKONTROL2's play button is a plain
    /// controller 41 like its mute buttons, which no dialect covers, so the same tick did
    /// nothing at all and the only way to reach the transport was to point the button at it by
    /// hand. Same tick, same expectation, different answer.
    ///
    /// So this is a fourth dialect and the file is where it is written down, because the number
    /// is what differs between devices and the meaning is not. It adds no capability the
    /// hardware lacks: the device really does have a play button, and this says which one it is.
    /// A device with no file has no transport buttons here, which is what it had before.
    /// </remarks>
    public string Transport { get; set; } = "";

    /// <summary>
    /// What a press does to the value: <c>momentary</c>, <c>latching</c>, or nothing said.
    /// </summary>
    /// <remarks>
    /// The one fact about a button that its numbers cannot carry. Both kinds send nought and a
    /// hundred and twenty seven and nothing else, so no amount of watching separates them, and
    /// they mean opposite things. A latching button reports its own state and reports it once a
    /// press: following the value is exactly right. A momentary button reports a finger, full
    /// while it is held and nought when it is let go, so following the value mutes a track for
    /// as long as somebody keeps a thumb on the button.
    ///
    /// Every one of a nanoKONTROL2's thirty five buttons reads Momentary, which is in its scene
    /// and in no mapping list anywhere, and it is why mute and solo behaved the way they did.
    ///
    /// Nothing said means the value is followed, which is what everything did before this
    /// existed, so no controller anybody has already pointed at anything changes.
    /// </remarks>
    public string Press { get; set; } = "";

    /// <summary>
    /// True when nothing is sent unless a modifier is held.
    /// </summary>
    /// <remarks>
    /// Not so that anything can send one. So that a person can be told, rather than left to
    /// conclude the software is broken, which is where half an hour went on a MiniLab whose
    /// play button is Shift and a pad.
    /// </remarks>
    public bool Shift { get; set; }
}
