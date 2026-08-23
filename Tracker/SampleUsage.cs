using System;
using System.Collections.Generic;
using System.IO;

namespace JingleBox2.Tracker;

/// <summary>
/// Which instruments a recording is the sound of. A sample instrument is nothing but a file
/// and a base note, so removing the file leaves the instrument silent in every song that
/// plays it. Asking this first is what stops that happening.
/// </summary>
public static class SampleUsage
{
    /// <summary>
    /// True when the instrument plays this file. Paths are compared as the file system would:
    /// the same file reached two ways is still the same file.
    /// </summary>
    public static bool Uses(TrackerInstrument? instrument, string? filePath)
    {
        if (instrument is null || string.IsNullOrWhiteSpace(filePath)) return false;

        string wanted = Normalize(filePath);

        foreach (string path in Files(instrument))
            if (string.Equals(Normalize(path), wanted, PathComparison)) return true;

        return false;
    }

    /// <summary>
    /// Every recording an instrument plays, whichever machine it is.
    /// </summary>
    /// <remarks>
    /// A kit plays sixteen and a map up to thirty-two, and asking only the one an instrument
    /// keeps at the top would say a recording is free when a drum kit is built on it. Which is
    /// how a file gets deleted out from under a song.
    /// </remarks>
    public static IEnumerable<string> Files(TrackerInstrument? instrument)
    {
        if (instrument is null || instrument.IsSynth) yield break;

        if (instrument.Kit != null)
        {
            foreach (string path in instrument.Kit.Files) yield return path;
            yield break;
        }

        if (instrument.Zones != null)
        {
            foreach (string path in instrument.Zones.Files) yield return path;
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(instrument.FilePath)) yield return instrument.FilePath;
    }

    /// <summary>
    /// Points an instrument at a recording's new place, wherever it was playing the old one.
    /// True when something moved.
    /// </summary>
    /// <remarks>
    /// A recording's name is its file name, so renaming one moves it and every instrument
    /// holding the old path goes silent. This is what stops that: the rename and the repointing
    /// are one action, and an instrument never sees the moment in between.
    /// </remarks>
    public static bool Repoint(TrackerInstrument? instrument, string? from, string? to)
    {
        if (instrument is null || instrument.IsSynth) return false;
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) return false;

        string wanted = Normalize(from);
        bool moved = false;

        bool Same(string path) =>
            !string.IsNullOrWhiteSpace(path) && string.Equals(Normalize(path), wanted, PathComparison);

        if (Same(instrument.FilePath))
        {
            instrument.FilePath = to;
            moved = true;
        }

        if (instrument.Kit != null)
        {
            foreach (var pad in instrument.Kit.Pads)
            {
                if (!Same(pad.FilePath)) continue;

                pad.FilePath = to;
                moved = true;
            }
        }

        if (instrument.Zones != null)
        {
            foreach (var zone in instrument.Zones.Zones)
            {
                if (!Same(zone.FilePath)) continue;

                zone.FilePath = to;
                moved = true;
            }
        }

        return moved;
    }

    /// <summary>The names of the instruments that play this file, in the order given.</summary>
    public static IReadOnlyList<string> By(IEnumerable<TrackerInstrument>? instruments, string? filePath)
    {
        if (instruments is null) return Array.Empty<string>();

        var names = new List<string>();

        foreach (var instrument in instruments)
        {
            if (Uses(instrument, filePath))
                names.Add(string.IsNullOrWhiteSpace(instrument.Name) ? "(unnamed)" : instrument.Name);
        }

        return names;
    }

    /// <summary>
    /// The same list as a phrase to put in a sentence. A long list is cut short: the point is
    /// that the recording is spoken for, not to read out the whole library.
    /// </summary>
    public static string Describe(IReadOnlyList<string>? names)
    {
        if (names is null || names.Count == 0) return "";

        if (names.Count == 1) return $"'{names[0]}'";
        if (names.Count == 2) return $"'{names[0]}' and '{names[1]}'";

        int rest = names.Count - 2;
        return $"'{names[0]}', '{names[1]}' and {rest} {(rest == 1 ? "other" : "others")}";
    }

    /// <summary>Windows does not care about case in a path; the others do.</summary>
    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        }
        catch (Exception)
        {
            // An unusable path cannot match a real one, and that is answer enough.
            return path;
        }
    }
}

/// <summary>
/// Asked before a recording is removed. Kept as an interface so the RECORD page can put the
/// question without knowing where instruments are kept.
/// </summary>
public interface ISampleUsage
{
    /// <summary>The names of the instruments that play this file. Empty when it is free.</summary>
    IReadOnlyList<string> InstrumentsUsing(string filePath);

    /// <summary>
    /// Points every instrument playing <paramref name="from"/> at <paramref name="to"/> instead,
    /// and says how many moved. For a recording that has been renamed, which is a recording that
    /// has been moved.
    /// </summary>
    int Repoint(string from, string to);
}
