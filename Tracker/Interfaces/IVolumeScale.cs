using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// What a number in the volume column means, and how a song written on the old scale is read.
/// </summary>
/// <remarks>
/// The column holds 0 to <see cref="TrackerCell.MaxVolume"/>, which is 0x00 to 0x80, so a MIDI
/// velocity is written into it unchanged and what the pattern shows is the number the keyboard
/// sent. It was 0 to 64 for as long as this had a pattern at all, which is FastTracker's scale
/// and halves the 128 steps MIDI has: two keys struck a little apart wrote the same number, and
/// a full hit read 40, which is not a number anybody reads as loud.
///
/// So every song written before that widening holds volumes on the old scale, and a 40 in one
/// of them means full rather than half. Reading one is doubling every volume column and every
/// V command, which is exact rather than a rescaling: the old scale is precisely half of this
/// one, so nothing is rounded and nothing is lost. A song brought across is what it always
/// sounded like.
///
/// The V command goes with the column deliberately. The two set the same thing and the effect
/// wins where both are present, so leaving one on each scale would mean 40 being full in one
/// column of a cell and half in the next.
/// </remarks>
public interface IVolumeScale
{
    /// <summary>One volume column written on the old scale, as this one holds it.</summary>
    /// <remarks>
    /// A blank column stays blank. Anything past the old full is held at this one's full, since
    /// it already played at full: the old reading clamped its gain to one.
    /// </remarks>
    /// <param name="volume">The number as the old file holds it, or <see cref="TrackerCell.NoVolume"/>.</param>
    int Widen(int volume);

    /// <summary>One cell, its volume column and its V command both.</summary>
    /// <param name="cell">The cell as the old file holds it.</param>
    TrackerCell Widen(TrackerCell cell);

    /// <summary>Every cell of every pattern, in place.</summary>
    /// <remarks>
    /// The song rather than the pattern, because a song is what is read off disc and no pattern
    /// in it can be on a different scale from the rest.
    /// </remarks>
    /// <param name="song">The song just read, which is changed where it stands.</param>
    void Widen(Song song);
}
