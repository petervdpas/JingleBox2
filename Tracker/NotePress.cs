using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
public sealed class NotePress : INotePress
{
    /// <inheritdoc/>
    public NoteWant Wants(bool again, int held) =>
        !again ? NoteWant.SoundAndWrite
        : held > 1 ? NoteWant.Nothing
        : NoteWant.Write;
}
