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
    /// <summary>
    /// Backs <see cref="Caption"/>: what this part of the panel is called.
    /// </summary>
    /// <remarks>
    /// Empty for a frame with no name, which takes no room at the top and is drawn as a plain
    /// box. A machine that only wants the parts kept visibly apart does not have to name them.
    /// </remarks>
    public static readonly StyledProperty<string> CaptionProperty =
        AvaloniaProperty.Register<PanelGroup, string>(nameof(Caption), "");

    /// <summary>Backs <see cref="CaptionSize"/>, which also decides how much room the head takes.</summary>
    public static readonly StyledProperty<double> CaptionSizeProperty =
        AvaloniaProperty.Register<PanelGroup, double>(nameof(CaptionSize), 11);

    /// <summary>Backs <see cref="Inset"/>: how far inside the frame its contents sit.</summary>
    public static readonly StyledProperty<double> InsetProperty =
        AvaloniaProperty.Register<PanelGroup, double>(nameof(Inset), 8);

    /// <summary>
    /// Backs <see cref="ContentAlignment"/>: where what is inside sits when the section is
    /// taller than its contents.
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

    /// <inheritdoc cref="ContentAlignmentProperty"/>
    public VerticalAlignment ContentAlignment
    {
        get => GetValue(ContentAlignmentProperty);
        set => SetValue(ContentAlignmentProperty, value);
    }

    /// <summary>The air between the caption and what it names.</summary>
    private const double CaptionGap = 5;

    /// <summary>How much the frame's corners are rounded.</summary>
    private const double Corner = 4;

    /// <summary>Says which properties change the picture, the size, and where the contents go.</summary>
    static PanelGroup()
    {
        AffectsRender<PanelGroup>(CaptionProperty, CaptionSizeProperty);
        AffectsMeasure<PanelGroup>(CaptionProperty, CaptionSizeProperty, InsetProperty);
        AffectsArrange<PanelGroup>(ContentAlignmentProperty);
    }

    /// <inheritdoc cref="CaptionProperty"/>
    public string Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>How large the caption is written.</summary>
    public double CaptionSize
    {
        get => GetValue(CaptionSizeProperty);
        set => SetValue(CaptionSizeProperty, value);
    }

    /// <summary>How far inside the frame its contents sit, on every side.</summary>
    public double Inset
    {
        get => GetValue(InsetProperty);
        set => SetValue(InsetProperty, value);
    }

    /// <summary>How much room the caption takes off the top, or none when there is no caption.</summary>
    private double Head => Caption.Length == 0 ? 0 : Math.Ceiling(CaptionSize * 1.35) + CaptionGap;

    /// <summary>
    /// Room for whatever is inside, plus the inset all round and the caption on top.
    /// </summary>
    /// <remarks>
    /// Wide enough for the caption as well as the contents, or a short row under a long name
    /// would have the name running out of its own frame.
    ///
    /// The caption is measured against the fallback palette, since only its width is wanted here
    /// and the width of a piece of text does not depend on what colour it is drawn in.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        double inset = Math.Max(0, Inset);
        double head = Head;

        var room = new Size(
            Math.Max(0, availableSize.Width - inset * 2),
            Math.Max(0, availableSize.Height - inset * 2 - head));

        Child?.Measure(room);

        var wanted = Child?.DesiredSize ?? default;

        double captionWidth = Caption.Length == 0 ? 0 : Label(ThemePalette.Fallback.Text).Width;

        return new Size(
            Math.Max(wanted.Width, captionWidth) + inset * 2,
            wanted.Height + inset * 2 + head);
    }

    /// <summary>
    /// Places the contents inside the frame, under the caption, at whichever height
    /// <see cref="ContentAlignment"/> asks for.
    /// </summary>
    /// <remarks>
    /// Sections in a row are all as tall as the tallest of them, so a short one has room to
    /// spare and where that room goes is the machine's to say.
    /// </remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        double inset = Math.Max(0, Inset);
        double head = Head;

        double room = Math.Max(0, finalSize.Height - inset * 2 - head);

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

    /// <summary>
    /// Paints the frame and the caption sitting inside its top left corner.
    /// </summary>
    /// <remarks>
    /// The box is drawn on half pixels so its one pixel line lands on a pixel rather than
    /// straddling two and coming out grey and two wide.
    /// </remarks>
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

    /// <summary>
    /// The caption laid out, for measuring and for drawing.
    /// </summary>
    /// <remarks>
    /// Trimming is off: the frame is measured to fit the caption, so there is nothing to trim,
    /// and a caption that came out with an ellipsis would mean the measure had gone wrong rather
    /// than that the name was too long. The look being aimed at is a panel's silkscreen, which
    /// the app already gets elsewhere on the octave lamps and the location row.
    /// </remarks>
    private FormattedText Label(Color colour) =>
        new(Caption,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            CaptionSize,
            new SolidColorBrush(colour))
        {
            Trimming = TextTrimming.None
        };

    /// <summary>
    /// Starts listening for the theme moving.
    /// </summary>
    /// <remarks>
    /// The same wiring <see cref="ThemedControl"/> carries, written out again because this is a
    /// decorator rather than a control and cannot inherit from it. A frame painted in
    /// <c>Render</c> hears nothing about a theme swap on its own, so it keeps the colours it was
    /// last painted with.
    /// </remarks>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        ActualThemeVariantChanged += OnThemeChanged;
        ResourcesChanged += OnResourcesChanged;

        InvalidateVisual();
    }

    /// <summary>Stops listening, so a frame off the tree is not kept alive by the theme.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        ActualThemeVariantChanged -= OnThemeChanged;
        ResourcesChanged -= OnResourcesChanged;
    }

    /// <summary>The theme variant moved, so what was painted is the wrong colours now.</summary>
    private void OnThemeChanged(object? sender, EventArgs e) => InvalidateVisual();

    /// <summary>
    /// A resource dictionary somewhere above changed, which is how a whole theme is swapped
    /// rather than a variant flipped.
    /// </summary>
    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => InvalidateVisual();
}
