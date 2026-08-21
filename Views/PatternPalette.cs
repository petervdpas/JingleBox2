using Avalonia.Media;
using Avalonia;
using Avalonia.Controls;

namespace JingleBox2.Views;

/// <summary>
/// The theme colours the pattern grid and its header draw with, resolved once per render.
/// </summary>
/// <remarks>
/// Colours are looked up rather than brushes on purpose. The theme's brushes are declared as
/// a SolidColorBrush whose Color is itself a DynamicResource, and immediately after a theme
/// swap that indirection has not resolved yet, so the brush paints transparent. The Color
/// keys are plain values with no indirection, so they are correct the moment they are read.
/// </remarks>
public readonly record struct PatternPalette(
    Color Text,
    Color Muted,
    Color Accent,
    Color Border,
    Color Background)
{
    // Declared, not built from a variable, so the keys stay greppable.
    private const string TextKey = "Color.TextPrimary";
    private const string MutedKey = "Color.TextMuted";
    private const string AccentKey = "Color.Accent";
    private const string BorderKey = "Color.Border";
    private const string BackgroundKey = "Color.Background";

    public static readonly PatternPalette Fallback = new(
        Colors.Gainsboro,
        Color.FromRgb(0x6B, 0x72, 0x80),
        Color.FromRgb(0xFB, 0x8C, 0x00),
        Color.FromRgb(0x3A, 0x40, 0x46),
        Colors.Black);

    public static PatternPalette From(StyledElement element) => new(
        Resolve(element, TextKey, Fallback.Text),
        Resolve(element, MutedKey, Fallback.Muted),
        Resolve(element, AccentKey, Fallback.Accent),
        Resolve(element, BorderKey, Fallback.Border),
        Resolve(element, BackgroundKey, Fallback.Background));

    private static Color Resolve(StyledElement element, string key, Color fallback)
    {
        if (element.TryFindResource(key, out var value))
        {
            if (value is Color color) return color;
            if (value is ISolidColorBrush brush && brush.Color.A > 0) return brush.Color;
        }

        return fallback;
    }

    /// <summary>Beat shading has to darken a light theme and lighten a dark one.</summary>
    public bool IsLightBackground =>
        (0.2126 * Background.R + 0.7152 * Background.G + 0.0722 * Background.B) / 255.0 > 0.5;

    public IBrush TextBrush => new SolidColorBrush(Text);
    public IBrush MutedBrush => new SolidColorBrush(Muted);
    public IBrush AccentBrush => new SolidColorBrush(Accent);
    public IBrush BorderBrush => new SolidColorBrush(Border);

    public IBrush AccentTint(byte alpha) => new SolidColorBrush(Alpha(Accent, alpha));

    /// <summary>Row shading that follows the background instead of always painting white.</summary>
    public IBrush RowShade(byte alpha) => IsLightBackground
        ? new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0))
        : new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255));

    public static Color Alpha(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);
}
