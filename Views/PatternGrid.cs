using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.Tracker;
using JingleBox2.Machines.Ui;

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

    /// <summary>The block being worked on, dragged here and shown here.</summary>
    public static readonly StyledProperty<PatternSelection> SelectionProperty =
        AvaloniaProperty.Register<PatternGrid, PatternSelection>(
            nameof(Selection), PatternSelection.None, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> DropTargetTrackProperty =
        AvaloniaProperty.Register<PatternGrid, int>(nameof(DropTargetTrack), -1);

    /// <summary>The pattern before this one in the song, drawn dimmed above line 00.</summary>
    public static readonly StyledProperty<Pattern?> BeforeProperty =
        AvaloniaProperty.Register<PatternGrid, Pattern?>(nameof(Before));

    /// <summary>The one after it, drawn dimmed below the last line.</summary>
    public static readonly StyledProperty<Pattern?> AfterProperty =
        AvaloniaProperty.Register<PatternGrid, Pattern?>(nameof(After));

    /// <summary>
    /// Half the height of the window this is being looked at through, less half a row: how far
    /// the middle of the screen is from either edge.
    /// </summary>
    /// <remarks>
    /// Set by whoever owns the scroll viewer. This control is measured inside one with no
    /// height limit, so it never learns how tall the hole it is seen through is, and the amount
    /// of a neighbouring pattern worth drawing is exactly that.
    /// </remarks>
    public static readonly StyledProperty<double> HalfViewProperty =
        AvaloniaProperty.Register<PatternGrid, double>(nameof(HalfView), 0);

    static PatternGrid()
    {
        AffectsRender<PatternGrid>(PatternProperty, EditCursorProperty, PlayingLineProperty,
            LinesPerBeatProperty, RowHeightProperty, DropTargetTrackProperty,
            BeforeProperty, AfterProperty, HalfViewProperty);
        AffectsMeasure<PatternGrid>(PatternProperty, RowHeightProperty,
            BeforeProperty, AfterProperty, HalfViewProperty);
        FocusableProperty.OverrideDefaultValue<PatternGrid>(true);
    }

    public Pattern? Before
    {
        get => GetValue(BeforeProperty);
        set => SetValue(BeforeProperty, value);
    }

    public Pattern? After
    {
        get => GetValue(AfterProperty);
        set => SetValue(AfterProperty, value);
    }

    public double HalfView
    {
        get => GetValue(HalfViewProperty);
        set => SetValue(HalfViewProperty, value);
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
    public PatternSelection Selection
    {
        get => GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

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
    public PatternMetrics Metrics =>
        new(_charWidth, RowHeight, Pattern?.TrackCount ?? 0, Pad, Pad);

    /// <summary>
    /// Half a screen, above the pattern and below it, whether or not there is anything to put
    /// in it.
    /// </summary>
    /// <remarks>
    /// Always, which is the point: the cursor is on the middle of the screen at line 00 of the
    /// first pattern in a song exactly as it is anywhere else, and what is above it there is
    /// blank rather than absent. The space is the rule; a neighbouring pattern is only what
    /// fills it when one is really coming.
    /// </remarks>
    private double Pad => Math.Max(0, HalfView);

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
        DrawSelection(context, metrics, palette, pattern);

        int lpb = Math.Max(1, LinesPerBeat);

        // The neighbours first, so the shading and the cursor of the pattern being worked on
        // are drawn over them rather than under.
        DrawNeighbours(context, metrics, palette, rowWidth, pattern.Lines);

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

            DrawRow(context, metrics, pattern, line, y, text, muted);
        }

        DrawTrackSeparators(context, metrics, palette, pattern.TrackCount, contentHeight);
        DrawDropTarget(context, metrics, palette, contentHeight);
        DrawCursor(context, metrics, palette, cursor);
    }

    /// <summary>One line: its number in the gutter and one cell per track.</summary>
    private void DrawRow(DrawingContext context, PatternMetrics metrics, Pattern pattern,
        int line, double y, IBrush text, IBrush muted)
    {
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

    /// <summary>
    /// The end of the pattern before this one above line 00, and the start of the one after it
    /// below the last line, both dimmed.
    /// </summary>
    /// <remarks>
    /// What the space is for, when there is anything to put in it. The last bar you played
    /// into stays on screen while you write the first bar of the next. Nothing at all is drawn
    /// where a song has no pattern coming, and that is not a gap to be closed up: the space
    /// stays and the cursor stays on the middle of the screen with it.
    ///
    /// Dimmed rather than drawn plainly, because these rows are context. Nothing here can be
    /// typed into or selected, and a click in it lands on the nearest row of the pattern that
    /// can be: a row that looked the same as the ones you can edit would be a trap.
    /// </remarks>
    private void DrawNeighbours(DrawingContext context, PatternMetrics metrics,
        ThemePalette palette, double rowWidth, int lines)
    {
        var ghost = palette.MutedBrush;
        var text = palette.TextBrush;

        // Faded as a whole rather than by choosing paler colours, so a note that is really
        // there still reads as one and an empty cell still reads as empty. Picking the muted
        // colour for everything made a neighbour look exactly like an empty pattern, which is
        // the one thing it must not look like.
        using var faded = context.PushOpacity(GhostOpacity);

        if (Before is { } before && metrics.TopPad > 0)
        {
            int rows = (int)Math.Ceiling(metrics.TopPad / RowHeight);

            for (int back = 1; back <= rows && back <= before.Lines; back++)
                DrawGhost(context, metrics, palette, before, before.Lines - back,
                    metrics.TopPad - back * RowHeight, rowWidth, text, ghost);
        }

        if (After is { } after && metrics.BottomPad > 0)
        {
            int rows = (int)Math.Ceiling(metrics.BottomPad / RowHeight);
            double first = metrics.RowY(lines);

            for (int ahead = 0; ahead < rows && ahead < after.Lines; ahead++)
                DrawGhost(context, metrics, palette, after, ahead,
                    first + ahead * RowHeight, rowWidth, text, ghost);
        }
    }

    /// <summary>How much of a neighbouring pattern comes through.</summary>
    private const double GhostOpacity = 0.4;

    private void DrawGhost(DrawingContext context, PatternMetrics metrics, ThemePalette palette,
        Pattern pattern, int line, double y, double rowWidth, IBrush text, IBrush muted)
    {
        context.FillRectangle(palette.RowShade(0x0A), new Rect(0, y, rowWidth, RowHeight));

        DrawRow(context, metrics, pattern, line, y, text, muted);
    }

    /// <summary>
    /// The block, as a wash over the cells it covers. Drawn under the text so the notes in it
    /// stay readable: a selection has to show what it holds, not hide it.
    /// </summary>
    private void DrawSelection(DrawingContext context, PatternMetrics metrics, ThemePalette palette, Pattern pattern)
    {
        var block = Selection.Clamp(pattern.Lines, pattern.TrackCount);
        if (block.IsEmpty) return;

        double top = metrics.RowY(block.FirstLine);
        double height = block.LineCount * RowHeight;

        double left = metrics.TrackDividerX(block.FirstTrack);
        double width = block.TrackCount * metrics.TrackWidth;

        var area = new Rect(left, top, width, height);

        context.FillRectangle(palette.AccentTint(48), area);
        context.DrawRectangle(null, new Pen(palette.AccentBrush, 1), area);
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
        bool left = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;

        // Shift keeps the anchor where it was, which is how a block is grown after the fact.
        if (left && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            Selection = Selection.IsEmpty
                ? PatternSelection.At(EditCursor).ExtendTo(cursor)
                : Selection.ExtendTo(cursor);
        }
        else if (left)
        {
            // A plain click puts the cursor down and drops any block. A drag from here turns
            // into one as soon as the pointer moves onto another cell.
            _dragAnchor = cursor;
            Selection = PatternSelection.None;
        }
        else if (!Selection.Contains(cursor.Line, cursor.Track))
        {
            // A right click outside the block works on what was clicked, not on the block
            // that happens to be somewhere else.
            Selection = PatternSelection.None;
        }

        EditCursor = cursor;
        CursorMoved?.Invoke(this, cursor);

        // A right click moves the cursor too, so the menu that follows acts on the track
        // under the pointer. It is deliberately not handled: handling it here swallows the
        // request for the menu itself.
        e.Handled = left;
    }

    /// <summary>Where a left press landed, so a drag from it can become a block.</summary>
    private PatternCursor? _dragAnchor;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var pattern = Pattern;
        if (pattern == null || _dragAnchor == null) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var point = e.GetPosition(this);
        var at = Metrics.CursorAt(point.X, point.Y, pattern.Lines);

        // Still on the cell it started on: a click, not a drag, and a click selects nothing.
        if (Selection.IsEmpty && at.Line == _dragAnchor.Value.Line && at.Track == _dragAnchor.Value.Track) return;

        Selection = Selection.IsEmpty
            ? PatternSelection.At(_dragAnchor.Value).ExtendTo(at)
            : Selection.ExtendTo(at);

        EditCursor = at;
        CursorMoved?.Invoke(this, at);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        _dragAnchor = null;
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
