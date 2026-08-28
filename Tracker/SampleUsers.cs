using System;
using System.Collections.Generic;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
public sealed class SampleUsers : ISampleUsers
{
    /// <summary>The places to put the question to, in the order they were given.</summary>
    private readonly IReadOnlyList<ISampleUsage> _asked;

    /// <summary>How this system decides two names are the same recording.</summary>
    private readonly IFilePaths _paths;

    /// <summary>
    /// Takes however many places there are to ask, nulls included.
    /// </summary>
    /// <remarks>
    /// Nulls are dropped rather than refused, because whether there is a songs folder to ask is
    /// something the caller finds out while it is being built, and a missing one should mean one
    /// fewer place to ask rather than a constructor that throws.
    ///
    /// The comparison rule is the one this machine really has, which is what the application
    /// always wants. A test that wants the other one hands it in.
    /// </remarks>
    /// <param name="asked">The places to ask, in the order they should be asked.</param>
    public SampleUsers(params ISampleUsage?[] asked) : this(null, asked)
    {
    }

    /// <summary>
    /// The same, with the path comparison rule handed in rather than read off the machine.
    /// </summary>
    /// <param name="paths">
    /// Which paths count as the same file. Left out, the rule this system really has; given,
    /// what some other system would have decided, which is what a test wants.
    /// </param>
    /// <param name="asked">The places to ask, in the order they should be asked.</param>
    public SampleUsers(IFilePaths? paths, params ISampleUsage?[] asked)
    {
        _paths = paths ?? new FilePaths();

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

    /// <inheritdoc/>
    public bool Uses(TrackerInstrument? instrument, string? filePath)
    {
        if (instrument is null || string.IsNullOrWhiteSpace(filePath)) return false;

        string wanted = Normalize(filePath);

        foreach (string path in Files(instrument))
            if (_paths.Same(Normalize(path), wanted)) return true;

        return false;
    }

    /// <inheritdoc/>
    public IEnumerable<string> Files(TrackerInstrument? instrument)
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

    /// <inheritdoc/>
    public bool Repoint(TrackerInstrument? instrument, string? from, string? to)
    {
        if (instrument is null || instrument.IsSynth) return false;
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) return false;

        string wanted = Normalize(from);
        bool moved = false;

        bool Same(string path) =>
            !string.IsNullOrWhiteSpace(path) && _paths.Same(Normalize(path), wanted);

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

    /// <inheritdoc/>
    public IReadOnlyList<string> By(IEnumerable<TrackerInstrument>? instruments, string? filePath)
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

    /// <inheritdoc/>
    public string Describe(IReadOnlyList<string>? names)
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
    private string Normalize(string path) => _paths.Full(path);
}
