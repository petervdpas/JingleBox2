using System;
using System.IO;

namespace JingleBox2.Tracker;

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
/// </remarks>
public static class FilePaths
{
    /// <summary>
    /// How this system decides two paths are the same path.
    /// </summary>
    /// <remarks>
    /// By the file system's default, which is what the person typing the name expects. A
    /// Windows volume mounted case sensitive, or a Linux one mounted otherwise, would each
    /// disagree; neither is worth asking the operating system about on every comparison.
    /// </remarks>
    public static StringComparison Comparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    /// <summary>The same answer, for a set, a dictionary or a sort keyed by paths.</summary>
    public static StringComparer Comparer =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    /// <summary>True when those two names are the same file as far as this system is concerned.</summary>
    /// <remarks>
    /// The strings as they stand. Both sides almost always come from the same place, so there is
    /// nothing to resolve; where they do not, <see cref="SameFile"/> is the one to ask.
    /// </remarks>
    public static bool Same(string? left, string? right) =>
        string.Equals(left ?? "", right ?? "", Comparison);

    /// <summary>
    /// True when those two names reach the same file, whichever way each was written.
    /// </summary>
    /// <remarks>
    /// For the two that came from different places: one typed, one off a list, one relative to
    /// wherever the program was started. Resolved first, so a trailing separator or a doubled
    /// one does not make two files out of one.
    /// </remarks>
    public static bool SameFile(string? left, string? right) => Same(Full(left), Full(right));

    /// <summary>The path as it really reads, or as it stands when it cannot be worked out.</summary>
    public static string Full(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "";

        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        }
        catch (Exception)
        {
            // A name with something in it no file can have. It is not a path, so it is only
            // ever equal to itself, which is what handing it back gives.
            return path;
        }
    }
}
