using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.Machines;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// The map itself: every zone drawn as the stretch of keyboard it answers to.
/// </summary>
/// <remarks>
/// A list of ranges is a table of numbers and tells you nothing about whether the keyboard is
/// covered. Drawn, the gaps are the thing you see first, which is the whole reason the machine
/// this follows put a map on its screen: a zone map is read by shape.
///
/// Zones that overlap are stacked, so the one in front is the one that wins, which is also the
/// rule the map plays by.
///
/// Edited the way a sampler is edited: drag an edge to move it, drag the middle to slide the
/// whole zone along, drag the white line to say which key the recording was made at. Nobody
/// dials a zone's edges on a knob.
/// </remarks>
public class ZoneMapView : ThemedControl
{
    private const int Lowest = 0;
    private const int Highest = 119;

    /// <summary>The map behind it.</summary>
    public static readonly StyledProperty<IMachineZones?> ZonesProperty =
        AvaloniaProperty.Register<ZoneMapView, IMachineZones?>(nameof(Zones));

    /// <summary>How tall one lane of zones is.</summary>
    public static readonly StyledProperty<double> LaneHeightProperty =
        AvaloniaProperty.Register<ZoneMapView, double>(nameof(LaneHeight), 20.0);

    /// <summary>The air between one lane and the next.</summary>
    public static readonly StyledProperty<double> LaneGapProperty =
        AvaloniaProperty.Register<ZoneMapView, double>(nameof(LaneGap), 3.0);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<ZoneMapView, double>(nameof(FontSize), 9.0);

    static ZoneMapView()
    {
        AffectsRender<ZoneMapView>(ZonesProperty, LaneHeightProperty, LaneGapProperty, FontSizeProperty);
        AffectsMeasure<ZoneMapView>(ZonesProperty, LaneHeightProperty, LaneGapProperty, FontSizeProperty);
    }

    public ZoneMapView()
    {
        DetachedFromVisualTree += (_, _) => Unwatch();
    }

    public IMachineZones? Zones
    {
        get => GetValue(ZonesProperty);
        set => SetValue(ZonesProperty, value);
    }

    public double LaneHeight
    {
        get => GetValue(LaneHeightProperty);
        set => SetValue(LaneHeightProperty, value);
    }

    public double LaneGap
    {
        get => GetValue(LaneGapProperty);
        set => SetValue(LaneGapProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ZonesProperty) return;

        Unwatch();
        Watch();
    }

    private IMachineZones? _watching;
    private EventHandler? _listening;

    private void Watch()
    {
        if (Zones is not { } map) return;

        _watching = map;

        // A range moves while the map is being dragged and while a preset is landing on it, and
        // neither of those is a property this control could bind to: the map is somebody else's
        // object and says so itself.
        _listening = (_, _) =>
        {
            InvalidateMeasure();
            InvalidateVisual();
        };

        map.Changed += _listening;
    }

    private void Unwatch()
    {
        if (_watching != null && _listening != null) _watching.Changed -= _listening;

        _watching = null;
        _listening = null;
    }

