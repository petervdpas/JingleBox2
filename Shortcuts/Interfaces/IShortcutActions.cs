using System.Collections.Generic;
using JingleBox2.Shortcuts.Enums;

namespace JingleBox2.Shortcuts.Interfaces;

/// <summary>
/// What each shortcut is called and what it is on by default.
/// </summary>
/// <remarks>
/// Written out rather than derived, because both halves are things somebody chose: the wording
/// on the settings page, and which key it starts on. A settings page builds itself from this,
/// the way the log's page builds itself from the areas the log knows about, so a shortcut added
/// here turns up there without anybody being told to add it.
/// </remarks>
public interface IShortcutActions
{
    /// <summary>Every action, what it is called, and the keystroke it ships on.</summary>
    /// <remarks>
    /// The order is the order a settings page lists them in, so it is the order somebody reads
    /// rather than the order the enum happens to be written in.
    /// </remarks>
    IReadOnlyList<(ShortcutAction Action, string Name, string Default)> Everything { get; }

    /// <summary>What to call one, for a page listing them.</summary>
    /// <remarks>
    /// Falls back to the member's own name for an action nobody has given a wording to, which
    /// is ugly rather than broken and is what a page shows the day one is added and forgotten.
    /// </remarks>
    string Named(ShortcutAction action);
}
