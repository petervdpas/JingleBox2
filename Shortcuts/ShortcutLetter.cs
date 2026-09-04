using System;
using JingleBox2.Shortcuts.Interfaces;

namespace JingleBox2.Shortcuts;

/// <inheritdoc/>
public sealed class ShortcutLetter : IShortcutLetter
{
    /// <summary>What every page shortcut begins with, which is the whole of the form.</summary>
    private const string Held = "Ctrl+Alt+";

    /// <inheritdoc/>
    /// <remarks>
    /// Anything that is not exactly that form and one character is answered with nothing rather
    /// than being taken apart further. Nothing else can be a page shortcut, so a keystroke of
    /// another shape here means the caller asked about a key that is not one of these.
    /// </remarks>
    public int In(string? word, string? keys)
    {
        if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(keys)) return -1;

        if (!keys.StartsWith(Held, StringComparison.Ordinal)) return -1;

        string letter = keys[Held.Length..];

        if (letter.Length != 1) return -1;

        return word.IndexOf(letter, StringComparison.OrdinalIgnoreCase);
    }
}
