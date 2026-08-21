using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.Tracker;

namespace JingleBox2.Views;

/// <summary>
/// Draws a pattern and takes keyboard input for it. Custom-drawn rather than built from
/// controls: a 64 line pattern across 8 tracks is 2048 cells, and one Render pass over the
/// visible rows costs a fraction of what that many TextBlocks would.
/// </summary>
public sealed class PatternGrid : ThemedControl
{
    public static readonly StyledProperty<Pattern?> PatternProperty =
        AvaloniaProperty.Register<PatternGrid, Pattern?>(nameof(Pattern));

    public static readonly StyledProperty<PatternCursor> EditCursorProperty =
        AvaloniaProperty.Register<PatternGrid, PatternCursor>(nameof(EditCursor), PatternCursor.Start);

    public static readonly StyledProperty<int> PlayingLineProperty =
        AvaloniaProperty.Register<PatternGrid, int>(nameof(PlayingLine), -1);

    public static readonly StyledProperty<int> LinesPerBeatProperty =
        AvaloniaProperty.Register<PatternGrid, int>(nameof(LinesPerBeat), TrackerTiming.DefaultLinesPerBeat);

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<PatternGrid, double>(nameof(RowHeight), 18);

    public static readonly StyledProperty<int> DropTargetTrackProperty =
        AvaloniaProperty.Register<PatternGrid, int>(nameof(DropTargetTrack), -1);

    static PatternGrid()
    {
        AffectsRender<PatternGrid>(PatternProperty, EditCursorProperty, PlayingLineProperty,
            LinesPerBeatProperty, RowHeightProperty, DropTargetTrackProperty);
        AffectsMeasure<PatternGrid>(PatternProperty, RowHeightProperty);
        FocusableProperty.OverrideDefaultValue<PatternGrid>(true);
    }

    public Pattern? Pattern
    {
        get => GetValue(PatternProperty);
        set => SetValue(PatternProperty, value);
    }

    public PatternCursor EditCursor
    {
        get => GetValue(EditCursorProperty);
        set => SetValue(EditCursorProperty, value);
    }

    /// <summary>The line the player is on, or -1 when stopped.</summary>
    public int PlayingLine
    {
        get => GetValue(PlayingLineProperty);
        set => SetValue(PlayingLineProperty, value);
    }

    public int LinesPerBeat
    {
        get => GetValue(LinesPerBeatProperty);
        set => SetValue(LinesPerBeatProperty, value);
    }

    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    /// <summary>The track a drag is hovering, or -1. Drawn as a drop outline down the column.</summary>
    public int DropTargetTrack
    {
        get => GetValue(DropTargetTrackProperty);
        set => SetValue(DropTargetTrackProperty, value);
    }

    /// <summary>Raised when a click moves the cursor, so the view model can follow.</summary>
    public event EventHandler<PatternCursor>? CursorMoved;

    /// <summary>The track under a point, or -1 for the line number gutter and empty space.</summary>
    public int TrackAtPoint(Point point)
    {
        var pattern = Pattern;
        if (pattern == null) return -1;

        var metrics = Metrics;
        if (point.X < metrics.GutterWidth || point.X > metrics.ContentWidth) return -1;

        return metrics.TrackAt(point.X);
    }

    private double _charWidth = 8;
    private double _fontSize = 13;
    private Typeface _typeface = new(FontFamily.Default);

    /// <summary>Layout for the pattern currently bound, shared with the header control.</summary>
    public PatternMetrics Metrics => new(_charWidth, RowHeight, Pattern?.TrackCount ?? 0);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (Pattern != null)
        {
            Pattern.Changed -= OnPatternChanged;
            Pattern.Changed += OnPatternChanged;
        }

