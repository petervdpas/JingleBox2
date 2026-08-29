namespace JingleBox2.Tracker.Enums;

/// <summary>
/// What becomes of the note a track is still sounding when the next one lands on it.
/// </summary>
/// <remarks>
/// A tracker plays one note per track and has to decide what to do with the one before it. The
/// three answers are Impulse Tracker's, minus the fourth it had: a fade needs a rate, and no
/// patch here carries one. They are a fact about the sound rather than about the track, so the
/// choice lives on <see cref="TrackerInstrument.NewNoteAction"/>: a piano overlaps and a bass
/// does not, whichever track either is played on.
///
/// The numbers are written into every song that sets one, so they do not move.
///
/// A note arriving where the same note is already sounding is cut whichever of the three is
/// chosen. Two copies of one note are a retrigger everywhere else in music, and letting them
/// pile up is how a held chord walks into the voice limit and starts stealing.
/// </remarks>
public enum VoiceEnding
{
    /// <summary>A short fade, so the next note starts on silence. What a tracker has always done.</summary>
    Cut = 0,

    /// <summary>The patch's own release, so a tail carries on under the note that follows it.</summary>
    Release = 1,

    /// <summary>Nothing at all: it holds until an OFF, the transport stops, or the same note returns.</summary>
    Sustain = 2
}
