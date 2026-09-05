namespace JingleBox2.Shortcuts.Enums;

/// <summary>
/// Everything a key can ask the pattern for, which is a closed list on purpose.
/// </summary>
/// <remarks>
/// The pattern answers keys itself rather than through <see cref="ShortcutAction"/> and the map,
/// because these cannot be moved: they are what the pattern is rather than where you go. What
/// they must not be is written down twice, so the key, the words and this are one table and the
/// only thing left in the view is which method each of these calls.
/// </remarks>
public enum PatternAction
{
    /// <summary>No key of the pattern's was pressed.</summary>
    None,

    /// <summary>Up a line.</summary>
    CursorUp,

    /// <summary>Down a line.</summary>
    CursorDown,

    /// <summary>Left a column, or a track while a block is being marked.</summary>
    CursorLeft,

    /// <summary>Right a column, or a track while a block is being marked.</summary>
    CursorRight,

    /// <summary>Up four beats.</summary>
    PageUp,

    /// <summary>Down four beats.</summary>
    PageDown,

    /// <summary>The next track, or the one before while shift is held.</summary>
    NextTrack,

    /// <summary>Marks the whole pattern.</summary>
    SelectAll,

    /// <summary>Copies what is marked.</summary>
    Copy,

    /// <summary>Cuts what is marked.</summary>
    Cut,

    /// <summary>Puts back what was copied.</summary>
    Paste,

    /// <summary>Lets a marked block go.</summary>
    ClearSelection,

    /// <summary>Empties the cell under the cursor.</summary>
    ClearCell,

    /// <summary>Pushes a line in.</summary>
    InsertLine,

    /// <summary>Pulls a line out.</summary>
    DeleteLine,

    /// <summary>The octave the letter keys play in, a step up.</summary>
    OctaveUp,

    /// <summary>The same, a step down.</summary>
    OctaveDown,

    /// <summary>Turns over whether a typed note carries a velocity.</summary>
    TypedVelocity
}
