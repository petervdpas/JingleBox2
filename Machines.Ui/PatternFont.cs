using Avalonia.Media;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// The face the pattern grid and its header both measure against. They must agree exactly,
/// or the header labels drift away from the columns they name.
/// </summary>
public static class PatternFont
{
    public static readonly FontFamily Family =
        new("Cascadia Mono,Consolas,DejaVu Sans Mono,Menlo,monospace");
}
