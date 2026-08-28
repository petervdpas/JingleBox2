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

    /// <summary>How far the marks between the thirds reach, shorter so the thirds read as thirds.</summary>
    private const double MinorTickReach = 6.5;

    /// <summary>The name under the dial, small enough that a row of them does not shout.</summary>
    private const double LabelFontSize = 11;

    /// <summary>
    /// The reading, half a point larger than the name.
    /// </summary>
    /// <remarks>
    /// It is drawn in the monospaced face, which sits visually smaller than the proportional one
    /// at the same size, so matching the numbers makes the reading look like the quieter of the
    /// two when it is the one you came to read.
    /// </remarks>
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

    /// <summary>Backs <see cref="Minimum"/>, the value at seven o'clock.</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(Minimum));

    /// <summary>
    /// Backs <see cref="Maximum"/>, the value at five o'clock.
    /// </summary>
    /// <remarks>
    /// One rather than nought, so a knob nobody has given a range to turns over the nought to
    /// one every parameter here already uses, rather than being stuck against a dead range.
    /// </remarks>
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

    /// <summary>Backs <see cref="SmallStep"/>: the grid the value snaps to, and one arrow key.</summary>
    public static readonly StyledProperty<double> SmallStepProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(SmallStep), 0.01);

    /// <summary>Backs <see cref="LargeStep"/>: one arrow key with shift held.</summary>
    public static readonly StyledProperty<double> LargeStepProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(LargeStep), 0.1);

    /// <summary>Backs <see cref="Label"/>, the name printed with the dial.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<Knob, string>(nameof(Label), "");

    /// <summary>Backs <see cref="Unit"/>, written straight after the number, as in "5.0Hz".</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<Knob, string>(nameof(Unit), "");

    /// <summary>
    /// Backs <see cref="Format"/>, the standard numeric format the reading is worded with.
    /// </summary>
    /// <remarks>
    /// Two decimals unless a panel says otherwise, which is right for the nought to one a
    /// parameter defaults to and wrong for a tempo. A machine that means whole numbers says so.
    /// </remarks>
    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<Knob, string>(nameof(Format), "0.00");

    /// <summary>
    /// Shown instead of the number, for a knob whose position is not what it means: a filter
    /// cutoff moves in octaves, so the dial holds 0 to 1 and this says what that is in hertz.
    /// </summary>
    public static readonly StyledProperty<string> DisplayProperty =
        AvaloniaProperty.Register<Knob, string>(nameof(Display), "");

    /// <summary>
    /// Backs <see cref="DialSize"/>, the diameter of the dial itself.
    /// </summary>
    /// <remarks>
    /// The marks reach past this in every direction, so the control is always wider and taller
    /// than the number here. See <see cref="TickReach"/>.
    /// </remarks>
    public static readonly StyledProperty<double> DialSizeProperty =
        AvaloniaProperty.Register<Knob, double>(nameof(DialSize), 34.0);

    /// <summary>
    /// Backs <see cref="DefaultValue"/>: where a double click puts the knob back to, and nothing
    /// happens when it is not set.
    /// </summary>
    public static readonly StyledProperty<double?> DefaultValueProperty =
        AvaloniaProperty.Register<Knob, double?>(nameof(DefaultValue));

    /// <summary>One cursor for every knob: each instance would otherwise hold a platform handle.</summary>
    private static readonly Cursor DragCursor = new(StandardCursorType.SizeNorthSouth);

    /// <summary>Whether a button is down on it, which is the whole of the drag state machine.</summary>
    private bool _dragging;

    /// <summary>
    /// Where the drag began, and what the value was there.
    /// </summary>
    /// <remarks>
    /// The pair is kept rather than the last position, because the movement is measured from the
    /// start of the drag: a hand that goes down and comes back up ends on the value it began
    /// with, where accumulating each move would leave it somewhere else through rounding.
    /// </remarks>
    private double _dragStartY;

    /// <inheritdoc cref="_dragStartY"/>
    private double _dragStartValue;

    /// <summary>Whether the pointer is over it, which lights the rim.</summary>
    private bool _hovered;

    /// <summary>
    /// Says which properties change the drawing and which change the size, and puts the ends
    /// back into the question of what the value may be.
    /// </summary>
    /// <remarks>
    /// The ends decide what the value may be, so a change to either has to put that question to
    /// the value again; a panel builds a control's range and its value in whichever order the
    /// layout happens to take them.
    ///
    /// <see cref="LinkGlow.LitProperty"/> is in here because the glow is painted by this control
    /// itself rather than by a style, so the flag that turns it on has to bring it back round to
    /// paint again.
    ///
    /// <see cref="ValueProperty"/> is deliberately absent from the measure list. A control
    /// measured off its current reading is as wide as the number under it, and a knob would then
    /// change width as it was turned; the width comes from <see cref="NumericInput.Widest"/>
    /// instead, which is the longest thing it could ever say.
    /// </remarks>
    static Knob()
    {
        MinimumProperty.Changed.AddClassHandler<Knob>(EndsMoved);
        MaximumProperty.Changed.AddClassHandler<Knob>(EndsMoved);

        AffectsMeasure<Knob>(LabelAboveProperty, LabelLinesProperty, HeadRoomProperty, TicksProperty);

        AffectsRender<Knob>(LinkGlow.LitProperty);

        AffectsRender<Knob>(
            LabelAboveProperty, LabelLinesProperty, HeadRoomProperty, TicksProperty,
            ValueProperty, MinimumProperty, MaximumProperty, LabelProperty,
            UnitProperty, FormatProperty, DisplayProperty, DialSizeProperty);

        AffectsMeasure<Knob>(LabelProperty, UnitProperty, FormatProperty, DisplayProperty, DialSizeProperty);
    }

    /// <summary>Takes the keyboard, and wears the up-and-down cursor so the drag is discoverable.</summary>
    public Knob()
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

    /// <summary>The value at seven o'clock, where the sweep begins.</summary>
    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>The value at five o'clock, where the sweep ends.</summary>
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

    /// <summary>What the knob is called, printed with the dial.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>What it is measured in, written straight after the number.</summary>
    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>The numeric format the reading is worded with.</summary>
    public string Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    /// <summary>Wording that replaces the number outright, for a dial whose position is not its meaning.</summary>
    public string Display
    {
        get => GetValue(DisplayProperty);
        set => SetValue(DisplayProperty, value);
    }

    /// <summary>How wide across the dial itself is, not counting the ring of marks round it.</summary>
    public double DialSize
    {
        get => GetValue(DialSizeProperty);
        set => SetValue(DialSizeProperty, value);
    }

    /// <summary>Where a double click puts it back to, or nothing when it has no detent.</summary>
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

    /// <inheritdoc cref="LabelAboveProperty"/>
    public bool LabelAbove
    {
        get => GetValue(LabelAboveProperty);
        set => SetValue(LabelAboveProperty, value);
    }

    /// <inheritdoc cref="LabelLinesProperty"/>
    public int LabelLines
    {
        get => GetValue(LabelLinesProperty);
        set => SetValue(LabelLinesProperty, value);
    }

    /// <inheritdoc cref="HeadRoomProperty"/>
    public double HeadRoom
    {
        get => GetValue(HeadRoomProperty);
        set => SetValue(HeadRoomProperty, value);
    }

    /// <inheritdoc cref="TicksProperty"/>
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
        Math.Max(HeadSpace,
                 Math.Max(label.Height, LabelFontSize * 1.35 * Math.Max(1, LabelLines)));

    /// <summary>
    /// What is kept clear above the dial when the name is not up there.
    /// </summary>
    /// <remarks>
    /// Only what <see cref="HeadRoom"/> asks for, which is a knob being pushed down to stand on
    /// the same line as a switch beside it. A knob whose name is underneath it needs nothing
    /// else up there, and reserving the name's height as well is what left a hole under every
    /// one of them: the room was measured at the top, the text was drawn at the bottom, and the
    /// difference came out as empty space below the value.
    /// </remarks>
    private double HeadSpace => HeadRoom > 0 ? Math.Max(0, HeadRoom - TextGap) : 0;

    /// <summary>What is printed under the dial: the wording if there is any, the number otherwise.</summary>
    public string ValueText =>
        string.IsNullOrEmpty(Display) ? NumericInput.Format(Value, Format) + Unit : Display;

    /// <summary>
    /// Room for the dial, the ring of marks all the way round it, the name and the reading.
    /// </summary>
    /// <remarks>
    /// Measured the way it is drawn, and the two are not the same shape. With the name above,
    /// the order down the control is name, dial, reading. With it below, which is every knob
    /// written in XAML rather than described by a machine, it is dial, name, reading, and
    /// nothing at all goes over the dial. Measuring both cases as the first reserved room for a
    /// name at the top that was never used, and the control came out half a line taller than
    /// what it drew, with the slack sitting under the reading: a hole under the mixer's pan knob
    /// and a squeeze on its ducking knobs.
    ///
    /// The tick reach counts across as well as down. The marks are drawn outwards from the rim
    /// in every direction, so a knob measured as its dial alone is measured too narrow, and two
    /// of them side by side end up with their rings almost touching however much spacing the
    /// panel between them asks for.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        _room = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width;

        var label = BuildText(Label, LabelFontSize, FontFamily.Default, Brushes.Black, _room);
        var value = BuildText(ValueText, ValueFontSize, PatternFont.Family, Brushes.Black);

        double width = Math.Max(DialSize + TickReach * 2, Math.Max(label.Width, value.Width));

        double height = LabelAbove
            ? LabelRoom(label) + TextGap + TickReach + DialSize + TickReach + TextGap + value.Height
            : HeadSpace + TickReach + DialSize + TickReach + TextGap + label.Height
              + TextGap + value.Height;

        return new Size(width, height);
    }

    /// <summary>
    /// Paints the name, the dial with its ring, and the reading, in whichever of the two orders
    /// <see cref="LabelAbove"/> asks for.
    /// </summary>
    /// <remarks>
    /// The marks reach past the dial at both ends, so the room for the top ones is left above it
    /// rather than being drawn over whatever happens to be up there. That never showed while
    /// every panel reserved fifty pixels over each dial; the moment that came off, every name on
    /// the machine was sitting on its own tick marks.
    ///
    /// The link glow goes on last, over everything else, because while a controller is being
    /// pointed at something the control being offered has to say so itself and nothing may
    /// paint over that. See <see cref="LinkGlow"/>.
    /// </remarks>
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

        double middle = HeadSpace + TickReach + radius + 1;

        DrawDial(context, palette, centerX, middle, radius);
        DrawText(context, palette, middle + radius + 1 + TickReach);

        if (LinkGlow.GetLit(this)) LinkGlow.Paint(context, new Rect(Bounds.Size));
    }

    /// <summary>
    /// The dial itself: the ring of marks round it, the moulded face, and the pointer.
    /// </summary>
    /// <remarks>
    /// The marks are what lets somebody read roughly where a knob is set from across the room,
    /// which is why the two ends and the middle are drawn longer and heavier than the rest.
    ///
    /// The face is a gradient from light at the top to the page colour at the bottom, which is
    /// what a real pot does under a light above it, and the rim takes the accent colour while
    /// the knob is hovered or holds the keyboard.
    /// </remarks>
    private void DrawDial(DrawingContext context, ThemePalette palette, double centerX, double centerY, double radius)
    {
        var center = new Point(centerX, centerY);

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


    /// <summary>The name and the reading, stacked under the dial and centred on it.</summary>
    private void DrawText(DrawingContext context, ThemePalette palette, double top)
    {
        var label = BuildText(Label, LabelFontSize, FontFamily.Default, palette.MutedBrush, Bounds.Width);
        var value = BuildText(ValueText, ValueFontSize, PatternFont.Family, palette.TextBrush);

        double labelY = top + TextGap;
        context.DrawText(label, new Point((Bounds.Width - label.Width) / 2, labelY));
        context.DrawText(value, new Point((Bounds.Width - value.Width) / 2, labelY + label.Height + TextGap));
    }

    /// <summary>A piece of text laid out with no width limit, for the reading, which never folds.</summary>
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

    /// <summary>Lights the rim, so a knob under the hand is visibly the one that will move.</summary>
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
    /// Starts a drag, remembering where it began and what the value was there.
    /// </summary>
    /// <remarks>
    /// The pointer is captured, so a hand that runs off the edge of the knob goes on turning it
    /// rather than losing it half way through a movement.
    /// </remarks>
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

    /// <summary>
    /// Turns the knob by how far the hand has moved since the press.
    /// </summary>
    /// <remarks>
    /// From where the drag started rather than from the last move, so going down and back up
    /// returns the value it began with instead of drifting. Shift makes the same movement cover
    /// a quarter as much.
    /// </remarks>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_dragging) return;

        double draggedUp = _dragStartY - e.GetPosition(this).Y;

        Value = RangeValue.FromDrag(
            _dragStartValue, draggedUp, Minimum, Maximum, SmallStep,
            KnobMath.DragPixelsForFullRange, e.KeyModifiers.HasFlag(KeyModifiers.Shift));

        e.Handled = true;
    }

    /// <summary>Ends the drag and lets the pointer go.</summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_dragging) return;

        _dragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>
    /// One notch of the wheel is one step.
    /// </summary>
    /// <remarks>
    /// The base is deliberately not called and the event is marked handled: over a knob the
    /// wheel turns the knob rather than scrolling the panel it sits in, and a panel that
    /// scrolled underneath the hand would take the knob out from under it mid-turn.
    /// </remarks>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        StepBy(Math.Sign(e.Delta.Y), e.KeyModifiers);
        e.Handled = true;
    }

    /// <summary>
    /// Arrow keys step it, Home and End take it to its ends.
    /// </summary>
    /// <remarks>
    /// Up and right both raise it and down and left both lower it, because a knob turns and has
    /// no axis of its own: which pair somebody reaches for depends on whether they are thinking
    /// of it as a dial or as a value in a row.
    ///
    /// A key this does not answer is left unhandled, so it carries on out to whatever the panel
    /// wants to do with it.
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

    /// <summary>Double click puts a knob back where it started, the way a pot has a detent.</summary>
    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);

        if (DefaultValue is not double reset) return;

        Value = RangeValue.Quantize(reset, Minimum, Maximum, SmallStep);
        e.Handled = true;
    }

    /// <summary>One step up or down, large if shift is held, landing on the small step's grid.</summary>
    private void StepBy(int direction, KeyModifiers modifiers)
    {
        if (direction == 0) return;

        double step = modifiers.HasFlag(KeyModifiers.Shift) ? LargeStep : SmallStep;
        Value = RangeValue.Quantize(Value + direction * step, Minimum, Maximum, SmallStep);
    }

    /// <summary>A colour taken towards white, keeping its transparency, for the lit top of the face.</summary>
    private static Color Lighten(Color color, double amount) => Color.FromArgb(
        color.A,
        (byte)Math.Clamp(color.R + 255 * amount, 0, 255),
        (byte)Math.Clamp(color.G + 255 * amount, 0, 255),
        (byte)Math.Clamp(color.B + 255 * amount, 0, 255));
}
