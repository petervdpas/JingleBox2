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
/// A vertical fader, with its label above and its value below. The same shape as
/// <see cref="Knob"/>, for the settings that read better as a throw than as a dial.
/// </summary>
/// <remarks>
/// Pressing the cap picks it up where it is; pressing anywhere else on the track sends it
/// there first, which is what a mixer fader does. Holding shift switches to a fine drag
/// measured from the press, for the last few units.
/// </remarks>
public class Fader : ThemedControl
{
    private const double GrooveWidth = 5;
    private const double CapWidth = 22;
    private const double CapHeight = 11;
    private const double TextGap = 4;

    /// <summary>Short enough to fit anywhere, long enough to still be a fader.</summary>
    private const double MinimumTrackLength = 40;

    private const double LabelFontSize = 11;
    private const double ValueFontSize = 11.5;

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Fader, double>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<Fader, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<Fader, double>(nameof(Maximum), 1.0);

    /// <summary>The grid the value snaps to, and one press of an arrow key.</summary>
    public static readonly StyledProperty<double> SmallStepProperty =
        AvaloniaProperty.Register<Fader, double>(nameof(SmallStep), 0.01);

    /// <summary>One press with shift held.</summary>
    public static readonly StyledProperty<double> LargeStepProperty =
        AvaloniaProperty.Register<Fader, double>(nameof(LargeStep), 0.1);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<Fader, string>(nameof(Label), "");

    /// <summary>Written straight after the number, as in "80ms".</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<Fader, string>(nameof(Unit), "");

    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<Fader, string>(nameof(Format), "0.00");

    /// <summary>
    /// How long the throw is. Longer means finer control for the same range. Zero means take
    /// whatever height the fader is given, for a strip that should fill its panel.
    /// </summary>
    public static readonly StyledProperty<double> TrackLengthProperty =
        AvaloniaProperty.Register<Fader, double>(nameof(TrackLength), 110.0);

    /// <summary>Where a double click puts the fader back to. Nothing happens when it is not set.</summary>
    public static readonly StyledProperty<double?> DefaultValueProperty =
        AvaloniaProperty.Register<Fader, double?>(nameof(DefaultValue));

    private static readonly Cursor DragCursor = new(StandardCursorType.SizeNorthSouth);

    private bool _dragging;
    private bool _fineDrag;
    private double _grabOffset;
    private double _dragStartY;
    private double _dragStartValue;
    private bool _hovered;

    static Fader()
    {
        AffectsRender<Fader>(
            ValueProperty, MinimumProperty, MaximumProperty, LabelProperty,
            UnitProperty, FormatProperty, TrackLengthProperty);

        AffectsMeasure<Fader>(LabelProperty, UnitProperty, FormatProperty, TrackLengthProperty);
    }

    public Fader()
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

    public double TrackLength
    {
        get => GetValue(TrackLengthProperty);
        set => SetValue(TrackLengthProperty, value);
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

        // Asking for the minimum when stretching: a panel that hands out the space left over
        // gives it the whole area anyway, whatever was asked for.
        double throwLength = TrackLength > 0 ? TrackLength : MinimumTrackLength;

        double width = Math.Max(CapWidth, Math.Max(label.Width, value.Width));
        double height = label.Height + TextGap + throwLength + CapHeight + TextGap + value.Height;

        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);

        var label = BuildText(Label, LabelFontSize, FontFamily.Default, palette.MutedBrush);
        var value = BuildText(ValueText, ValueFontSize, PatternFont.Family, palette.TextBrush);

        context.DrawText(label, new Point((Bounds.Width - label.Width) / 2, 0));

        var (trackTop, trackLength) = Track();
        DrawTrack(context, palette, trackTop, trackLength);

