using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.Tracker;
using JingleBox2.Rack.Controls;
using JingleBox2.Tracker.Enums;
using JingleBox2.Rack.Controls.Records;
using JingleBox2.Tracker.Records;
using JingleBox2.UI;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// Draws a pattern and takes keyboard input for it. Custom-drawn rather than built from
/// controls: a 64 line pattern across 8 tracks is 2048 cells, and one Render pass over the
/// visible rows costs a fraction of what that many TextBlocks would.
/// </summary>
/// <remarks>
/// The cursor stays on the middle of the screen and the pattern runs under it, which is what
/// every tracker does and what makes the line you are working on somewhere your eye can rest
/// rather than a highlight to follow down the page. Always, with no exceptions: line 00 of a
/// song's first pattern is on the middle exactly as any other row is, and what is above it there
/// is blank. See <see cref="Pad"/>.
/// </remarks>
public sealed class PatternGrid : ThemedControl
{
    /// <summary>The pattern being worked on. Edited in place, so its own event is watched too.</summary>
    public static readonly StyledProperty<Pattern?> PatternProperty =
        AvaloniaProperty.Register<PatternGrid, Pattern?>(nameof(Pattern));

    /// <summary>Where typing goes: the line, the track and which column of it.</summary>
    public static readonly StyledProperty<PatternCursor> EditCursorProperty =
        AvaloniaProperty.Register<PatternGrid, PatternCursor>(nameof(EditCursor), PatternCursor.Start);

    /// <summary>Which rows are shaded: every beat, and more strongly every fourth of them.</summary>
    public static readonly StyledProperty<int> LinesPerBeatProperty =
        AvaloniaProperty.Register<PatternGrid, int>(nameof(LinesPerBeat), TrackerTiming.DefaultLinesPerBeat);

    /// <summary>How tall a line is, which also decides the lettering it is drawn in.</summary>
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<PatternGrid, double>(nameof(RowHeight), 18);

    /// <summary>The block being worked on, dragged here and shown here.</summary>
    public static readonly StyledProperty<PatternSelection> SelectionProperty =
        AvaloniaProperty.Register<PatternGrid, PatternSelection>(
            nameof(Selection), PatternSelection.None, defaultBindingMode: BindingMode.TwoWay);

    /// <inheritdoc cref="DropTargetTrack"/>
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

    /// <summary>
    /// What changes the drawing and what changes the room asked for, and the grid takes the
    /// keyboard, which is what makes it focusable.
    /// </summary>
    static PatternGrid()
    {
        AffectsRender<PatternGrid>(PatternProperty, EditCursorProperty,
            LinesPerBeatProperty, RowHeightProperty, DropTargetTrackProperty,
            BeforeProperty, AfterProperty, HalfViewProperty);
        AffectsMeasure<PatternGrid>(PatternProperty, RowHeightProperty,
            BeforeProperty, AfterProperty, HalfViewProperty);
        FocusableProperty.OverrideDefaultValue<PatternGrid>(true);
    }

    /// <inheritdoc cref="BeforeProperty"/>
    public Pattern? Before
    {
        get => GetValue(BeforeProperty);
        set => SetValue(BeforeProperty, value);
    }

    /// <inheritdoc cref="AfterProperty"/>
    public Pattern? After
    {
        get => GetValue(AfterProperty);
        set => SetValue(AfterProperty, value);
    }

    /// <inheritdoc cref="HalfViewProperty"/>
    public double HalfView
    {
        get => GetValue(HalfViewProperty);
        set => SetValue(HalfViewProperty, value);
    }

    /// <inheritdoc cref="PatternProperty"/>
    public Pattern? Pattern
    {
        get => GetValue(PatternProperty);
        set => SetValue(PatternProperty, value);
    }

    /// <inheritdoc cref="EditCursorProperty"/>
    public PatternCursor EditCursor
    {
        get => GetValue(EditCursorProperty);
        set => SetValue(EditCursorProperty, value);
    }

