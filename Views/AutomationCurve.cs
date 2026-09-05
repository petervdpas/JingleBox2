using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.Rack.Controls;
using JingleBox2.Tracker;
using System;
using JingleBox2.Tracker.Enums;
using JingleBox2.Rack.Controls.Records;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Views;

/// <summary>
/// A lane, drawn: the pattern's lines across, nought to one up, and the points you drag.
/// </summary>
/// <remarks>
/// Time runs left to right although the pattern above runs downwards, which looks like a
/// contradiction and is not: Renoise draws its automation the same way round for the same
/// reason. A curve is read as a shape, and a shape a hand recognises is one that rises and
/// falls left to right. Turned on its side to match the pattern it would be a shape nobody has
/// ever read.
///
/// Custom-drawn, like the pattern grid and for the same reason: a pattern of two hundred and
/// fifty six lines with a point on every one is a Render pass here and two hundred and fifty
/// six controls otherwise.
/// </remarks>
public sealed class AutomationCurve : ThemedControl
{
    /// <summary>How near the pointer has to be to a point to take hold of it.</summary>
    /// <remarks>
    /// Generous, because a point is four pixels across and a hand is not. Small enough that two
    /// points on neighbouring lines can still be told apart at the usual width.
    /// </remarks>
    private const double Grab = 9;

    /// <summary>Half the side of the square drawn on each point, so it can be aimed at.</summary>
    private const double PointSize = 3.5;

    /// <summary>The lane being drawn and edited, or nothing when the track has none.</summary>
    public static readonly StyledProperty<AutomationLane?> LaneProperty =
        AvaloniaProperty.Register<AutomationCurve, AutomationLane?>(nameof(Lane));

    /// <summary>How many lines the pattern has, which is the whole width of the picture.</summary>
    public static readonly StyledProperty<int> LinesProperty =
        AvaloniaProperty.Register<AutomationCurve, int>(nameof(Lines), Pattern.DefaultLines);

    /// <summary>Which of those lines are picked out, so time can be read off the grid.</summary>
    public static readonly StyledProperty<int> LinesPerBeatProperty =
        AvaloniaProperty.Register<AutomationCurve, int>(
            nameof(LinesPerBeat), TrackerTiming.DefaultLinesPerBeat);

    /// <summary>
    /// Where the parameter's own nought is, nought to one, which is what the shape rests on.
    /// </summary>
    /// <remarks>
    /// The floor for anything that runs from nothing upwards, and the middle for anything that
    /// runs either side of nothing: a pan, a pitch, a stereo width. Drawn resting on the floor,
    /// a pan reads as hard left the whole way with a bump in it, which is the opposite of what
    /// it says.
    /// </remarks>
    public static readonly StyledProperty<double> ZeroProperty =
        AvaloniaProperty.Register<AutomationCurve, double>(nameof(Zero), 0);

    /// <summary>Nothing here changes the room asked for: the lane fills what it is given.</summary>
    static AutomationCurve()
    {
        AffectsRender<AutomationCurve>(
            LaneProperty, LinesProperty, LinesPerBeatProperty, ZeroProperty);
    }

    /// <inheritdoc cref="LaneProperty"/>
    public AutomationLane? Lane
    {
        get => GetValue(LaneProperty);
        set => SetValue(LaneProperty, value);
    }

