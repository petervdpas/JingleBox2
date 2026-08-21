using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.Tracker;

namespace JingleBox2.Views;

/// <summary>
/// The track names above a pattern, with the selected one picked out. Sits outside the
/// pattern's scroll area so it stays put vertically, and takes the horizontal scroll offset
/// so it stays aligned with the columns it names.
/// </summary>
public sealed class PatternHeader : Control
{
    public static readonly StyledProperty<int> TrackCountProperty =
        AvaloniaProperty.Register<PatternHeader, int>(nameof(TrackCount), Song.DefaultTrackCount);

    public static readonly StyledProperty<int> SelectedTrackProperty =
        AvaloniaProperty.Register<PatternHeader, int>(nameof(SelectedTrack));

    public static readonly StyledProperty<double> CharWidthProperty =
        AvaloniaProperty.Register<PatternHeader, double>(nameof(CharWidth), 8);

    public static readonly StyledProperty<double> ScrollOffsetProperty =
        AvaloniaProperty.Register<PatternHeader, double>(nameof(ScrollOffset));

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<PatternHeader, double>(nameof(RowHeight), 18);

    static PatternHeader()
    {
        AffectsRender<PatternHeader>(TrackCountProperty, SelectedTrackProperty,
            CharWidthProperty, ScrollOffsetProperty, RowHeightProperty);
        AffectsMeasure<PatternHeader>(RowHeightProperty);
    }

    public int TrackCount
    {
        get => GetValue(TrackCountProperty);
        set => SetValue(TrackCountProperty, value);
    }

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

    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    /// <summary>Raised when a header is clicked, so the cursor can jump to that track.</summary>
    public event EventHandler<int>? TrackClicked;

    private const double VerticalPadding = 5;

    private PatternMetrics Metrics => new(CharWidth, RowHeight, TrackCount);

    protected override Size MeasureOverride(Size availableSize) =>
        new(0, RowHeight + VerticalPadding * 2);

    public override void Render(DrawingContext context)
    {
        if (TrackCount <= 0 || CharWidth <= 0) return;

        var metrics = Metrics;
        double height = Bounds.Height;

        var text = Brush(ThemeKey.Text, Colors.Gainsboro);
        var muted = Brush(ThemeKey.Muted, Color.FromRgb(0x6B, 0x72, 0x80));
        var accent = Brush(ThemeKey.Accent, Color.FromRgb(0xFB, 0x8C, 0x00));
        var border = Brush(ThemeKey.Border, Color.FromArgb(60, 128, 128, 128));

        // Everything shifts with the pattern's horizontal scroll so the labels stay over
        // their own columns.
        using var _ = context.PushTransform(Matrix.CreateTranslation(-ScrollOffset, 0));

        double fontSize = Math.Max(9, RowHeight - 6);
        var typeface = new Typeface(PatternFont.Family);

        for (int track = 0; track < TrackCount; track++)
        {
            double x = metrics.TrackDividerX(track);
            var area = new Rect(x + 1, 2, metrics.TrackWidth - 2, height - 4);
            bool selected = track == SelectedTrack;

            context.FillRectangle(
                selected
                    ? new SolidColorBrush(Color.FromArgb(56, 0xFB, 0x8C, 0x00))
                    : new SolidColorBrush(Color.FromArgb(18, 128, 128, 128)),
                area, 3);

            if (selected)
                context.DrawRectangle(new Pen(accent, 1), area, 3);
            else
                context.DrawRectangle(new Pen(border, 1), area, 3);

            string label = "Track " + (track + 1).ToString("00", CultureInfo.InvariantCulture);
            var formatted = new FormattedText(label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, fontSize, selected ? text : muted);

            context.DrawText(formatted, new Point(
                area.X + Math.Max(2, (area.Width - formatted.Width) / 2),
                area.Y + (area.Height - formatted.Height) / 2));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (TrackCount <= 0) return;

        double x = e.GetPosition(this).X + ScrollOffset;
        if (x < Metrics.GutterWidth) return; // the line number gutter names no track

        TrackClicked?.Invoke(this, Metrics.TrackAt(x));
        e.Handled = true;
    }

    private static class ThemeKey
    {
        public const string Text = "TextPrimaryBrush";
        public const string Muted = "TextMutedBrush";
        public const string Accent = "AccentBrush";
        public const string Border = "BorderBrush";
    }

    private IBrush Brush(string key, Color fallback) =>
        this.TryFindResource(key, out var value) && value is IBrush brush
            ? brush
            : new SolidColorBrush(fallback);
}
