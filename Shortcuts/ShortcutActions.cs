using System.Collections.Generic;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;

namespace JingleBox2.Shortcuts;

/// <inheritdoc/>
/// <remarks>
/// The pages are named as the tab strip spells them, capitals and all, because that is what
/// somebody setting a key is looking at while they do it. The system's four are named as
/// ordinary words for the same reason: they are not a place, they are a thing that happens.
///
/// Every page ships on no key. A shortcut nobody asked for is a keystroke that does something
/// surprising, and there is no key here that is obviously right: the ones that would be, the
/// digits with a modifier, are two characters somebody may want to type.
///
/// This file was called <c>ShortcutAction.cs</c> and held <c>ShortcutActions</c>, one letter
/// away from the enum that really is called that and lives in <c>Enums/</c>.
/// </remarks>
public sealed class ShortcutActions : IShortcutActions
{
    /// <inheritdoc/>
    public IReadOnlyList<(ShortcutAction Action, string Name, string Default)> Everything { get; } =
        new[]
        {
            (ShortcutAction.Save,   "Save",   "Ctrl+S"),
            (ShortcutAction.Delete, "Delete", "Ctrl+D"),
            (ShortcutAction.Undo,   "Undo",   "Ctrl+Z"),
            (ShortcutAction.Redo,   "Redo",   "Ctrl+Shift+Z"),

            (ShortcutAction.Mixer,    "MIXER",    ""),
            (ShortcutAction.Record,   "RECORD",   ""),
            (ShortcutAction.Pads,     "PADS",     ""),
            (ShortcutAction.Fire,     "FIRE",     ""),
            (ShortcutAction.Tracker,  "TRACKER",  ""),
            (ShortcutAction.Designer, "DESIGNER", ""),
            (ShortcutAction.Settings, "SETTINGS", ""),
            (ShortcutAction.MidiCc,   "MIDI CC",  "")
        };

    /// <inheritdoc/>
    public bool Fixed(ShortcutAction action) =>
        action is ShortcutAction.Save or ShortcutAction.Delete
               or ShortcutAction.Undo or ShortcutAction.Redo;

    /// <inheritdoc/>
    public string Named(ShortcutAction action)
    {
        foreach (var (one, name, _) in Everything)
            if (one == action) return name;

        return action.ToString();
    }
}
