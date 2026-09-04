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

    /// <summary>
    /// Whether that one is the system's, so it may be read and not moved.
    /// </summary>
    /// <remarks>
    /// The four the application ships with are what they are: what Save does is a fact about
    /// this program, every page answers it for itself, and a settings page that could move it
    /// would be offering to change something that is not a preference. What is a preference is
    /// a key onto a page along the top, which ships on nothing at all until somebody puts one
    /// there.
    ///
    /// Asked here rather than decided by the page that draws the list, since it is also what
    /// <c>IShortcutMap.Set</c> refuses on, and a rule spelled in two places is a rule that will
    /// eventually be spelled differently.
    /// </remarks>
    /// <param name="action">The shortcut being asked about.</param>
    bool Fixed(ShortcutAction action);

    /// <summary>What to call one, for a page listing them.</summary>
    /// <remarks>
    /// Falls back to the member's own name for an action nobody has given a wording to, which
    /// is ugly rather than broken and is what a page shows the day one is added and forgotten.
    /// </remarks>
    string Named(ShortcutAction action);
}
