using System;
using System.Collections.Generic;
using System.IO;
using JingleBox2.Tracker.Interfaces;

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
            if (FilePaths.Same(Normalize(path), wanted)) return true;

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
            !string.IsNullOrWhiteSpace(path) && FilePaths.Same(Normalize(path), wanted);

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

    /// <summary>
    /// A path as it will be compared: resolved, so a name typed by hand and a name off a list
    /// are the same file when they reach the same file.
    /// </summary>
    private static string Normalize(string path) => FilePaths.Full(path);
}

/// <summary>
/// Several places that might be playing a recording, asked as one.
/// </summary>
/// <remarks>
/// There are two: the rack, which is the instruments you own, and the songs folder, which is
/// the instruments your songs own. They are genuinely separate, on purpose, because a song
/// keeps its own copies of what it plays. The recording underneath is the one file, though, so
/// the question RECORD asks before deleting one has to be put to both.
/// </remarks>
public sealed class SampleUsers : ISampleUsage
{
    /// <summary>The places to put the question to, in the order they were given.</summary>
    private readonly IReadOnlyList<ISampleUsage> _asked;

    /// <summary>
    /// Takes however many places there are to ask, nulls included.
    /// </summary>
    /// <remarks>
    /// Nulls are dropped rather than refused, because whether there is a songs folder to ask is
    /// something the caller finds out while it is being built, and a missing one should mean one
    /// fewer place to ask rather than a constructor that throws.
    /// </remarks>
    public SampleUsers(params ISampleUsage?[] asked)
    {
        var list = new List<ISampleUsage>();

        foreach (var one in asked ?? Array.Empty<ISampleUsage?>())
            if (one != null) list.Add(one);

        _asked = list;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Every place asked and the answers run together, so a recording used by an instrument on
    /// the shelf and by two songs comes back as three names. One place throwing is one place
    /// that could not answer: the rest still can, and a question about deleting a file must not
    /// be turned into a crash.
    /// </remarks>
    public IReadOnlyList<string> InstrumentsUsing(string filePath)
    {
        var names = new List<string>();

        foreach (var one in _asked)
        {
            try { names.AddRange(one.InstrumentsUsing(filePath)); }
            catch (Exception) { }
        }

        return names;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Every place is asked, and the counts added. One place throwing leaves the others
    /// repointed, which is better than a rename that half happened and said nothing.
    /// </remarks>
    public int Repoint(string from, string to)
    {
        int moved = 0;

        foreach (var one in _asked)
        {
            try { moved += one.Repoint(from, to); }
            catch (Exception) { }
        }

        return moved;
    }
}
