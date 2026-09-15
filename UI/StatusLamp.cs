using Avalonia.Media;
using JingleBox2.UI.Enums;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class StatusLamp : IStatusLamp
{
    /// <summary>A warning.</summary>
    private static readonly Color Amber = Color.FromRgb(0xF5, 0xA6, 0x23);

    /// <summary>A fault.</summary>
    private static readonly Color Red = Color.FromRgb(0xE5, 0x39, 0x35);

    /// <summary>Something that worked.</summary>
    private static readonly Color Green = Color.FromRgb(0x4C, 0xAF, 0x50);

    /// <inheritdoc/>
    public Color For(StatusKind kind, Color accent, Color muted) => kind switch
    {
        StatusKind.Done => Green,
        StatusKind.Warning => Amber,
        StatusKind.Fault => Red,
        StatusKind.Plain => accent,
        _ => muted
    };
}
