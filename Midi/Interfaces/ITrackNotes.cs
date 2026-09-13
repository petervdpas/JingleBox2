using JingleBox2.Tracker.Records;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// Where a note goes once a track has claimed it by its MIDI in.
/// </summary>
/// <remarks>
/// Beside <see cref="INoteTrigger"/> rather than inside it, because the question answered is a
/// different one: a keyboard's note goes to the track the cursor is on, and a note on a channel
/// a track listens to goes to that track, whatever the cursor is doing. Both halves of a key are
/// here for the same reason they are there.
///
/// Called on the MIDI thread; whoever implements it owns getting to its own thread.
/// </remarks>
public interface ITrackNotes
{
    /// <summary>A key went down on a channel this track listens to.</summary>
    /// <param name="track">The track, counted from nought.</param>
    /// <param name="note">The note.</param>
    /// <param name="volume">The velocity, as the volume column holds it.</param>
    void PressOnTrack(int track, Note note, int volume);

    /// <summary>That key came up.</summary>
    /// <param name="track">The track, counted from nought.</param>
    /// <param name="note">The note.</param>
    void ReleaseOnTrack(int track, Note note);
}
