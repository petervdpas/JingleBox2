namespace JingleBox2.Midi.Enums;

/// <summary>What kind of thing a hardware control is pointed at.</summary>
public enum ControlKind
{
    /// <summary>
    /// A parameter on a device of ours: a knob on the face of a soundmachine or of an effect.
    /// </summary>
    /// <remarks>
    /// One kind for both, because to a hardware knob they are one thing: a box on the rack with
    /// a face, named by the id its manifest carries and the key the control turns. Which of the
    /// two it is decides where it is looked for, a machine on the track you are on or an effect
    /// on that track's chain, and nothing else about a link.
    ///
    /// It was called Instrument, which was true while a machine was the only thing here that
    /// could be pointed at. An effect went in as an <see cref="Insert"/> for a while and was
    /// thrown away on every start, since that word means a plugin and a plugin cannot be pointed
    /// at: one word for two things is how a link comes to be dropped by the code that reads it.
    /// The number is unchanged, so every link already in somebody's settings is what it was.
    /// </remarks>
    Device,

    /// <summary>
    /// A parameter on a plugin in a track's insert chain.
    /// </summary>
    /// <remarks>
    /// Somebody else's program, which cannot be pointed a hardware control at and never could
    /// be: it brings its own MIDI learn and nothing can make the two agree. What still uses this
    /// is automation, where a lane names an insert on a chain and the song says what it does over
    /// these lines. A link of this kind is dropped as the settings are read.
    /// </remarks>
    Insert,

    /// <summary>Something on a track's mixer strip.</summary>
    Mix,

    /// <summary>
    /// A button on a device's face: something to be done rather than a value to be moved.
    /// </summary>
    /// <remarks>
    /// Last, so a mapping saved before this existed still reads as the kind it was given.
    /// </remarks>
    Action,

    /// <summary>
    /// One of the transport's four keys: play, pause, stop or record.
    /// </summary>
    /// <remarks>
    /// Last for the same reason <see cref="Action"/> is, and it is the one kind that names
    /// neither a track nor a machine. There is one transport and it is the same one from every
    /// page, so a button pointed at play means play.
    /// </remarks>
    Transport
}
