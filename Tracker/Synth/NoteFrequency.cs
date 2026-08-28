using System;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// Concert pitch: tracker semitones to Hz, with C-4 landing on middle C.
/// </summary>
/// <remarks>
/// Read on the audio thread when a note starts, and by the panels that draw a pitch. Pure
/// arithmetic with nothing kept between calls, so both can ask at once.
/// </remarks>
public static class NoteFrequency
{
    /// <summary>The reference the rest of the keyboard is worked out from.</summary>
    public const double A4Hz = 440.0;

    /// <summary>A-4 is semitone 57 here, the same note MIDI calls 69.</summary>
    public const int A4Semitone = 57;

    /// <summary>What a cell's note sounds at, before any tuning the instrument adds.</summary>
    public static double Hz(Note note) => Hz(note.Semitone);

    /// <summary>The same for a bare semitone, for anything holding a number rather than a note.</summary>
    public static double Hz(int semitone) => A4Hz * Math.Pow(2.0, (semitone - A4Semitone) / 12.0);

}
