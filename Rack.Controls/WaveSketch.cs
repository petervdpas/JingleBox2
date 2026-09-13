using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.Rack.Controls.Interfaces;
using JingleBox2.Rack.Controls.Records;
using JingleBox2.Rack.SoundDevices.Faces;

namespace JingleBox2.Rack.Controls;

/// <summary>
/// A pad one wave is drawn on with the pointer, with the other wave faint behind it.
/// </summary>
/// <remarks>
/// The light pen's half of the CMI's drawing page. Pressing and dragging lays the line under the
/// pointer, a straight stroke from where it was to where it is so a quick hand leaves no gaps, and
/// the line is handed back while the hand moves and once more when it comes up. Only the second
/// is somebody's decision: what a panel writes into a setting is the stroke, one step of undo, and
/// what it does with the first is follow the hand.
///
/// The other wave is drawn faintly behind the one in hand, because where one end of a sound is
/// drawn only means something against where the other end is.
///
/// Drawn as the points it has rather than as a smooth curve, a short flat step per point, since
/// a hundred and twenty eight points is what the sound is made of and a curve through them would
/// be a picture of a sound this is not.
/// </remarks>
public sealed class WaveSketch : ThemedControl
{
    /// <summary>Backs <see cref="Line"/>, the wave being drawn.</summary>
    public static readonly StyledProperty<double[]?> LineProperty =
        AvaloniaProperty.Register<WaveSketch, double[]?>(nameof(Line));

    /// <summary>Backs <see cref="Ghost"/>, the other wave, drawn faintly behind.</summary>
    public static readonly StyledProperty<double[]?> GhostProperty =
        AvaloniaProperty.Register<WaveSketch, double[]?>(nameof(Ghost));

    /// <summary>What happens to a line under the hand.</summary>
    private readonly IWavePen _pen = new WavePen();

    /// <summary>Where the pointer was at the last move, across and level, while it is down.</summary>
    private (double Across, double Level)? _last;

    /// <summary>Says which properties change the picture. None of them changes the size.</summary>
    static WaveSketch()
    {
        AffectsRender<WaveSketch>(LineProperty, GhostProperty);
    }

    /// <summary>Sets the size a pad takes when nobody says otherwise.</summary>
    public WaveSketch()
    {
        Width = 256;
        Height = 128;
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    /// <summary>The wave being drawn, each point from -1 to 1, or nothing for an empty pad.</summary>
    public double[]? Line
    {
        get => GetValue(LineProperty);
        set => SetValue(LineProperty, value);
    }

    /// <summary>The other wave, drawn faintly behind the one in hand.</summary>
    public double[]? Ghost
    {
        get => GetValue(GhostProperty);
        set => SetValue(GhostProperty, value);
    }

    /// <summary>Raised on every move of a stroke, with <see cref="Line"/> already holding it.</summary>
    public event EventHandler? Drawing;

    /// <summary>Raised once when the hand comes up, which is when a stroke is finished.</summary>
    public event EventHandler? Drawn;

    /// <summary>Where a point on the control is, as a place across the pad and a level.</summary>
    private (double Across, double Level) At(Point point)
    {
        double width = Math.Max(1, Bounds.Width);
        double height = Math.Max(1, Bounds.Height);

        return (point.X / width, 1 - (2 * point.Y / height));
    }

    /// <summary>A stroke starts where the pointer comes down.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var at = At(e.GetPosition(this));

        _last = at;
        e.Pointer.Capture(this);
        e.Handled = true;

        Lay(at);
    }

    /// <summary>And carries on from wherever it was to wherever it is.</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_last is null) return;

        Lay(At(e.GetPosition(this)));
    }

    /// <summary>And is finished when the hand comes up.</summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_last is null) return;

        _last = null;
        e.Pointer.Capture(null);
        e.Handled = true;

        Drawn?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A stroke taken away from under the hand is finished where it had got to.</summary>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        if (_last is null) return;

        _last = null;

        Drawn?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Lays the stroke from the last place to this one.</summary>
    private void Lay((double Across, double Level) at)
    {
        var from = _last ?? at;

        Line = _pen.Stroke(Line ?? new double[WaveSegments.Points], from.Across, from.Level, at.Across, at.Level);

        _last = at;

        Drawing?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The pad, the middle line, the other wave faintly, and this one.</summary>
    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width;
        double height = Bounds.Height;

        if (width <= 2 || height <= 2) return;

        var palette = ThemePalette.From(this);

        context.DrawRectangle(
            new SolidColorBrush(palette.Background),
            new Pen(palette.BorderBrush, 1),
            new RoundedRect(new Rect(0, 0, width, height), 4));

        context.DrawLine(new Pen(palette.AccentTint(50), 1, DashStyle.Dash),
            new Point(0, height / 2), new Point(width, height / 2));

        if (Ghost is { } ghost) Draw(context, ghost, new Pen(palette.AccentTint(70), 1), width, height);

        if (Line is { } line) Draw(context, line, new Pen(palette.AccentBrush, 2, lineJoin: PenLineJoin.Round), width, height);
    }

    /// <summary>One wave as a run of short steps, a point to each.</summary>
    private static void Draw(DrawingContext context, double[] line, IPen pen, double width, double height)
    {
        int count = Math.Min(line.Length, WaveSegments.Points);

        if (count == 0) return;

        double step = width / WaveSegments.Points;
        var geometry = new StreamGeometry();

        using (var sink = geometry.Open())
        {
            for (int point = 0; point < count; point++)
            {
                double value = double.IsFinite(line[point]) ? Math.Clamp(line[point], -1, 1) : 0;
                double y = (1 - value) * height / 2;

                if (point == 0) sink.BeginFigure(new Point(0, y), false);
                else sink.LineTo(new Point(point * step, y));

                sink.LineTo(new Point((point + 1) * step, y));
            }

            sink.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }
}
