using JingleBox2.Tracker.Records;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// One half of the application, as far as a key on a keyboard is concerned.
/// </summary>
/// <remarks>
/// The rack and the pattern both take notes and neither knows about the other. This is what
/// they have in common, and it exists so the thing that chooses between them can be put a
/// question to without a window: which half a key went to, and whether its release followed it.
/// </remarks>
public interface IPlaysNotes
{
    /// <summary>Sounds that note, and writes it down if this half writes anything down.</summary>
    void PlayMidiNote(Note note, int volume);

    /// <summary>
    /// The key came up.
    /// </summary>
    /// <remarks>
    /// Sent to both halves even where there is nothing to write: a key coming up is also the
    /// moment a light goes out and a sound is let go of. Dropped for the rack once, on the
    /// grounds that a note-off has nothing to be written into there, which was true and beside
    /// the point, and left the two halves of one key press going to different places.
    /// </remarks>
    void ReleaseMidiNote(Note note);
}
