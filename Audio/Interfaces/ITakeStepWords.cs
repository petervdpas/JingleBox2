using JingleBox2.Audio.Enums;

namespace JingleBox2.Audio.Interfaces;

/// <summary>What a step is called where somebody has to read a list of them.</summary>
/// <remarks>
/// One place, because the same word is wanted on the tool, in the history and in the line that
/// says what just happened, and three spellings of it would eventually disagree about which of
/// them is the real name of the thing.
/// </remarks>
public interface ITakeStepWords
{
    /// <summary>The name of an edit, as it is written on the tool that does it.</summary>
    /// <param name="kind">Which edit.</param>
    /// <returns>Its name, which is never blank.</returns>
    string For(TakeEditKind kind);
}
