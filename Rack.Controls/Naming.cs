using System;
using JingleBox2.Rack.Controls.Interfaces;

namespace JingleBox2.Rack.Controls;

/// <inheritdoc/>
public sealed class Naming : INaming
{
    /// <summary>
    /// The words a panel prints in capitals whatever else is done to them.
    /// </summary>
    /// <remarks>
    /// Spelled out rather than guessed at from the length or the vowels. The set is small, it
    /// is the set this application actually uses, and a rule clever enough to find them would
    /// also find words that are not acronyms at all.
    /// </remarks>
    private static readonly string[] Acronyms = { "LFO", "VCO", "VCF", "VCA", "EG", "PW" };

    /// <inheritdoc/>
    public string Of(object? value)
    {
        string raw = value?.ToString() ?? "";
        if (raw.Length == 0) return raw;

        foreach (var acronym in Acronyms)
        {
            if (string.Equals(raw, acronym, StringComparison.OrdinalIgnoreCase)) return acronym;
        }

        var text = new System.Text.StringBuilder(raw.Length + 4);

        for (int index = 0; index < raw.Length; index++)
        {
            char letter = raw[index];

            if (index > 0 && char.IsUpper(letter))
            {
                text.Append(' ');
                text.Append(char.ToLowerInvariant(letter));
            }
            else
            {
                text.Append(letter);
            }
        }

        return text.ToString();
    }
}
