using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.ViewModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace JingleBox2.Views;

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
public class ZoneStrip : ThemedControl
{
    private const int Lowest = 0;
    private const int Highest = 119;
    private const double LaneGap = 3;
    private const double LabelGap = 3;

    /// <summary>The zones to draw.</summary>
    public static readonly StyledProperty<IEnumerable?> ZonesProperty =
        AvaloniaProperty.Register<ZoneStrip, IEnumerable?>(nameof(Zones));

    /// <summary>Which zone is in hand. Clicking one sets it.</summary>
    public static readonly StyledProperty<object?> SelectedProperty =
        AvaloniaProperty.Register<ZoneStrip, object?>(
            nameof(Selected), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Bumped by the map whenever a range moves, since the ranges are plain data.</summary>
    public static readonly StyledProperty<int> RevisionProperty =
        AvaloniaProperty.Register<ZoneStrip, int>(nameof(Revision));

    /// <summary>How tall one lane of zones is.</summary>
    public static readonly StyledProperty<double> LaneHeightProperty =
        AvaloniaProperty.Register<ZoneStrip, double>(nameof(LaneHeight), 20.0);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<ZoneStrip, double>(nameof(FontSize), 9.0);

    static ZoneStrip()
    {
        AffectsRender<ZoneStrip>(
            ZonesProperty, SelectedProperty, RevisionProperty, LaneHeightProperty, FontSizeProperty);

        AffectsMeasure<ZoneStrip>(ZonesProperty, RevisionProperty, LaneHeightProperty, FontSizeProperty);
    }

    public IEnumerable? Zones
    {
        get => GetValue(ZonesProperty);
        set => SetValue(ZonesProperty, value);
    }

    public object? Selected
    {
        get => GetValue(SelectedProperty);
        set => SetValue(SelectedProperty, value);
    }

    public int Revision
    {
        get => GetValue(RevisionProperty);
        set => SetValue(RevisionProperty, value);
    }

    public double LaneHeight
    {
        get => GetValue(LaneHeightProperty);
        set => SetValue(LaneHeightProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Every zone with the lane it sits on: the first lane with room, so overlaps stack.
    /// </summary>
    private List<(SampleZoneViewModel Zone, int Lane)> Laid()
    {
        var laid = new List<(SampleZoneViewModel, int)>();

        if (Zones == null) return laid;

        // The rightmost key each lane has been filled to, so a zone can be told where it fits.
        var filled = new List<int>();

        foreach (var item in Zones)
        {
            if (item is not SampleZoneViewModel zone) continue;

            int lane = 0;

            while (lane < filled.Count && filled[lane] >= zone.Zone.Low) lane++;

            if (lane == filled.Count) filled.Add(zone.Zone.High);
            else filled[lane] = zone.Zone.High;

            laid.Add((zone, lane));
        }

        return laid;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var laid = Laid();
        int lanes = laid.Count == 0 ? 1 : laid.Max(l => l.Lane) + 1;

        double height = lanes * LaneHeight + (lanes - 1) * LaneGap + LabelGap + LineHeight;

        return new Size(Math.Max(240, double.IsInfinity(availableSize.Width) ? 480 : availableSize.Width), height);
    }

    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);

        double width = Bounds.Width;
        if (width <= 1) return;

        // The board the zones lie on, so a gap in the map reads as a gap rather than as panel.
        var bed = new SolidColorBrush(Shade(palette.Surface, -0.30));

        var laid = Laid();
        int lanes = laid.Count == 0 ? 1 : laid.Max(l => l.Lane) + 1;

        for (int lane = 0; lane < lanes; lane++)
        {
            context.DrawRectangle(
                bed, null,
                new Rect(0, lane * (LaneHeight + LaneGap), width, LaneHeight), 2, 2);
        }

        foreach (var (zone, lane) in laid)
        {
            double left = At(zone.Zone.Low, width);
            double right = At(zone.Zone.High + 1, width);
            double top = lane * (LaneHeight + LaneGap);

            var block = new Rect(left, top, Math.Max(3, right - left), LaneHeight);

            bool empty = !zone.HasSound;
            var seat = zone.IsSelected
                ? palette.Accent
                : empty ? Shade(palette.Surface, -0.05) : Shade(palette.Surface, 0.22);

            var face = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Shade(seat, 0.18), 0),
                    new GradientStop(Shade(seat, -0.14), 1)
                }
            };

