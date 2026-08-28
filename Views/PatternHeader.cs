using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.Tracker;
using JingleBox2.Machines.Ui;

namespace JingleBox2.Views;

/// <summary>
/// The track names above a pattern, with the selected one picked out. Sits outside the
/// pattern's scroll area so it stays put vertically, and takes the horizontal scroll offset
/// so it stays aligned with the columns it names.
/// </summary>
/// <remarks>
/// The square above the line numbers names no track, which makes it the one place in this row
/// where something that is not a thing you touch can sit. The pattern's help badge is laid over
/// it for exactly that reason.
/// </remarks>
public sealed class PatternHeader : ThemedControl
{
    /// <summary>How many tracks are named, which is how many the pattern has.</summary>
    public static readonly StyledProperty<int> TrackCountProperty =
        AvaloniaProperty.Register<PatternHeader, int>(nameof(TrackCount), Song.DefaultTrackCount);

    /// <summary>Which one the cursor is in, drawn picked out from the rest.</summary>
    public static readonly StyledProperty<int> SelectedTrackProperty =
        AvaloniaProperty.Register<PatternHeader, int>(nameof(SelectedTrack));

    /// <inheritdoc cref="CharWidth"/>
    public static readonly StyledProperty<double> CharWidthProperty =
        AvaloniaProperty.Register<PatternHeader, double>(nameof(CharWidth), 8);

    /// <inheritdoc cref="ScrollOffset"/>
    public static readonly StyledProperty<double> ScrollOffsetProperty =
        AvaloniaProperty.Register<PatternHeader, double>(nameof(ScrollOffset));

    /// <summary>The pattern's own row height, which the header's height and lettering follow.</summary>
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<PatternHeader, double>(nameof(RowHeight), 18);

    /// <inheritdoc cref="DropTargetTrack"/>
    public static readonly StyledProperty<int> DropTargetTrackProperty =
        AvaloniaProperty.Register<PatternHeader, int>(nameof(DropTargetTrack), -1);

    /// <summary>Only the row height changes the room asked for; the rest only changes the paint.</summary>
    static PatternHeader()
    {
        AffectsRender<PatternHeader>(TrackCountProperty, SelectedTrackProperty,
            CharWidthProperty, ScrollOffsetProperty, RowHeightProperty, DropTargetTrackProperty);
        AffectsMeasure<PatternHeader>(RowHeightProperty);
    }

    /// <inheritdoc cref="TrackCountProperty"/>
    public int TrackCount
    {
        get => GetValue(TrackCountProperty);
        set => SetValue(TrackCountProperty, value);
    }

    /// <inheritdoc cref="SelectedTrackProperty"/>
    public int SelectedTrack
    {
        get => GetValue(SelectedTrackProperty);
        set => SetValue(SelectedTrackProperty, value);
    }

    /// <summary>Taken from the grid, so both lay out on identical measurements.</summary>
    public double CharWidth
    {
        get => GetValue(CharWidthProperty);
        set => SetValue(CharWidthProperty, value);
    }

    /// <summary>How far the pattern below has been scrolled sideways.</summary>
    public double ScrollOffset
    {
        get => GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    /// <inheritdoc cref="RowHeightProperty"/>
    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    /// <summary>The track a drag is currently hovering, or -1. Drawn as a drop outline.</summary>
    public int DropTargetTrack
    {
        get => GetValue(DropTargetTrackProperty);
        set => SetValue(DropTargetTrackProperty, value);
    }

    /// <summary>Raised when a header is clicked, so the cursor can jump to that track.</summary>
    public event EventHandler<int>? TrackClicked;

    /// <summary>The track under a point, for drag and drop. Takes the scroll offset into account.</summary>
    public int TrackAtPoint(Point point)
    {
        double x = point.X + ScrollOffset;
        return x < Metrics.GutterWidth ? -1 : Metrics.TrackAt(x);
    }

    /// <summary>Above and below the names, so the tabs stand off the pattern under them.</summary>
    private const double VerticalPadding = 5;

    /// <summary>
    /// The same layout the grid uses, built from the same character width so the two cannot
    /// drift apart.
    /// </summary>
    /// <remarks>
    /// Without the pattern's padding, since the header has no lines above or below it: it is one
    /// row standing outside the scroll area.
    /// </remarks>
    private PatternMetrics Metrics => new(CharWidth, RowHeight, TrackCount);

    /// <summary>
    /// One row tall, and no width of its own: the header is stretched to whatever the pattern
    /// beneath it is being seen through, and the tabs are placed inside that by the transform.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize) =>
        new(0, RowHeight + VerticalPadding * 2);

    /// <summary>
    /// A tab per track, in the pattern's own columns.
    /// </summary>
    /// <remarks>
    /// Everything is shifted by the pattern's sideways scroll, so a name stays over the column it
    /// names rather than over whichever column happens to be at that place on screen.
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        if (TrackCount <= 0 || CharWidth <= 0) return;

        var metrics = Metrics;
        double height = Bounds.Height;

        var palette = ThemePalette.From(this);
        var text = palette.TextBrush;
        var muted = palette.MutedBrush;
        var selectedPen = new Pen(palette.AccentBrush, 1);
        var idlePen = new Pen(palette.BorderBrush, 1);
        var dropPen = new Pen(palette.AccentBrush, 2);
        var selectedFill = palette.AccentTint(56);
        var idleFill = palette.RowShade(0x12);

        using var _ = context.PushTransform(Matrix.CreateTranslation(-ScrollOffset, 0));

        double fontSize = Math.Max(9, RowHeight - 6);
        var typeface = new Typeface(PatternFont.Family);

        for (int track = 0; track < TrackCount; track++)
        {
            double x = metrics.TrackDividerX(track);
            var area = new Rect(x + 1, 2, metrics.TrackWidth - 2, height - 4);
            bool selected = track == SelectedTrack;

            bool dropTarget = track == DropTargetTrack;

            context.FillRectangle(dropTarget ? palette.AccentTint(90) : selected ? selectedFill : idleFill, area, 3);
            context.DrawRectangle(dropTarget ? dropPen : selected ? selectedPen : idlePen, area, 3);

            string label = "Track " + (track + 1).ToString("00", CultureInfo.InvariantCulture);
            var formatted = new FormattedText(label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, fontSize, selected || dropTarget ? text : muted)
            {
                MaxTextWidth = Math.Max(1, area.Width - 6),
                Trimming = TextTrimming.CharacterEllipsis
            };

            context.DrawText(formatted, new Point(
                area.X + Math.Max(2, (area.Width - formatted.Width) / 2),
                area.Y + (area.Height - formatted.Height) / 2));
        }
    }

    /// <summary>
    /// A click on a tab puts the cursor in that track.
    /// </summary>
    /// <remarks>
    /// A click over the line number gutter does nothing, since that square names no track.
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (TrackCount <= 0) return;

        double x = e.GetPosition(this).X + ScrollOffset;
        if (x < Metrics.GutterWidth) return;

        TrackClicked?.Invoke(this, Metrics.TrackAt(x));
        e.Handled = true;
    }

}
