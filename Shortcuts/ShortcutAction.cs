using System.Collections.Generic;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;

namespace JingleBox2.Shortcuts;

/// <inheritdoc/>
public sealed class ShortcutActions : IShortcutActions
{
    /// <inheritdoc/>
    public IReadOnlyList<(ShortcutAction Action, string Name, string Default)> Everything { get; } =
        new[]
        {
            (ShortcutAction.Save,   "Save",   "Ctrl+S"),
            (ShortcutAction.Delete, "Delete", "Ctrl+D"),
            (ShortcutAction.Undo,   "Undo",   "Ctrl+Z"),
            (ShortcutAction.Redo,   "Redo",   "Ctrl+Shift+Z")
        };

    /// <inheritdoc/>
    public string Named(ShortcutAction action)
    {
        foreach (var (one, name, _) in Everything)
            if (one == action) return name;

        return action.ToString();
    }
}
