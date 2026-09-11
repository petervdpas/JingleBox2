namespace JingleBox2.Views.Enums;

/// <summary>
/// The mark on one of the take editor's tool buttons.
/// </summary>
/// <remarks>
/// Drawn rather than written, which is the rule this codebase already paid for once: the three
/// bars on a machine's menu button were U+2630, and a character is at the mercy of whichever
/// font the machine running this falls back to. It came out a third of the height of the cap
/// and left of the middle, because the fallback's advance is wider than its ink. The two
/// magnifying glasses on this window were exactly that fault again, U+1F50D beside a hyphen,
/// and on a machine with no emoji face they are an empty rectangle.
///
/// A closed list rather than a picture per button, because every one of these is a few lines of
/// geometry: an icon set that is a folder of images is one that has to be drawn twice for a
/// light theme and a dark one, and cannot follow the accent colour at all.
/// </remarks>
public enum ToolMark
{
    /// <summary>Nothing, for a button that says a word instead.</summary>
    None = 0,

    /// <summary>A dashed box: the stretch of the take that is marked out.</summary>
    Select,

    /// <summary>Two crop corners, the mark every editor uses for keeping what is inside them.</summary>
    Trim,

    /// <summary>A waveform with its middle laid flat.</summary>
    Silence,

    /// <summary>Two arrows about a mirror line, which is what reversing audio is.</summary>
    Reverse,

    /// <summary>A wedge climbing from nothing.</summary>
    FadeIn,

    /// <summary>And one falling to it.</summary>
    FadeOut,

    /// <summary>An arrow lifting to a ceiling.</summary>
    Normalize,

    /// <summary>A glass with a plus in it.</summary>
    ZoomIn,

    /// <summary>And one with a minus.</summary>
    ZoomOut,

    /// <summary>Arrows reaching both walls: the whole take on the screen at once.</summary>
    Fit,

    /// <summary>An arrow curving back on itself.</summary>
    Undo,

    /// <summary>And the same one the other way about.</summary>
    Redo,

    /// <summary>The transport's triangle.</summary>
    Play,

    /// <summary>And its square.</summary>
    Stop
}
