namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// The tempo a MIDI clock is running at, worked out from the moments its ticks arrive.
/// </summary>
/// <remarks>
/// A clock says nothing about its tempo: twenty four ticks to the quarter note is all there is,
/// so the tempo is how far apart they are. A straight line is fitted through the last
/// <c>Window</c> ticks rather than the first and last being subtracted, because a tick off a USB
/// port arrives a millisecond or so either side of where it was sent, and a line through all of
/// them is that wobble averaged away where two ends would carry it whole.
///
/// Said to a tenth, and only when it has moved by more than the wobble could, so a device standing
/// at 108 is heard as 108 once rather than as 108.0, 108.1 and 107.9 in turn. A gap longer than a
/// second, a moment earlier than the last, or a rate past what a song can have starts again: those
/// are a stopped clock, a confused one, or not a clock at all.
/// </remarks>
public interface IClockTempo
{
    /// <summary>
    /// Takes one tick's moment and answers a tempo when there is a new one to say, or null.
    /// </summary>
    /// <param name="timestamp">When the tick arrived, in the stopwatch ticks the instance was made with.</param>
    double? Heard(long timestamp);

    /// <summary>Forgets every tick and the last tempo said, for a clock that has just started again.</summary>
    void Forget();
}
