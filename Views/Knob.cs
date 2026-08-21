using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.Tracker;
using JingleBox2.UI;
using System;
using System.Globalization;

namespace JingleBox2.Views;

/// <summary>
/// A pot knob: drag up or down to turn it, with the label and the value underneath. Drawn
/// rather than templated, because a dial is one ellipse, one line, and two pieces of text,
/// and every one of them follows the theme's colours.
/// </summary>
/// <remarks>
/// The turning maths lives in <see cref="KnobMath"/>. What is left here is input handling and
/// painting, the same split the pattern grid uses.
/// </remarks>
public class Knob : Control
{
    /// <summary>Gap between the dial and the label, and between the label and the value.</summary>
    private const double TextGap = 2;

    private const double LabelFontSize = 11;
    private const double ValueFontSize = 11.5;

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(Maximum), 1.0);

    /// <summary>The grid the value snaps to, and one press of an arrow key.</summary>
    public static readonly StyledProperty<double> SmallStepProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(SmallStep), 0.01);

    /// <summary>One press with shift held.</summary>
    public static readonly StyledProperty<double> LargeStepProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(LargeStep), 0.1);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<Knob, string>(nameof(Label), "");

    /// <summary>Written straight after the number, as in "5.0Hz".</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<Knob, string>(nameof(Unit), "");

    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<Knob, string>(nameof(Format), "0.00");

    public static readonly StyledProperty<double> DialSizeProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(DialSize), 34.0);

    /// <summary>Where a double click puts the knob back to. Nothing happens when it is not set.</summary>
    public static readonly StyledProperty<double?> DefaultValueProperty =
        AvaloniaProperty.Register<Knob, double?>(nameof(DefaultValue));

    /// <summary>One cursor for every knob: each instance would otherwise hold a platform handle.</summary>
    private static readonly Cursor DragCursor = new(StandardCursorType.SizeNorthSouth);

    private bool _dragging;
    private double _dragStartY;
    private double _dragStartValue;
    private bool _hovered;

    static Knob()
    {
        AffectsRender<Knob>(
            ValueProperty, MinimumProperty, MaximumProperty, LabelProperty,
            UnitProperty, FormatProperty, DialSizeProperty);

        AffectsMeasure<Knob>(LabelProperty, UnitProperty, FormatProperty, DialSizeProperty);
    }

    public Knob()
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

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public string Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    public double DialSize
    {
        get => GetValue(DialSizeProperty);
        set => SetValue(DialSizeProperty, value);
    }

    public double? DefaultValue
    {
        get => GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    public string ValueText => NumericInput.Format(Value, Format) + Unit;

    protected override Size MeasureOverride(Size availableSize)
    {
        var label = BuildText(Label, LabelFontSize, FontFamily.Default, Brushes.Black);
        var value = BuildText(ValueText, ValueFontSize, PatternFont.Family, Brushes.Black);

        double width = Math.Max(DialSize, Math.Max(label.Width, value.Width));
        double height = DialSize + TextGap + label.Height + TextGap + value.Height;

        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);

        double radius = Math.Max(4, DialSize / 2 - 1);
        double centerX = Bounds.Width / 2;
        double centerY = radius + 1;

        DrawDial(context, palette, centerX, centerY, radius);
        DrawText(context, palette, centerY + radius + 1);
    }

    private void DrawDial(DrawingContext context, ThemePalette palette, double centerX, double centerY, double radius)
    {
        var center = new Point(centerX, centerY);

        // The face is lit from above, the way a real pot catches the light.
        var face = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Lighten(palette.Surface, 0.14), 0),
                new GradientStop(palette.Background, 1)
            }
        };

        var rim = IsFocused || _hovered ? palette.Accent : palette.Border;
        context.DrawEllipse(face, new Pen(new SolidColorBrush(rim), IsFocused ? 1.6 : 1), center, radius, radius);

        // The travel the pointer sweeps, so the ends of the range are visible when it is not there.
        DrawTick(context, palette.BorderBrush, center, radius, KnobMath.StartDegrees);
        DrawTick(context, palette.BorderBrush, center, radius, KnobMath.StartDegrees + KnobMath.SweepDegrees);

        double angle = KnobMath.AngleFor(Value, Minimum, Maximum);
        var (innerX, innerY) = KnobMath.PointAt(centerX, centerY, radius * 0.15, angle);
        var (outerX, outerY) = KnobMath.PointAt(centerX, centerY, radius * 0.82, angle);

        var pointer = new Pen(palette.AccentBrush, 2, lineCap: PenLineCap.Round);
        context.DrawLine(pointer, new Point(innerX, innerY), new Point(outerX, outerY));
    }

    /// <summary>A short mark just outside the dial, at one end of the sweep.</summary>
    private static void DrawTick(DrawingContext context, IBrush brush, Point center, double radius, double angleDegrees)
    {
        var (x1, y1) = KnobMath.PointAt(center.X, center.Y, radius + 1, angleDegrees);
        var (x2, y2) = KnobMath.PointAt(center.X, center.Y, radius + 3.5, angleDegrees);

        context.DrawLine(new Pen(brush, 1), new Point(x1, y1), new Point(x2, y2));
    }

    private void DrawText(DrawingContext context, ThemePalette palette, double top)
    {
        var label = BuildText(Label, LabelFontSize, FontFamily.Default, palette.MutedBrush);
        var value = BuildText(ValueText, ValueFontSize, PatternFont.Family, palette.TextBrush);

        double labelY = top + TextGap;
        context.DrawText(label, new Point((Bounds.Width - label.Width) / 2, labelY));
        context.DrawText(value, new Point((Bounds.Width - value.Width) / 2, labelY + label.Height + TextGap));
    }

    private FormattedText BuildText(string? text, double size, FontFamily family, IBrush brush) =>
        new(text ?? "",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(family),
            size,
            brush);

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

        _dragging = true;
        _dragStartY = e.GetPosition(this).Y;
        _dragStartValue = Value;

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_dragging) return;

        // Measured from where the drag started, so going down and back up returns the value
        // it began with instead of drifting.
        double draggedUp = _dragStartY - e.GetPosition(this).Y;

        Value = KnobMath.ValueFromDrag(
            _dragStartValue, draggedUp, Minimum, Maximum, SmallStep,
            e.KeyModifiers.HasFlag(KeyModifiers.Shift));

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_dragging) return;

        _dragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        // Deliberately not calling the base: over a knob the wheel turns it rather than
        // scrolling the panel it sits in.
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
                Value = KnobMath.Quantize(Minimum, Minimum, Maximum, SmallStep);
                break;

            case Key.End:
                Value = KnobMath.Quantize(Maximum, Minimum, Maximum, SmallStep);
                break;

            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>Double click puts a knob back where it started, the way a pot has a detent.</summary>
    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);

        if (DefaultValue is not double reset) return;

        Value = KnobMath.Quantize(reset, Minimum, Maximum, SmallStep);
        e.Handled = true;
    }

    private void StepBy(int direction, KeyModifiers modifiers)
    {
        if (direction == 0) return;

        double step = modifiers.HasFlag(KeyModifiers.Shift) ? LargeStep : SmallStep;
        Value = KnobMath.Quantize(Value + direction * step, Minimum, Maximum, SmallStep);
    }

    private static Color Lighten(Color color, double amount) => Color.FromArgb(
        color.A,
        (byte)Math.Clamp(color.R + 255 * amount, 0, 255),
        (byte)Math.Clamp(color.G + 255 * amount, 0, 255),
        (byte)Math.Clamp(color.B + 255 * amount, 0, 255));
}
