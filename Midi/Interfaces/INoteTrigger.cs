using JingleBox2.Tracker;
using JingleBox2.Midi;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// Where a key on a keyboard comes out, once it is a note rather than three bytes.
/// </summary>
/// <remarks>
/// The seam between the wire and the music. Everything above it deals in a
/// <see cref="Note"/> and a volume and has never heard of a status byte; everything below it is
/// the wire and has never heard of a track. That is what lets the whole path from a raw buffer
/// to a note being played be put a question to with no port, no window and no hand: see
/// <c>Tests/NotePathTests.cs</c>.
///
/// Both halves of a press are here, and they are here because a press is one thing with two
/// halves. Anything that hears the first and not the second is a place a note can hang.
/// <see cref="MidiMonitor"/> stands in front of an implementation of this and passes every note
/// on untouched, which is how a drawn keyboard knows what is down without owning the notes.
/// </remarks>
public interface INoteTrigger
{
    /// <summary>A key going down, at that velocity.</summary>
    void TriggerNote(Note note, int volume);

    /// <summary>A key coming up. Whether that writes anything is not this end's business.</summary>
    void ReleaseNote(Note note);
}
