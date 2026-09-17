namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// How close together two notes have to be struck to be one chord rather than two notes.
/// </summary>
/// <remarks>
/// Only the transport running needs this. Stopped, the keys held together are the chord, because
/// there is no clock and the hand is the only thing that can say. Running, a note's line is the
/// one nearest the moment it was struck, and the moment nearest a line is settled at the half way
/// point between two of them, so a chord whose notes are a few milliseconds apart falls on either
/// side of that point whenever the hand happens to land there: at a hundred and twenty to the
/// minute and four lines to the beat a line is 125 ms, so a chord spread over 20 ms would be
/// split about one time in six. A chord written across two lines is not the chord that was
/// played, and quantising afterwards cannot put it back, since by then the two halves are notes
/// on two lines like any other.
///
/// So the notes struck within the window of the first one share its line whatever line each of
/// them is nearest, and everything else goes on its own. Anchored on the first rather than on the
/// one before it, or a run of notes each inside the window of the last would chain onto one line
/// for as long as somebody kept playing.
///
/// A rule with no song and no keyboard in it, so what makes a chord can be put a question to
/// without either.
/// </remarks>
public interface IChordWindow
{
    /// <summary>Whether the second was struck closely enough after the first to share its line.</summary>
    /// <param name="first">When the note that has the line arrived, in stopwatch ticks.</param>
    /// <param name="second">When this note arrived, in the same ticks.</param>
    /// <param name="line">How long a line is in those ticks, or nought where that is not known.</param>
    bool Together(long first, long second, long line);
}
