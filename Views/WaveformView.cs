using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.Waveform;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

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
    /// <summary>
    /// How much of the file is on screen. Its own, not a shared one: two pictures of the same
    /// recording, on a panel and in a chop editor, are looked at separately.
    /// </summary>
    /// <remarks>
    /// Everything here works in fractions of the file and goes through <see cref="X"/> and
    /// <see cref="At"/>, so zooming is a matter of what those two do and nothing else in the
    /// control has to know about it.
    /// </remarks>
    private readonly WaveformViewport _view = new();

    /// <summary>How much one notch of the wheel changes the zoom by.</summary>
    private const double ZoomStep = 1.25;

    /// <summary>Where a pan started, in pixels, or NaN while nothing is being panned.</summary>
    private double _panFrom = double.NaN;

    private static readonly Cursor PanCursor = new(StandardCursorType.SizeWestEast);

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

    /// <summary>
    /// Where the recording is cut into pieces, as fractions of it. Null for a waveform that is
    /// not sliced, which is every one that was here before this arrived.
    /// </summary>
    /// <remarks>
    /// One more point than there are pieces: the first is where the sliced region begins, the
    /// last is where it ends, and every point between is a boundary two pieces share. The
    /// control edits this list in place, so whoever owns it hears about a drag, an added point
    /// or a removed one through its own collection rather than through an event here.
    /// </remarks>
    public static readonly StyledProperty<ObservableCollection<double>?> SlicePointsProperty =
        AvaloniaProperty.Register<WaveformView, ObservableCollection<double>?>(nameof(SlicePoints));

    /// <summary>
    /// Where the sound has got to in the recording, as a fraction of it, or -1 for silence.
    /// </summary>
    public static readonly StyledProperty<double> PlayheadProperty =
        AvaloniaProperty.Register<WaveformView, double>(nameof(Playhead), -1);

    /// <summary>Which piece is being worked on, or -1 for none.</summary>
    public static readonly StyledProperty<int> SelectedSliceProperty =
        AvaloniaProperty.Register<WaveformView, int>(
            nameof(SelectedSlice), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// How many pieces this waveform will let you cut. A kit holds sixteen, a map thirty-two.
    /// </summary>
    public static readonly StyledProperty<int> MaxSlicesProperty =
        AvaloniaProperty.Register<WaveformView, int>(nameof(MaxSlices), 32);

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

    /// <summary>Which slice point the pointer has hold of, or -1.</summary>
    private int _draggingPoint = -1;

    /// <summary>The list being watched, so it can be let go of when another arrives.</summary>
    private ObservableCollection<double>? _watching;

    static WaveformView()
    {
        AffectsRender<WaveformView>(
            PeaksProperty, PlaceholderProperty, ShowMarkersProperty, ShowLoopProperty,
            StartProperty, EndProperty, LoopStartProperty, LoopEndProperty,
            SlicePointsProperty, SelectedSliceProperty, PlayheadProperty);

        // Zoomed in, the parts of the file that are off screen are still drawn: a window that
        // starts before the left edge is a rectangle beginning at a negative x. Clipped, so
        // none of it lands on whatever the picture is standing next to.
        ClipToBoundsProperty.OverrideDefaultValue<WaveformView>(true);
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

    public ObservableCollection<double>? SlicePoints
    {
        get => GetValue(SlicePointsProperty);
        set => SetValue(SlicePointsProperty, value);
    }

    public int SelectedSlice
    {
        get => GetValue(SelectedSliceProperty);
        set => SetValue(SelectedSliceProperty, value);
    }

    public double Playhead
    {
        get => GetValue(PlayheadProperty);
        set => SetValue(PlayheadProperty, value);
    }

    public int MaxSlices
    {
        get => GetValue(MaxSlicesProperty);
        set => SetValue(MaxSlicesProperty, value);
    }

    /// <summary>True when there is a slicing to show. Two points is one piece, the least there is.</summary>
    private bool Slicing => SlicePoints is { Count: >= 2 };

    /// <summary>
    /// A point moved, arrived or went. The list is the property, so a change to it is a change
    /// to the picture and has to be repainted like any other.
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // A different recording is a different picture, and being dropped into it eight times
        // magnified at somebody else's scroll position tells you nothing about it.
        if (change.Property == PeaksProperty) _view.ZoomTo(WaveformViewport.MinZoom);

        if (change.Property != SlicePointsProperty) return;

        if (_watching != null) _watching.CollectionChanged -= OnSlicePointsChanged;

        _watching = SlicePoints;

        if (_watching != null) _watching.CollectionChanged += OnSlicePointsChanged;
    }

    private void OnSlicePointsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        InvalidateVisual();

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

        var geometry = WaveformGeometry.Build(peaks, _view, width, height);
        context.DrawGeometry(new SolidColorBrush(palette.Accent, 0.85), null, geometry);

        if (Slicing) DrawSlices(context, palette, area);
        else if (ShowMarkers) DrawMarkers(context, palette, area);

        DrawPlayhead(context, palette, area);
    }

    /// <summary>
    /// Where the sound has got to, drawn over everything else.
    /// </summary>
    /// <remarks>
    /// Over the boundaries and the shading, because it is the one thing here that is moving and
    /// a moving line half hidden behind a still one reads as a drawing fault. In the text
    /// colour rather than the accent, so it cannot be mistaken for a boundary you could take
    /// hold of: this one is telling you something, not offering anything.
    /// </remarks>
    private void DrawPlayhead(DrawingContext context, ThemePalette palette, Rect area)
    {
        double at = Playhead;

        if (double.IsNaN(at) || at < 0 || at > 1) return;

        double x = Math.Clamp(X(at, area.Width), area.X + 1, area.Right - 1);

        context.DrawLine(
            new Pen(new SolidColorBrush(palette.Text, 0.95), 1.5),
            new Point(x, area.Y + 1),
            new Point(x, area.Bottom - 1));
    }

    /// <summary>
    /// The pieces, with the one being worked on picked out and the recording either side of
    /// the sliced region dimmed.
    /// </summary>
    /// <remarks>
    /// The boundaries are drawn last and over everything, because a boundary that a shading
    /// rectangle has half covered is one you cannot tell you are allowed to take hold of.
    /// </remarks>
    private void DrawSlices(DrawingContext context, ThemePalette palette, Rect area)
    {
        var points = SlicePoints!;
        double width = area.Width;
        double head = X(points[0], width);
        double tail = X(points[^1], width);

        var shade = new SolidColorBrush(palette.Background, 0.72);

        if (head > 1) context.FillRectangle(shade, new Rect(1, 1, head - 1, area.Height - 2));
        if (tail < width - 1) context.FillRectangle(shade, new Rect(tail, 1, width - tail - 1, area.Height - 2));

        int selected = SelectedSlice;
        bool picked = selected >= 0 && selected < points.Count - 1;

        if (picked)
        {
            double from = X(points[selected], width);
            double to = X(points[selected + 1], width);

            context.FillRectangle(
                new SolidColorBrush(palette.Accent, 0.16),
                new Rect(from, 1, Math.Max(1, to - from), area.Height - 2));
        }

        if (picked && ShowLoop)
        {
            double from = X(points[selected], width);
            double to = X(points[selected + 1], width);
            double loopStart = Math.Clamp(X(LoopStart, width), from, to);
            double loopEnd = Math.Clamp(X(LoopEnd, width), from, to);

            if (loopEnd > loopStart)
            {
                context.FillRectangle(
                    new SolidColorBrush(palette.Accent, 0.20),
                    new Rect(loopStart, 1, loopEnd - loopStart, area.Height - 2));
            }

            DrawHandle(context, palette.Accent, loopStart, area, dashed: true, atFoot: true);
            DrawHandle(context, palette.Accent, loopEnd, area, dashed: true, atFoot: true);
        }

        DrawSliceNumbers(context, palette, area, points);

        for (int i = 0; i < points.Count; i++)
        {
            bool edge = i == 0 || i == points.Count - 1;

            DrawHandle(context, edge ? palette.Text : palette.Accent, X(points[i], width), area, dashed: false);
        }
    }

    /// <summary>
    /// Which piece is which, where there is room to say so. A number in a piece too narrow to
    /// hold it would sit over its neighbour and read as belonging to the wrong one.
    /// </summary>
    private void DrawSliceNumbers(
        DrawingContext context, ThemePalette palette, Rect area, IList<double> points)
    {
        const double Narrowest = 16;

        double width = area.Width;
        var brush = new SolidColorBrush(palette.Muted, 0.9);

        for (int i = 0; i < points.Count - 1; i++)
        {
            double from = X(points[i], width);
            double to = X(points[i + 1], width);

            if (to - from < Narrowest) continue;

            var text = new FormattedText(
                (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                10,
                i == SelectedSlice ? new SolidColorBrush(palette.Text) : brush);

            context.DrawText(text, new Point(from + 4, area.Bottom - text.Height - GripHeight - 3));
        }
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

    /// <summary>How tall the grip on a handle is, and how far up the picture it reaches.</summary>
    private const double GripHeight = 7;

    private static void DrawHandle(
        DrawingContext context, Color colour, double x, Rect area, bool dashed, bool atFoot = false)
    {
        // Scrolled out of the view. Left where it belongs in the file rather than pinned to the
        // edge, where it would look like something to take hold of and point at the wrong sample.
        if (x < area.X - 0.5 || x > area.Right + 0.5) return;

        var pen = new Pen(new SolidColorBrush(colour, dashed ? 0.9 : 0.75), dashed ? 1 : 1.5)
        {
            DashStyle = dashed ? new DashStyle(new double[] { 3, 3 }, 0) : null
        };

        double clamped = Math.Clamp(x, area.X + 1, area.Right - 1);
        context.DrawLine(pen, new Point(clamped, area.Y + 1), new Point(clamped, area.Bottom - 1));

        // A grip, so it is clear the line can be taken hold of. At the foot for a loop and at
        // the head for a boundary, because on a looping piece the two lines can lie on the same
        // pixel and something has to say which one a click meant.
        double top = atFoot ? area.Bottom - 1 - GripHeight : area.Y + 1;

        context.FillRectangle(new SolidColorBrush(colour, 0.9), new Rect(clamped - 3, top, 6, GripHeight));
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

    /// <summary>
    /// The wheel zooms, holding whatever is under the pointer still.
    /// </summary>
    /// <remarks>
    /// The picture sits inside a panel that scrolls, so the wheel is only taken while there is
    /// somewhere to zoom to. At the far end, zoomed right out and asked to zoom out further,
    /// the wheel is left alone and the panel scrolls as it always did.
    /// </remarks>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (Peaks == null || Bounds.Width <= 0) return;

        double wanted = e.Delta.Y > 0 ? _view.Zoom * ZoomStep : _view.Zoom / ZoomStep;

        if (!_view.ZoomAt(wanted, e.GetPosition(this).X, Bounds.Width)) return;

        InvalidateVisual();
        e.Handled = true;
    }

    /// <summary>
    /// True when a press means "move what is on screen" rather than "take hold of something":
    /// the middle button, or shift with the left one.
    /// </summary>
    private bool MeansPan(PointerPressedEventArgs e) =>
        e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed ||
        e.KeyModifiers.HasFlag(KeyModifiers.Shift);

    private void StartPan(PointerPressedEventArgs e, double x)
    {
        _panFrom = x;
        Cursor = PanCursor;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Peaks == null) return;

        double x = e.GetPosition(this).X;

        if (_view.CanPan && MeansPan(e))
        {
            StartPan(e, x);
            return;
        }

        if (Slicing)
        {
            PressedOnSlices(e, x);
            return;
        }

        if (!ShowMarkers) return;

        _dragging = Nearest(x);

        // Nothing to take hold of here, so the drag moves the picture instead. Only while
        // zoomed in: with the whole file on screen there is nowhere to move it to.
        if (_dragging == Handle.None && _view.CanPan)
        {
            StartPan(e, x);
            return;
        }

        if (_dragging == Handle.None) return;

        e.Pointer.Capture(this);
        Move(x);
        e.Handled = true;
    }

    /// <summary>
    /// A click on a sliced waveform: take hold of a boundary, or pick the piece under the
    /// pointer. Twice adds a boundary where there was none and takes one away where there was.
    /// </summary>
    /// <remarks>
    /// The same gesture both ways round on purpose. A cut and an uncut are the same kind of
    /// thing to want, and asking for them differently, one on the picture and one on a button
    /// somewhere else, makes the picture read as something you can only look at.
    /// </remarks>
    private void PressedOnSlices(PointerPressedEventArgs e, double x)
    {
        // A loop handle taken by its grip at the foot of the picture, before the boundaries are
        // considered: on a looping piece the two lines can be the same line, and the grips are
        // the only thing that tells them apart.
        if (e.ClickCount < 2 && GrabbedLoop(e, x)) return;

        int point = NearestPoint(x);

        if (e.ClickCount >= 2)
        {
            if (point >= 0) RemovePoint(point);
            else AddPoint(At(x));

            e.Handled = true;
            return;
        }

        if (point >= 0)
        {
            // The two ends and the boundaries drag the same way; only what they are next to
            // differs, and MovePoint already knows that.
            _draggingPoint = point;
            e.Pointer.Capture(this);
            MovePoint(point, At(x));
            e.Handled = true;
            return;
        }

        int slice = SliceAt(At(x));

        if (slice >= 0) SelectedSlice = slice;

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!double.IsNaN(_panFrom))
        {
            double x = e.GetPosition(this).X;

            _view.ScrollTo(_view.Scroll - _view.PanDistance(x - _panFrom, Bounds.Width));
            _panFrom = x;

            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_draggingPoint >= 0)
        {
            MovePoint(_draggingPoint, At(e.GetPosition(this).X));
            e.Handled = true;
            return;
        }

        if (_dragging == Handle.None) return;

        if (Slicing) MoveLoop(At(e.GetPosition(this).X));
        else Move(e.GetPosition(this).X);

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!double.IsNaN(_panFrom))
        {
            _panFrom = double.NaN;
            Cursor = Cursor.Default;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (_draggingPoint >= 0)
        {
            _draggingPoint = -1;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

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

        double at = At(x);

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

    /// <summary>
    /// Takes hold of a loop handle when the press was on one of their grips. False when it was
    /// not, and the press goes on to mean whatever else it meant.
    /// </summary>
    private bool GrabbedLoop(PointerPressedEventArgs e, double x)
    {
        var points = SlicePoints;
        int selected = SelectedSlice;

        if (!ShowLoop || points == null) return false;
        if (selected < 0 || selected >= points.Count - 1) return false;

        // Only the foot of the picture, where the loop grips are drawn.
        if (e.GetPosition(this).Y < Bounds.Height - GripHeight - GrabPixels / 2) return false;

        double width = Bounds.Width;
        if (width <= 0) return false;

        double toStart = Math.Abs(X(LoopStart, width) - x);
        double toEnd = Math.Abs(X(LoopEnd, width) - x);

        if (Math.Min(toStart, toEnd) > GrabPixels) return false;

        _dragging = toStart <= toEnd ? Handle.LoopStart : Handle.LoopEnd;

        e.Pointer.Capture(this);
        MoveLoop(At(x));
        e.Handled = true;

        return true;
    }

    /// <summary>
    /// Moves the held loop handle, kept inside the piece it belongs to rather than inside the
    /// whole recording: a loop that wandered out of its piece would play the one next door.
    /// </summary>
    private void MoveLoop(double at)
    {
        var points = SlicePoints;
        int selected = SelectedSlice;

        if (points == null || selected < 0 || selected >= points.Count - 1) return;

        double from = points[selected];
        double to = points[selected + 1];

        if (_dragging == Handle.LoopStart)
            LoopStart = Math.Clamp(at, from, Math.Max(from, Math.Min(to, LoopEnd) - MinGap));
        else if (_dragging == Handle.LoopEnd)
            LoopEnd = Math.Clamp(at, Math.Min(to, Math.Max(from, LoopStart) + MinGap), to);
    }

    /// <summary>Where in the recording a click landed, as a fraction of it.</summary>
    private double At(double x)
    {
        double width = Bounds.Width;

        return width <= 0 ? 0 : Math.Clamp(_view.XToFraction(x, width), 0, 1);
    }

    /// <summary>The slice point a click means, or -1 when the click is nowhere near one.</summary>
    private int NearestPoint(double x)
    {
        var points = SlicePoints;
        double width = Bounds.Width;

        if (points == null || width <= 0) return -1;

        int best = -1;
        double closest = GrabPixels;

        for (int i = 0; i < points.Count; i++)
        {
            double distance = Math.Abs(X(points[i], width) - x);

            if (distance > closest) continue;

            closest = distance;
            best = i;
        }

        return best;
    }

    /// <summary>Which piece a position falls in, or -1 when it falls outside the sliced region.</summary>
    private int SliceAt(double at)
    {
        var points = SlicePoints;

        if (points == null) return -1;

        for (int i = 0; i < points.Count - 1; i++)
            if (at >= points[i] && at <= points[i + 1]) return i;

        return -1;
    }

    /// <summary>
    /// Moves one boundary, stopping it next to its neighbours rather than through them. The
    /// two ends are free to move out to the ends of the recording.
    /// </summary>
    private void MovePoint(int index, double at)
    {
        var points = SlicePoints;

        if (points == null || index < 0 || index >= points.Count) return;

        double lowest = index > 0 ? points[index - 1] + MinGap : 0;
        double highest = index < points.Count - 1 ? points[index + 1] - MinGap : 1;

        if (highest < lowest) return;

        double moved = Math.Clamp(at, lowest, highest);

        if (Math.Abs(moved - points[index]) < 1e-9) return;

        points[index] = moved;
    }

    /// <summary>
    /// Cuts a piece in two. Nothing happens outside the sliced region, where a boundary would
    /// belong to no piece, or when there is no room left for another one.
    /// </summary>
    private void AddPoint(double at)
    {
        var points = SlicePoints;

        if (points == null || points.Count < 2) return;
        if (points.Count - 1 >= MaxSlices) return;
        if (at <= points[0] || at >= points[^1]) return;

        int index = 0;
        while (index < points.Count && points[index] < at) index++;

        if (index > 0 && at - points[index - 1] < MinGap) return;
        if (index < points.Count && points[index] - at < MinGap) return;

        points.Insert(index, at);

        // The new boundary opened a piece before it; that is the one the click asked about.
        SelectedSlice = index - 1;
    }

    /// <summary>
    /// Takes a boundary away, so the two pieces either side of it become one. The ends of the
    /// sliced region are not boundaries between pieces and do not go.
    /// </summary>
    private void RemovePoint(int index)
    {
        var points = SlicePoints;

        if (points == null || index <= 0 || index >= points.Count - 1) return;

        points.RemoveAt(index);

        if (SelectedSlice >= points.Count - 1) SelectedSlice = points.Count - 2;
    }

    /// <summary>
    /// A fraction of the file to where it is on screen. Outside the picture when the view is
    /// zoomed in past it, which is what tells the drawing to leave it out.
    /// </summary>
    private double X(double fraction, double width) =>
        _view.FractionToX(Math.Clamp(double.IsNaN(fraction) ? 0 : fraction, 0, 1), width);
}
