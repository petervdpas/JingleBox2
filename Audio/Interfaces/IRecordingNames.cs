using System.Collections.Generic;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Checks a recording name before it is turned into a file name, and generates the next name in
/// a "base-001" series.
/// </summary>
/// <remarks>
/// Saving overwrites without asking, so a clash has to be caught before the take starts rather
/// than after it. That is the whole reason this is a seam of its own: by the time the recorder
/// has the name, somebody's afternoon is already on the line.
///
/// Two names that differ only in case are treated as a clash on both systems, deliberately.
/// File names are case insensitive on Windows and case sensitive on Linux, and a shelf that
/// behaved differently per system would let a take be made on one machine that cannot be opened
/// on another.
/// </remarks>
public interface IRecordingNames
{
    /// <summary>Why a blank name cannot be used, in the words the page shows.</summary>
    string EmptyMessage { get; }

    /// <summary>Why a name holding a character no file name may hold cannot be used.</summary>
    string InvalidCharsMessage { get; }

    /// <summary>Why a name somebody else's take already has cannot be used.</summary>
    string InUseMessage { get; }

    /// <summary>The series a take falls into when its name says nothing about one.</summary>
    string DefaultBaseName { get; }

    /// <summary>Width of the numeric suffix. Numbers past 999 simply grow past it.</summary>
    int NumberWidth { get; }

    /// <summary>Returns null when the name can be used, otherwise the reason it cannot.</summary>
    /// <param name="name">What somebody typed, which is trimmed before it is judged.</param>
    /// <param name="existingNames">The takes already on the shelf.</param>
    string? Validate(string? name, IEnumerable<string> existingNames);

    /// <summary>
    /// The series a name belongs to: lowercased, with any "-001" style suffix removed, so
    /// "Jingle-004" and "jingle" both belong to the series "jingle".
    /// </summary>
    /// <param name="name">A take's name, or null.</param>
    string BaseNameOf(string? name);

    /// <summary>
    /// The next free name in the series that name belongs to, one past the highest number
    /// already taken.
    /// </summary>
    /// <remarks>
    /// Numbers are not reused after a delete, so a name never points at two different takes
    /// over a session. A name in the series carrying no number still occupies the place its
    /// number would map to, so the search walks upwards until it finds one nothing answers to.
    /// </remarks>
    /// <param name="baseName">Any name in the series, numbered or not.</param>
    /// <param name="existingNames">The takes already on the shelf.</param>
    string NextName(string? baseName, IEnumerable<string> existingNames);
}
