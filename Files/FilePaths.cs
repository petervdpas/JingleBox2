using JingleBox2.Files.Interfaces;
using System;
using System.IO;

namespace JingleBox2.Files;

/// <inheritdoc/>
/// <remarks>
/// The rule is taken once, when one of these is made, rather than asked of the operating system
/// on every comparison: it cannot change while the program runs, and paths are compared in the
/// inner loop of anything that keys by them.
/// </remarks>
public sealed class FilePaths : IFilePaths
{
    /// <summary>
    /// Reads the rule off the machine this is running on, or takes the one it is given.
    /// </summary>
    /// <param name="comparison">
    /// Which rule to use. Left out, the one this system really has, which is what the
    /// application always wants; given, what some other system would have decided, which is
    /// what a test wants and is the whole reason this is not a static class.
    /// </param>
    public FilePaths(StringComparison? comparison = null)
    {
        Comparison = comparison ?? (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);

        Comparer = Comparison == StringComparison.OrdinalIgnoreCase
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    /// <inheritdoc/>
    public StringComparison Comparison { get; }

    /// <inheritdoc/>
    public StringComparer Comparer { get; }

    /// <inheritdoc/>
    public bool Same(string? left, string? right) =>
        string.Equals(left ?? "", right ?? "", Comparison);

    /// <inheritdoc/>
    public bool SameFile(string? left, string? right) => Same(Full(left), Full(right));

    /// <inheritdoc/>
    public string Full(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "";

        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        }
        catch (Exception)
        {
            return path;
        }
    }
}
