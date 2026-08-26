using Avalonia;
using Avalonia.Media;
using System;
using System.Globalization;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// A lamp on the panel: lit, unlit, and dark enough when unlit to still look like a lamp.
/// </summary>
/// <remarks>
/// The third thing a front panel is made of, after something to turn and something to press.
/// A machine uses a row of these to say where it is in a sequence, which octave is picked, or
/// that a low frequency oscillator is going round, none of which is a control at all: they only
/// report. Kept separate from the push button for that reason, since most lamps have no button
/// under them and most buttons have no lamp.
/// </remarks>
public class Led : ThemedControl
{
    public static readonly StyledProperty<bool> IsLitProperty =
        AvaloniaProperty.Register<Led, bool>(nameof(IsLit));

    /// <summary>How big the lamp is across.</summary>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<Led, double>(nameof(Size), 11.0);

    /// <summary>What colour it is when lit. Unlit is a much darker version of the same.</summary>
    public static readonly StyledProperty<Color> ColourProperty =
        AvaloniaProperty.Register<Led, Color>(nameof(Colour), Color.FromRgb(0xE5, 0x39, 0x35));

    /// <summary>Written under the lamp, for a row that is counting something.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Led, string?>(nameof(Label));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<Led, double>(nameof(FontSize), 9.0);

    /// <summary>
    /// How fast it goes round on its own, in hertz. Nought and it does not: something else says
    /// whether it is lit.
    /// </summary>
    /// <remarks>
    /// A lamp beside a rate knob is not reporting a value, it is the value: a number in hertz is
    /// a number, and a light going round at it is the rate itself, which is the thing being set.
    /// It runs whether or not anything is sounding, which is exactly when the rate is being set.
    ///
    /// The lamp does its own timing rather than being flashed from outside, so nothing above it
    /// has to keep a timer, and a panel with one on it is not read from top to bottom ten times
    /// a second to move a single dot.
    /// </remarks>
    public static readonly StyledProperty<double> RateProperty =
        AvaloniaProperty.Register<Led, double>(nameof(Rate));

    private const double LabelGap = 3;

    /// <summary>
    /// The slowest and fastest it is worth going round at.
    /// </summary>
    /// <remarks>
    /// Faster than the top and a lamp is simply on; slower than the bottom and it is a light
    /// that changes once a minute, which reads as broken rather than as slow.
    /// </remarks>
    private const double SlowestBlink = 0.2;

    private const double FastestBlink = 20;

    static Led()
    {
        AffectsRender<Led>(IsLitProperty, SizeProperty, ColourProperty, LabelProperty, FontSizeProperty);
        AffectsMeasure<Led>(SizeProperty, LabelProperty, FontSizeProperty);
    }

    public Led()
    {
        AttachedToVisualTree += (_, _) => { _shown = true; Beat(); };
        DetachedFromVisualTree += (_, _) => { _shown = false; _clock?.Stop(); };
    }

    public double Rate
    {
        get => GetValue(RateProperty);
        set => SetValue(RateProperty, value);
    }

    private Avalonia.Threading.DispatcherTimer? _clock;

    /// <summary>Whether it is on screen. A lamp in a panel nobody is looking at does not tick.</summary>
    private bool _shown;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RateProperty) Beat();
    }

    /// <summary>Sets the lamp going at whatever rate it has now, or stops it having none.</summary>
    private void Beat()
    {
        if (Rate <= 0 || !_shown)
        {
            _clock?.Stop();

            return;
        }

        // Half a turn per tick, so it is lit for half of every cycle.
        var half = TimeSpan.FromMilliseconds(500.0 / Math.Clamp(Rate, SlowestBlink, FastestBlink));

        if (_clock is null)
        {
            _clock = new Avalonia.Threading.DispatcherTimer { Interval = half };
            _clock.Tick += (_, _) => IsLit = !IsLit;
        }
        else
        {
            _clock.Interval = half;
        }

        _clock.Start();
    }

    public bool IsLit
    {
        get => GetValue(IsLitProperty);
        set => SetValue(IsLitProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public Color Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = Size;
        double height = Size;

        if (!string.IsNullOrEmpty(Label))
        {
            var text = Text(Label, Brushes.Black);
            width = Math.Max(width, text.Width);
            height += LabelGap + text.Height;
        }

        return new Size(width, height);
    }


    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);
        double middle = Bounds.Width / 2;

        DrawLamp(context, new Point(middle, Size / 2), Size / 2, Colour, IsLit);

        if (string.IsNullOrEmpty(Label)) return;

        var label = Text(Label, palette.MutedBrush);
        context.DrawText(label, new Point(middle - label.Width / 2, Size + LabelGap));
    }

    /// <summary>
    /// Draws one lamp, so a row of them looks like the single one beside it.
    /// </summary>
    /// <remarks>
    /// Shared with <see cref="LedRow"/>. A row is not eight of this control in a stack panel,
    /// because then every lamp needs its own binding to work out whether it is the lit one;
    /// but a row still has to be made of the same lamp, or the panel has two kinds on it.
    /// </remarks>
    public static void DrawLamp(DrawingContext context, Point centre, double radius, Color colour, bool lit)
    {
        // Lit, it spills onto the panel around it. That halo is most of what makes a lamp read
        // as on rather than as a coloured dot.
        if (lit)
        {
            context.DrawEllipse(new SolidColorBrush(colour, 0.20), null, centre, radius * 2.2, radius * 2.2);
            context.DrawEllipse(new SolidColorBrush(colour, 0.32), null, centre, radius * 1.5, radius * 1.5);
        }

        // The dome. Even unlit it is a piece of coloured plastic catching the light, not a
        // hole: dark, but plainly there, which is how an unlit lamp looks on a real panel.
        var body = lit ? colour : Lighten(colour, -0.55);

        var dome = new RadialGradientBrush
        {
            GradientOrigin = new RelativePoint(0.34, 0.28, RelativeUnit.Relative),
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.72, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.72, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Lighten(body, lit ? 0.60 : 0.28), 0),
                new GradientStop(body, 0.55),
                new GradientStop(Lighten(body, -0.45), 1)
            }
        };

        // A dark ring, the way a lamp sits in its hole.
        context.DrawEllipse(dome, new Pen(new SolidColorBrush(Lighten(body, -0.7)), 1), centre, radius, radius);

        // The catchlight, up and to the left, where the panel light comes from.
        double gloss = radius * 0.34;
        context.DrawEllipse(
            new SolidColorBrush(Colors.White, lit ? 0.55 : 0.22), null,
            new Point(centre.X - radius * 0.3, centre.Y - radius * 0.32), gloss, gloss * 0.8);
    }


    private FormattedText Text(string? text, IBrush brush) =>
        new(text ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), FontSize, brush);

    private static Color Lighten(Color colour, double amount)
    {
        double Mix(byte channel) => amount >= 0
            ? channel + (255 - channel) * amount
            : channel * (1 + amount);

        return Color.FromRgb((byte)Mix(colour.R), (byte)Mix(colour.G), (byte)Mix(colour.B));
    }
}
