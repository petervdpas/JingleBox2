namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// What can be done to a stretch of a recording without changing how long it is.
/// </summary>
/// <remarks>
/// Interleaved samples and a range of frames, which is what a WAV holds and what a region
/// dragged out on a picture comes to. Nothing here opens a file, so every one of these can be
/// put a question to with an array and no disc: what a fade really does to the sample at each
/// end, and whether a stereo take comes back with its two channels still the way round they
/// went in, are the two things about this that go wrong in silence.
///
/// **Frames throughout and never samples**, since the two differ by the channel count and
/// confusing them is inaudible as a fault in the ordinary case: a stereo take reversed a sample
/// at a time is the right audio with its left and right swapped, which reads as a stereo image
/// that has quietly turned round rather than as an edit that went wrong.
///
/// None of them changes the length, which is what lets everything else that knows this take go
/// on being right about it. A pad, an instrument or a slice points into a recording by position,
/// so an edit that moved the audio along would silently repoint all three.
///
/// Nothing is refused: a region with no frames in it, a range off either end of the take and an
/// array that is not there all leave the samples exactly as they were. What decides whether
/// somebody is told is the caller, which is the one that knows whether a hand asked for this.
/// </remarks>
public interface IRecordingEdit
{
    /// <summary>Empties the region and leaves the rest of the take where it is.</summary>
    /// <param name="samples">The take's samples, interleaved.</param>
    /// <param name="channels">How many channels they are interleaved across.</param>
    /// <param name="from">The first frame of the region.</param>
    /// <param name="to">One past its last frame.</param>
    void Silence(short[]? samples, int channels, long from, long to);

    /// <summary>Turns the region back to front, frame by frame.</summary>
    /// <remarks>
    /// A frame at a time rather than a sample at a time, so the channels stay the way round they
    /// were. The frames either side of the region are untouched, so reversing part of a take
    /// leaves a join at each end: that is what was asked for, and a reversal of the whole take
    /// is the region being the whole take.
    /// </remarks>
    /// <param name="samples">The take's samples, interleaved.</param>
    /// <param name="channels">How many channels they are interleaved across.</param>
    /// <param name="from">The first frame of the region.</param>
    /// <param name="to">One past its last frame.</param>
    void Reverse(short[]? samples, int channels, long from, long to);

    /// <summary>
    /// Ramps the region evenly from silence, or evenly to it.
    /// </summary>
    /// <remarks>
    /// The two ends are exact rather than nearly: a fade in is silent on the region's first
    /// frame and untouched on its last, and a fade out the other way about. An off-by-one here
    /// leaves a fade that never quite reaches silence, which is a click at the head of a take
    /// somebody put a fade on precisely to be rid of one.
    ///
    /// Straight rather than curved, which is what an editor's plain fade is and what a hand can
    /// predict. A shape somebody wanted instead would be a second fade beside this rather than a
    /// setting on it, since what a curve is worth arguing about is how it sounds.
    /// </remarks>
    /// <param name="samples">The take's samples, interleaved.</param>
    /// <param name="channels">How many channels they are interleaved across.</param>
    /// <param name="from">The first frame of the region.</param>
    /// <param name="to">One past its last frame.</param>
    /// <param name="rising">True to come up from silence, false to go down to it.</param>
    void Fade(short[]? samples, int channels, long from, long to, bool rising);
}
