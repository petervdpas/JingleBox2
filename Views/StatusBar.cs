using Avalonia;
using Avalonia.Media;
using JingleBox2.UI;
using System;
using System.Globalization;

namespace JingleBox2.Views;

/// <summary>
/// The line along the bottom of a window: a lamp saying what kind of thing it is, and the thing.
/// </summary>
/// <remarks>
/// Drawn rather than a border round a text block, because the lamp is the point. Where you are
/// and what has just gone wrong are the same line of text in the same place, and without
/// something that changes colour they read the same as each other.
///
/// It knows nothing about pages. Whatever fills it decides what it says.
/// </remarks>
public class StatusBar : ThemedControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<StatusBar, string>(nameof(Text), "");

    public static readonly StyledProperty<StatusKind> KindProperty =
        AvaloniaProperty.Register<StatusBar, StatusKind>(nameof(Kind), StatusKind.Context);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<StatusBar, double>(nameof(FontSize), 12);

    /// <summary>How tall the bar is, which is the same whatever it says.</summary>
    public static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<StatusBar, double>(nameof(BarHeight), 26);

    public static readonly StyledProperty<double> InputLevelProperty =
        AvaloniaProperty.Register<StatusBar, double>(nameof(InputLevel));

    public static readonly StyledProperty<double> OutputLevelProperty =
        AvaloniaProperty.Register<StatusBar, double>(nameof(OutputLevel));

    /// <summary>Draws the two meters at the right end. Off for a bar in a dialog.</summary>
    public static readonly StyledProperty<bool> ShowLevelsProperty =
        AvaloniaProperty.Register<StatusBar, bool>(nameof(ShowLevels));

    private const double LampSize = 8;

    /// <summary>The meters: two thin columns, tall enough to read and no taller.</summary>
    private const double MeterWidth = 4;

    private const double MeterGap = 3;

    /// <summary>Where the scale stops being green. Above this it is amber, then red.</summary>
    private const double Warm = 0.72;

    private const double Hot = 0.92;

    private const double Inset = 10;

    private const double Gap = 8;

    /// <summary>A warning is amber and a fault is the red a record button is.</summary>
    private static readonly Color Amber = Color.FromRgb(0xF5, 0xA6, 0x23);

    private static readonly Color Red = Color.FromRgb(0xE5, 0x39, 0x35);

    private static readonly Color Green = Color.FromRgb(0x4C, 0xAF, 0x50);

    static StatusBar()
    {
        AffectsRender<StatusBar>(TextProperty, KindProperty, FontSizeProperty, BarHeightProperty,
                                 InputLevelProperty, OutputLevelProperty, ShowLevelsProperty);
        AffectsMeasure<StatusBar>(BarHeightProperty);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public StatusKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public double BarHeight
    {
        get => GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    /// <summary>What is coming in, 0 to 1. The recorder's input, whether or not it is recording.</summary>
    public double InputLevel
    {
        get => GetValue(InputLevelProperty);
        set => SetValue(InputLevelProperty, value);
    }

    /// <summary>What is going out, 0 to 1. Everything the app is playing, pads and tracker both.</summary>
    public double OutputLevel
    {
        get => GetValue(OutputLevelProperty);
        set => SetValue(OutputLevelProperty, value);
    }

    public bool ShowLevels
    {
        get => GetValue(ShowLevelsProperty);
        set => SetValue(ShowLevelsProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width, BarHeight);

    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width;
        double height = Bounds.Height;

        if (width <= 1 || height <= 1) return;

        var palette = ThemePalette.From(this);
        var area = new Rect(0, 0, width, height);

        context.DrawRectangle(
            new SolidColorBrush(palette.Surface, 0.55),
            new Pen(new SolidColorBrush(palette.Border, 0.8), 1),
            new RoundedRect(new Rect(0.5, 0.5, width - 1, height - 1), 4));

        var lamp = Lamp(palette);

        Led.DrawLamp(context, new Point(Inset + LampSize / 2, height / 2), LampSize / 2, lamp,
                     Kind != StatusKind.Context);

        if (Text.Length == 0) return;

        // The context is the resting state and reads quieter than something that just happened.
        var ink = Kind == StatusKind.Context
            ? new SolidColorBrush(palette.Muted)
            : new SolidColorBrush(palette.Text);

        double meters = ShowLevels ? MeterWidth * 2 + MeterGap + Inset + LabelRoom : 0;

        var text = new FormattedText(
            Text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            FontSize,
            ink)
        {
            MaxTextWidth = Math.Max(0, width - Inset * 2 - LampSize - Gap - meters),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };

        context.DrawText(text, new Point(Inset + LampSize + Gap, (height - text.Height) / 2));

        if (ShowLevels) DrawLevels(context, palette, area);
    }

    /// <summary>How much room the two little letters beside the meters take.</summary>
    private const double LabelRoom = 16;

    /// <summary>
    /// The main input and the main output, as two thin columns at the far end.
    /// </summary>
    /// <remarks>
    /// Peak rather than average, and coloured by where the peak is rather than by a line drawn
    /// across it: the whole use of a meter this size is to be read without being looked at, and
    /// a colour is the only thing that can be.
    /// </remarks>
    private void DrawLevels(DrawingContext context, ThemePalette palette, Rect area)
    {
        double top = 4;
        double bottom = area.Height - 4;
        double tall = bottom - top;

        if (tall <= 2) return;

        double right = area.Width - Inset;
        double outputX = right - MeterWidth;
        double inputX = outputX - MeterGap - MeterWidth;

        Draw(inputX, InputLevel);
        Draw(outputX, OutputLevel);

        var letters = new FormattedText(
            "io",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            9,
            new SolidColorBrush(palette.Muted, 0.8));

        context.DrawText(letters, new Point(inputX - LabelRoom + 2, (area.Height - letters.Height) / 2));

        void Draw(double x, double level)
        {
            level = double.IsNaN(level) ? 0 : Math.Clamp(level, 0, 1);

            context.FillRectangle(
                new SolidColorBrush(palette.Border, 0.7),
                new Rect(x, top, MeterWidth, tall));

            if (level <= 0) return;

            double lit = tall * level;

            var colour = level >= Hot ? Red : level >= Warm ? Amber : Green;

            context.FillRectangle(
                new SolidColorBrush(colour, 0.95),
                new Rect(x, bottom - lit, MeterWidth, lit));
        }
    }

    private Color Lamp(ThemePalette palette) => Kind switch
    {
        StatusKind.Done => Green,
        StatusKind.Warning => Amber,
        StatusKind.Fault => Red,
        StatusKind.Plain => palette.Accent,
        _ => palette.Muted
    };
}
