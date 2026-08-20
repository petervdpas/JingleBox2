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
public sealed class PatternGrid : Control
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

    static PatternGrid()
    {
        AffectsRender<PatternGrid>(PatternProperty, EditCursorProperty, PlayingLineProperty,
            LinesPerBeatProperty, RowHeightProperty);
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

    /// <summary>Raised when a click moves the cursor, so the view model can follow.</summary>
    public event EventHandler<PatternCursor>? CursorMoved;

    // Column layout, in characters, matching the text each column renders.
    private const int LineNumberChars = 3;
    private const string ColumnGap = " ";

    private static readonly int[] ColumnWidths = { 3, 2, 2, 3 }; // note, instrument, volume, effect

    private double _charWidth = 8;
    private double _fontSize = 13;
    private Typeface _typeface = new(FontFamily.Default);

    protected override Size MeasureOverride(Size availableSize)
    {
        var pattern = Pattern;
        if (pattern == null) return new Size(0, 0);

        EnsureMetrics();

        double width = (LineNumberChars + 1) * _charWidth + pattern.TrackCount * TrackWidth;
        return new Size(width, pattern.Lines * RowHeight);
    }

    private double TrackWidth
    {
        get
        {
            int chars = 0;
            foreach (int w in ColumnWidths) chars += w + ColumnGap.Length;
            return (chars + 1) * _charWidth;
        }
    }

    public override void Render(DrawingContext context)
    {
        var pattern = Pattern;
        if (pattern == null) return;

        EnsureMetrics();

        var bounds = new Rect(Bounds.Size);
        var text = Brush(ThemeKey.Text, Colors.Gainsboro);
        var muted = Brush(ThemeKey.Muted, Color.FromRgb(0x6B, 0x72, 0x80));
        var accent = Brush(ThemeKey.Accent, Color.FromRgb(0xFB, 0x8C, 0x00));

        int lpb = Math.Max(1, LinesPerBeat);

        for (int line = 0; line < pattern.Lines; line++)
        {
            double y = line * RowHeight;
            if (y + RowHeight < 0 || y > bounds.Height) continue; // outside the viewport

            bool isBeat = line % lpb == 0;
            bool isBar = line % (lpb * 4) == 0;

            if (isBar)
                context.FillRectangle(new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
                    new Rect(0, y, bounds.Width, RowHeight));
            else if (isBeat)
                context.FillRectangle(new SolidColorBrush(Color.FromArgb(14, 255, 255, 255)),
                    new Rect(0, y, bounds.Width, RowHeight));

            if (line == PlayingLine)
                context.FillRectangle(new SolidColorBrush(Color.FromArgb(60, 0xFB, 0x8C, 0x00)),
                    new Rect(0, y, bounds.Width, RowHeight));

            DrawText(context, line.ToString("00", CultureInfo.InvariantCulture), 0, y, muted);

            for (int track = 0; track < pattern.TrackCount; track++)
            {
                var cell = pattern[line, track];
                double x = (LineNumberChars + 1) * _charWidth + track * TrackWidth;

                DrawColumn(context, cell.Note.ToString(), x, y, cell.Note.IsEmpty ? muted : text);
                x += (ColumnWidths[0] + ColumnGap.Length) * _charWidth;

                DrawColumn(context, cell.InstrumentText, x, y,
                    cell.Instrument == TrackerCell.NoInstrument ? muted : text);
                x += (ColumnWidths[1] + ColumnGap.Length) * _charWidth;

                DrawColumn(context, cell.VolumeText, x, y,
                    cell.Volume == TrackerCell.NoVolume ? muted : text);
                x += (ColumnWidths[2] + ColumnGap.Length) * _charWidth;

                DrawColumn(context, cell.Effect.ToString(), x, y, cell.Effect.IsNone ? muted : text);
            }
        }

        DrawCursor(context, pattern, accent);
    }

    private void DrawCursor(DrawingContext context, Pattern pattern, IBrush accent)
    {
        var cursor = EditCursor.Clamp(pattern.Lines, pattern.TrackCount);

        double x = ColumnX(cursor.Track, cursor.Column);
        double width = ColumnWidths[(int)cursor.Column] * _charWidth;
        double y = cursor.Line * RowHeight;

        context.FillRectangle(new SolidColorBrush(Color.FromArgb(48, 0xFB, 0x8C, 0x00)),
            new Rect(x - 1, y, width + 2, RowHeight));
        context.DrawRectangle(new Pen(accent, 1), new Rect(x - 1, y, width + 2, RowHeight));
    }

    private double ColumnX(int track, CellColumn column)
    {
        double x = (LineNumberChars + 1) * _charWidth + track * TrackWidth;
        for (int i = 0; i < (int)column; i++)
            x += (ColumnWidths[i] + ColumnGap.Length) * _charWidth;
        return x;
    }

    private void DrawColumn(DrawingContext context, string text, double x, double y, IBrush brush) =>
        DrawText(context, text, x, y, brush);

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
        int line = Math.Clamp((int)(point.Y / RowHeight), 0, pattern.Lines - 1);

        double trackArea = point.X - (LineNumberChars + 1) * _charWidth;
        int track = Math.Clamp((int)(trackArea / TrackWidth), 0, pattern.TrackCount - 1);

        // Which of the four columns inside that track the click landed on.
        double insideTrack = trackArea - track * TrackWidth;
        var column = CellColumn.Note;
        double edge = 0;
        for (int i = 0; i < ColumnWidths.Length; i++)
        {
            edge += (ColumnWidths[i] + ColumnGap.Length) * _charWidth;
            if (insideTrack < edge) { column = (CellColumn)i; break; }
            column = (CellColumn)Math.Min(i + 1, ColumnWidths.Length - 1);
        }

        var cursor = new PatternCursor(line, track, column);
        EditCursor = cursor;
        CursorMoved?.Invoke(this, cursor);
        e.Handled = true;
    }

    private void EnsureMetrics()
    {
        // A monospace face is what makes the columns line up; measuring one glyph gives the
        // cell width every column position is derived from.
        _fontSize = Math.Max(9, RowHeight - 5);
        _typeface = new Typeface(new FontFamily("Cascadia Mono,Consolas,DejaVu Sans Mono,Menlo,monospace"));

        var probe = new FormattedText("0", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, _typeface, _fontSize, Brushes.White);
        _charWidth = probe.Width > 0 ? probe.Width : _fontSize * 0.6;
    }

    private static class ThemeKey
    {
        public const string Text = "TextPrimaryBrush";
        public const string Muted = "TextMutedBrush";
        public const string Accent = "AccentBrush";
    }

    private IBrush Brush(string key, Color fallback) =>
        this.TryFindResource(key, out var value) && value is IBrush brush
            ? brush
            : new SolidColorBrush(fallback);
}
