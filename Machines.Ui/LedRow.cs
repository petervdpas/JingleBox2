using Avalonia;
using Avalonia.Media;
using System;
using System.Globalization;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// A row of lamps that counts something, with one of them lit.
/// </summary>
/// <remarks>
/// A panel says "you are here" with a row of lamps rather than a number, because a row can be
/// read without being read: which octave is picked, which step is playing, which page of a
/// pattern is showing. The Mother-32 calls its row OCTAVE / LOCATION and uses it for all three
/// at different times, which is exactly why the row is worth having as one control instead of
/// eight lamps and a converter apiece.
///
/// One binding drives it: the index that is lit. Nothing is lit at -1.
///
/// It lives beside <see cref="Led"/> rather than with the app's own views because a machine
/// bought from somebody else is built out of the same lamps the app's own machines are.
/// </remarks>
public class LedRow : ThemedControl
{
    private const double LabelGap = 3;
    private const double CaptionGap = 4;

    /// <summary>How many lamps are in the row.</summary>
    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<LedRow, int>(nameof(Count), 8);

    /// <summary>Which lamp is lit, counted from zero. Nothing is lit below zero.</summary>
    public static readonly StyledProperty<int> SelectedProperty =
        AvaloniaProperty.Register<LedRow, int>(nameof(Selected), -1);

    /// <summary>What the numbering under the lamps starts at. A step row counts from one.</summary>
    public static readonly StyledProperty<int> FirstNumberProperty =
        AvaloniaProperty.Register<LedRow, int>(nameof(FirstNumber), 1);

    /// <summary>True to write a number under each lamp.</summary>
    public static readonly StyledProperty<bool> NumberedProperty =
        AvaloniaProperty.Register<LedRow, bool>(nameof(Numbered), true);

    /// <summary>Written above the row, the way a panel names a section.</summary>
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<LedRow, string?>(nameof(Caption));

    public static readonly StyledProperty<Color> ColourProperty =
        AvaloniaProperty.Register<LedRow, Color>(nameof(Colour), Color.FromRgb(0xE5, 0x39, 0x35));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<LedRow, double>(nameof(Size), 9.0);

    /// <summary>The gap between one lamp and the next.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<LedRow, double>(nameof(Gap), 9.0);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<LedRow, double>(nameof(FontSize), 8.5);

    static LedRow()
    {
        AffectsRender<LedRow>(
            CountProperty, SelectedProperty, FirstNumberProperty, NumberedProperty,
            CaptionProperty, ColourProperty, SizeProperty, GapProperty, FontSizeProperty);

        AffectsMeasure<LedRow>(
            CountProperty, FirstNumberProperty, NumberedProperty, CaptionProperty,
            SizeProperty, GapProperty, FontSizeProperty);
    }

    public int Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public int Selected
    {
        get => GetValue(SelectedProperty);
        set => SetValue(SelectedProperty, value);
    }

    public int FirstNumber
    {
        get => GetValue(FirstNumberProperty);
        set => SetValue(FirstNumberProperty, value);
    }

    public bool Numbered
    {
        get => GetValue(NumberedProperty);
        set => SetValue(NumberedProperty, value);
    }

    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public Color Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>How wide one lamp and its number take, so the row spaces evenly whichever is wider.</summary>
    private double Pitch => Math.Max(Size, Numbered ? NumberWidth() : 0) + Gap;

    protected override Size MeasureOverride(Size availableSize)
    {
        int lamps = Math.Max(0, Count);
        if (lamps == 0) return default;

        double width = Pitch * lamps - Gap;
        double height = Size;

        if (Numbered) height += LabelGap + Text("0", Brushes.Black).Height;

        if (!string.IsNullOrEmpty(Caption))
        {
            var caption = Text(Caption, Brushes.Black);
            width = Math.Max(width, caption.Width);
            height += caption.Height + CaptionGap;
        }

        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        int lamps = Math.Max(0, Count);
        if (lamps == 0) return;

        var palette = ThemePalette.From(this);

        double pitch = Pitch;
        double rowWidth = pitch * lamps - Gap;
        double left = (Bounds.Width - rowWidth) / 2;
        double top = 0;

        if (!string.IsNullOrEmpty(Caption))
        {
            var caption = Text(Caption, palette.MutedBrush);
            context.DrawText(caption, new Point((Bounds.Width - caption.Width) / 2, top));
            top += caption.Height + CaptionGap;
        }

        double radius = Size / 2;

        for (int i = 0; i < lamps; i++)
        {
            double middle = left + pitch * i + (pitch - Gap) / 2;

            Led.DrawLamp(context, new Point(middle, top + radius), radius, Colour, i == Selected);

            if (!Numbered) continue;

            var number = Text((FirstNumber + i).ToString(CultureInfo.CurrentCulture), palette.MutedBrush);
            context.DrawText(number, new Point(middle - number.Width / 2, top + Size + LabelGap));
        }
    }

    private double NumberWidth()
    {
        double widest = 0;
        for (int i = 0; i < Math.Max(0, Count); i++)
            widest = Math.Max(widest, Text((FirstNumber + i).ToString(CultureInfo.CurrentCulture), Brushes.Black).Width);

        return widest;
    }

    private FormattedText Text(string? text, IBrush brush) =>
        new(text ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), FontSize, brush);
}
