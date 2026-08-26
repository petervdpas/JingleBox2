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
    Volume,
    Pan,
    Mute,
    Solo,
    Duck
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
    /// <summary>1 to 16, as the message says it.</summary>
    public int Channel { get; set; } = 1;

    /// <summary>Which continuous controller, 0 to 127.</summary>
    public int Cc { get; set; }

    public ControlKind Kind { get; set; } = ControlKind.Instrument;

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

    public ControlPickup Pickup { get; set; } = ControlPickup.Sensed;

    /// <summary>Which way an encoder counts, once that has been worked out.</summary>
    public ControlTurn Turn { get; set; } = ControlTurn.Offset;

    /// <summary>What to call it in a list of mappings. Filled in when it is learned.</summary>
    public string Name { get; set; } = "";

    /// <summary>True when this mapping and that message are about each other.</summary>
    public bool Answers(MidiMessage message) =>
        message != null
        && message.Type == MidiMessageType.ControlChange
        && message.Channel == Channel
        && message.Value == Cc;
}