    /// <inheritdoc cref="LinesProperty"/>
    public int Lines
    {
        get => GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    /// <inheritdoc cref="LinesPerBeatProperty"/>
    public int LinesPerBeat
    {
        get => GetValue(LinesPerBeatProperty);
        set => SetValue(LinesPerBeatProperty, value);
    }

    /// <inheritdoc cref="ZeroProperty"/>
    public double Zero
    {
        get => GetValue(ZeroProperty);
        set => SetValue(ZeroProperty, value);
    }

    /// <summary>
    /// Raised once when a gesture starts, with what to call it, before anything has changed.
    /// </summary>
    /// <remarks>
    /// Once per gesture and not per movement, so a point dragged across the pattern is one
    /// press of undo rather than forty. The same rule the recorder follows and the instrument
    /// knobs follow, arrived at from the same direction: a hand doing one thing is one thing.
    /// </remarks>
    public event Action<string>? Editing;

    /// <summary>Raised after the lane changed, so the song can be marked and the count reread.</summary>
    public event Action? Edited;

    /// <summary>The time of the point in the hand, or NaN when nothing is being dragged.</summary>
    private double _holding = double.NaN;

    /// <summary>
    /// Takes no focus, since the keyboard belongs to the pattern above and a lane that stole it
    /// would stop the arrow keys working, and shows a crosshair because every place on it is a
    /// place a point can go.
    /// </summary>
    public AutomationCurve()
    {
        Focusable = false;
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    /// <summary>
    /// Left takes hold of the point under the pointer, or puts a new one there; right takes one
    /// away.
    /// </summary>
    /// <remarks>
    /// The right button rather than a modifier, because that is the button this application
    /// already uses for taking a thing away from a picture: the chain's blocks and the machine
    /// designer's parts both go that way.
    ///
    /// The gesture is announced once, before anything has changed, so a point dragged across the
    /// pattern is one press of undo rather than forty.
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Lane is not { } lane || Lines <= 0) return;

        var at = e.GetPosition(this);
        var kind = e.GetCurrentPoint(this).Properties;

        if (kind.IsRightButtonPressed)
        {
            if (Nearest(lane, at) is not double gone) return;

            Editing?.Invoke("a point");
            lane.Remove(gone);
            Edited?.Invoke();
            InvalidateVisual();
            e.Handled = true;

            return;
        }

        if (!kind.IsLeftButtonPressed) return;

        Editing?.Invoke("a point");

        if (Nearest(lane, at) is double held)
        {
            _holding = held;
        }
        else
        {
            _holding = LineAt(at.X);
            lane.Put(_holding, ValueAt(at.Y));
            Edited?.Invoke();
        }

        e.Pointer.Capture(this);
        InvalidateVisual();
        e.Handled = true;
    }

    /// <summary>
    /// Moves the point in the hand, snapped to a line across and free up and down.
    /// </summary>
    /// <remarks>
    /// A point dragged onto a line that already has one would replace it, since a lane holds one
    /// per time. So the time is refused and only the value moves: a drag that swallowed its
    /// neighbours as it passed would destroy work on the way to somewhere else.
    /// </remarks>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (double.IsNaN(_holding) || Lane is not { } lane) return;

        var at = e.GetPosition(this);

        double time = LineAt(at.X);
        double value = ValueAt(at.Y);

        if (time != _holding && Held(lane, time)) time = _holding;

        if (time != _holding)
        {
            lane.Remove(_holding);
            _holding = time;
        }

        lane.Put(_holding, value);

