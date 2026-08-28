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
    /// <summary>
    /// Backs <see cref="Value"/>, where the handle stands.
    /// </summary>
    /// <remarks>
    /// Two way, because the handle is pushed by hand and whatever it is bound to has to hear
    /// about it. Not coerced into range the way a knob's and a fader's are: this one is only
    /// ever driven from a row of settings, never from a machine's own arithmetic.
    /// </remarks>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Slide, double>(nameof(Value), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Backs <see cref="Minimum"/>, the value at the left hand end of the rail.</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<Slide, double>(nameof(Minimum));

    /// <summary>
    /// Backs <see cref="Maximum"/>, the value at the right hand end.
    /// </summary>
    /// <remarks>
    /// One rather than nought, so a rail nobody has given a range to runs over the nought to one
    /// every parameter here already uses rather than being stuck against a dead range.
    /// </remarks>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<Slide, double>(nameof(Maximum), 1.0);

    /// <summary>Backs <see cref="SmallStep"/>: the grid the value snaps to, and one arrow key.</summary>
    public static readonly StyledProperty<double> SmallStepProperty =
        AvaloniaProperty.Register<Slide, double>(nameof(SmallStep), 0.01);

    /// <summary>Backs <see cref="LargeStep"/>: one arrow key with shift held.</summary>
    public static readonly StyledProperty<double> LargeStepProperty =
        AvaloniaProperty.Register<Slide, double>(nameof(LargeStep), 0.1);

    /// <summary>
    /// Backs <see cref="DefaultValue"/>: where a double click puts it back to, and nothing
    /// happens when it is not set.
    /// </summary>
    public static readonly StyledProperty<double?> DefaultValueProperty =
        AvaloniaProperty.Register<Slide, double?>(nameof(DefaultValue));

    /// <summary>
    /// Backs <see cref="HandleWidth"/>, which is also how much shorter than the control the
    /// travel is.
    /// </summary>
    /// <remarks>
    /// The middle of the handle has to reach both ends of the value without half the handle
    /// hanging off the control, so the rail is inset by half of this at each end.
    /// </remarks>
    public static readonly StyledProperty<double> HandleWidthProperty =
        AvaloniaProperty.Register<Slide, double>(nameof(HandleWidth), 10.0);

    /// <summary>
    /// Backs <see cref="HandleHeight"/>, which also sets how tall the whole control stands.
    /// </summary>
    /// <remarks>
    /// Taller than it is wide, so the handle reads as something to push sideways rather than as
    /// a bead threaded on the rail.
    /// </remarks>
    public static readonly StyledProperty<double> HandleHeightProperty =
        AvaloniaProperty.Register<Slide, double>(nameof(HandleHeight), 18.0);

    /// <summary>Backs <see cref="RailThickness"/>, the line the handle runs along.</summary>
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

    /// <summary>
    /// How much the handle's corners are rounded.
    /// </summary>
    /// <remarks>
    /// A slight round rather than a full one, so on a row that is setting a colour the handle
    /// and the swatch beside it are the same shape. A dot would read as a third thing on a row
    /// that has only two.
    /// </remarks>
    private const double HandleCorner = 2;

    /// <summary>One cursor for every one of these: each instance would hold a platform handle.</summary>
    private static readonly Cursor DragCursor = new(StandardCursorType.SizeWestEast);

    /// <summary>Says which properties change the picture and which change the size.</summary>
    static Slide()
    {
        AffectsRender<Slide>(
            ValueProperty, MinimumProperty, MaximumProperty,
            HandleWidthProperty, HandleHeightProperty, RailThicknessProperty, FillProperty);

        AffectsMeasure<Slide>(HandleWidthProperty, HandleHeightProperty);
    }

    /// <summary>Takes the keyboard, and wears the side-to-side cursor so the drag is discoverable.</summary>
    public Slide()
    {
        Focusable = true;
        Cursor = DragCursor;
    }

    /// <inheritdoc cref="ValueProperty"/>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>The value at the left hand end of the rail.</summary>
    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>The value at the right hand end.</summary>
    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>The grid every value lands on, and how far one arrow key moves it.</summary>
    public double SmallStep
    {
        get => GetValue(SmallStepProperty);
        set => SetValue(SmallStepProperty, value);
    }

    /// <summary>How far one arrow key moves it with shift held.</summary>
    public double LargeStep
    {
        get => GetValue(LargeStepProperty);
        set => SetValue(LargeStepProperty, value);
    }

    /// <summary>Where a double click puts it back to, or nothing when it has no detent.</summary>
    public double? DefaultValue
    {
        get => GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    /// <inheritdoc cref="HandleWidthProperty"/>
    public double HandleWidth
    {
        get => GetValue(HandleWidthProperty);
        set => SetValue(HandleWidthProperty, value);
    }

    /// <inheritdoc cref="HandleHeightProperty"/>
    public double HandleHeight
    {
        get => GetValue(HandleHeightProperty);
        set => SetValue(HandleHeightProperty, value);
    }

    /// <summary>How thick the rail is drawn.</summary>
    public double RailThickness
    {
        get => GetValue(RailThicknessProperty);
        set => SetValue(RailThicknessProperty, value);
    }

    /// <inheritdoc cref="FillProperty"/>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>
    /// The shortest rail worth drawing, plus the room the handle needs at the ends.
    /// </summary>
    /// <remarks>
    /// A minimum rather than a want, since these are put in rows that hand out whatever is left
    /// over and a rail takes the whole width it is given.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize) =>
        new(ShortestRail + HandleWidth, HandleHeight);

    /// <summary>
    /// Paints the rail, the part of it behind the handle, and the handle itself.
    /// </summary>
    /// <remarks>
    /// The groove goes down first in the same colour a fader's is: what has not been travelled
    /// yet is a line on the panel, not a dimmer version of the value. The travelled part is then
    /// drawn over it so the value can be read without finding the handle first, which is what
    /// <see cref="Fill"/> is for on a row that is setting a colour.
    ///
    /// The rim takes the accent colour while the control is hovered or holds the keyboard.
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);

        double middle = Bounds.Height / 2;
        double travel = Travel();

        if (travel <= 0) return;

        double left = HandleWidth / 2;
        double at = left + RangeValue.Fraction(Value, Minimum, Maximum) * travel;

        var rail = new Rect(left, middle - RailThickness / 2, travel, RailThickness);

        context.DrawRectangle(palette.BorderBrush, null, new RoundedRect(rail, RailThickness / 2));

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

    /// <summary>
    /// Where the handle sits for a given middle.
    /// </summary>
    /// <remarks>
    /// Both the drawing and the press test go through here, so the handle cannot end up drawn
    /// somewhere other than where a grab at it is looked for.
    /// </remarks>
    private Rect HandleAt(double at) =>
        new(at - HandleWidth / 2, (Bounds.Height - HandleHeight) / 2, HandleWidth, HandleHeight);

    /// <summary>Whether the pointer is over it, which lights the handle's rim.</summary>
    private bool _hovered;

    /// <summary>Whether a button is down on it.</summary>
    private bool _dragging;

    /// <summary>
    /// Whether shift was held at the press, which trades following the pointer for a slow drag.
    /// </summary>
    /// <remarks>
    /// Remembered from the press as well as read live, so shift taken at the start holds for the
    /// whole movement even if the key is let go part way.
    /// </remarks>
    private bool _fineDrag;

    /// <summary>Where on the handle it was taken hold of, so it does not jump under the hand.</summary>
    private double _grabOffset;

    /// <summary>Where the drag began, and what the value was there, for the fine drag.</summary>
    private double _dragStartX;

    /// <inheritdoc cref="_dragStartX"/>
    private double _dragStartValue;

    /// <summary>Lights the handle's rim, so the one under the hand is visibly the one that will move.</summary>
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);

        _hovered = true;

        InvalidateVisual();
    }

    /// <summary>Puts the rim back.</summary>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        _hovered = false;

        InvalidateVisual();
    }

    /// <summary>
    /// Starts a drag. On the handle it is picked up where it is; anywhere else on the rail sends
    /// it there first.
    /// </summary>
    /// <remarks>
    /// The pointer is captured, so a hand that runs off the top or bottom of the rail goes on
    /// moving the handle rather than losing it half way through a movement.
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        Focus();

        double x = e.GetPosition(this).X;
        double at = HandleWidth / 2 + RangeValue.Fraction(Value, Minimum, Maximum) * Travel();

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

    /// <summary>
    /// Moves the handle with the hand, or a quarter as fast as it when shift is in play.
    /// </summary>
    /// <remarks>
    /// The fine drag is measured from where the press landed rather than from the last move, so
    /// a hand that goes out and comes back ends on the value it began with. The ordinary drag
    /// keeps the handle under the point that grabbed it instead.
    ///
    /// Shift is read live as well as remembered from the press, so it can be taken up part way
    /// through a movement for the last few units.
    /// </remarks>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_dragging) return;

        double x = e.GetPosition(this).X;

        bool fine = _fineDrag || e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        Value = fine
            ? RangeValue.FromDrag(_dragStartValue, x - _dragStartX, Minimum, Maximum, SmallStep, Travel(), fine: true)
            : ValueAt(x - _grabOffset);

        e.Handled = true;
    }

    /// <summary>Ends the drag and lets the pointer go.</summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_dragging) return;

        _dragging = false;
        _fineDrag = false;

        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>
    /// One notch of the wheel is one step.
    /// </summary>
    /// <remarks>
    /// The base is deliberately not called and the event is marked handled: over one of these
    /// the wheel moves the handle rather than scrolling whatever it sits in, and a panel that
    /// scrolled underneath the hand would take the rail out from under it.
    /// </remarks>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        StepBy(Math.Sign(e.Delta.Y), e.KeyModifiers);

        e.Handled = true;
    }

    /// <summary>
    /// Arrow keys step it, Home takes it to the left hand end and End to the right.
    /// </summary>
    /// <remarks>
    /// Up and right both raise it and down and left both lower it, so the pair somebody reaches
    /// for does not have to match the way the control happens to lie.
    ///
    /// A key this does not answer is left unhandled, so it carries on out to the panel.
    /// </remarks>
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

    /// <summary>One step either way, large if shift is held, landing on the small step's grid.</summary>
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
