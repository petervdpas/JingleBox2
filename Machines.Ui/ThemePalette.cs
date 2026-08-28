using Avalonia.Media;
using Avalonia;
using Avalonia.Controls;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// The theme colours a custom-drawn control paints with, resolved once per render.
/// </summary>
/// <remarks>
/// Colours are looked up rather than brushes on purpose. The theme's brushes are declared as
/// a SolidColorBrush whose Color is itself a DynamicResource, and immediately after a theme
/// swap that indirection has not resolved yet, so the brush paints transparent. The Color
/// keys are plain values with no indirection, so they are correct the moment they are read.
/// </remarks>
/// <param name="Text">Ordinary writing.</param>
/// <param name="Muted">A label, a unit or a scale mark, quieter than the writing.</param>
/// <param name="Accent">The theme's own colour, for the part of a control showing where it is set.</param>
/// <param name="Border">The line round the edge of a drawn thing.</param>
/// <param name="Background">The page behind everything.</param>
/// <param name="Surface">What a control sits on, one step up from the page.</param>
/// <param name="Danger">The alarm colour, for what has to be found rather than looked at.</param>
public readonly record struct ThemePalette(
    Color Text,
    Color Muted,
    Color Accent,
    Color Border,
    Color Background,
    Color Surface,
    Color Danger)
{
    /// <summary>
    /// The resource keys, written out one per line rather than built from the field name.
    /// </summary>
    /// <remarks>
    /// A key assembled from a variable is a key that never appears in the source as a string, so
    /// nothing that greps the themes for an unused colour, and nobody reading them, can find it.
    /// </remarks>
    private const string TextKey = "Color.TextPrimary";
    /// <inheritdoc cref="TextKey"/>
    private const string MutedKey = "Color.TextMuted";
    /// <inheritdoc cref="TextKey"/>
    private const string AccentKey = "Color.Accent";
    /// <inheritdoc cref="TextKey"/>
    private const string BorderKey = "Color.Border";
    /// <inheritdoc cref="TextKey"/>
    private const string BackgroundKey = "Color.Background";
    /// <inheritdoc cref="TextKey"/>
    private const string SurfaceKey = "Color.Surface";
    /// <inheritdoc cref="TextKey"/>
    private const string DangerKey = "Color.Danger";

    /// <summary>
    /// What is painted when no theme answers: the dark theme's own colours, near enough.
    /// </summary>
    /// <remarks>
    /// A drawn control can be asked to render before it is under anything that carries the
    /// theme, in the designer's part library and in the first frame after a swap. Painting
    /// nothing there would read as a control that is broken rather than one that is early.
    /// </remarks>
    public static readonly ThemePalette Fallback = new(
        Colors.Gainsboro,
        Color.FromRgb(0x6B, 0x72, 0x80),
        Color.FromRgb(0xFB, 0x8C, 0x00),
        Color.FromRgb(0x3A, 0x40, 0x46),
        Colors.Black,
        Color.FromRgb(0x1E, 0x1E, 0x1E),
        Color.FromRgb(0xB6, 0x4A, 0x4A));

    /// <summary>Reads the whole palette off whatever the control is sitting under.</summary>
    public static ThemePalette From(StyledElement element) => new(
        Resolve(element, TextKey, Fallback.Text),
        Resolve(element, MutedKey, Fallback.Muted),
        Resolve(element, AccentKey, Fallback.Accent),
        Resolve(element, BorderKey, Fallback.Border),
        Resolve(element, BackgroundKey, Fallback.Background),
        Resolve(element, SurfaceKey, Fallback.Surface),
        Resolve(element, DangerKey, Fallback.Danger));

    /// <summary>
    /// One key, as a colour, whether the theme spelled it as a colour or as a brush.
    /// </summary>
    /// <remarks>
    /// A fully transparent brush is refused rather than taken, since that is what the theme's
    /// own indirection hands back in the moment after a swap when it has not resolved yet, and
    /// a control painted with it disappears.
    /// </remarks>
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

    /// <summary>The colour ordinary writing is drawn in.</summary>
    public IBrush TextBrush => new SolidColorBrush(Text);

    /// <summary>The colour a label, a unit or a scale mark is drawn in, quieter than the writing.</summary>
    public IBrush MutedBrush => new SolidColorBrush(Muted);

    /// <summary>The theme's own colour, for the part of a control that shows where it is set.</summary>
    public IBrush AccentBrush => new SolidColorBrush(Accent);

    /// <summary>The line round the edge of a drawn thing.</summary>
    public IBrush BorderBrush => new SolidColorBrush(Border);

    /// <summary>What a control sits on, one step up from the page behind it.</summary>
    public IBrush SurfaceBrush => new SolidColorBrush(Surface);

    /// <summary>
    /// The theme's alarm colour, for the few drawn things that have to be found rather than
    /// looked at: a trim handle, a clip light.
    /// </summary>
    public IBrush DangerBrush => new SolidColorBrush(Danger);

    /// <summary>The accent at a given transparency, for a wash rather than a stroke.</summary>
    public IBrush AccentTint(byte alpha) => new SolidColorBrush(Alpha(Accent, alpha));

    /// <summary>Row shading that follows the background instead of always painting white.</summary>
    public IBrush RowShade(byte alpha) => IsLightBackground
        ? new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0))
        : new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255));

    /// <summary>The same colour at a different transparency.</summary>
    public static Color Alpha(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);

    /// <summary>
    /// A colour taken towards white or towards black, for the light and shade a moulded control
    /// is drawn with.
    /// </summary>
    /// <remarks>
    /// Here rather than in each control, because every drawn cap, slot and bar in the app needs
    /// the same arithmetic and three of them had already written it out separately. A fourth
    /// copy is where a fourth answer starts.
    /// </remarks>
    public static Color Shade(Color colour, double amount)
    {
        double Mix(byte channel) => amount >= 0
            ? channel + (255 - channel) * amount
            : channel * (1 + amount);

        return Color.FromRgb((byte)Mix(colour.R), (byte)Mix(colour.G), (byte)Mix(colour.B));
    }
}
