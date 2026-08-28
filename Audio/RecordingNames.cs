using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class RecordingNames : IRecordingNames
{
    /// <inheritdoc cref="IRecordingNames.EmptyMessage"/>
    public const string EmptyMessage = "Enter a name for the recording.";

    /// <inheritdoc cref="IRecordingNames.InvalidCharsMessage"/>
    public const string InvalidCharsMessage = "That name cannot be used as a file name.";

    /// <inheritdoc cref="IRecordingNames.InUseMessage"/>
    public const string InUseMessage = "A recording with this name already exists.";

    /// <inheritdoc cref="IRecordingNames.DefaultBaseName"/>
    public const string DefaultBaseName = "recording";

    /// <inheritdoc cref="IRecordingNames.NumberWidth"/>
    public const int NumberWidth = 3;

    /// <inheritdoc/>
    string IRecordingNames.EmptyMessage => EmptyMessage;

    /// <inheritdoc/>
    string IRecordingNames.InvalidCharsMessage => InvalidCharsMessage;

    /// <inheritdoc/>
    string IRecordingNames.InUseMessage => InUseMessage;

    /// <inheritdoc/>
    string IRecordingNames.DefaultBaseName => DefaultBaseName;

    /// <inheritdoc/>
    int IRecordingNames.NumberWidth => NumberWidth;

    private static readonly Regex NumberedName = new(@"^(?<base>.*?)-(?<number>\d+)$", RegexOptions.Compiled);

    /// <inheritdoc/>
    public string? Validate(string? name, IEnumerable<string> existingNames)
    {
        string trimmed = (name ?? string.Empty).Trim();

        if (trimmed.Length == 0)
            return EmptyMessage;

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return InvalidCharsMessage;

        return existingNames.Any(n => Matches(n, trimmed)) ? InUseMessage : null;
    }

    /// <inheritdoc/>
    public string BaseNameOf(string? name)
    {
        string trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return DefaultBaseName;

        var match = NumberedName.Match(trimmed);
        string baseName = match.Success ? match.Groups["base"].Value : trimmed;

        baseName = baseName.Trim();
        return baseName.Length == 0 ? DefaultBaseName : baseName.ToLowerInvariant();
    }

    /// <inheritdoc/>
    public string NextName(string? baseName, IEnumerable<string> existingNames)
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
