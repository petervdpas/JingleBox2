using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// Counts how many times a slot's pattern and its stretch of lines have been played, and says
/// where the song goes back to when a repeat is not finished.
/// </summary>
/// <remarks>
/// Asked by the clock after every line it plays. Nothing means the song goes on the way it always
/// does, so a song with no repeats set plays exactly as before. The counts belong to one run of the
/// transport and start again whenever a slot is finished, so coming round to the same slot again,
/// by the song looping, repeats it again. See <see cref="SlotRepeat"/> for what a slot can say.
///
/// Playing the song, a slot's pattern repeat comes round at the slot's last line, which is where
/// its break stops when it has one. Where the song goes once the slot is finished is the clock's
/// business, which reads the break the same way.
/// </remarks>
public interface ISlotPasses
{
    /// <summary>Where the song goes after a line, where a repeat says so; nothing to go on as usual.</summary>
    /// <param name="song">The song being played.</param>
    /// <param name="played">The line just played.</param>
    /// <param name="wholePattern">
    /// Whether the slot's own count applies: true playing the song, false playing one pattern
    /// round, where the pattern goes round anyway and only the stretch is counted.
    /// </param>
    TrackerPosition? After(Song song, TrackerPosition played, bool wholePattern);

    /// <summary>Starts every count again, for a fresh run of the transport.</summary>
    void Reset();
}
