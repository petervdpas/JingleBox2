using Avalonia;
using Avalonia.Media;
using System;
using System.Globalization;
using JingleBox2.Rack.Controls.Records;

namespace JingleBox2.Rack.Controls;

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
    /// <summary>The air between a lamp and the number under it.</summary>
    private const double LabelGap = 3;

    /// <summary>And between the caption and the row it names, which is the wider of the two.</summary>
    private const double CaptionGap = 4;

    /// <summary>Backs <see cref="Count"/>: how many lamps are in the row.</summary>
    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<LedRow, int>(nameof(Count), 8);

    /// <summary>
    /// Backs <see cref="Selected"/>: which lamp is lit, counted from zero.
    /// </summary>
    /// <remarks>
    /// Minus one to start with, so a row bound to nothing yet is a row with nothing lit rather
    /// than one claiming to be on its first step.
    /// </remarks>
    public static readonly StyledProperty<int> SelectedProperty =
        AvaloniaProperty.Register<LedRow, int>(nameof(Selected), -1);

    /// <summary>
    /// Backs <see cref="FirstNumber"/>: what the numbering under the lamps starts at.
    /// </summary>
    /// <remarks>
    /// One rather than nought, since a row of steps is counted the way a musician counts them.
    /// An octave row says so by starting at nought.
    /// </remarks>
    public static readonly StyledProperty<int> FirstNumberProperty =
        AvaloniaProperty.Register<LedRow, int>(nameof(FirstNumber), 1);

    /// <summary>Backs <see cref="Numbered"/>: whether a number is written under each lamp.</summary>
    public static readonly StyledProperty<bool> NumberedProperty =
        AvaloniaProperty.Register<LedRow, bool>(nameof(Numbered), true);

    /// <summary>Backs <see cref="Caption"/>, written above the row the way a panel names a section.</summary>
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<LedRow, string?>(nameof(Caption));

    /// <summary>Backs <see cref="Colour"/>: what the lamps burn, red unless a machine says otherwise.</summary>
    public static readonly StyledProperty<Color> ColourProperty =
        AvaloniaProperty.Register<LedRow, Color>(nameof(Colour), Color.FromRgb(0xE5, 0x39, 0x35));

    /// <summary>
    /// Backs <see cref="Size"/>: how big one lamp is across.
    /// </summary>
    /// <remarks>
    /// Smaller than a lamp on its own, since a row of eight has to fit across a panel and the
    /// row itself is what is being read rather than any one of them.
    /// </remarks>
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<LedRow, double>(nameof(Size), 9.0);

    /// <summary>Backs <see cref="Gap"/>: the space between one lamp and the next.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<LedRow, double>(nameof(Gap), 9.0);

    /// <summary>Backs <see cref="FontSize"/>, the size of the numbers and the caption.</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<LedRow, double>(nameof(FontSize), 8.5);

    /// <summary>
    /// Says which properties change the picture and which change the size.
    /// </summary>
    /// <remarks>
    /// <see cref="SelectedProperty"/> is in the render list alone: moving the lit lamp along the
    /// row changes nothing about how much room the row takes, and a step sequencer writes it
    /// several times a second.
    /// </remarks>
    static LedRow()
    {
        AffectsRender<LedRow>(
            CountProperty, SelectedProperty, FirstNumberProperty, NumberedProperty,
            CaptionProperty, ColourProperty, SizeProperty, GapProperty, FontSizeProperty);

        AffectsMeasure<LedRow>(
            CountProperty, FirstNumberProperty, NumberedProperty, CaptionProperty,
            SizeProperty, GapProperty, FontSizeProperty);
    }

    /// <summary>How many lamps there are, which is what the row is counting up to.</summary>
    public int Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    /// <inheritdoc cref="SelectedProperty"/>
    public int Selected
    {
        get => GetValue(SelectedProperty);
        set => SetValue(SelectedProperty, value);
    }

    /// <inheritdoc cref="FirstNumberProperty"/>
    public int FirstNumber
    {
        get => GetValue(FirstNumberProperty);
        set => SetValue(FirstNumberProperty, value);
    }

    /// <summary>Whether a number is written under each lamp.</summary>
    public bool Numbered
    {
        get => GetValue(NumberedProperty);
        set => SetValue(NumberedProperty, value);
    }

    /// <summary>What the row is called, or nothing where the panel around it already says.</summary>
    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>What the lamps burn.</summary>
    public Color Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    /// <inheritdoc cref="SizeProperty"/>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>The space between one lamp and the next.</summary>
    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <summary>How big the numbers and the caption are.</summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// How wide one lamp and its number take, so the row spaces evenly whichever is wider.
    /// </summary>
    /// <remarks>
    /// Measured against the widest number in the row rather than against each lamp's own, or a
    /// row that ran past nine would space unevenly from that point on.
    /// </remarks>
    private double Pitch => Math.Max(Size, Numbered ? NumberWidth() : 0) + Gap;

    /// <summary>
    /// Room for the lamps, their numbers, and the caption over them.
    /// </summary>
    /// <remarks>
    /// One gap is taken back off the width, since the pitch carries a gap per lamp and the last
    /// one has nothing after it to be spaced from.
    /// </remarks>
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

    /// <summary>
    /// The caption, then the lamps with their numbers, the row centred on whatever width it was
    /// given.
    /// </summary>
    /// <remarks>
    /// Each lamp is drawn through <see cref="Led.DrawLamp"/> rather than by holding eight
    /// <see cref="Led"/> controls, so a row and the single lamp beside it are the same lamp. The
    /// controls would each need their own binding to work out whether they were the lit one.
    /// </remarks>
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

    /// <summary>The widest number the row will print, which is what sets the spacing.</summary>
    private double NumberWidth()
    {
        double widest = 0;
        for (int i = 0; i < Math.Max(0, Count); i++)
            widest = Math.Max(widest, Text((FirstNumber + i).ToString(CultureInfo.CurrentCulture), Brushes.Black).Width);

        return widest;
    }

    /// <summary>A number or the caption, laid out for measuring or for drawing.</summary>
    private FormattedText Text(string? text, IBrush brush) =>
        new(text ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), FontSize, brush);
}
