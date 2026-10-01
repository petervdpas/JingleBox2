namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// Writes a change of tempo at one place in the song into the song's tempo lane, which is what
/// typing a tempo into the pattern does.
/// </summary>
/// <remarks>
/// Tempo lives in one place, the song's tempo lane, which runs along the order, and typing
/// <c>T</c> with two digits is a quicker way of putting a point in it rather than a second place
/// tempo is kept. What is typed means from this line on, so it is written as a step: the lane holds
/// what it already said up to the line before, and the new tempo from the line itself. A song with
/// no lane yet is given one, starting at the song's own tempo, so everything before the change
/// plays as it always did.
/// </remarks>
public interface ITempoSteps
{
    /// <summary>Writes the step, giving the song a lane where it has none.</summary>
    /// <param name="song">The song.</param>
    /// <param name="line">Where the new tempo starts, along the order, as <see cref="Song.LineOf"/> counts.</param>
    /// <param name="bpm">The new tempo, held to what a song allows.</param>
    void Step(Song song, int line, double bpm);
}
