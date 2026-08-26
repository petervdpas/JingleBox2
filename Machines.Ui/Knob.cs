using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.UI;
using System;
using System.Globalization;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// A pot knob: drag up or down to turn it, with the label and the value underneath. Drawn
/// rather than templated, because a dial is one ellipse, one line, and two pieces of text,
/// and every one of them follows the theme's colours.
/// </summary>
/// <remarks>
/// The turning maths lives in <see cref="KnobMath"/>. What is left here is input handling and
/// painting, the same split the pattern grid uses.
/// </remarks>
public class Knob : ThemedControl
{
    /// <summary>Gap between the dial and the label, and between the label and the value.</summary>
    /// <summary>
    /// The air between a knob's name and its dial, and between its dial and its value.
    /// </summary>
    /// <remarks>
    /// The same a fader leaves under its name and a switch under its title. These three stand
    /// beside each other in a row all over the app, and a knob that left half as much read as
    /// crammed against everything around it.
    /// </remarks>
    private const double TextGap = 4;

    /// <summary>How far a tick reaches past the dial's edge, the long ones and the short ones.</summary>
    /// <remarks>
    /// Written out here rather than at the one place they are drawn, because the layout has to
    /// leave room for them before anything is drawn at all.
    /// </remarks>
    private const double MajorTickReach = 8.5;

    private const double MinorTickReach = 6.5;

    private const double LabelFontSize = 11;
    private const double ValueFontSize = 11.5;