    /// <inheritdoc cref="LinesPerBeatProperty"/>
    public int LinesPerBeat
    {
        get => GetValue(LinesPerBeatProperty);
        set => SetValue(LinesPerBeatProperty, value);
    }

    /// <inheritdoc cref="RowHeightProperty"/>
    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    /// <inheritdoc cref="SelectionProperty"/>
    public PatternSelection Selection
    {
        get => GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
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

    /// <summary>
    /// One glyph's width in the monospaced face, which every column position is worked out from.
    /// </summary>
    /// <remarks>Measured in <see cref="EnsureMetrics"/> rather than assumed.</remarks>
    private double _charWidth = 8;

    /// <summary>The lettering size, taken from the row height so the two cannot disagree.</summary>
    private double _fontSize = 13;

    /// <summary>The monospaced face. A proportional one would not line the columns up at all.</summary>
    private Typeface _typeface = new(FontFamily.Default);

    /// <summary>Layout for the pattern currently bound, shared with the header control.</summary>
    /// <remarks>
    /// The note column counts come off the pattern itself rather than off the song, so the
    /// picture and the storage cannot disagree about how wide a track is: a click lands where
    /// the cells really are.
    /// </remarks>
    public PatternMetrics Metrics =>
        new(_charWidth, RowHeight, Pattern?.TrackCount ?? 0, Pad, Pad, Widths);

    /// <summary>How many note columns each track of the bound pattern shows.</summary>
    /// <remarks>
    /// Made per read rather than kept, because a pattern is edited in place: a list cached here
    /// would be the shape the pattern had when it was last bound.
    /// </remarks>
    private NoteColumns Widths
    {
        get
        {
            if (Pattern is not { } pattern) return default;

            var counts = new int[pattern.TrackCount];
            for (int track = 0; track < counts.Length; track++) counts[track] = pattern.ColumnsOn(track);

            return new NoteColumns(counts);
        }
    }

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

    /// <summary>
    /// Takes up the pattern's own event, and asks to be measured again.
    /// </summary>
    /// <remarks>
    /// The pattern binding lands after the first measure, so without that the control keeps the
    /// nought size it was first measured at and the scroll viewer clips it away entirely.
    /// </remarks>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (Pattern != null)
        {
            Pattern.Changed -= OnPatternChanged;
            Pattern.Changed += OnPatternChanged;
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Lets go of the pattern's event, since the grid is no longer drawing it.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (Pattern != null) Pattern.Changed -= OnPatternChanged;
    }

    /// <summary>
    /// Moves the subscription from the pattern that was here to the one that is now.
    /// </summary>
    /// <remarks>
    /// Patterns are edited in place, so watching the property alone is not enough: the same
    /// object grows tracks and gains notes without the reference ever changing.
    /// </remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != PatternProperty) return;

        if (change.OldValue is Pattern previous) previous.Changed -= OnPatternChanged;
        if (change.NewValue is Pattern current) current.Changed += OnPatternChanged;

        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>The pattern said something: it may have grown, so the room is asked for again.</summary>
    private void OnPatternChanged(object? sender, EventArgs e)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>
    /// Every track across, and every line plus half a screen at each end down.
    /// </summary>
    /// <remarks>
    /// Whatever room it is offered, since it is measured inside a scroll viewer with no height
    /// limit and the whole point is that the pattern is taller than the hole it is seen through.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        var pattern = Pattern;
        if (pattern == null) return new Size(0, 0);

        EnsureMetrics();

        var metrics = Metrics;
        return new Size(metrics.ContentWidth, metrics.ContentHeight(pattern.Lines));
    }

    /// <summary>
    /// The whole page: the track tint, the block, the neighbours, then every visible line with
    /// its shading and its cells, and the rules, the drop column and the cursor over them.
    /// </summary>
    /// <remarks>
    /// The neighbours are drawn first, so the shading and the cursor of the pattern being worked
    /// on go over them rather than under.
    ///
    /// Before the first arrange the bounds are empty, and then the whole pattern is drawn: that
    /// is cheaper than culling every row against a size that is not real yet, and it is one pass.
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        var pattern = Pattern;
        if (pattern == null) return;

