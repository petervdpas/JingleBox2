using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;
using JingleBox2.Shortcuts.Records;

namespace JingleBox2.Shortcuts;

/// <inheritdoc/>
public sealed class PatternKeys : IPatternKeys
{
    /// <inheritdoc/>
    public IReadOnlyList<PatternKey> All { get; } = new[]
    {
        new PatternKey(Key.Up, KeyModifiers.None, PatternAction.CursorUp, "Up"),
        new PatternKey(Key.Down, KeyModifiers.None, PatternAction.CursorDown, "Down"),
        new PatternKey(Key.Left, KeyModifiers.None, PatternAction.CursorLeft, "Left"),
        new PatternKey(Key.Right, KeyModifiers.None, PatternAction.CursorRight, "Right"),
        new PatternKey(Key.PageUp, KeyModifiers.None, PatternAction.PageUp, "Page Up"),
        new PatternKey(Key.PageDown, KeyModifiers.None, PatternAction.PageDown, "Page Down"),
        new PatternKey(Key.Tab, KeyModifiers.None, PatternAction.NextTrack, "Tab"),
        new PatternKey(Key.A, KeyModifiers.Control, PatternAction.SelectAll, "Ctrl+A"),
        new PatternKey(Key.C, KeyModifiers.Control, PatternAction.Copy, "Ctrl+C"),
        new PatternKey(Key.X, KeyModifiers.Control, PatternAction.Cut, "Ctrl+X"),
        new PatternKey(Key.V, KeyModifiers.Control, PatternAction.Paste, "Ctrl+V"),
        new PatternKey(Key.Escape, KeyModifiers.None, PatternAction.ClearSelection, "Escape"),
        new PatternKey(Key.Delete, KeyModifiers.None, PatternAction.ClearCell, "Delete"),
        new PatternKey(Key.Insert, KeyModifiers.None, PatternAction.InsertLine, "Insert"),
        new PatternKey(Key.Back, KeyModifiers.None, PatternAction.DeleteLine, "Backspace"),
        new PatternKey(Key.Multiply, KeyModifiers.None, PatternAction.OctaveUp, "Numpad *"),
        new PatternKey(Key.OemCloseBrackets, KeyModifiers.Control, PatternAction.OctaveUp, "Ctrl+]"),
        new PatternKey(Key.Divide, KeyModifiers.None, PatternAction.OctaveDown, "Numpad /"),
        new PatternKey(Key.OemOpenBrackets, KeyModifiers.Control, PatternAction.OctaveDown, "Ctrl+["),
        new PatternKey(Key.V, KeyModifiers.Control | KeyModifiers.Shift, PatternAction.TypedVelocity, "Ctrl+Shift+V")
    };

    /// <summary>What each action does, in the words a list row wants.</summary>
    /// <remarks>
    /// Once per action rather than once per key, which is what lets the octave read as one line
    /// naming both of its keys. Every action but <see cref="PatternAction.None"/> has to be in
    /// here, and <c>Tests/PatternKeyTests.cs</c> is what says so: an action with no words is a
    /// key the card cannot name.
    /// </remarks>
    private static readonly Dictionary<PatternAction, string> Means = new()
    {
        [PatternAction.CursorUp] = "moves the cursor up a line, and marks a block while shift is held",
        [PatternAction.CursorDown] = "moves the cursor down a line, and marks a block while shift is held",
        [PatternAction.CursorLeft] = "moves left a column, or a whole track while shift is held",
        [PatternAction.CursorRight] = "moves right a column, or a whole track while shift is held",
        [PatternAction.PageUp] = "moves up four beats, however many lines that is in this song",
        [PatternAction.PageDown] = "moves down four beats",
        [PatternAction.NextTrack] = "goes to the next track, or the one before with shift",
        [PatternAction.SelectAll] = "marks the whole pattern",
        [PatternAction.Copy] = "copies what is marked",
        [PatternAction.Cut] = "cuts what is marked",
        [PatternAction.Paste] = "puts back what was copied",
        [PatternAction.ClearSelection] = "lets a marked block go",
        [PatternAction.ClearCell] = "empties the cell under the cursor",
        [PatternAction.InsertLine] = "pushes a line in",
        [PatternAction.DeleteLine] = "pulls a line out",
        [PatternAction.OctaveUp] = "moves the octave the letter keys play in up a step",
        [PatternAction.OctaveDown] = "and down a step",
        [PatternAction.TypedVelocity] = "turns the typed velocity over, so a letter key writes 7F or leaves the column blank"
    };

    /// <inheritdoc/>
    public PatternAction Find(Key key, KeyModifiers held)
    {
        var found = PatternAction.None;
        int best = -1;

        foreach (var row in All)
        {
            if (row.Key != key) continue;
            if ((held & row.Modifiers) != row.Modifiers) continue;

            int weight = Count(row.Modifiers);

            if (weight <= best) continue;

            best = weight;
            found = row.Does;
        }

        return found;
    }

    /// <summary>How particular a row is, which is how many modifiers it insists on.</summary>
    /// <param name="modifiers">The row's own.</param>
    private static int Count(KeyModifiers modifiers)
    {
        int held = 0;

        foreach (KeyModifiers one in new[] { KeyModifiers.Control, KeyModifiers.Shift, KeyModifiers.Alt, KeyModifiers.Meta })
            if (modifiers.HasFlag(one)) held++;

        return held;
    }

    /// <inheritdoc/>
    public IReadOnlyList<SystemKey> Listed =>
        All.GroupBy(row => row.Does)
           .Select(rows => new SystemKey(string.Join(" or ", rows.Select(row => row.Said)), Words(rows.Key)))
           .ToArray();

    /// <summary>What an action does, or an empty line rather than a missing one.</summary>
    /// <param name="action">The action being described.</param>
    public static string Words(PatternAction action) => Means.TryGetValue(action, out var said) ? said : "";
}
