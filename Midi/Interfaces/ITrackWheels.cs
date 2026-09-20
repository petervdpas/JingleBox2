namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// Where a wheel goes once a track has claimed it by its MIDI in.
/// </summary>
/// <remarks>
/// Beside <see cref="IWheels"/> exactly as <see cref="ITrackNotes"/> is beside
/// <see cref="INoteTrigger"/>, and for the same reason: a keyboard's wheel is about the track
/// the cursor is on, and a wheel arriving on a port and channel a track listens to is about
/// that track, whatever the cursor is doing.
///
/// A sequencer feeding four tracks is four hands, so each track keeps its own lean and its own
/// amount and one of them moving says nothing about the others.
///
/// Called on the MIDI thread; whoever implements it owns getting to its own thread.
/// </remarks>
public interface ITrackWheels
{
    /// <summary>The pitch wheel on a port this track listens to.</summary>
    /// <param name="track">The track, counted from nought.</param>
    /// <param name="lean">Where the wheel is, -1 to 1.</param>
    void BendTrack(int track, double lean);

    /// <summary>And the modulation wheel.</summary>
    /// <param name="track">The track, counted from nought.</param>
    /// <param name="amount">How far up the wheel is, 0 to 1.</param>
    void ModulateTrack(int track, double amount);
}
