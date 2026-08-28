namespace JingleBox2.Midi.Enums;

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
