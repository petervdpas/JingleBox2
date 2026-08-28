using System;

namespace JingleBox2.Midi;

/// <summary>What kind of thing a hardware control is pointed at.</summary>
public enum ControlKind
{
    /// <summary>A parameter on the machine a track plays: a knob on its own panel.</summary>
    Instrument,

    /// <summary>A parameter on a plugin in a track's insert chain.</summary>
    Insert,

    /// <summary>Something on a track's mixer strip.</summary>
    Mix,

    /// <summary>
    /// A button on a machine's panel: something to be done rather than a value to be moved.
    /// </summary>
    /// <remarks>
    /// Last, so a mapping saved before this existed still reads as the kind it was given.
    /// </remarks>
    Action
}

/// <summary>The handful of things a mixer strip has, named rather than counted.</summary>
/// <remarks>
/// An enum and not a string key, so the set of them is visible to anything reading this and
/// there is no name to spell wrong. A strip is a fixed thing with a fixed set of controls,
/// unlike a machine, whose parameters are its own business.
/// </remarks>
public enum MixControl
{
    /// <summary>The fader.</summary>
    Volume,

    /// <summary>Where it sits between the two sides.</summary>
    Pan,

    /// <summary>Off. A knob writes it as anything at or above the middle of its range.</summary>
    Mute,

    /// <summary>And on its own, which the same rule applies to.</summary>
    Solo,

    /// <summary>How far another track's ducker pulls this one down.</summary>
    Duck,

    /// <summary>
    /// How long the ducking takes to come back up.
    /// </summary>
    /// <remarks>
    /// Last, so a mapping saved before this existed still reads as the control it was given. It
    /// was missing rather than left out: every other value on a strip could be pointed at and
    /// this one had no name for a link to use, so the knob beside Duck was the one thing on the
    /// mixer a controller could not reach.
    /// </remarks>
    Release
}

/// <summary>
/// What happens when the hardware and the software disagree about where a control is.
/// </summary>
/// <remarks>
/// They will disagree, constantly. A knob does not move when you open a different song, so the
/// hardware sits wherever your hand left it and the parameter is wherever the patch says. Touch
/// the knob and something has to give.
/// </remarks>
public enum ControlPickup
{
    /// <summary>
    /// The value follows the knob at once. Simple, and it lurches: a filter at 200 Hz with the
    /// knob at three o'clock snaps wide open on the first degree of movement.
    /// </summary>
    Jump,

    /// <summary>
    /// The knob is ignored until it passes where the parameter already is, and follows from
    /// there. What a hardware desk does, and what makes a controller feel attached to the sound
    /// rather than fighting it.
    /// </summary>
    Takeover,

    /// <summary>
    /// For an endless encoder, which sends how far it turned rather than where it is. There is
    /// nothing to reconcile: the parameter moves by what arrives.
    /// </summary>
    Relative,

    /// <summary>
    /// Work it out from what the control sends, and then behave as that.
    /// </summary>
    /// <remarks>
    /// What a new link starts as, and what almost every link stays as. A MIDI message says a
    /// controller number and a value and nothing whatever about the thing that sent it: a
    /// button, a fader and an endless encoder are the same three bytes. But they do not send
    /// the same values, and three messages is enough to tell them apart. See
    /// <see cref="ControlSense"/>.
    ///
    /// Last in the list on purpose. The numbers are what a settings file holds, so a mapping
    /// saved before this existed still reads as the pickup it was given.
    /// </remarks>
    Sensed,

    /// <summary>
    /// For a knob with no end stop that reports a position anyway.
    /// </summary>
    /// <remarks>
    /// The awkward one, and common: the knob turns for ever, but its firmware answers with a
    /// number between nought and a hundred and twenty seven, and that number comes round. Read
    /// as a position it is right until the moment it wraps, and then the parameter you had just
    /// brought down to its floor leaps to its ceiling.
    ///
    /// So the difference between one message and the next is read instead, and the wrap is
    /// unwound: a step of a hundred and twenty seven downward is a step of one upward wearing a
    /// disguise. That makes it behave as the endless knob it physically is, which also means it
    /// stops at the ends rather than coming round them.
    ///
    /// Nothing starts as this. A control becomes it the first time it is seen to wrap, which is
    /// the only moment the difference between it and an ordinary knob shows.
    /// </remarks>
    Endless
}