        Edited?.Invoke();
        InvalidateVisual();
    }

    /// <summary>Lets go of the point, which ends the gesture and so ends the undo step.</summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        _holding = double.NaN;
        e.Pointer.Capture(null);
    }

    /// <summary>Which line a place on the picture is, snapped, since there is no finer grid.</summary>
    private double LineAt(double x)
    {
        double lines = Math.Max(1, Lines);
        double line = Math.Round(x / Math.Max(1, Bounds.Width) * lines);

        return Math.Clamp(line, 0, lines - 1);
    }

    /// <summary>How far up the picture is, which is the value, nought at the floor.</summary>
    private double ValueAt(double y) =>
        Math.Clamp(1 - y / Math.Max(1, Bounds.Height), 0, 1);

    /// <summary>Whether the lane already has a point on that line.</summary>
    private static bool Held(AutomationLane lane, double time)
    {
        foreach (var point in lane.Points)
            if (point.Time == time) return true;

        return false;
    }

    /// <summary>The time of the point under the pointer, or nothing when there is none.</summary>
    private double? Nearest(AutomationLane lane, Point at)
    {
        double best = Grab;
        double? found = null;

        foreach (var point in lane.Points)
        {
            double dx = X(point.Time) - at.X;
            double dy = Y(point.Value) - at.Y;
            double away = Math.Sqrt(dx * dx + dy * dy);

            if (away > best) continue;

            best = away;
            found = point.Time;
        }

        return found;
    }

    /// <summary>Where a time falls across the picture, time running left to right.</summary>
    private double X(double time) => time / Math.Max(1, Lines) * Bounds.Width;

    /// <summary>And where a value falls up it, one at the top and nought on the floor.</summary>
    private double Y(double value) => (1 - value) * Bounds.Height;

    /// <summary>
    /// The ground, the grid over it, the playhead, and the shape with its points.
    /// </summary>
    /// <remarks>
    /// A track with no lane still gets the ground and the grid, so the strip reads as somewhere a
    /// lane could go rather than as a hole.
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);
        var size = Bounds.Size;

        if (size.Width <= 0 || size.Height <= 0) return;

        context.FillRectangle(
            new SolidColorBrush(ThemePalette.Shade(palette.Background, -0.2)), new Rect(size));

        DrawGrid(context, palette, size);

        if (Lane is not { } lane) return;

        DrawLane(context, palette, lane, size);
    }

    /// <summary>
    /// The lines of the pattern, with the beats picked out, so time can be read.
    /// </summary>
    /// <remarks>
    /// Every line only when they would be far enough apart to read as lines rather than as a
    /// wash: a pattern of two hundred and fifty six lines in three hundred pixels is not a grid.
    ///
    /// The parameter's own nought is drawn only when it is somewhere on the picture rather than
    /// along one of its edges. A level's nought is the floor, and a line on the floor says
    /// nothing the floor did not already say.
    /// </remarks>
    private void DrawGrid(DrawingContext context, ThemePalette palette, Size size)
    {
        var faint = new Pen(new SolidColorBrush(ThemePalette.Alpha(palette.Border, 0x60)), 1);
        var beat = new Pen(new SolidColorBrush(ThemePalette.Alpha(palette.Border, 0xC0)), 1);

        int lines = Math.Max(1, Lines);
        int perBeat = Math.Max(1, LinesPerBeat);

        bool everyLine = size.Width / lines >= 6;

        for (int line = 0; line <= lines; line++)
        {
            bool onBeat = line % perBeat == 0;
            if (!onBeat && !everyLine) continue;

            double x = Math.Round(X(line)) + 0.5;
            context.DrawLine(onBeat ? beat : faint, new Point(x, 0), new Point(x, size.Height));
        }

        if (Zero > 0.02 && Zero < 0.98)
            context.DrawLine(beat,
                new Point(0, Math.Round(Y(Zero)) + 0.5),
                new Point(size.Width, Math.Round(Y(Zero)) + 0.5));
    }

    /// <summary>The shape itself: what is under it, the line, and the points on it.</summary>
    /// <remarks>
    /// Filled underneath as well as drawn, because a line alone on a dark ground is read as a
    /// border and a filled shape is read as a level. Renoise fills its automation for the same
    /// reason and so does every mixer meter ever built.
    ///
    /// Stepped or swept by the lane's play mode, which is the one thing that mode changes about
    /// the picture and has to: a lane that steps drawn as one that sweeps would be a picture of
    /// a different lane.
    /// </remarks>
    private void DrawLane(DrawingContext context, ThemePalette palette, AutomationLane lane, Size size)
    {
        if (lane.Points.Count == 0) return;

        var shape = new StreamGeometry();
        double floor = Y(Zero);

        using (var draw = shape.Open())
        {
            draw.BeginFigure(new Point(0, floor), true);

            var first = lane.Points[0];
            draw.LineTo(new Point(0, Y(first.Value)));

            var was = new Point(X(first.Time), Y(first.Value));
            draw.LineTo(was);

            for (int at = 1; at < lane.Points.Count; at++)
            {
                var point = lane.Points[at];
                var next = new Point(X(point.Time), Y(point.Value));

                if (lane.Play == AutomationPlay.Points)
                    draw.LineTo(new Point(next.X, was.Y));

                draw.LineTo(next);
                was = next;
            }

            draw.LineTo(new Point(size.Width, was.Y));
            draw.LineTo(new Point(size.Width, floor));
            draw.EndFigure(true);
        }

        context.DrawGeometry(
            new SolidColorBrush(ThemePalette.Alpha(palette.Accent, 0x30)),
            new Pen(new SolidColorBrush(palette.Accent), 1.5),
            shape);

        var handle = new SolidColorBrush(palette.Accent);

        foreach (var point in lane.Points)
        {
            var middle = new Point(X(point.Time), Y(point.Value));

            context.FillRectangle(handle, new Rect(
                middle.X - PointSize, middle.Y - PointSize, PointSize * 2, PointSize * 2));
        }
    }
}
