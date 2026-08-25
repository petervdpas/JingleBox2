using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Globalization;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// A named part of a panel, framed, so several of them can stand side by side and still read
/// as separate things.
/// </summary>
/// <remarks>
/// A heading above a row says what the row is only while it is the only row. Put two rows of
/// faders next to each other with a heading over each and the headings stop belonging to
/// anything in particular: the eye reads left to right across both. A frame is what tells you
/// where one part ends and the next begins, which is the same job the silkscreened boxes do on
/// a real front panel.
///
/// It holds one thing and stretches to it, so what goes inside is an ordinary row or a
/// <see cref="PanelStrip"/>, unchanged.
/// </remarks>
public class PanelGroup : Decorator
{
    /// <summary>What this part of the panel is called.</summary>
    public static readonly StyledProperty<string> CaptionProperty =
        AvaloniaProperty.Register<PanelGroup, string>(nameof(Caption), "");

    public static readonly StyledProperty<double> CaptionSizeProperty =
        AvaloniaProperty.Register<PanelGroup, double>(nameof(CaptionSize), 11);

    /// <summary>How far inside the frame its contents sit.</summary>
    public static readonly StyledProperty<double> InsetProperty =
        AvaloniaProperty.Register<PanelGroup, double>(nameof(Inset), 8);

    /// <summary>Between the caption and what it names.</summary>
    /// <summary>
    /// Where what is inside sits when the section is taller than its contents.
    /// </summary>
    /// <remarks>
    /// A section in a row is as tall as the tallest section beside it, so a short one has room
    /// to spare and something has to decide where the spare room goes. That is the machine's
    /// choice and not ours: knobs centred in their frame is the usual look on a rack, but a
    /// section whose contents belong under its caption wants them at the top.
    /// </remarks>
    public static readonly StyledProperty<VerticalAlignment> ContentAlignmentProperty =
        AvaloniaProperty.Register<PanelGroup, VerticalAlignment>(
            nameof(ContentAlignment), VerticalAlignment.Center);

    public VerticalAlignment ContentAlignment
    {
        get => GetValue(ContentAlignmentProperty);
        set => SetValue(ContentAlignmentProperty, value);
    }

    private const double CaptionGap = 5;

    private const double Corner = 4;

    static PanelGroup()
    {
        AffectsRender<PanelGroup>(CaptionProperty, CaptionSizeProperty);
        AffectsMeasure<PanelGroup>(CaptionProperty, CaptionSizeProperty, InsetProperty);
        AffectsArrange<PanelGroup>(ContentAlignmentProperty);
    }

    public string Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public double CaptionSize
    {
        get => GetValue(CaptionSizeProperty);
        set => SetValue(CaptionSizeProperty, value);
    }

    public double Inset
    {
        get => GetValue(InsetProperty);
        set => SetValue(InsetProperty, value);
    }

    /// <summary>How much room the caption takes off the top, or none when there is no caption.</summary>
    private double Head => Caption.Length == 0 ? 0 : Math.Ceiling(CaptionSize * 1.35) + CaptionGap;

    protected override Size MeasureOverride(Size availableSize)
    {
        double inset = Math.Max(0, Inset);
        double head = Head;

        var room = new Size(
            Math.Max(0, availableSize.Width - inset * 2),
            Math.Max(0, availableSize.Height - inset * 2 - head));

        Child?.Measure(room);

        var wanted = Child?.DesiredSize ?? default;

        // Wide enough for the caption as well as the contents, or a short row under a long name
        // would have the name running out of its own frame.
        double captionWidth = Caption.Length == 0 ? 0 : Label(ThemePalette.Fallback.Text).Width;

        return new Size(
            Math.Max(wanted.Width, captionWidth) + inset * 2,
            wanted.Height + inset * 2 + head);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double inset = Math.Max(0, Inset);
        double head = Head;

        double room = Math.Max(0, finalSize.Height - inset * 2 - head);

        // Sections in a row are all as tall as the tallest of them, so a short one has room to
        // spare, and where that room goes is the machine's to say.
        double wanted = Math.Min(room, Child?.DesiredSize.Height ?? room);
        double over = Math.Max(0, room - wanted);

        double spare = ContentAlignment switch
        {
            VerticalAlignment.Top => 0,
            VerticalAlignment.Bottom => over,
            VerticalAlignment.Stretch => 0,
            _ => over / 2,
        };

        if (ContentAlignment == VerticalAlignment.Stretch) wanted = room;

        Child?.Arrange(new Rect(
            inset,
            inset + head + spare,
            Math.Max(0, finalSize.Width - inset * 2),
            wanted));

        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width;
        double height = Bounds.Height;

        if (width <= 1 || height <= 1) return;

        var palette = ThemePalette.From(this);

        context.DrawRectangle(
            new SolidColorBrush(palette.Surface, 0.35),
            new Pen(new SolidColorBrush(palette.Border, 0.9), 1),
            new RoundedRect(new Rect(0.5, 0.5, width - 1, height - 1), Corner));

        if (Caption.Length == 0) return;

        context.DrawText(Label(palette.Muted), new Point(Math.Max(0, Inset), Math.Max(0, Inset) - 1));
    }

    private FormattedText Label(Color colour) =>
        new(Caption,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            CaptionSize,
            new SolidColorBrush(colour))
        {
            // Small caps are what a panel's silkscreen does, and the app's own captions already
            // do it on the octave lamps and the location row.
            Trimming = TextTrimming.None
        };

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        ActualThemeVariantChanged += OnThemeChanged;
        ResourcesChanged += OnResourcesChanged;

        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        ActualThemeVariantChanged -= OnThemeChanged;
        ResourcesChanged -= OnResourcesChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => InvalidateVisual();
}
