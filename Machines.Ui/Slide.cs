using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.UI;
using System;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// A value on a rail, set by a handle you push along it.
/// </summary>
/// <remarks>
/// The horizontal one, where <see cref="Fader"/> is the upright one and <see cref="Knob"/> is
/// the round one. It is the quiet member of the three: no label, no reading, no scale, just the
/// rail and the handle, because it is meant for a row that already says what it is on the left
/// and what it comes to on the right. A control that drew its own name would be fighting the
/// grid it was put in.
///
/// Drawn rather than a styled <c>Slider</c>. The stock one carries the toolkit's own look and
/// its own colours, which have to be prised off resource by resource, and a control theme's
/// setters outrank a style's, so half of that only works by a trick. Painting it here is fewer
/// lines than the overrides were, it reads <see cref="ThemePalette"/> like every other drawn
/// control in the application, and it follows a theme swap without being told twice.
///
/// The handle is a small square with a slight round on it, not a dot. On a row that is setting
/// a colour, the handle and the swatch beside it are then the same shape, and a dot reads as a
/// third thing on a row that only has two.
/// </remarks>
public class Slide : ThemedControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Slide, double>(nameof(Value), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<Slide, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<Slide, double>(nameof(Maximum), 1.0);

    /// <summary>The grid the value snaps to, and one press of an arrow key.</summary>
    public static readonly StyledProperty<double> SmallStepProperty =
        AvaloniaProperty.Register<Slide, double>(nameof(SmallStep), 0.01);

    /// <summary>One press with shift held.</summary>
    public static readonly StyledProperty<double> LargeStepProperty =
        AvaloniaProperty.Register<Slide, double>(nameof(LargeStep), 0.1);

    /// <summary>Where a double click puts it back to. Nothing happens when it is not set.</summary>
    public static readonly StyledProperty<double?> DefaultValueProperty =
        AvaloniaProperty.Register<Slide, double?>(nameof(DefaultValue));

    public static readonly StyledProperty<double> HandleWidthProperty =
        AvaloniaProperty.Register<Slide, double>(nameof(HandleWidth), 10.0);

    public static readonly StyledProperty<double> HandleHeightProperty =
        AvaloniaProperty.Register<Slide, double>(nameof(HandleHeight), 18.0);

    public static readonly StyledProperty<double> RailThicknessProperty =
        AvaloniaProperty.Register<Slide, double>(nameof(RailThickness), 2.0);

    /// <summary>
    /// What the travelled part of the rail is painted with, or nothing for the usual grey.
    /// </summary>
    /// <remarks>
    /// For the row that is setting a colour: hand it that colour and the rail says the same
    /// thing the number does. Left alone it stays out of the way, which is what most rows want.
    /// </remarks>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<Slide, IBrush?>(nameof(Fill));

    /// <summary>The shortest rail worth drawing, for a layout that asks what it wants.</summary>
    private const double ShortestRail = 80;

    private const double HandleCorner = 2;

    /// <summary>One cursor for every one of these: each instance would hold a platform handle.</summary>
    private static readonly Cursor DragCursor = new(StandardCursorType.SizeWestEast);

    static Slide()
    {
        AffectsRender<Slide>(
            ValueProperty, MinimumProperty, MaximumProperty,
            HandleWidthProperty, HandleHeightProperty, RailThicknessProperty, FillProperty);

        AffectsMeasure<Slide>(HandleWidthProperty, HandleHeightProperty);
    }

    public Slide()
    {
        Focusable = true;
        Cursor = DragCursor;
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double SmallStep
    {
        get => GetValue(SmallStepProperty);
        set => SetValue(SmallStepProperty, value);
    }

    public double LargeStep
    {
        get => GetValue(LargeStepProperty);
        set => SetValue(LargeStepProperty, value);
    }

    public double? DefaultValue
    {
        get => GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    public double HandleWidth
    {
        get => GetValue(HandleWidthProperty);
        set => SetValue(HandleWidthProperty, value);
    }

    public double HandleHeight
    {
        get => GetValue(HandleHeightProperty);
        set => SetValue(HandleHeightProperty, value);
    }

    public double RailThickness
    {
        get => GetValue(RailThicknessProperty);
        set => SetValue(RailThicknessProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(ShortestRail + HandleWidth, HandleHeight);

    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);

        double middle = Bounds.Height / 2;
        double travel = Travel();

        if (travel <= 0) return;

        double left = HandleWidth / 2;
        double at = left + RangeValue.Fraction(Value, Minimum, Maximum) * travel;

        var rail = new Rect(left, middle - RailThickness / 2, travel, RailThickness);

        // The groove first, in the same colour a fader's is: what has not been travelled yet is
        // a line on the panel, not a dimmer version of the value.
        context.DrawRectangle(palette.BorderBrush, null, new RoundedRect(rail, RailThickness / 2));

        // The part behind the handle, so the value can be read without finding the handle first.
        if (at > left)
        {
            var travelled = new Rect(left, rail.Y, at - left, RailThickness);

            context.DrawRectangle(Fill ?? palette.MutedBrush, null, new RoundedRect(travelled, RailThickness / 2));
        }

        var handle = HandleAt(at);
        var rim = IsFocused || _hovered ? palette.Accent : palette.Border;

        context.DrawRectangle(
            palette.TextBrush,
            new Pen(new SolidColorBrush(rim), IsFocused ? 1.6 : 1),
            new RoundedRect(handle, HandleCorner));
    }

    /// <summary>How far the middle of the handle moves, end to end.</summary>
    private double Travel() => Bounds.Width - HandleWidth;

    private Rect HandleAt(double at) =>
        new(at - HandleWidth / 2, (Bounds.Height - HandleHeight) / 2, HandleWidth, HandleHeight);

    private bool _hovered;
    private bool _dragging;
    private bool _fineDrag;

    /// <summary>Where on the handle it was taken hold of, so it does not jump under the hand.</summary>
    private double _grabOffset;

    private double _dragStartX;
    private double _dragStartValue;

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);

        _hovered = true;

        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        _hovered = false;

        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        Focus();

        double x = e.GetPosition(this).X;
        double at = HandleWidth / 2 + RangeValue.Fraction(Value, Minimum, Maximum) * Travel();

        // On the handle: pick it up where it is. Anywhere else on the rail: send it there first.
        if (HandleAt(at).Contains(new Point(x, Bounds.Height / 2)))
        {
            _grabOffset = x - at;
        }
        else
        {
            _grabOffset = 0;

            Value = ValueAt(x);
        }

        _dragging = true;
        _fineDrag = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _dragStartX = x;
        _dragStartValue = Value;

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_dragging) return;

        double x = e.GetPosition(this).X;

        // Shift trades the handle following the pointer for a quarter speed drag from the press.
        bool fine = _fineDrag || e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        Value = fine
            ? RangeValue.FromDrag(_dragStartValue, x - _dragStartX, Minimum, Maximum, SmallStep, Travel(), fine: true)
            : ValueAt(x - _grabOffset);

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_dragging) return;

        _dragging = false;
        _fineDrag = false;

        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        // Deliberately not calling the base: over one of these the wheel moves it rather than
        // scrolling whatever it sits in.
        StepBy(Math.Sign(e.Delta.Y), e.KeyModifiers);

        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Up:
            case Key.Right:
                StepBy(1, e.KeyModifiers);
                break;

            case Key.Down:
            case Key.Left:
                StepBy(-1, e.KeyModifiers);
                break;

            case Key.Home:
                Value = RangeValue.Quantize(Minimum, Minimum, Maximum, SmallStep);
                break;

            case Key.End:
                Value = RangeValue.Quantize(Maximum, Minimum, Maximum, SmallStep);
                break;

            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>Double click puts it back where it started.</summary>
    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);

        if (DefaultValue is not double reset) return;

        Value = RangeValue.Quantize(reset, Minimum, Maximum, SmallStep);

        e.Handled = true;
    }

    private void StepBy(int direction, KeyModifiers modifiers)
    {
        if (direction == 0) return;

        double step = modifiers.HasFlag(KeyModifiers.Shift) ? LargeStep : SmallStep;

        Value = RangeValue.Quantize(Value + direction * step, Minimum, Maximum, SmallStep);
    }

    /// <summary>The value the middle of the handle would have at that point on the rail.</summary>
    private double ValueAt(double x)
    {
        double travel = Travel();

        if (travel <= 0 || Maximum <= Minimum) return Minimum;

        double fraction = Math.Clamp((x - HandleWidth / 2) / travel, 0, 1);

        return RangeValue.Quantize(Minimum + fraction * (Maximum - Minimum), Minimum, Maximum, SmallStep);
    }
}
