namespace JingleBox2.Midi.Enums;

/// <summary>What kind of thing a hardware control is pointed at.</summary>
public enum ControlKind
{
    /// <summary>
    /// A parameter on a sound device of ours: a knob on the face of a soundmachine or of a
    /// sound effect.
    /// </summary>
    /// <remarks>
    /// One kind for both, because to a hardware knob they are one thing: a box on the rack with
    /// a face, named by the id its manifest carries and the key the control turns. Which of the
    /// two it is decides where it is looked for, a soundmachine on the track you are on or a
    /// sound effect on that track's chain, and nothing else about a link.
    /// </remarks>
    SoundDevice,

    /// <summary>
    /// A parameter on a plugin in a track's chain.
    /// </summary>
    /// <remarks>
    /// Somebody else's program, which cannot be pointed a hardware control at and never could
    /// be: it brings its own MIDI learn and nothing can make the two agree. What still uses this
    /// is automation, where a lane names a slot on a chain and the song says what that parameter
    /// does over these lines. A link of this kind is dropped as the settings are read.
    ///
    /// It was called Insert, which is the word for the slot rather than for what is standing in
    /// it, and a sound effect of ours was briefly written down under the same word. One word for
    /// two things is how a link comes to be dropped by the code that reads it.
    /// </remarks>
    Plugin,

    /// <summary>Something on a track's mixer strip.</summary>
    Mix,

    /// <summary>
    /// A button on a sound device's face: something to be done rather than a value to be moved.
    /// </summary>
    /// <remarks>
    /// Its own kind rather than a flag on <see cref="SoundDevice"/>, because what a knob writes
    /// is a number and what a button sends is a finger, and the two are reconciled with the
    /// thing they point at by different rules.
    /// </remarks>
    Action,

    /// <summary>
    /// One of the transport's four keys: play, pause, stop or record.
    /// </summary>
    /// <remarks>
    /// The one kind that names neither a track nor a sound device. There is one transport and it
    /// is the same one from every page, so a button pointed at play means play.
    /// </remarks>
    Transport
}
