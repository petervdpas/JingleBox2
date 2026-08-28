using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
public sealed class Slices(IFilePaths? paths = null) : ISlices
{
    /// <summary>How this system decides two names are the same recording.</summary>
    private readonly IFilePaths _paths = paths ?? new FilePaths();

    /// <inheritdoc/>
    public IReadOnlyList<double> PointsFrom(IReadOnlyList<SampleShape?> windows)
    {
        if (windows is null || windows.Count == 0) return Array.Empty<double>();

        var points = new List<double>(windows.Count + 1) { windows[0]?.Start ?? 0 };

        foreach (var window in windows) points.Add(window?.End ?? 1);

        return points;
    }

    /// <inheritdoc/>
    public string OneFile(IEnumerable<string> paths)
    {
        string? first = null;

        foreach (string path in paths)
        {
            if (path.Length == 0) return "";

            if (first is null) first = path;
            else if (!_paths.Same(path, first)) return "";
        }

        return first ?? "";
    }

    /// <inheritdoc/>
    public int CountFor(IReadOnlyList<double>? points, int room) =>
        points is null || points.Count < 2 ? 0 : Math.Min(points.Count - 1, room);

    /// <inheritdoc/>
    public bool Auto(string name, string wasCalled)
    {
        if (name.Length == 0) return true;
        if (wasCalled.Length == 0) return false;
        if (string.Equals(name, wasCalled, StringComparison.Ordinal)) return true;

        if (!name.StartsWith(wasCalled + " ", StringComparison.Ordinal)) return false;

        string tail = name[(wasCalled.Length + 1)..];

        return tail.Length > 0 && tail.All(char.IsDigit);
    }

    /// <inheritdoc/>
    public string NameFor(string filePath, int index)
    {
        string title = Path.GetFileNameWithoutExtension(filePath);

        if (title.Length == 0) title = "Slice";

        return title + " " + (index + 1);
    }
}
