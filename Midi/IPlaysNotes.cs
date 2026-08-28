using JingleBox2.Tracker;

namespace JingleBox2.Midi;

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
    void PlayMidiNote(Note note, int volume);

    void ReleaseMidiNote(Note note);
}