        EnsureMetrics();

        var metrics = Metrics;
        var bounds = new Rect(Bounds.Size);
        var palette = ThemePalette.From(this);

        var text = palette.Text;
        var muted = palette.Muted;

        double contentHeight = metrics.ContentHeight(pattern.Lines);

        double visibleHeight = bounds.Height > 0 ? bounds.Height : contentHeight;
        double rowWidth = Math.Max(bounds.Width, metrics.ContentWidth);

        var cursor = EditCursor.Clamp(pattern.Lines, pattern.TrackCount, metrics.Columns);
        var barShade = palette.RowShade(0x1C);
        var beatShade = palette.RowShade(0x0E);

        DrawSelectedTrack(context, metrics, palette, cursor.Track, contentHeight);
        DrawSelection(context, metrics, palette, pattern);

        int lpb = Math.Max(1, LinesPerBeat);

        DrawNeighbours(context, metrics, palette, rowWidth, pattern.Lines);

        for (int line = 0; line < pattern.Lines; line++)
        {
            double y = metrics.RowY(line);
            if (y + RowHeight < 0 || y > visibleHeight) continue;

            if (line % (lpb * 4) == 0)
                context.FillRectangle(barShade, new Rect(0, y, rowWidth, RowHeight));
            else if (line % lpb == 0)
                context.FillRectangle(beatShade, new Rect(0, y, rowWidth, RowHeight));

            DrawRow(context, metrics, pattern, line, y, text, muted);
        }