    /// <summary>
    /// Every zone with the lane it sits on: the first lane with room, so overlaps stack.
    /// </summary>
    private List<(int At, int Lane)> Laid()
    {
        var laid = new List<(int, int)>();

        if (Zones is not { } map) return laid;

        // The rightmost key each lane has been filled to, so a zone can be told where it fits.
        var filled = new List<int>();

        for (int at = 0; at < map.Count; at++)
        {
            int lane = 0;

            while (lane < filled.Count && filled[lane] >= map.Low(at)) lane++;

            if (lane == filled.Count) filled.Add(map.High(at));
            else filled[lane] = map.High(at);

            laid.Add((at, lane));
        }

        return laid;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var laid = Laid();
        int lanes = laid.Count == 0 ? 1 : laid.Max(l => l.Lane) + 1;

        double height = lanes * LaneHeight + (lanes - 1) * LaneGap + LabelGap + LineHeight;

        return new Size(
            Math.Max(240, double.IsInfinity(availableSize.Width) ? 480 : availableSize.Width), height);
    }

    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);

        double width = Bounds.Width;
        if (width <= 1) return;

        if (Zones is not { } map) return;

        // The board the zones lie on, so a gap in the map reads as a gap rather than as panel.
        var bed = new SolidColorBrush(ThemePalette.Shade(palette.Surface, -0.30));

        var laid = Laid();
        int lanes = laid.Count == 0 ? 1 : laid.Max(l => l.Lane) + 1;

        for (int lane = 0; lane < lanes; lane++)
        {
            context.DrawRectangle(
                bed, null,
                new Rect(0, lane * (LaneHeight + LaneGap), width, LaneHeight), 2, 2);
        }

        foreach (var (at, lane) in laid)
        {
            double left = At(map.Low(at), width);
            double right = At(map.High(at) + 1, width);
            double top = lane * (LaneHeight + LaneGap);

            var block = new Rect(left, top, Math.Max(3, right - left), LaneHeight);

            bool empty = !map.Filled(at);
            bool picked = map.Picked == at;

            var seat = picked
                ? palette.Accent
                : empty ? ThemePalette.Shade(palette.Surface, -0.05) : ThemePalette.Shade(palette.Surface, 0.22);

            var face = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(ThemePalette.Shade(seat, 0.18), 0),
                    new GradientStop(ThemePalette.Shade(seat, -0.14), 1)
                }
            };

            context.DrawRectangle(
                face,
                new Pen(new SolidColorBrush(ThemePalette.Shade(seat, -0.45)), picked ? 1.5 : 1),
                block, 2, 2);

            // The root: the one key in the zone where the recording plays untouched.
            double root = Middle(map.Root(at));

            if (root >= block.Left && root <= block.Right)
            {
                context.DrawLine(
                    new Pen(new SolidColorBrush(Colors.White, 0.75), 1.5),
                    new Point(root, block.Top + 2), new Point(root, block.Bottom - 2));
            }

            // Trimmed rather than dropped. A zone whose name did not fit said nothing at all,
            // which reads as an empty zone: put a take called "Piano - Somebody like you" on
            // one octave and the map looked exactly as it had before.
            var text = Text(map.Cap(at), empty ? palette.MutedBrush : Brushes.White);

            double room = block.Width - 8;

            if (room >= MinLabel)
            {
                text.MaxTextWidth = room;
                text.MaxLineCount = 1;
                text.Trimming = TextTrimming.CharacterEllipsis;

                context.DrawText(text, new Point(
                    block.Left + 4, block.Top + (LaneHeight - text.Height) / 2));
            }
        }

        // The octaves along the bottom, so a range can be read without counting keys.
        double labels = lanes * (LaneHeight + LaneGap) - LaneGap + LabelGap;

        for (int octave = 0; octave <= 9; octave++)
        {
            double at = At(octave * 12, width);

            context.DrawLine(
                new Pen(new SolidColorBrush(ThemePalette.Shade(palette.Surface, 0.25)), 1),
                new Point(at, 0), new Point(at, labels - LabelGap));

            var mark = Text("C" + octave.ToString(CultureInfo.CurrentCulture), palette.MutedBrush);
            context.DrawText(mark, new Point(at + 2, labels));
        }
    }

    /// <summary>Where a key sits across the strip.</summary>
    private static double At(int semitone, double width) =>
        width * Math.Clamp(semitone, Lowest, Highest + 1) / (Highest + 1 - Lowest);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (Zones is not { } map) return;

        var at = e.GetPosition(this);

        foreach (var (zone, lane) in Laid())
        {
            double top = lane * (LaneHeight + LaneGap);
            double left = At(map.Low(zone), Bounds.Width);
            double right = At(map.High(zone) + 1, Bounds.Width);

            if (!new Rect(left, top, Math.Max(3, right - left), LaneHeight).Contains(at)) continue;

            map.Picked = zone;

            _held = zone;
            _offset = Key(at.X) - map.Low(zone);

            // The edges first, then the root, then the whole thing: an edge is a smaller
            // target than a body and has to win where the two overlap.
            _holding =
                Math.Abs(at.X - left) <= EdgeReach ? Grip.Left :
                Math.Abs(at.X - right) <= EdgeReach ? Grip.Right :
                Math.Abs(at.X - Middle(map.Root(zone))) <= EdgeReach ? Grip.Root :
                Grip.Body;

            e.Pointer.Capture(this);
            e.Handled = true;

            InvalidateVisual();

            return;
        }
    }

    /// <summary>Dragging an edge resizes, dragging the middle moves, dragging the line retunes.</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_held < 0 || _holding == Grip.None) return;

        if (Zones is not { } map || _held >= map.Count) return;

        int key = Key(e.GetPosition(this).X);

        int low = map.Low(_held);
        int high = map.High(_held);
        int root = map.Root(_held);

        switch (_holding)
        {
            case Grip.Left:
                // Held to its own right edge: a zone turned inside out answers to nothing.
                low = Math.Min(key, high);
                break;

            case Grip.Right:
                high = Math.Max(key, low);
                break;

            case Grip.Root:
                root = key;
                break;

            case Grip.Body:
                (low, high, root) = Slid(low, high, root, key - _offset);
                break;
        }

        map.Move(_held, low, high, root);

        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_held < 0) return;

        _held = -1;
        _holding = Grip.None;

        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>
    /// A zone moved bodily: it keeps its width and carries its root along with it.
    /// </summary>
    /// <remarks>
    /// The root travels because it is a property of the recording rather than of the keyboard:
    /// moving a zone up the keyboard should not quietly transpose everything in it.
    /// </remarks>
    private static (int Low, int High, int Root) Slid(int low, int high, int root, int wantedLow)
    {
        int width = high - low;
        int put = Math.Clamp(wantedLow, Lowest, Highest - width);
        int moved = put - low;

        if (moved == 0) return (low, high, root);

        return (put, put + width, Math.Clamp(root + moved, Lowest, Highest));
    }

    /// <summary>Which key is under a point across the strip.</summary>
    private int Key(double x) =>
        (int)Math.Clamp(Math.Floor(x / Bounds.Width * (Highest + 1 - Lowest)), Lowest, Highest);

    /// <summary>The middle of one key, for drawing and for grabbing at.</summary>
    private double Middle(int semitone) =>
        At(semitone, Bounds.Width) + (At(semitone + 1, Bounds.Width) - At(semitone, Bounds.Width)) / 2;

    /// <summary>What part of a zone the pointer took hold of.</summary>
    private enum Grip
    {
        None,
        Left,
        Right,
        Root,
        Body
    }

    private Grip _holding;

    /// <summary>Which zone is being dragged, or -1 for none.</summary>
    private int _held = -1;

    /// <summary>Where in the held zone the pointer was, so a dragged zone does not jump.</summary>
    private int _offset;

    /// <summary>How near an edge counts as taking hold of it.</summary>
    private const double EdgeReach = 5;

    /// <summary>Narrower than this and there is no room for a name worth reading.</summary>
    private const double MinLabel = 22;

    /// <summary>The air between the lanes and the octave marks under them.</summary>
    private const double LabelGap = 3;

    private double LineHeight => Text("C0", Brushes.Black).Height;

    private FormattedText Text(string? text, IBrush brush) =>
        new(text ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), FontSize, brush);
}
