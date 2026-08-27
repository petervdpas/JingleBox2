using System.Collections.Generic;

namespace JingleBox2.Shortcuts;

/// <summary>
/// The things a keystroke can ask for, as a closed list rather than a name to spell.
/// </summary>
/// <remarks>
/// An enum and not a string, so the set of them is visible in one place and a page cannot ask
/// for something nothing offers. Adding one is adding a member here and a line in
/// <see cref="ShortcutMap.Everything"/>, and every page that does not answer it simply says it
/// cannot.
/// </remarks>
public enum ShortcutAction
{
    Save,
    Delete,
    Undo,
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
    public static readonly IReadOnlyList<(ShortcutAction Action, string Name, string Default)> Everything =
        new[]
        {
            (ShortcutAction.Save,   "Save",   "Ctrl+S"),
            (ShortcutAction.Delete, "Delete", "Ctrl+D"),
            (ShortcutAction.Undo,   "Undo",   "Ctrl+Z"),
            (ShortcutAction.Redo,   "Redo",   "Ctrl+Shift+Z")
        };

    /// <summary>What to call one, for a page listing them.</summary>
    public static string Named(ShortcutAction action)
    {
        foreach (var (one, name, _) in Everything)
            if (one == action) return name;

        return action.ToString();
    }
}
