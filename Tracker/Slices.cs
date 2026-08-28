using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace JingleBox2.Tracker;

/// <summary>
/// The parts of holding a sliced recording that a kit and a map do identically.
/// </summary>
/// <remarks>
/// A kit and a map disagree about where a piece goes: one puts it on a fixed key, the other
/// hands it a stretch of keyboard. They agree about everything else, and this is that
/// everything else. Reading the cuts back off the pieces matters most, because it is what
/// keeps the cuts from being stored twice: the pieces are where they live, and there is no
/// second copy to fall out of step with them.
/// </remarks>
public static class Slices
{
    /// <summary>
    /// Where the recording was cut, read off the windows of the pieces. One more point than
    /// there are pieces: the first is where the sliced region starts, the last where it ends.
    /// </summary>
    public static IReadOnlyList<double> PointsFrom(IReadOnlyList<SampleShape?> windows)
    {
        if (windows is null || windows.Count == 0) return Array.Empty<double>();

        var points = new List<double>(windows.Count + 1) { windows[0]?.Start ?? 0 };

        foreach (var window in windows) points.Add(window?.End ?? 1);

        return points;
    }

    /// <summary>
    /// The one recording a set of pieces came from, or empty when they do not agree on one.
    /// </summary>
    public static string OneFile(IEnumerable<string> paths)
    {
        string? first = null;

        foreach (string path in paths)
        {
            if (path.Length == 0) return "";

            if (first is null) first = path;
            else if (!FilePaths.Same(path, first)) return "";
        }

        return first ?? "";
    }

    /// <summary>How many pieces those points describe, never more than there is room for.</summary>
    public static int CountFor(IReadOnlyList<double>? points, int room) =>
        points is null || points.Count < 2 ? 0 : Math.Min(points.Count - 1, room);

    /// <summary>
    /// True when that name is one the app gave the piece rather than one somebody typed.
    /// </summary>
    /// <remarks>
    /// Either the recording's own name, or the recording's name and which piece of it this is,
    /// which is what a chop calls its pieces. Both are the app talking to itself, and both
    /// should be replaced when another take lands. Anything else is yours and is kept.
    ///
    /// A piece's name is the take's name, a space, and a number: "Countdown 3". That shape is
    /// what the tail is measured against, so a take called "Countdown" and a piece somebody
    /// renamed "Countdown intro" are told apart.
    /// </remarks>
    public static bool Auto(string name, string wasCalled)
    {
        if (name.Length == 0) return true;
        if (wasCalled.Length == 0) return false;
        if (string.Equals(name, wasCalled, StringComparison.Ordinal)) return true;

        if (!name.StartsWith(wasCalled + " ", StringComparison.Ordinal)) return false;

        string tail = name[(wasCalled.Length + 1)..];

        return tail.Length > 0 && tail.All(char.IsDigit);
    }

    /// <summary>What a piece is called: the recording's name and which piece of it this is.</summary>
    public static string NameFor(string filePath, int index)
    {
        string title = Path.GetFileNameWithoutExtension(filePath);

        if (title.Length == 0) title = "Slice";

        return title + " " + (index + 1);
    }
}
