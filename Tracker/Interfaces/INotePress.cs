using JingleBox2.Tracker.Enums;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// What a note key means, given the keys already down.
/// </summary>
/// <remarks>
/// The letter rows repeat while a key is held, and that is wanted: it is how a column is filled
/// quickly and stays filled. What the repeat must not do is sound the note again, because the
/// note it would sound is already sounding, and what it must not do while a chord is under the
/// hand is write at all, because there the repeat is a hand resting rather than somebody filling
/// a column.
///
/// Those are three different answers and they were one call, which is what let a held key stack
/// up voices: measured in a log at one voice to forty eight in two seconds, each alive for ten,
/// summing to four times full scale into the master's saturation and taking the collector with
/// it. The machine was not struggling either side of it.
///
/// Hardware never reaches any of this, since a key that is down cannot be pressed again; it is
/// the computer keyboard's own repeat and nothing else.
///
/// A rule with no view model in it, so what a press means can be put a question to without a
/// song, a pattern or a keyboard.
/// </remarks>
public interface INotePress
{
    /// <summary>What this press asks for.</summary>
    /// <param name="again">Whether that same note is already being held.</param>
    /// <param name="held">How many notes are being held, this one included when it is.</param>
    NoteWant Wants(bool again, int held);
}