        context.DrawText(value, new Point(
            (Bounds.Width - value.Width) / 2,
            trackTop + trackLength + CapHeight / 2 + TextGap));
    }

    private void DrawTrack(DrawingContext context, ThemePalette palette, double trackTop, double trackLength)
    {
        double centerX = Bounds.Width / 2;
        double capY = FaderMath.CapCenterY(Value, trackTop, trackLength, Minimum, Maximum);

        // The groove, then the travelled part of it. Both rounded, so the ends do not look cut.
        var groove = new Rect(centerX - GrooveWidth / 2, trackTop, GrooveWidth, trackLength);
        context.DrawRectangle(palette.BorderBrush, null, new RoundedRect(groove, GrooveWidth / 2));

        if (capY < trackTop + trackLength)
        {
            var travelled = new Rect(groove.X, capY, GrooveWidth, trackTop + trackLength - capY);
            context.DrawRectangle(palette.AccentTint(190), null, new RoundedRect(travelled, GrooveWidth / 2));
        }

        var cap = CapRect(capY);
        var face = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Lighten(palette.Surface, 0.16), 0),
                new GradientStop(palette.Background, 1)
            }
        };

        var rim = IsFocused || _hovered ? palette.Accent : palette.Border;
        context.DrawRectangle(face, new Pen(new SolidColorBrush(rim), IsFocused ? 1.6 : 1),
            new RoundedRect(cap, 3));

        // The grip line across the middle of the cap, which is what the eye reads the value off.
        context.DrawLine(
            new Pen(palette.AccentBrush, 1.5),
            new Point(cap.X + 4, capY),
            new Point(cap.Right - 4, capY));
    }

    private Rect CapRect(double capY) =>
        new(Bounds.Width / 2 - CapWidth / 2, capY - CapHeight / 2, CapWidth, CapHeight);

    /// <summary>
    /// Where the throw starts and how long it is. The label sits above it and the value below,
    /// so a stretching fader gives the groove whatever is left between them.
    /// </summary>
    private (double Top, double Length) Track()
    {
        var label = BuildText(Label, LabelFontSize, FontFamily.Default, Brushes.Black);
        var value = BuildText(ValueText, ValueFontSize, PatternFont.Family, Brushes.Black);

        double top = label.Height + TextGap + CapHeight / 2;

        double length = TrackLength > 0
            ? TrackLength
            : Math.Max(MinimumTrackLength, Bounds.Height - top - CapHeight / 2 - TextGap - value.Height);

        return (top, length);
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

        double y = e.GetPosition(this).Y;
        var (trackTop, trackLength) = Track();
        double capY = FaderMath.CapCenterY(Value, trackTop, trackLength, Minimum, Maximum);

        // On the cap: pick it up where it is. Anywhere else: send it there first.
        if (CapRect(capY).Contains(new Point(Bounds.Width / 2, y)))
        {
            _grabOffset = y - capY;
        }
        else
        {
            _grabOffset = 0;
            Value = FaderMath.ValueAt(y, trackTop, trackLength, Minimum, Maximum, SmallStep);
        }

        _dragging = true;
        _fineDrag = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _dragStartY = y;
        _dragStartValue = Value;

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_dragging) return;

        double y = e.GetPosition(this).Y;
        var (trackTop, trackLength) = Track();

        // Shift trades the cap following the pointer for a quarter-speed drag from the press.
        bool fine = _fineDrag || e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        Value = fine
            ? RangeValue.FromDrag(_dragStartValue, _dragStartY - y, Minimum, Maximum, SmallStep, trackLength, fine: true)
            : FaderMath.ValueAt(y - _grabOffset, trackTop, trackLength, Minimum, Maximum, SmallStep);

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
        // Deliberately not calling the base: over a fader the wheel moves it rather than
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
                Value = RangeValue.Quantize(Maximum, Minimum, Maximum, SmallStep);
                break;

            case Key.End:
                Value = RangeValue.Quantize(Minimum, Minimum, Maximum, SmallStep);
                break;

            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>Double click puts a fader back where it started.</summary>
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

    private static Color Lighten(Color color, double amount) => Color.FromArgb(
        color.A,
        (byte)Math.Clamp(color.R + 255 * amount, 0, 255),
        (byte)Math.Clamp(color.G + 255 * amount, 0, 255),
        (byte)Math.Clamp(color.B + 255 * amount, 0, 255));
}
