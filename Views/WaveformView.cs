using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.Waveform;
using System;

namespace JingleBox2.Views;

/// <summary>
/// A recording's shape, with the part an instrument plays marked on it. The window and the
/// loop can be dragged straight on the picture, which is the only way of setting them that
/// lets you see what you are doing.
/// </summary>
/// <remarks>
/// Positions are fractions of the file rather than frames, the way the instrument stores
/// them, so the control never needs to know the sample rate or the length.
/// </remarks>
public class WaveformView : ThemedControl
{
    /// <summary>The whole file, always: zooming and panning belong to the editor dialog.</summary>
    private static readonly WaveformViewport FullView = new();

    /// <summary>How close a click has to be to a handle to take hold of it.</summary>
    private const double GrabPixels = 12;

    /// <summary>Handles cannot be dragged onto each other, or the window would vanish.</summary>
    private const double MinGap = 0.005;

    public static readonly StyledProperty<float[]?> PeaksProperty =
        AvaloniaProperty.Register<WaveformView, float[]?>(nameof(Peaks));

    /// <summary>What to say when there is nothing to draw yet.</summary>
    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<WaveformView, string>(nameof(Placeholder), "");

    /// <summary>Draws the window and the loop, and lets them be dragged.</summary>
    public static readonly StyledProperty<bool> ShowMarkersProperty =
        AvaloniaProperty.Register<WaveformView, bool>(nameof(ShowMarkers));

    public static readonly StyledProperty<bool> ShowLoopProperty =
        AvaloniaProperty.Register<WaveformView, bool>(nameof(ShowLoop));