            context.DrawRectangle(
                face,
                new Pen(new SolidColorBrush(Shade(seat, -0.45)), zone.IsSelected ? 1.5 : 1),
                block, 2, 2);

            // The root: the one key in the zone where the recording plays untouched.
            double root = Middle(zone.Zone.Root);

            if (root >= block.Left && root <= block.Right)
            {
                context.DrawLine(
                    new Pen(new SolidColorBrush(Colors.White, 0.75), 1.5),
                    new Point(root, block.Top + 2), new Point(root, block.Bottom - 2));
            }

            var text = Text(zone.Title, empty ? palette.MutedBrush : Brushes.White);

            if (text.Width + 8 < block.Width)
            {
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
                new Pen(new SolidColorBrush(Shade(palette.Surface, 0.25)), 1),
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

        var at = e.GetPosition(this);

        foreach (var (zone, lane) in Laid())
        {
            double top = lane * (LaneHeight + LaneGap);
            double left = At(zone.Zone.Low, Bounds.Width);
            double right = At(zone.Zone.High + 1, Bounds.Width);

            if (!new Rect(left, top, Math.Max(3, right - left), LaneHeight).Contains(at)) continue;

            Selected = zone;

            _held = zone;
            _offset = Key(at.X) - zone.Zone.Low;

            // The edges first, then the root, then the whole thing: an edge is a smaller
            // target than a body and has to win where the two overlap.
            _holding =
                Math.Abs(at.X - left) <= EdgeReach ? Grip.Left :
                Math.Abs(at.X - right) <= EdgeReach ? Grip.Right :
                Math.Abs(at.X - Middle(zone.Zone.Root)) <= EdgeReach ? Grip.Root :
                Grip.Body;

            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
    }

    /// <summary>Dragging an edge resizes, dragging the middle moves, dragging the line retunes.</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_held == null || _holding == Grip.None) return;

        int key = Key(e.GetPosition(this).X);

        switch (_holding)
        {
            case Grip.Left:
                // Held to its own right edge: a zone turned inside out answers to nothing.
                _held.Low = Math.Min(key, _held.Zone.High);
                break;

            case Grip.Right:
                _held.High = Math.Max(key, _held.Zone.Low);
                break;

            case Grip.Root:
                _held.Root = key;
                break;

            case Grip.Body:
                Slide(_held, key - _offset);
                break;
        }

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_held == null) return;

        _held = null;
        _holding = Grip.None;

        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>
    /// Moves a zone bodily, keeping its width and carrying its root along with it.
    /// </summary>
    /// <remarks>
    /// The root travels because it is a property of the recording rather than of the keyboard:
    /// moving a zone up the keyboard should not quietly transpose everything in it.
    /// </remarks>
    private void Slide(SampleZoneViewModel zone, int wantedLow)
    {
        int width = zone.Zone.High - zone.Zone.Low;
        int low = Math.Clamp(wantedLow, Lowest, Highest - width);
        int moved = low - zone.Zone.Low;

        if (moved == 0) return;

        int root = Math.Clamp(zone.Zone.Root + moved, Lowest, Highest);

        // High first when moving up, or the two would cross on the way and be turned round.
        if (moved > 0)
        {
            zone.High = low + width;
            zone.Low = low;
        }
        else
        {
            zone.Low = low;
            zone.High = low + width;
        }

        zone.Root = root;
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
    private SampleZoneViewModel? _held;

    /// <summary>Where in the held zone the pointer was, so a dragged zone does not jump.</summary>
    private int _offset;

    /// <summary>How near an edge counts as taking hold of it.</summary>
    private const double EdgeReach = 5;

    private double LineHeight => Text("C0", Brushes.Black).Height;

    private FormattedText Text(string? text, IBrush brush) =>
        new(text ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), FontSize, brush);

    private static Color Shade(Color colour, double amount)
    {
        double Mix(byte channel) => amount >= 0
            ? channel + (255 - channel) * amount
            : channel * (1 + amount);

        return Color.FromRgb((byte)Mix(colour.R), (byte)Mix(colour.G), (byte)Mix(colour.B));
    }
}