    /// <summary>
    /// Where it is set to, which is never outside its own ends.
    /// </summary>
    /// <remarks>
    /// Held in range by the property itself rather than by whoever writes it. A control drawn
    /// on a front panel is the last thing standing between a number and somebody's eyes, and it
    /// has to be able to say what it is showing without asking anything else whether that is
    /// allowed. Only the drawing used to clamp, so a value from outside the ends was kept whole,
    /// drawn at the nearest end, and handed straight back to whatever the control was writing
    /// to: the picture said one thing and the machine held another.
    ///
    /// Nothing sets a knob to a number outside its range on purpose. Things do it by accident,
    /// through arithmetic that went wrong somewhere else, and this is where that stops rather
    /// than where it spreads.
    /// </remarks>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Knob, double>(
            nameof(Value), defaultBindingMode: BindingMode.TwoWay, coerce: Held);

    /// <summary>A value as this control is prepared to hold it: inside its ends, and a number.</summary>
    private static double Held(AvaloniaObject sender, double value)
    {
        if (sender is not Knob control) return value;

        double low = control.Minimum;
        double high = control.Maximum;

        if (double.IsNaN(value)) return low;
        if (high < low) return low;

        return Math.Clamp(value, low, high);
    }

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(Maximum), 1.0);

    /// <summary>
    /// The ends moved, so what is being held has to be asked again whether it still fits.
    /// </summary>
    /// <remarks>
    /// A panel hands a control its range and its value in whatever order the layout happens to
    /// build them. Without this, a value set while the ends were still their defaults would
    /// keep whatever it was coerced to then.
    /// </remarks>
    private static void EndsMoved(Knob control, AvaloniaPropertyChangedEventArgs e) =>
        control.CoerceValue(ValueProperty);

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

    /// <summary>
    /// Shown instead of the number, for a knob whose position is not what it means: a filter
    /// cutoff moves in octaves, so the dial holds 0 to 1 and this says what that is in hertz.
    /// </summary>
    public static readonly StyledProperty<string> DisplayProperty =
        AvaloniaProperty.Register<Knob, string>(nameof(Display), "");

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
        // The ends decide what the value may be, so a change to either has to put the question
        // to it again.
        MinimumProperty.Changed.AddClassHandler<Knob>(EndsMoved);
        MaximumProperty.Changed.AddClassHandler<Knob>(EndsMoved);

        AffectsMeasure<Knob>(LabelAboveProperty, LabelLinesProperty, HeadRoomProperty, TicksProperty);

        AffectsRender<Knob>(
            LabelAboveProperty, LabelLinesProperty, HeadRoomProperty, TicksProperty,
            ValueProperty, MinimumProperty, MaximumProperty, LabelProperty,
            UnitProperty, FormatProperty, DisplayProperty, DialSizeProperty);

        AffectsMeasure<Knob>(LabelProperty, UnitProperty, FormatProperty, DisplayProperty, DialSizeProperty);
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

    public string Display
    {
        get => GetValue(DisplayProperty);
        set => SetValue(DisplayProperty, value);
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

    /// <summary>
    /// Puts the name above the dial rather than under it, with the reading still below.
    /// </summary>
    /// <remarks>
    /// Which way round the name goes is what makes a row of these read as a panel or as a form.
    /// A machine prints the name above the control it belongs to, so the eye runs along the
    /// names and drops to whichever one it wants. Off by default: everywhere else in the
    /// application the name sits under its dial, and this is not a reason to move all of them.
    /// </remarks>
    public static readonly StyledProperty<bool> LabelAboveProperty =
        AvaloniaProperty.Register<Knob, bool>(nameof(LabelAbove));

    /// <summary>
    /// How many lines of room the name gets, used or not.
    /// </summary>
    /// <remarks>
    /// A row of controls whose names are different lengths is a row whose dials sit at
    /// different heights, because a name that folds onto two lines pushes its dial down and a
    /// short one does not. Reserving the same room for every name in a row puts them all back
    /// on one line. Two is enough for the names a panel uses.
    /// </remarks>
    public static readonly StyledProperty<int> LabelLinesProperty =
        AvaloniaProperty.Register<Knob, int>(nameof(LabelLines), 1);

    /// <summary>
    /// How far down the dial starts, so it stands on the same line as the switches beside it.
    /// Zero lets it follow its own name.
    /// </summary>
    /// <remarks>
    /// A switch carries a word above its handle and a knob does not, so a row of both sits at
    /// two heights unless something says otherwise. The switch cannot come up, since its word
    /// has to go somewhere, so the knob goes down to meet it. That line is a panel's scribe
    /// line and everything in the row stands on it.
    /// </remarks>
    public static readonly StyledProperty<double> HeadRoomProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(HeadRoom));

    /// <summary>
    /// How many marks are printed round the dial. None unless a panel asks for them.
    /// </summary>
    /// <remarks>
    /// The ring of little lines a machine prints around a knob, which is what lets you read
    /// roughly where it is set from across the room. Every knob in the app has one, so it is the
    /// default and no panel has to ask: a page where some dials carry a scale and some do not
    /// reads as two pages, and there was no telling which kind you were looking at without
    /// counting the marks.
    ///
    /// Eleven, so there is a mark at each end and one in the middle with four between.
    ///
    /// A machine can still say fewer, or none, and none means none: the ring is the only thing
    /// that says where a knob's travel ends, so a knob without it says nothing until it is
    /// pointed at.
    /// </remarks>
    public static readonly StyledProperty<int> TicksProperty =
        AvaloniaProperty.Register<Knob, int>(nameof(Ticks), 11);

    public bool LabelAbove
    {
        get => GetValue(LabelAboveProperty);
        set => SetValue(LabelAboveProperty, value);
    }

    public int LabelLines
    {
        get => GetValue(LabelLinesProperty);
        set => SetValue(LabelLinesProperty, value);
    }

    public double HeadRoom
    {
        get => GetValue(HeadRoomProperty);
        set => SetValue(HeadRoomProperty, value);
    }

    public int Ticks
    {
        get => GetValue(TicksProperty);
        set => SetValue(TicksProperty, value);
    }

    /// <summary>
    /// How far the ring of marks reaches past the dial, or nothing where a knob has none.
    /// </summary>
    /// <remarks>
    /// Room the layout has to leave, not decoration on top of it. The marks are drawn from the
    /// dial's edge outwards, so a knob measured as its dial alone is measured too small at both
    /// ends: the top marks climb into the name above and the bottom ones into the value below.
    /// It never showed while every panel reserved fifty pixels over each dial, and the moment
    /// that came off, every name on the machine was sitting on its own tick marks.
    /// </remarks>
    private double TickReach => Ticks > 1 ? MajorTickReach + 1 : 0;

    /// <summary>The room the name is given, however much of it the name actually uses.</summary>
    private double LabelRoom(FormattedText label) =>
        Math.Max(HeadRoom > 0 ? HeadRoom - TextGap : 0,
                 Math.Max(label.Height, LabelFontSize * 1.35 * Math.Max(1, LabelLines)));

    public string ValueText =>
        string.IsNullOrEmpty(Display) ? NumericInput.Format(Value, Format) + Unit : Display;

    protected override Size MeasureOverride(Size availableSize)
    {
        _room = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width;

        var label = BuildText(Label, LabelFontSize, FontFamily.Default, Brushes.Black, _room);
        var value = BuildText(ValueText, ValueFontSize, PatternFont.Family, Brushes.Black);

        double width = Math.Max(DialSize, Math.Max(label.Width, value.Width));
        double height = LabelRoom(label) + TextGap + TickReach + DialSize + TickReach + TextGap + value.Height;

        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);

        double radius = Math.Max(4, DialSize / 2 - 1);
        double centerX = Bounds.Width / 2;

        var label = BuildText(Label, LabelFontSize, FontFamily.Default, palette.MutedBrush, Bounds.Width);
        var value = BuildText(ValueText, ValueFontSize, PatternFont.Family, palette.TextBrush);

        if (LabelAbove)
        {
            context.DrawText(label, new Point((Bounds.Width - label.Width) / 2, 0));

            double centerY = LabelRoom(label) + TextGap + TickReach + radius + 1;

            DrawDial(context, palette, centerX, centerY, radius);

            context.DrawText(
                value,
                new Point((Bounds.Width - value.Width) / 2, centerY + radius + 1 + TickReach + TextGap));

            return;
        }

        double middle = radius + 1;

        DrawDial(context, palette, centerX, middle, radius);
        DrawText(context, palette, middle + radius + 1);
    }

    private void DrawDial(DrawingContext context, ThemePalette palette, double centerX, double centerY, double radius)
    {
        var center = new Point(centerX, centerY);

        // The marks printed round the dial, so where it is set can be read at a glance.
        if (Ticks > 1)
        {
            var ink = new SolidColorBrush(Lighten(palette.Muted, 0.1), 0.75);

            for (int mark = 0; mark < Ticks; mark++)
            {
                double at = KnobMath.StartDegrees + KnobMath.SweepDegrees * mark / (Ticks - 1.0);
                bool major = mark == 0 || mark == Ticks - 1 || mark * 2 == Ticks - 1;

                var (ax, ay) = KnobMath.PointAt(centerX, centerY, radius + 3, at);
                var (bx, by) = KnobMath.PointAt(
                    centerX, centerY, radius + (major ? MajorTickReach : MinorTickReach), at);

                context.DrawLine(new Pen(ink, major ? 1.6 : 1), new Point(ax, ay), new Point(bx, by));
            }
        }

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



        double angle = KnobMath.AngleFor(Value, Minimum, Maximum);
        var (innerX, innerY) = KnobMath.PointAt(centerX, centerY, radius * 0.15, angle);
        var (outerX, outerY) = KnobMath.PointAt(centerX, centerY, radius * 0.82, angle);

        var pointer = new Pen(palette.AccentBrush, 2, lineCap: PenLineCap.Round);
        context.DrawLine(pointer, new Point(innerX, innerY), new Point(outerX, outerY));
    }


    private void DrawText(DrawingContext context, ThemePalette palette, double top)
    {
        var label = BuildText(Label, LabelFontSize, FontFamily.Default, palette.MutedBrush, Bounds.Width);
        var value = BuildText(ValueText, ValueFontSize, PatternFont.Family, palette.TextBrush);

        double labelY = top + TextGap;
        context.DrawText(label, new Point((Bounds.Width - label.Width) / 2, labelY));
        context.DrawText(value, new Point((Bounds.Width - value.Width) / 2, labelY + label.Height + TextGap));
    }

    private FormattedText BuildText(string? text, double size, FontFamily family, IBrush brush) =>
        BuildText(text, size, family, brush, double.PositiveInfinity);

    /// <summary>
    /// The same, folded to a width so a long name sits over its own dial instead of over its
    /// neighbour's.
    /// </summary>
    /// <remarks>
    /// A panel prints a long name on two short lines rather than one long one, because the
    /// control it belongs to is only so wide and the one beside it needs its own room. Given a
    /// width to work in, this does the same.
    /// </remarks>
    private FormattedText BuildText(string? text, double size, FontFamily family, IBrush brush, double maxWidth)
    {
        var built = new FormattedText(
            text ?? "",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(family),
            size,
            brush);

        if (!double.IsInfinity(maxWidth) && maxWidth > 1) built.MaxTextWidth = maxWidth;

        return built;
    }

    /// <summary>How wide the name may be: what the layout offered, or the dial if it offered nothing.</summary>
    private double _room = double.PositiveInfinity;

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

        Value = RangeValue.FromDrag(
            _dragStartValue, draggedUp, Minimum, Maximum, SmallStep,
            KnobMath.DragPixelsForFullRange, e.KeyModifiers.HasFlag(KeyModifiers.Shift));

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

    /// <summary>Double click puts a knob back where it started, the way a pot has a detent.</summary>
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
