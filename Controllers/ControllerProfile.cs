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

    /// <summary>Anything a person opening the file should know. Read by nobody.</summary>
    public string Note { get; set; } = "";
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
    /// True when nothing is sent unless a modifier is held.
    /// </summary>
    /// <remarks>
    /// Not so that anything can send one. So that a person can be told, rather than left to
    /// conclude the software is broken, which is where half an hour went on a MiniLab whose
    /// play button is Shift and a pad.
    /// </remarks>
    public bool Shift { get; set; }
}