        // The pattern binding lands after the first measure, so without this the control
        // keeps the zero size it was first measured at and the ScrollViewer clips it away.
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (Pattern != null) Pattern.Changed -= OnPatternChanged;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != PatternProperty) return;

        // Patterns are edited in place, so watching the property alone is not enough: the
        // same object grows tracks and gains notes without the reference ever changing.
        if (change.OldValue is Pattern previous) previous.Changed -= OnPatternChanged;
        if (change.NewValue is Pattern current) current.Changed += OnPatternChanged;

        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnPatternChanged(object? sender, EventArgs e)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var pattern = Pattern;
        if (pattern == null) return new Size(0, 0);

        EnsureMetrics();

        var metrics = Metrics;
        return new Size(metrics.ContentWidth, metrics.ContentHeight(pattern.Lines));
    }

    public override void Render(DrawingContext context)
    {
        var pattern = Pattern;
        if (pattern == null) return;

        EnsureMetrics();

        var metrics = Metrics;
        var bounds = new Rect(Bounds.Size);
        var palette = ThemePalette.From(this);

        var text = palette.TextBrush;
        var muted = palette.MutedBrush;

        double contentHeight = metrics.ContentHeight(pattern.Lines);

        // Before the first arrange the bounds are empty. Drawing the whole pattern is better
        // than culling every row against a size that is not real yet.
        double visibleHeight = bounds.Height > 0 ? bounds.Height : contentHeight;
        double rowWidth = Math.Max(bounds.Width, metrics.ContentWidth);

        var cursor = EditCursor.Clamp(pattern.Lines, pattern.TrackCount);
        var barShade = palette.RowShade(0x1C);
        var beatShade = palette.RowShade(0x0E);

        DrawSelectedTrack(context, metrics, palette, cursor.Track, contentHeight);

        int lpb = Math.Max(1, LinesPerBeat);

        for (int line = 0; line < pattern.Lines; line++)
        {
            double y = metrics.RowY(line);
            if (y + RowHeight < 0 || y > visibleHeight) continue; // outside the viewport

            if (line % (lpb * 4) == 0)
                context.FillRectangle(barShade, new Rect(0, y, rowWidth, RowHeight));
            else if (line % lpb == 0)
                context.FillRectangle(beatShade, new Rect(0, y, rowWidth, RowHeight));

            if (line == PlayingLine)
                context.FillRectangle(palette.AccentTint(60), new Rect(0, y, rowWidth, RowHeight));

            DrawText(context, line.ToString("00", CultureInfo.InvariantCulture), 0, y, muted);

            for (int track = 0; track < pattern.TrackCount; track++)
            {
                var cell = pattern[line, track];

                DrawText(context, cell.Note.ToString(),
                    metrics.ColumnX(track, CellColumn.Note), y, cell.Note.IsEmpty ? muted : text);

                DrawText(context, cell.InstrumentText,
                    metrics.ColumnX(track, CellColumn.Instrument), y,
                    cell.Instrument == TrackerCell.NoInstrument ? muted : text);

                DrawText(context, cell.VolumeText,
                    metrics.ColumnX(track, CellColumn.Volume), y,
                    cell.Volume == TrackerCell.NoVolume ? muted : text);

                DrawText(context, cell.Effect.ToString(),
                    metrics.ColumnX(track, CellColumn.Effect), y, cell.Effect.IsNone ? muted : text);
            }
        }

        DrawTrackSeparators(context, metrics, palette, pattern.TrackCount, contentHeight);
        DrawDropTarget(context, metrics, palette, contentHeight);
        DrawCursor(context, metrics, palette, cursor);
    }

    /// <summary>
    /// A tint down the whole track the cursor is in. On a wide pattern the cursor box alone
    /// is a few pixels of a very repetitive grid, which is not enough to find at a glance.
    /// </summary>
    private static void DrawSelectedTrack(DrawingContext context, PatternMetrics metrics,
        ThemePalette palette, int track, double height)
    {
        context.FillRectangle(palette.AccentTint(22),
            new Rect(metrics.TrackDividerX(track), 0, metrics.TrackWidth, height));
    }

    /// <summary>
    /// A vertical rule down each track boundary. Without them the columns of a wide pattern
    /// read as one block of text and it is hard to tell which track a note belongs to.
    /// </summary>
    private static void DrawTrackSeparators(DrawingContext context, PatternMetrics metrics,
        ThemePalette palette, int trackCount, double height)
    {
        var pen = new Pen(palette.BorderBrush, 1);

        // One after the line numbers, then one at the start of every track after the first.
        for (int track = 0; track <= trackCount; track++)
        {
            double x = Math.Round(metrics.TrackDividerX(track)) - 0.5;
            context.DrawLine(pen, new Point(x, 0), new Point(x, height));
        }
    }

    /// <summary>The column a dragged instrument would land on.</summary>
    private void DrawDropTarget(DrawingContext context, PatternMetrics metrics,
        ThemePalette palette, double height)
    {
        int track = DropTargetTrack;
        if (track < 0 || track >= (Pattern?.TrackCount ?? 0)) return;

        var area = new Rect(metrics.TrackDividerX(track), 0, metrics.TrackWidth, height);

        context.FillRectangle(palette.AccentTint(40), area);
        context.DrawRectangle(new Pen(palette.AccentBrush, 2), area);
    }

    private void DrawCursor(DrawingContext context, PatternMetrics metrics,
        ThemePalette palette, PatternCursor cursor)
    {
        double x = metrics.ColumnX(cursor.Track, cursor.Column);
        double width = metrics.ColumnWidth(cursor.Column);
        double y = metrics.RowY(cursor.Line);
        var area = new Rect(x - 1, y, width + 2, RowHeight);

        context.FillRectangle(palette.AccentTint(48), area);
        context.DrawRectangle(new Pen(palette.AccentBrush, 1), area);
    }

    private void DrawText(DrawingContext context, string text, double x, double y, IBrush brush)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, _typeface, _fontSize, brush);

        context.DrawText(formatted, new Point(x, y + (RowHeight - formatted.Height) / 2));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var pattern = Pattern;
        if (pattern == null) return;

        Focus();

        var point = e.GetPosition(this);
        var cursor = Metrics.CursorAt(point.X, point.Y, pattern.Lines);

        EditCursor = cursor;
        CursorMoved?.Invoke(this, cursor);
        e.Handled = true;
    }

    private void EnsureMetrics()
    {
        // A monospace face is what makes the columns line up; measuring one glyph gives the
        // cell width every column position is derived from.
        _fontSize = Math.Max(9, RowHeight - 5);
        _typeface = new Typeface(PatternFont.Family);

        var probe = new FormattedText("0", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, _typeface, _fontSize, Brushes.White);
        _charWidth = probe.Width > 0 ? probe.Width : _fontSize * 0.6;
    }

}
