using JingleBox2.Midi;
using JingleBox2.Midi.Enums;

namespace JingleBox2.Controllers.Interfaces;

/// <summary>
/// What is known about the controllers plugged in: what each control is called, which of a
/// device's programs is running, and how a control should be read.
/// </summary>
/// <remarks>
/// Nothing here adds capability. A controller nobody has written a file for works exactly as it
/// always did, taught by hovering a knob and touching the hardware; a profile may add names,
/// shape and shortcuts and may never add a feature. Plain MIDI is the floor, not a fallback.
///
/// It holds a cache, and that is the reason it is an object rather than a set of functions. A
/// CC number implies which of a device's programs is running, and a control implies how it
/// should be picked up, and both used to be worked out on every message: one message cost 2200
/// bytes and 3.2 microseconds and now costs 200 and 1.2. What is remembered belongs to this
/// object, so a test gets its own and cannot be told what a previous test saw.
/// </remarks>
public interface IControllerProfiles
{
    /// <summary>
    /// Reads every profile again, from scratch.
    /// </summary>
    /// <remarks>
    /// Called at startup and whenever a file in the folder is saved, which is what makes writing
    /// one of these a matter of editing and touching a knob rather than editing and restarting.
    /// Everything worked out from the old files is thrown away with them: what was decided about
    /// a port, which program a device was believed to be in, and what a number implied.
    /// </remarks>
    void Reload();

    /// <summary>The profile for a port, or nothing, which is ordinary.</summary>
    ControllerProfile? For(string? device);

    /// <summary>What to call a device: its own name where one is known, else the port's.</summary>
    string Called(string? device);

    /// <summary>True when that port has a profile, so a page can say the match happened.</summary>
    bool Knows(string? device);

    /// <summary>
    /// Another controller message arrived, which is a clue about which program is running.
    /// </summary>
    /// <remarks>
    /// The device will not say, and cannot be asked without speaking its manufacturer's own
    /// language. But its programs do not overlap: a MiniLab's knobs send 86 in one and 74 in
    /// another and never both, so a single number is usually enough to know which is in front of
    /// you. A number that appears in more than one program says nothing and is ignored, and one
    /// that appears in none is somebody's control this file does not describe.
    ///
    /// Self correcting by construction. Switch the device to another program and its first
    /// message moves this along with it.
    /// </remarks>
    void Saw(string? device, int channel, int cc);

    /// <summary>Which program a device is believed to be in, or nothing while nobody knows.</summary>
    /// <remarks>
    /// A device whose file describes exactly one program is in it, and nothing has to be watched
    /// to find that out. <see cref="Saw"/> declines to work on such a device, correctly, since
    /// there is no ambiguity for it to resolve; without this the declining would mean no program
    /// was ever running, and a file that puts its controls in its one program would describe a
    /// device whose every control came back unknown. Which is what a nanoKONTROL2 did.
    /// </remarks>
    string ProgramOn(string? device);

    /// <summary>
    /// What a control is called on the front of the device, or nothing when nobody knows.
    /// </summary>
    /// <remarks>
    /// Answering nothing is the common case and is not a failure. Everywhere this is asked has
    /// something perfectly good to fall back on, which is the number itself, and a list that
    /// says `CC 89 ch 1` is a list that works.
    ///
    /// Three questions in order: the program that is running, where that is known; then anything
    /// true whatever program the device is in; and failing those, any program at all, but only
    /// when they agree. Before a device has said anything there is no way to tell its programs
    /// apart, and a name from the wrong one is worse than a number, since a number is merely
    /// unhelpful and a wrong name is a lie.
    /// </remarks>
    string Named(string? device, int channel, int cc);

    /// <summary>
    /// What a port is for, as a line to put under its name in a list.
    /// </summary>
    /// <remarks>
    /// Nothing at all for a device with no profile, and nothing for a port the profile does not
    /// mention, which is right: a blank line says the honest thing, and a guess would not.
    /// </remarks>
    string PortIs(string? device);

    /// <summary>
    /// Which screen protocol writes to that port, or nothing for a port with no screen on it.
    /// </summary>
    /// <remarks>
    /// The port and not the device, because a device is several ports and at most one of them is
    /// the screen. Writing to the wrong one is silent, so this is the difference between a
    /// greeting appearing and nobody being able to say why it did not.
    ///
    /// Nothing for a device with no file, which is the same answer the rest of this gives and is
    /// the rule the whole idea rests on: a profile may add names and shape and may never add
    /// capability. A controller nobody has written a file for had no screen before this existed
    /// and has none now.
    /// </remarks>
    string ScreenOn(string? device);

    /// <summary>
    /// Whether that device's screen has to be switched on before it will take anything.
    /// </summary>
    /// <remarks>
    /// False for a device with no file and for one that does not ask, since the message that does
    /// the switching on is a write into a device's settings and the wrong device is worse off for
    /// having had it.
    /// </remarks>
    bool ScreenWakes(string? device);


    /// <summary>
    /// Whether a job belongs on this port, for a device that presents several.
    /// </summary>
    /// <remarks>
    /// The thing a person cannot be expected to know and should not have to. A MiniLab 3 is four
    /// ports, its notes and knobs come out one of them, and the name of that one is no more
    /// suggestive than the other three. Ticking Transport against the port called MCU/HUI is the
    /// obvious guess and it is wrong whenever the device is in a DAW program, which is a whole
    /// evening lost to a checkbox.
    ///
    /// Transport goes on both, deliberately. The two are alternatives on the hardware and the
    /// device sends one or the other depending on its program, never both, so listening to both
    /// costs nothing and removes the only decision that needed a manual.
    ///
    /// A port the profile does not mention takes everything, because nothing is known about it
    /// and a silent refusal is worse than a port that does too much.
    /// </remarks>
    bool PortTakes(string? device, MidiDeviceRole role);

    /// <summary>
    /// How a control should be read, when the file knows the hardware well enough to say.
    /// </summary>
    /// <remarks>
    /// A fact beating a guess, which is the whole of what a profile buys here.
    /// <see cref="ControlSense"/> works out what a control is from what it sends, and it is
    /// right about everything it can see. What it cannot see is the shape of the thing under
    /// the hand. An endless encoder reporting a position walks smoothly through its range and
    /// is, to three messages, indistinguishable from a fader; so it is read as a fader, saved
    /// as one, and from then on every session begins by hunting for the value with a knob that
    /// has no beginning and no end to hunt with. Which is exactly what happened to nine links
    /// in one song, five of them on encoders.
    ///
    /// So a control the file calls an encoder in a program that sends positions is read as
    /// movement between messages instead, which works whether the firmware wraps at the top or
    /// stops there: a wrap unwinds and a stop reads as no movement, and turning back moves it
    /// at once either way.
    ///
    /// Nothing is claimed for an encoder in a program that counts notches. Which of the two
    /// conventions it counts in is not in the file and getting it wrong throws the parameter
    /// across its range, so that one is left to be watched rather than assumed.
    ///
    /// Asked on every message, so the answer is worked out once per control per program and
    /// looked up after that. It cannot change without the program changing, and the program is
    /// part of the question.
    /// </remarks>
    ControlPickup? Pickup(string? device, int channel, int cc);

    /// <summary>Everything the profile says about a control, for a tip or a log line.</summary>
    ControllerControl? Control(string? device, int channel, int cc);
}
