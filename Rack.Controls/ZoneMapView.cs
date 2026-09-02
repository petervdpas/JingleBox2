using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.Rack.Controls.Records;

namespace JingleBox2.Rack.Controls;

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
    /// <summary>
    /// The keyboard the strip covers: ten octaves from C0.
    /// </summary>
    /// <remarks>
    /// Wider than any keyboard anybody owns, deliberately, since a zone can be put anywhere a
    /// note number can go and a map that could not show one would be lying about its own
    /// contents. The octave marks along the foot run C0 to C9 over the same range.
    /// </remarks>
    private const int Lowest = 0;

    /// <inheritdoc cref="Lowest"/>
    private const int Highest = 119;

    /// <summary>The map behind it.</summary>
    public static readonly StyledProperty<IPanelZones?> ZonesProperty =
        AvaloniaProperty.Register<ZoneMapView, IPanelZones?>(nameof(Zones));

    /// <summary>How tall one lane of zones is.</summary>
    public static readonly StyledProperty<double> LaneHeightProperty =
        AvaloniaProperty.Register<ZoneMapView, double>(nameof(LaneHeight), 20.0);

    /// <summary>The air between one lane and the next.</summary>
    public static readonly StyledProperty<double> LaneGapProperty =
        AvaloniaProperty.Register<ZoneMapView, double>(nameof(LaneGap), 3.0);

    /// <summary>
    /// Backs <see cref="FontSize"/>, which sizes the zone names and the octave marks alike.
    /// </summary>
    /// <remarks>
    /// It also decides how tall the control is, through <see cref="LineHeight"/>, so a change to
    /// it has to be measured again rather than merely repainted.
    /// </remarks>
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<ZoneMapView, double>(nameof(FontSize), 9.0);

    /// <summary>
    /// Says which properties change the picture and the size, which here is all of them.
    /// </summary>
    /// <remarks>
    /// The map itself is in the measure list because how many lanes there are depends on how the
    /// zones overlap, and that is a fact about the map rather than about the room it is given.
    /// </remarks>
    static ZoneMapView()
    {
        AffectsRender<ZoneMapView>(ZonesProperty, LaneHeightProperty, LaneGapProperty, FontSizeProperty);
        AffectsMeasure<ZoneMapView>(ZonesProperty, LaneHeightProperty, LaneGapProperty, FontSizeProperty);
    }

    /// <summary>Lets go of the map when the control leaves the tree.</summary>
    public ZoneMapView()
    {
        DetachedFromVisualTree += (_, _) => Unwatch();
    }

    /// <inheritdoc cref="ZonesProperty"/>
    public IPanelZones? Zones
    {
        get => GetValue(ZonesProperty);
        set => SetValue(ZonesProperty, value);
    }

    /// <inheritdoc cref="LaneHeightProperty"/>
    public double LaneHeight
    {
        get => GetValue(LaneHeightProperty);
        set => SetValue(LaneHeightProperty, value);
    }

    /// <inheritdoc cref="LaneGapProperty"/>
    public double LaneGap
    {
        get => GetValue(LaneGapProperty);
        set => SetValue(LaneGapProperty, value);
    }

    /// <inheritdoc cref="FontSizeProperty"/>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Moves the listening to whichever map has just arrived.
    /// </summary>
    /// <remarks>
    /// The old one is let go of first, or a control handed two maps in a row would go on
    /// repainting for the first as long as anything else held it.
    /// </remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ZonesProperty) return;

        Unwatch();
        Watch();
    }

    /// <summary>
    /// The map being listened to, and the handler doing it.
    /// </summary>
    /// <remarks>
    /// Both are kept so the subscription can be taken off again: the handler is a closure rather
    /// than a method, so it is not the same delegate twice and could not be unsubscribed without
    /// having been held on to.
    /// </remarks>
    private IPanelZones? _watching;

    /// <inheritdoc cref="_watching"/>
    private EventHandler? _listening;

    /// <summary>
    /// Starts listening to the map now in hand.
    /// </summary>
    /// <remarks>
    /// A range moves while the map is being dragged and while a preset is landing on it, and
    /// neither of those is a property this control could bind to: the map is somebody else's
    /// object and says so itself.
    ///
    /// Both the measure and the drawing are thrown away on a change, since a zone that has moved
    /// can want a lane that was not there before.
    /// </remarks>
    private void Watch()
    {
        if (Zones is not { } map) return;

        _watching = map;

        _listening = (_, _) =>
        {
            InvalidateMeasure();
            InvalidateVisual();
        };

        map.Changed += _listening;
    }

    /// <summary>Stops listening, so nothing here keeps a map alive after it has been put down.</summary>
    private void Unwatch()
    {
        if (_watching != null && _listening != null) _watching.Changed -= _listening;

        _watching = null;
        _listening = null;
    }

    /// <summary>
    /// Every zone with the lane it sits on: the first lane with room, so overlaps stack.
    /// </summary>
    /// <remarks>
    /// The running list is the rightmost key each lane has been filled to, which is all a zone
    /// needs to be told where it fits. Zones are taken in the map's own order, so the one in
    /// front is the one lying lowest, which is also the rule the map plays by when a key falls
    /// under two of them.
    /// </remarks>
    private List<(int At, int Lane)> Laid()
    {
        var laid = new List<(int, int)>();

        if (Zones is not { } map) return laid;

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

    /// <summary>
    /// As tall as the lanes plus the octave marks under them, and as wide as it is offered.
    /// </summary>
    /// <remarks>
    /// One lane is claimed even when there are no zones at all, so an empty map is a strip
    /// somebody can drop a zone onto rather than nothing at all.
    ///
    /// The fallback width is for being measured with no limit, which is what a panel that
    /// scrolls sideways offers: a strip that asked for infinity would be drawn off the page.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        var laid = Laid();
        int lanes = laid.Count == 0 ? 1 : laid.Max(l => l.Lane) + 1;

        double height = lanes * LaneHeight + (lanes - 1) * LaneGap + LabelGap + LineHeight;

        return new Size(
            Math.Max(240, double.IsInfinity(availableSize.Width) ? 480 : availableSize.Width), height);
    }

    /// <summary>
    /// The lanes, the zones on them, and the octave marks along the foot.
    /// </summary>
    /// <remarks>
    /// The board under the zones is drawn darker than the panel, so a gap in the map reads as a
    /// gap rather than as bare panel. That is the thing a drawn map is for: a list of ranges
    /// tells you nothing about whether the keyboard is covered.
    ///
    /// A zone's name is trimmed rather than dropped when it will not fit. Dropped, a zone with a
    /// long name said nothing at all, which reads as an empty zone: putting a take called
    /// "Piano - Somebody like you" on one octave left the map looking exactly as it had before.
    ///
    /// The white line inside a zone is its root, the one key where the recording plays untouched.
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);

        double width = Bounds.Width;
        if (width <= 1) return;

        if (Zones is not { } map) return;

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

            double root = Middle(map.Root(at));

            if (root >= block.Left && root <= block.Right)
            {
                context.DrawLine(
                    new Pen(new SolidColorBrush(Colors.White, 0.75), 1.5),
                    new Point(root, block.Top + 2), new Point(root, block.Bottom - 2));
            }

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

    /// <summary>
    /// Picks the zone under the pointer and works out which part of it was taken hold of.
    /// </summary>
    /// <remarks>
    /// The edges are tested first, then the root, then the whole body. An edge is a smaller
    /// target than a body and has to win wherever the two overlap, or a narrow zone could never
    /// be resized at all.
    /// </remarks>
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
    /// <remarks>
    /// Each edge is held to the other one, so a zone cannot be dragged inside out: a zone whose
    /// low key is above its high key answers to nothing at all, and there would be no way of
    /// seeing that on the map to put it right.
    /// </remarks>
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

    /// <summary>Lets go of whatever was being dragged.</summary>
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

    /// <summary>Which part of the held zone the hand took, and none while nothing is being dragged.</summary>
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

    /// <summary>
    /// How tall a line of the octave marks is, measured off a real one.
    /// </summary>
    /// <remarks>
    /// Measured rather than assumed, since the size is a property somebody can set and the marks
    /// are the last thing down the control: guessing wrong clips them.
    /// </remarks>
    private double LineHeight => Text("C0", Brushes.Black).Height;

    /// <summary>A piece of text at the map's own size, for a zone's name or an octave mark.</summary>
    private FormattedText Text(string? text, IBrush brush) =>
        new(text ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), FontSize, brush);
}
