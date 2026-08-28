namespace JingleBox2.Shortcuts.Enums;

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
