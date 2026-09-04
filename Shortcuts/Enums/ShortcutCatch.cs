namespace JingleBox2.Shortcuts.Enums;

/// <summary>
/// What one keystroke means while somebody is putting a shortcut on a key.
/// </summary>
/// <remarks>
/// A closed list rather than a nullable gesture, because most of the answers are not a gesture:
/// a hand arriving is not a keystroke yet, Escape is somebody changing their mind, Backspace is
/// somebody taking the shortcut off, and a keystroke a page shortcut may not be is somebody
/// pressing the wrong thing and still waiting to press the right one.
/// </remarks>
public enum ShortcutCatch
{
    /// <summary>Not a keystroke yet: keep listening.</summary>
    Waiting,

    /// <summary>Leave it as it was.</summary>
    Cancel,

    /// <summary>Take it off keys altogether.</summary>
    Clear,

    /// <summary>That is the one: put the shortcut on it.</summary>
    Take,

    /// <summary>A real keystroke, and not one a page shortcut may be: keep listening.</summary>
    Refused
}