        DrawTrackSeparators(context, metrics, palette, pattern.TrackCount, contentHeight);
        DrawDropTarget(context, metrics, palette, contentHeight);
        DrawCursor(context, metrics, palette, cursor);
    }

    /// <summary>One line: its number in the gutter, then every note column of every track.</summary>
    private void DrawRow(DrawingContext context, PatternMetrics metrics, Pattern pattern,
        int line, double y, Color text, Color muted)
    {
        DrawText(context, line.ToString("00", CultureInfo.InvariantCulture), 0, y, muted);

        for (int track = 0; track < pattern.TrackCount; track++)
        {
            for (int column = 0; column < pattern.ColumnsOn(track); column++)
            {
                var cell = pattern[line, track, column];

                DrawText(context, cell.Note.ToString(),
                    metrics.ColumnX(track, CellColumn.Note, column), y,
                    cell.Note.IsEmpty ? muted : text);

                DrawText(context, cell.InstrumentText,
                    metrics.ColumnX(track, CellColumn.Instrument, column), y,
                    cell.Instrument == TrackerCell.NoInstrument ? muted : text);

                DrawText(context, cell.VolumeText,
                    metrics.ColumnX(track, CellColumn.Volume, column), y,
                    cell.Volume == TrackerCell.NoVolume ? muted : text);

                DrawText(context, cell.Effect.ToString(),
                    metrics.ColumnX(track, CellColumn.Effect, column), y,
                    cell.Effect.IsNone ? muted : text);
            }
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
    ///
    /// Faded as a whole rather than by choosing paler colours, so a note that is really there
    /// still reads as one and an empty cell still reads as empty. Picking the muted colour for
    /// everything made a neighbour look exactly like an empty pattern, which is the one thing it
    /// must not look like.
    /// </remarks>
    private void DrawNeighbours(DrawingContext context, PatternMetrics metrics,
        ThemePalette palette, double rowWidth, int lines)
    {
        var ghost = palette.Muted;
        var text = palette.Text;

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

    /// <summary>
    /// One row of a neighbouring pattern, shaded so it reads as a row rather than as loose text
    /// in the space above or below the pattern.
    /// </summary>
    private void DrawGhost(DrawingContext context, PatternMetrics metrics, ThemePalette palette,
        Pattern pattern, int line, double y, double rowWidth, Color text, Color muted)
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
        double width = metrics.TrackDividerX(block.LastTrack + 1) - left;

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
            new Rect(metrics.TrackDividerX(track), 0, metrics.TrackWidth(track), height));
    }

    /// <summary>
    /// A vertical rule down each track boundary. Without them the columns of a wide pattern
    /// read as one block of text and it is hard to tell which track a note belongs to.
    /// </summary>
    /// <remarks>
    /// One after the line numbers, then one at the start of every track, which is why the loop
    /// runs to the track count inclusive.
    /// </remarks>
    private static void DrawTrackSeparators(DrawingContext context, PatternMetrics metrics,
        ThemePalette palette, int trackCount, double height)
    {
        var pen = new Pen(palette.BorderBrush, 1);

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

        var area = new Rect(metrics.TrackDividerX(track), 0, metrics.TrackWidth(track), height);

        context.FillRectangle(palette.AccentTint(40), area);
        context.DrawRectangle(new Pen(palette.AccentBrush, 2), area);
    }

    /// <summary>
    /// The box round the one column typing goes into, a pixel wider each side than the column so
    /// the lettering inside it is not touched by its own outline.
    /// </summary>
    private void DrawCursor(DrawingContext context, PatternMetrics metrics,
        ThemePalette palette, PatternCursor cursor)
    {
        double x = metrics.ColumnX(cursor.Track, cursor.Column, cursor.NoteColumn);
        double width = metrics.ColumnWidth(cursor.Column);
        double y = metrics.RowY(cursor.Line);
        var area = new Rect(x - 1, y, width + 2, RowHeight);

        context.FillRectangle(palette.AccentTint(48), area);
        context.DrawRectangle(new Pen(palette.AccentBrush, 1), area);
    }

    /// <summary>
    /// One piece of lettering, sat on the middle of its row whatever height that is.
    /// </summary>
    /// <remarks>
    /// Laid out once for each thing it ever has to say, and kept. A pattern is a wall of the same
    /// few dozen strings drawn over and over, and laying one out is not free: it is a shaping run
    /// with buffers behind it. Made fresh for every cell of every frame this alone was allocating
    /// **48 megabytes a second** with a pattern on screen and the transport running, which the
    /// runtime answered with sixty collections and a third of a second of every thread stopped in
    /// every five, and a third of a second of stopped threads is a stumble in the audio however
    /// little the mixing itself is doing. On any other page the same transport allocated 0.1.
    ///
    /// Keyed by the colour rather than by the brush, because <see cref="ThemePalette"/> hands back
    /// a new brush on every read: a cache keyed on the object would never hit once and would grow
    /// for ever, which is the same fault one layer along.
    ///
    /// Emptied when the lettering is remeasured, which is the only thing that can make what is in
    /// here wrong, and again if it ever grows past what a pattern could honestly need.
    /// </remarks>
    private void DrawText(DrawingContext context, string text, double x, double y, Color colour)
    {
        var key = (text, colour.ToUInt32());

        if (!_lettering.TryGetValue(key, out var formatted))
        {
            if (_lettering.Count > MostLettering) _lettering.Clear();

            formatted = new FormattedText(text, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, _typeface, _fontSize, Ink(colour));

            _lettering[key] = formatted;
        }

        context.DrawText(formatted, new Point(x, y + (RowHeight - formatted.Height) / 2));
    }

    /// <summary>The brush for a colour, made once.</summary>
    /// <remarks>
    /// Two of them in practice, and they are held for the same reason the lettering is: a brush
    /// made per cell is a brush the collector has to take away again.
    /// </remarks>
    /// <param name="colour">What to paint with.</param>
    private IBrush Ink(Color colour)
    {
        if (_inks.TryGetValue(colour.ToUInt32(), out var brush)) return brush;

        brush = new SolidColorBrush(colour);
        _inks[colour.ToUInt32()] = brush;

        return brush;
    }

    /// <summary>Every string this grid has had to draw, laid out, by what it says and its colour.</summary>
    private readonly Dictionary<(string Text, uint Colour), FormattedText> _lettering = new();

    /// <summary>And a brush for each colour it has been asked for.</summary>
    private readonly Dictionary<uint, IBrush> _inks = new();

    /// <summary>
    /// How much lettering is kept before the lot is thrown away and gathered again.
    /// </summary>
    /// <remarks>
    /// A pattern says a few hundred different things: a hundred and twenty note names and a blank,
    /// two hundred and fifty six instrument numbers, as many volumes, and the effect column. Two
    /// colours apiece. Past this something is being drawn that was not foreseen, and starting
    /// again costs one frame where growing without end costs the session.
    /// </remarks>
    private const int MostLettering = 4096;

    /// <summary>
    /// Puts the cursor where the click landed, and decides what happens to the block.
    /// </summary>
    /// <remarks>
    /// Shift keeps the anchor where it was, which is how a block is grown after the fact. A plain
    /// click puts the cursor down and drops any block, and a drag from there turns into one as
    /// soon as the pointer moves onto another cell. A right click outside the block works on what
    /// was clicked rather than on a block that happens to be somewhere else.
    ///
    /// A right click moves the cursor too, so the menu that follows acts on the track under the
    /// pointer, and it is deliberately left unhandled: handling it here swallows the request for
    /// the menu itself.
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var pattern = Pattern;
        if (pattern == null) return;

        Focus();

        var point = e.GetPosition(this);
        var cursor = Metrics.CursorAt(point.X, point.Y, pattern.Lines);
        bool left = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;

        if (left && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            Selection = Selection.IsEmpty
                ? PatternSelection.At(EditCursor).ExtendTo(cursor)
                : Selection.ExtendTo(cursor);
        }
        else if (left)
        {
            _dragAnchor = cursor;
            _pressedAt = e.GetPosition(null);
            Grabbed = true;
            Selection = PatternSelection.None;
        }
        else if (!Selection.Contains(cursor.Line, cursor.Track))
        {
            Selection = PatternSelection.None;
        }

        EditCursor = cursor;
        CursorMoved?.Invoke(this, cursor);

        e.Handled = left;
    }

    /// <summary>Where a left press landed, so a drag from it can become a block.</summary>
    private PatternCursor? _dragAnchor;

    /// <summary>
    /// The same press in pixels, in the window's coordinates rather than the grid's.
    /// </summary>
    /// <remarks>
    /// The cell is not enough on its own: a press near the edge of a row is a pixel away from
    /// the row below it, so a block would begin before the hand had really moved.
    ///
    /// And the window's coordinates rather than this control's, which is the part that made the
    /// whole thing misbehave. Pressing a cell moves the cursor, the cursor is kept on the middle
    /// of the screen, so the pattern scrolls under a pointer that has not moved at all. In this
    /// control's own coordinates that scroll reads as the hand having flown across the page, and
    /// it happens between the press and the first movement every single time. The window does
    /// not scroll.
    /// </remarks>
    private Point _pressedAt;

    /// <summary>
    /// Whether the hand has hold of the pattern: a button is down on it and has not come up.
    /// </summary>
    /// <remarks>
    /// The page reads this to stop chasing the cursor while that is true. Following it here
    /// scrolls the pattern under a pointer that is trying to point at it, and it goes wrong in
    /// two ways rather than one. During a drag the block runs away downwards on its own, since
    /// each movement lands further on than it was aimed at, moves the cursor, and scrolls again.
    /// And on the press itself, before any movement at all, the pattern jumps to put the pressed
    /// line on the middle, so a drag that follows begins from a page that has moved out from
    /// under the hand and its first cell is several lines from the one that was clicked.
    ///
    /// So it covers the press and not only the drag, and the page catches up when the button
    /// comes up. Whoever is dragging is already looking at the place they are dragging to, and
    /// a click gets its line centred a fraction of a second later than it used to.
    /// </remarks>
    public bool Grabbed { get; private set; }

    /// <summary>When a press has become a drag. See <see cref="IPointerDrag"/>.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IPointerDrag Drags = new PointerDrag();

    /// <summary>
    /// Grows the block as the hand moves, once it has really begun to move.
    /// </summary>
    /// <remarks>
    /// Two tests before a block starts and both have to pass. The hand has to have moved far
    /// enough to be dragging at all, which is <see cref="IPointerDrag"/>, and it has to be over
    /// a different cell from the one it was pressed on, so a drag inside one cell selects
    /// nothing. The cell test used to be the only one, and a row is under twenty pixels tall:
    /// a click landing near the edge of one needed a single pixel of movement to select two
    /// lines, which made clicking to move the cursor select a block about as often as not.
    ///
    /// Only the start is guarded. Once a block exists the hand is dragging, and it goes on
    /// dragging however far back towards the press it wanders.
    /// </remarks>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var pattern = Pattern;
        if (pattern == null || _dragAnchor == null) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var point = e.GetPosition(this);
        var at = Metrics.CursorAt(point.X, point.Y, pattern.Lines);

        if (Selection.IsEmpty)
        {
            var now = e.GetPosition(null);

            if (!Drags.Begun(_pressedAt.X, _pressedAt.Y, now.X, now.Y)) return;
            if (at.Line == _dragAnchor.Value.Line && at.Track == _dragAnchor.Value.Track) return;
        }

        Selection = Selection.IsEmpty
            ? PatternSelection.At(_dragAnchor.Value).ExtendTo(at)
            : Selection.ExtendTo(at);

        EditCursor = at;
        CursorMoved?.Invoke(this, at);
        e.Handled = true;
    }

    /// <summary>
    /// A press that turned out to be a click: it left no block behind it.
    /// </summary>
    /// <remarks>
    /// The page centres the clicked line on this rather than on the cursor moving, because the
    /// cursor also moves throughout a drag and the view is deliberately still for all of that.
    /// Not raised for a drag, deliberately: yanking the pattern about the moment somebody lets
    /// go of a block they have just drawn moves it out from under the eyes that drew it.
    /// </remarks>
    public event EventHandler<PatternCursor>? Clicked;

    /// <summary>Lets go of the anchor, so the next move is not read as a continuing drag.</summary>
    /// <remarks>
    /// And says whether it was a click, which is what gives the page back the job of following
    /// the cursor. Without that the page would sit wherever the press left it until something
    /// else moved the cursor, and a click would never centre its line at all.
    /// </remarks>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        bool held = Grabbed;

        _dragAnchor = null;
        Grabbed = false;

        if (held && Selection.IsEmpty) Clicked?.Invoke(this, EditCursor);
    }

    /// <summary>
    /// Works out the lettering and measures one glyph of it.
    /// </summary>
    /// <remarks>
    /// A monospaced face is what makes the columns line up, and measuring one glyph gives the
    /// cell width every column position is worked out from. Measured rather than assumed: the
    /// face is whatever the system found for the family, and a guessed width would put the
    /// cursor box beside the cell it is about rather than round it.
    /// </remarks>
    private void EnsureMetrics()
    {
        if (_measuredAt == RowHeight) return;

        _measuredAt = RowHeight;
        _fontSize = Math.Max(9, RowHeight - 5);
        _typeface = new Typeface(PatternFont.Family);

        var probe = new FormattedText("0", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, _typeface, _fontSize, Brushes.White);
        _charWidth = probe.Width > 0 ? probe.Width : _fontSize * 0.6;

        _lettering.Clear();
    }

    /// <summary>
    /// The row height the lettering was last measured at, or nought before it ever was.
    /// </summary>
    /// <remarks>
    /// The row height is the only thing this measurement depends on, and it hardly ever moves,
    /// so measuring on every frame was a font lookup and a shaping run for an answer that was
    /// already correct. Nought rather than a flag, since no row is nought high.
    /// </remarks>
    private double _measuredAt;

}
