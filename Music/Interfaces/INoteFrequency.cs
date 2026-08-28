using JingleBox2.Tracker.Records;

namespace JingleBox2.Music.Interfaces;

/// <summary>
/// Concert pitch: tracker semitones to Hz, with C-4 landing on middle C.
/// </summary>
/// <remarks>
/// Read on the audio thread when a note starts, and by the panels that draw a pitch. Pure
/// arithmetic with nothing kept between calls, so both can ask at once.
/// </remarks>
public interface INoteFrequency
{
    /// <summary>The reference the rest of the keyboard is worked out from.</summary>
    double A4Hz { get; }

    /// <summary>A-4 is semitone 57 here, the same note MIDI calls 69.</summary>
    int A4Semitone { get; }

    /// <summary>What a cell's note sounds at, before any tuning the instrument adds.</summary>
    /// <param name="note">The note to sound.</param>
    double Hz(Note note);

    /// <summary>The same for a bare semitone, for anything holding a number rather than a note.</summary>
    /// <param name="semitone">The semitone, counted from C-0.</param>
    double Hz(int semitone);
}
