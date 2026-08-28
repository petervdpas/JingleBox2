using System;

namespace JingleBox2.Files.Interfaces;

/// <summary>
/// Whether two paths are the same file, by the rules of the disc rather than of the language.
/// </summary>
/// <remarks>
/// Windows treats two paths that differ only in case as one path, and Linux does not, so there
/// is no one right answer and no answer that can be left to <c>==</c>. Asked with an exact
/// comparison, a program on Windows decides that a recording it is already holding is a
/// different recording, and quietly does the work twice or refuses to notice they are the same.
/// None of that shows up as an error. It shows up as a chop that stops being a chop because two
/// of its pieces were spelled differently, or a picture drawn again on every keystroke.
///
/// One place, because the answer is a fact about the machine the program is running on and not
/// about whatever is asking. Anything comparing, sorting or keying by a path goes through here.
///
/// A seam rather than a static class, and this is the one whose reason is easy to miss: the
/// rule reads the operating system, so a program running on Linux cannot ask what Windows would
/// have decided, and the half of this application that keys recordings by path is exactly the
/// half where getting that wrong is silent. Handed the rule instead of reading it, both answers
/// can be put a question to on either machine, which is what the two platform-specific tests in
/// the suite were standing in for.
/// </remarks>
public interface IFilePaths
{
    /// <summary>
    /// How this system decides two paths are the same path.
    /// </summary>
    /// <remarks>
    /// By the file system's default, which is what the person typing the name expects. A
    /// Windows volume mounted case sensitive, or a Linux one mounted otherwise, would each
    /// disagree; neither is worth asking the operating system about on every comparison.
    /// </remarks>
    StringComparison Comparison { get; }

    /// <summary>The same answer, for a set, a dictionary or a sort keyed by paths.</summary>
    StringComparer Comparer { get; }

    /// <summary>True when those two names are the same file as far as this system is concerned.</summary>
    /// <remarks>
    /// The strings as they stand. Both sides almost always come from the same place, so there is
    /// nothing to resolve; where they do not, <see cref="SameFile"/> is the one to ask.
    /// </remarks>
    /// <param name="left">One of the two names. Nothing reads as the empty name.</param>
    /// <param name="right">The other.</param>
    bool Same(string? left, string? right);

    /// <summary>
    /// True when those two names reach the same file, whichever way each was written.
    /// </summary>
    /// <remarks>
    /// For the two that came from different places: one typed, one off a list, one relative to
    /// wherever the program was started. Resolved first, so a trailing separator or a doubled
    /// one does not make two files out of one.
    /// </remarks>
    /// <param name="left">One of the two names.</param>
    /// <param name="right">The other.</param>
    bool SameFile(string? left, string? right);

    /// <summary>The path as it really reads, or as it stands when it cannot be worked out.</summary>
    /// <remarks>
    /// A name with a character in it that no file can have is not a path at all, so there is
    /// nothing to resolve. Handed back as it stands, which makes it equal to itself and to
    /// nothing else, which is the only honest answer available.
    /// </remarks>
    /// <param name="path">The name to resolve. Nothing and the empty name both read as empty.</param>
    string Full(string? path);
}
