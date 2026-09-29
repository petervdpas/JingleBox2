using Avalonia;
using Avalonia.Media;

namespace JingleBox2.Rack.Controls.Records;

/// <summary>
/// The three colours a level meter is lit in, as the theme says them: safe, warning and over.
/// </summary>
/// <remarks>
/// Apart from <see cref="ThemePalette"/> because they are a meter's and nothing else's, and a
/// palette every drawn control resolves on every frame should not grow three colours only one
/// control reads. What they mean is fixed, which is the meter convention: the first is a level
/// with room above it, the second is close to the ceiling, the third is at it. Which colours say
/// that is the theme's, so a theme whose accent is green can still have a meter that reads, and a
/// light theme can darken them enough to stand out against a pale trough.
/// </remarks>
/// <param name="Safe">A level with room above it.</param>
/// <param name="Warn">A level close enough to the ceiling to warn about.</param>
/// <param name="Hot">A level at the ceiling.</param>
public readonly record struct MeterColours(Color Safe, Color Warn, Color Hot)
{
    /// <summary>The resource keys, written out rather than built.</summary>
    private const string SafeKey = "Color.MeterSafe";
    /// <inheritdoc cref="SafeKey"/>
    private const string WarnKey = "Color.MeterWarn";
    /// <inheritdoc cref="SafeKey"/>
    private const string HotKey = "Color.MeterHot";

    /// <summary>
    /// What is lit when no theme answers: green, orange and red, which is every meter's.
    /// </summary>
    public static readonly MeterColours Fallback = new(
        Color.FromRgb(0x43, 0xC0, 0x4A),
        Color.FromRgb(0xFB, 0x8C, 0x00),
        Color.FromRgb(0xE5, 0x39, 0x35));

    /// <summary>Reads the three off whatever the meter is sitting under.</summary>
    /// <param name="element">The meter.</param>
    /// <returns>The colours, each falling back on its own.</returns>
    public static MeterColours From(StyledElement element) => new(
        ThemePalette.Resolve(element, SafeKey, Fallback.Safe),
        ThemePalette.Resolve(element, WarnKey, Fallback.Warn),
        ThemePalette.Resolve(element, HotKey, Fallback.Hot));
}
