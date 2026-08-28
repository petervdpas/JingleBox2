using System.Collections.Generic;

namespace JingleBox2.Shortcuts;

/// <summary>
/// The things a keystroke can ask for, as a closed list rather than a name to spell.
/// </summary>
/// <remarks>
/// An enum and not a string, so the set of them is visible in one place and a page cannot ask
/// for something nothing offers. Adding one is adding a member here and a line in
/// <see cref="ShortcutActions.Everything"/>, and every page that does not answer it simply says it
/// cannot.
/// </remarks>
public enum ShortcutAction
{
    /// <summary>Write down whatever the page in front of you owns: a song, a machine, the pads.</summary>
    Save,

    /// <summary>Take away whatever is picked out, on a page that has something to pick out.</summary>
    Delete,

    /// <summary>
    /// The last thing that was done on this page, put back.
    /// </summary>
    /// <remarks>
    /// There is no undo for the application: each page keeps its own, because what the last
    /// thing you did was is a question only the page you did it on can answer.
    /// </remarks>
    Undo,

    /// <summary>And the last thing undone, done again.</summary>
    Redo
}

/// <summary>What each one is called and what it is on by default.</summary>
/// <remarks>
/// Written out rather than derived, because both halves are things somebody chose: the wording
/// on the settings page, and which key it starts on. A settings page builds itself from this,
/// the way the log's page builds itself from the areas the log knows about, so a shortcut added
/// here turns up there without anybody being told to add it.
/// </remarks>
public static class ShortcutActions
{
    /// <summary>Every action, what it is called, and the keystroke it ships on.</summary>
    /// <remarks>
    /// The order is the order a settings page lists them in, so it is the order somebody reads
    /// rather than the order the enum happens to be written in.
    /// </remarks>
    public static readonly IReadOnlyList<(ShortcutAction Action, string Name, string Default)> Everything =
        new[]
        {
            (ShortcutAction.Save,   "Save",   "Ctrl+S"),
            (ShortcutAction.Delete, "Delete", "Ctrl+D"),
            (ShortcutAction.Undo,   "Undo",   "Ctrl+Z"),
            (ShortcutAction.Redo,   "Redo",   "Ctrl+Shift+Z")
        };

    /// <summary>What to call one, for a page listing them.</summary>
    /// <remarks>
    /// Falls back to the member's own name for an action nobody has given a wording to, which
    /// is ugly rather than broken and is what a page shows the day one is added and forgotten.
    /// </remarks>
    public static string Named(ShortcutAction action)
    {
        foreach (var (one, name, _) in Everything)
            if (one == action) return name;

        return action.ToString();
    }
}
