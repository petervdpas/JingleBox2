namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// Writes a change of tempo on one line into a pattern's tempo lane, which is what typing a
/// tempo into the pattern does.
/// </summary>
/// <remarks>
/// Tempo lives in one place, the tempo lane on the song's master, and typing <c>T</c> with two
/// digits is a quicker way of putting a point in it rather than a second place tempo is kept.
/// What is typed means from this line on, so it is written as a step: the lane holds what it
/// already said up to the line before, and the new tempo from the line itself. A lane that does
/// not exist yet is made, starting at the song's own tempo, so the lines above the change play as
/// they always did.
/// </remarks>
public interface ITempoSteps
{
    /// <summary>Writes the step, making the lane where there is none.</summary>
    /// <param name="pattern">The pattern being edited.</param>
    /// <param name="line">The line the new tempo starts on.</param>
    /// <param name="bpm">The new tempo, held to what a song allows.</param>
    /// <param name="songBpm">The song's own tempo, which a new lane starts at.</param>
    void Step(Pattern pattern, int line, double bpm, double songBpm);
}