    public static readonly StyledProperty<double> StartProperty =
        AvaloniaProperty.Register<WaveformView, double>(
            nameof(Start), 0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> EndProperty =
        AvaloniaProperty.Register<WaveformView, double>(
            nameof(End), 1, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> LoopStartProperty =
        AvaloniaProperty.Register<WaveformView, double>(
            nameof(LoopStart), 0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> LoopEndProperty =
        AvaloniaProperty.Register<WaveformView, double>(
            nameof(LoopEnd), 1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Which handle the pointer has hold of, or none.</summary>
    private enum Handle
    {
        None,
        Start,
        End,
        LoopStart,
        LoopEnd
    }

    private Handle _dragging = Handle.None;

    static WaveformView()
    {
        AffectsRender<WaveformView>(
            PeaksProperty, PlaceholderProperty, ShowMarkersProperty, ShowLoopProperty,
            StartProperty, EndProperty, LoopStartProperty, LoopEndProperty);
    }

    public float[]? Peaks
    {
        get => GetValue(PeaksProperty);
        set => SetValue(PeaksProperty, value);
    }

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public bool ShowMarkers
    {
        get => GetValue(ShowMarkersProperty);
        set => SetValue(ShowMarkersProperty, value);
    }

    public bool ShowLoop
    {
        get => GetValue(ShowLoopProperty);
        set => SetValue(ShowLoopProperty, value);
    }

    public double Start
    {
        get => GetValue(StartProperty);
        set => SetValue(StartProperty, value);
    }

    public double End
    {
        get => GetValue(EndProperty);
        set => SetValue(EndProperty, value);
    }

    public double LoopStart
    {
        get => GetValue(LoopStartProperty);
        set => SetValue(LoopStartProperty, value);
    }

    public double LoopEnd
    {
        get => GetValue(LoopEndProperty);
        set => SetValue(LoopEndProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 1 || height <= 1) return;

        var palette = ThemePalette.From(this);
        var area = new Rect(0, 0, width, height);

        context.DrawRectangle(
            new SolidColorBrush(palette.Background),
            new Pen(new SolidColorBrush(palette.Border), 1),
            new RoundedRect(area, 4));

        var peaks = Peaks;
        if (peaks == null || peaks.Length == 0)
        {
            DrawPlaceholder(context, palette, area);
            return;
        }

        // Silence at the start and end of a take is common, so the middle line is drawn: a
        // flat stretch then reads as quiet rather than as nothing loaded.
        double centre = height / 2;
        context.FillRectangle(new SolidColorBrush(palette.Muted, 0.35), new Rect(1, centre, width - 2, 1));

        var geometry = WaveformGeometry.Build(peaks, FullView, width, height);
        context.DrawGeometry(new SolidColorBrush(palette.Accent, 0.85), null, geometry);

        if (ShowMarkers) DrawMarkers(context, palette, area);
    }

    /// <summary>
    /// The part outside the window is dimmed rather than hidden: it is still in the file, and
    /// seeing it is what makes dragging the handles make sense.
    /// </summary>
    private void DrawMarkers(DrawingContext context, ThemePalette palette, Rect area)
    {
        double width = area.Width;
        double start = X(Start, width);
        double end = X(End, width);

        var shade = new SolidColorBrush(palette.Background, 0.72);

        if (start > 0) context.FillRectangle(shade, new Rect(1, 1, start - 1, area.Height - 2));
        if (end < width) context.FillRectangle(shade, new Rect(end, 1, width - end - 1, area.Height - 2));

        if (ShowLoop)
        {
            double loopStart = X(LoopStart, width);
            double loopEnd = X(LoopEnd, width);

            if (loopEnd > loopStart)
            {
                context.FillRectangle(
                    new SolidColorBrush(palette.Accent, 0.16),
                    new Rect(loopStart, 1, loopEnd - loopStart, area.Height - 2));
            }

            DrawHandle(context, palette.Accent, loopStart, area, dashed: true);
            DrawHandle(context, palette.Accent, loopEnd, area, dashed: true);
        }

        DrawHandle(context, palette.Text, start, area, dashed: false);
        DrawHandle(context, palette.Text, end, area, dashed: false);
    }

    private static void DrawHandle(DrawingContext context, Color colour, double x, Rect area, bool dashed)
    {
        var pen = new Pen(new SolidColorBrush(colour, dashed ? 0.9 : 0.75), dashed ? 1 : 1.5)
        {
            DashStyle = dashed ? new DashStyle(new double[] { 3, 3 }, 0) : null
        };

        double clamped = Math.Clamp(x, area.X + 1, area.Right - 1);
        context.DrawLine(pen, new Point(clamped, area.Y + 1), new Point(clamped, area.Bottom - 1));

        // A grip at the top, so it is clear the line can be taken hold of.
        context.FillRectangle(
            new SolidColorBrush(colour, 0.9),
            new Rect(clamped - 3, area.Y + 1, 6, 5));
    }

    private void DrawPlaceholder(DrawingContext context, ThemePalette palette, Rect area)
    {
        double centre = area.Height / 2;
        context.FillRectangle(new SolidColorBrush(palette.Muted, 0.35), new Rect(1, centre, area.Width - 2, 1));

        if (string.IsNullOrEmpty(Placeholder)) return;

        var text = new FormattedText(
            Placeholder,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            11,
            new SolidColorBrush(palette.Muted));

        context.DrawText(text, new Point(6, centre - text.Height - 4));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!ShowMarkers || Peaks == null) return;

        double x = e.GetPosition(this).X;
        _dragging = Nearest(x);

        if (_dragging == Handle.None) return;

        e.Pointer.Capture(this);
        Move(x);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_dragging == Handle.None) return;

        Move(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_dragging == Handle.None) return;

        _dragging = Handle.None;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>The handle a click means, or none when the click is nowhere near one.</summary>
    private Handle Nearest(double x)
    {
        double width = Bounds.Width;
        if (width <= 0) return Handle.None;

        var best = Handle.None;
        double closest = GrabPixels;

        void Consider(Handle handle, double position)
        {
            double distance = Math.Abs(X(position, width) - x);
            if (distance > closest) return;

            closest = distance;
            best = handle;
        }

        Consider(Handle.Start, Start);
        Consider(Handle.End, End);

        if (ShowLoop)
        {
            Consider(Handle.LoopStart, LoopStart);
            Consider(Handle.LoopEnd, LoopEnd);
        }

        return best;
    }

    /// <summary>
    /// Moves the held handle, keeping the order of them. A handle pushed past its neighbour
    /// stops next to it rather than swapping, which would make a drag jump under the pointer.
    /// </summary>
    private void Move(double x)
    {
        double width = Bounds.Width;
        if (width <= 0) return;

        double at = Math.Clamp(x / width, 0, 1);

        switch (_dragging)
        {
            case Handle.Start:
                Start = Math.Min(at, End - MinGap);
                if (LoopStart < Start) LoopStart = Start;
                if (LoopEnd < Start) LoopEnd = Start;
                break;

            case Handle.End:
                End = Math.Max(at, Start + MinGap);
                if (LoopEnd > End) LoopEnd = End;
                if (LoopStart > End) LoopStart = End;
                break;

            case Handle.LoopStart:
                LoopStart = Math.Clamp(at, Start, Math.Max(Start, LoopEnd - MinGap));
                break;

            case Handle.LoopEnd:
                LoopEnd = Math.Clamp(at, Math.Min(End, LoopStart + MinGap), End);
                break;
        }
    }

    private static double X(double fraction, double width) =>
        Math.Clamp(double.IsNaN(fraction) ? 0 : fraction, 0, 1) * width;
}
