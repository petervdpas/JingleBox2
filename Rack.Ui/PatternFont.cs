using Avalonia.Media;

namespace JingleBox2.Rack.Ui;

/// <summary>
/// The face the pattern grid and its header both measure against. They must agree exactly,
/// or the header labels drift away from the columns they name.
/// </summary>
public static class PatternFont
{
    /// <summary>
    /// A monospaced face with fallbacks down to whatever the system calls "monospace".
    /// </summary>
    /// <remarks>
    /// The list is in order of preference across the three platforms this runs on, and it ends
    /// in the generic name so that a machine with none of the named faces still gets even
    /// columns rather than a proportional font in a grid.
    /// </remarks>
    public static readonly FontFamily Family =
        new("Cascadia Mono,Consolas,DejaVu Sans Mono,Menlo,monospace");
}