/// <summary>How an endless encoder says which way it was turned.</summary>
/// <remarks>
/// There is no standard, only two conventions, and a controller sending one read as the other
/// turns the wrong way and jumps the length of the range doing it. Which one this is gets
/// worked out along with everything else: an encoder resting at the middle of the range is
/// counting from there, and one resting at either end is counting in two's complement.
/// </remarks>
public enum ControlTurn
{
    /// <summary>Middle of the range is still, above is clockwise, below is anticlockwise.</summary>
    Offset,

    /// <summary>Small numbers are clockwise, large ones are anticlockwise and count down from 128.</summary>
    Twos
}

/// <summary>Which track a mapping is about.</summary>
public enum ControlScope
{
    /// <summary>
    /// Whichever track you are working on. One knob, every track, no thought at the desk.
    /// </summary>
    Focused,

    /// <summary>
    /// One track and only that one. What a mixer wants: fader three is track three whether or
    /// not you are looking at it.
    /// </summary>
    Fixed
}

/// <summary>
/// One hardware control pointed at one thing in the program.
/// </summary>
/// <remarks>
/// The question this has to answer is what a knob means when the machines are not the same.
/// Knob one on Zampler is a filter and knob one on BongaBong is not; a mapping that says
/// "controller 21 moves the thing on track three" is a mapping that means something different
/// every time you change what track three plays.
///
/// So a mapping about a machine names the machine, and the parameter by the key that machine
/// stores it under. Controller 21 is Zampler's cutoff, and it is Zampler's cutoff on every
/// track, in every song, for ever. Learn your controller once per machine and it is learned.
/// A track playing something else is not driven by it at all, which is right: that knob has
/// nothing to say to a drum machine.
///
/// Which track, then, is a separate question with a separate answer, and that is
/// <see cref="ControlScope"/>. Normally the one you are working on, so a bank of knobs drives
/// whatever is in front of you. Pinned to a track where that is the point, which is the mixer:
/// a fader bank is about all the tracks at once and none of them is the one you are looking at.
///
/// Kept in the settings rather than in the song, because the desk does not change when the song
/// does. The hardware is in the room, not in the file.
/// </remarks>
public sealed class ControlMapping
{
    /// <summary>
    /// The controller this was learned on, by name.
    /// </summary>
    /// <remarks>
    /// Kept because a controller number means nothing on its own: two devices both have a CC 22
    /// and they are not the same knob. Without the name, plugging in a second controller would
    /// have it quietly driving whatever the first one was pointed at.
    ///
    /// A mapping whose device is not plugged in is kept exactly as it is. Nothing prunes it and
    /// nothing warns about it: a controller left in the other room is not a decision to unwire
    /// it, and the layout has to be there when it comes back. It is only ever displaced by
    /// somebody pointing something else at the same control.
    ///
    /// Empty means any device, which is what a mapping made before this existed reads as.
    /// </remarks>
    public string Device { get; set; } = "";

    /// <summary>1 to 16, as the message says it.</summary>
    public int Channel { get; set; } = 1;

    /// <summary>Which continuous controller, 0 to 127.</summary>
    public int Cc { get; set; }

    /// <summary>What sort of thing it is pointed at, which decides which fields below are read.</summary>
    public ControlKind Kind { get; set; } = ControlKind.Instrument;

    /// <summary>Whether it follows the track you are working on or stays on one.</summary>
    public ControlScope Scope { get; set; } = ControlScope.Focused;

    /// <summary>Which track, counted from zero. Only read when the scope is fixed.</summary>
    public int Track { get; set; }

    /// <summary>
    /// The machine this knob is about, by its slot id. Empty means any of them, which is only
    /// sensible for a parameter every machine has.
    /// </summary>
    public string Machine { get; set; } = "";

