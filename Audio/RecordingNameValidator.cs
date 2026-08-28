using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace JingleBox2.Audio;

/// <summary>
/// Checks a recording name before it is turned into a file name, and generates the next
/// name in a "base-001" series. Saving overwrites without asking, so a clash has to be
/// caught before the take starts rather than after it.
/// </summary>
public static class RecordingNameValidator
{
    /// <summary>Why a blank name cannot be used, in the words the page shows.</summary>
    public const string EmptyMessage = "Enter a name for the recording.";

    /// <summary>Why a name holding a character no file name may hold cannot be used.</summary>
    public const string InvalidCharsMessage = "That name cannot be used as a file name.";

    /// <summary>Why a name somebody else's take already has cannot be used.</summary>
    public const string InUseMessage = "A recording with this name already exists.";

    /// <summary>The series a take falls into when its name says nothing about one.</summary>
    public const string DefaultBaseName = "recording";

    /// <summary>Width of the numeric suffix. Numbers past 999 simply grow past it.</summary>
    public const int NumberWidth = 3;

    private static readonly Regex NumberedName = new(@"^(?<base>.*?)-(?<number>\d+)$", RegexOptions.Compiled);

    /// <summary>Returns null when the name can be used, otherwise the reason it cannot.</summary>
    /// <param name="name">What somebody typed, which is trimmed before it is judged.</param>
    /// <param name="existingNames">The takes already on the shelf.</param>
    public static string? Validate(string? name, IEnumerable<string> existingNames)
    {
        string trimmed = (name ?? string.Empty).Trim();

        if (trimmed.Length == 0)
            return EmptyMessage;

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return InvalidCharsMessage;

        return existingNames.Any(n => Matches(n, trimmed)) ? InUseMessage : null;
    }

    /// <summary>
    /// The series a name belongs to: lowercased, with any "-001" style suffix removed.
    /// "Jingle-004" and "jingle" both belong to the series "jingle".
    /// </summary>
    /// <param name="name">A take's name, or null.</param>
    public static string BaseNameOf(string? name)
    {
        string trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return DefaultBaseName;

        var match = NumberedName.Match(trimmed);
        string baseName = match.Success ? match.Groups["base"].Value : trimmed;

        baseName = baseName.Trim();
        return baseName.Length == 0 ? DefaultBaseName : baseName.ToLowerInvariant();
    }

    /// <summary>
    /// The next free name in the series <paramref name="baseName"/> belongs to, one past the
    /// highest number already taken. Numbers are not reused after a delete, so a name never
    /// points at two different takes over a session.
    /// </summary>
    /// <remarks>
    /// A name in the series carrying no number still occupies the place its number would map to,
    /// so the search walks upwards until it finds one nothing answers to.
    /// </remarks>
    /// <param name="baseName">Any name in the series, numbered or not.</param>
    /// <param name="existingNames">The takes already on the shelf.</param>
    public static string NextName(string? baseName, IEnumerable<string> existingNames)
    {
        string series = BaseNameOf(baseName);
        var taken = existingNames as IList<string> ?? existingNames.ToList();

        int highest = 0;
        foreach (var name in taken)
        {
            var match = NumberedName.Match(name.Trim());
            if (!match.Success) continue;
            if (!string.Equals(match.Groups["base"].Value.Trim(), series, StringComparison.OrdinalIgnoreCase)) continue;
            if (!int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int number)) continue;

            if (number > highest) highest = number;
        }

        string candidate = Compose(series, highest + 1);
        while (taken.Any(n => Matches(n, candidate)))
        {
            highest++;
            candidate = Compose(series, highest + 1);
        }

        return candidate;
    }

    /// <summary>A series and a number as the name they make, with the number padded.</summary>
    private static string Compose(string series, int number) =>
        $"{series}-{number.ToString(CultureInfo.InvariantCulture).PadLeft(NumberWidth, '0')}";

    /// <summary>Whether two names would be the same file.</summary>
    /// <remarks>
    /// File names are case insensitive on Windows and case sensitive on Linux, so a difference of
    /// case is treated as a clash on both and a profile cannot behave differently per system.
    /// </remarks>
    private static bool Matches(string a, string b) =>
        string.Equals(a.Trim(), b, StringComparison.OrdinalIgnoreCase);
}
