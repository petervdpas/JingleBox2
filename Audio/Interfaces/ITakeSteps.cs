using JingleBox2.Audio.Records;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Does one written-down step to a take on the disc.
/// </summary>
/// <remarks>
/// The one place that turns a step into a call, so the tool that asks for an edit and the replay
/// that does it again both go through the same line. Written twice they would drift, and the way
/// that fails is an undo that leaves a take subtly unlike the one it was.
/// </remarks>
public interface ITakeSteps
{
    /// <summary>
    /// Does the step to the file at that path, which it rewrites where anything changed.
    /// </summary>
    /// <param name="step">What to do.</param>
    /// <param name="path">The take to do it to, which is the working copy rather than the shelf.</param>
    /// <returns>
    /// True where the file really changed. A normalize of a take already on its peak answers
    /// false, since a step nothing came of is a line in the history that undoes nothing.
    /// </returns>
    bool Run(TakeStep step, string path);
}