    /// <summary>Which parameter, for a machine. The key it is stored under, never its name.</summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// Which parameter by place rather than by name: the third knob on whatever face is in
    /// front of you. Below nought for a link that names one, which is every link a person made.
    /// </summary>
    /// <remarks>
    /// Only ever set by the layout a controller falls back on when nothing has been pointed at
    /// anything. A link made by hand names its parameter, because you pointed at that parameter
    /// and no other; a link nobody made cannot, because the machine in front of you tomorrow is
    /// not the one in front of you now. The place is what the two have in common.
    ///
    /// Read through the order a panel reads in, so it means the third control your eye lands on
    /// and not the third line of a file. See <see cref="Machines.PanelOrder"/>.
    /// </remarks>
    public int Ordinal { get; set; } = -1;

    /// <summary>
    /// The plugin this knob is about, by the id the scanner gave it. A plugin's parameter
    /// numbers mean nothing without knowing whose they are.
    /// </summary>
    public string Plugin { get; set; } = "";

    /// <summary>Which insert, counted from zero. Only read when the scope is fixed.</summary>
    public int Slot { get; set; }

    /// <summary>Which parameter, as the plugin numbers them.</summary>
    public uint Parameter { get; set; }

    /// <summary>Which strip control, for <see cref="ControlKind.Mix"/>.</summary>
    public MixControl Mix { get; set; } = MixControl.Volume;

    /// <summary>
    /// How the hardware and the software are reconciled when they disagree.
    /// </summary>
    /// <remarks>
    /// Starts as <see cref="ControlPickup.Sensed"/> and is written here once it has been worked
    /// out, so a session after this one does not have to watch three messages again. A
    /// controller's own file beats what was worked out by watching, and corrects this rather than
    /// migrating it: the number stored was a guess made before anything knew the device.
    /// </remarks>
    public ControlPickup Pickup { get; set; } = ControlPickup.Sensed;

    /// <summary>Which way an encoder counts, once that has been worked out.</summary>
    public ControlTurn Turn { get; set; } = ControlTurn.Offset;

    /// <summary>What to call it in a list of mappings. Filled in when it is learned.</summary>
    public string Name { get; set; } = "";

    /// <summary>One of its own, for a song keeping a copy of what it was handed.</summary>
    public static ControlMapping Copy(ControlMapping one) => new()
    {
        Device = one.Device,
        Channel = one.Channel,
        Cc = one.Cc,
        Kind = one.Kind,
        Scope = one.Scope,
        Track = one.Track,
        Machine = one.Machine,
        Key = one.Key,
        Ordinal = one.Ordinal,
        Plugin = one.Plugin,
        Slot = one.Slot,
        Parameter = one.Parameter,
        Mix = one.Mix,
        Pickup = one.Pickup,
        Turn = one.Turn,
        Name = one.Name
    };

    /// <summary>True when this mapping and that message are about each other.</summary>
    /// <remarks>
    /// The device as well as the number, unless the mapping does not name one. Two controllers
    /// on the desk both have a CC 22, and a mapping made on one has nothing to say about the
    /// other.
    /// </remarks>
    public bool Answers(MidiMessage message) =>
        message != null
        && message.Type == MidiMessageType.ControlChange
        && message.Channel == Channel
        && message.Value == Cc
        && (Device.Length == 0 || MidiService.SameName(Device, message.Device));

    /// <summary>
    /// True when both point at the same thing in the program, whatever they were learned on.
    /// </summary>
    /// <remarks>
    /// What makes a link the last one laid down on a control rather than one of a growing pile.
    /// Pointing a second knob at a filter is saying you want that knob on it, not that you want
    /// two, and the one you meant is the one you just turned.
    /// </remarks>
    public bool SameTarget(ControlMapping other)
    {
        if (other is null || other.Kind != Kind) return false;

        return Kind switch
        {
            ControlKind.Instrument or ControlKind.Action =>
                string.Equals(other.Machine, Machine, StringComparison.Ordinal)
                && string.Equals(other.Key, Key, StringComparison.Ordinal),

            ControlKind.Insert =>
                string.Equals(other.Plugin, Plugin, StringComparison.Ordinal)
                && other.Parameter == Parameter,

            ControlKind.Mix => other.Mix == Mix && other.Scope == Scope
                               && (Scope != ControlScope.Fixed || other.Track == Track),

            _ => false
        };
    }

    /// <summary>True when both are the same physical control on the same controller.</summary>
    public bool SameControl(ControlMapping other) =>
        other != null
        && other.Channel == Channel
        && other.Cc == Cc
        && MidiService.SameName(other.Device, Device);
}
