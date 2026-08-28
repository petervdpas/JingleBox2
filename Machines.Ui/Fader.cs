using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using JingleBox2.Machines.Ui.Records;
using JingleBox2.Machines.Ui.Interfaces;
using JingleBox2.Machines.Ui;

namespace JingleBox2.Machines.Ui;

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
    /// <summary>The scale marks beside a fader, as they are written in markup.</summary>
    private readonly ITickList _marks = new TickList();

    /// <summary>Stepping, clamping and reading a typed number. Holds nothing, so one is enough.</summary>
    private readonly INumericInput _number = new NumericInput();

    /// <summary>Where this fader's cap sits on its track, and what a height on it means.</summary>
    private readonly IFaderMath _track = new FaderMath();

    /// <summary>Where a value sits in its range, and what a drag does to it. Holds nothing, so one is enough.</summary>
    private readonly IRangeValue _range = new RangeValue();

    /// <summary>
    /// How wide the groove is, and the cap that rides it.
    /// </summary>
    /// <remarks>
    /// Sized against the knobs it stands beside rather than against nothing. A forty pixel dial
    /// with a ring of marks round it is a substantial thing, and a five pixel groove under a
    /// twenty two pixel cap read as a scratch next to one.
    /// </remarks>
    private const double GrooveWidth = 8;

    /// <summary>How wide the cap is across the groove, and how deep it is along it.</summary>
    private const double CapWidth = 28;

    /// <inheritdoc cref="CapWidth"/>
    private const double CapHeight = 14;

    /// <summary>
    /// The air between the name and the groove, and between the groove and the reading.
    /// </summary>
    /// <remarks>
    /// The same a knob leaves under its name and a switch under its title. All three stand in
    /// rows together all over the app, and one of them leaving less read as crammed.
    /// </remarks>
    private const double TextGap = 4;

    /// <summary>Short enough to fit anywhere, long enough to still be a fader.</summary>
    private const double MinimumTrackLength = 40;

    /// <summary>The name above the groove.</summary>
    private const double LabelFontSize = 11;

    /// <summary>
    /// The reading below it, half a point larger.
    /// </summary>
    /// <remarks>
    /// It is drawn in the monospaced face, which sits visually smaller than the proportional one
    /// at the same size.
    /// </remarks>
    private const double ValueFontSize = 11.5;

    /// <summary>The numbers beside the scale, small enough not to compete with the reading.</summary>
    private const double TickFontSize = 9;

    /// <summary>How far the marks and their labels sit from the cap.</summary>
    private const double TickGap = 3;

    /// <summary>How far each mark reaches out from there.</summary>
    private const double TickLength = 5;

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
        AvaloniaProperty.Register<Fader, double>(
            nameof(Value), defaultBindingMode: BindingMode.TwoWay, coerce: Held);

    /// <summary>A value as this control is prepared to hold it: inside its ends, and a number.</summary>
    private static double Held(AvaloniaObject sender, double value)
    {
        if (sender is not Fader control) return value;

        double low = control.Minimum;
        double high = control.Maximum;

        if (double.IsNaN(value)) return low;
        if (high < low) return low;

        return Math.Clamp(value, low, high);
    }

    /// <summary>Backs <see cref="Minimum"/>, the value at the bottom of the throw.</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<Fader, double>(nameof(Minimum));

    /// <summary>
    /// Backs <see cref="Maximum"/>, the value at the top of the throw.
    /// </summary>
    /// <remarks>
    /// One rather than nought, so a fader nobody has given a range to runs over the nought to
    /// one every parameter here already uses rather than being stuck against a dead range.
    /// </remarks>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<Fader, double>(nameof(Maximum), 1.0);

    /// <summary>
    /// The ends moved, so what is being held has to be asked again whether it still fits.
    /// </summary>
    /// <remarks>
    /// A panel hands a control its range and its value in whatever order the layout happens to
    /// build them. Without this, a value set while the ends were still their defaults would
    /// keep whatever it was coerced to then.
    /// </remarks>
    private static void EndsMoved(Fader control, AvaloniaPropertyChangedEventArgs e) =>
        control.CoerceValue(ValueProperty);

    /// <summary>Backs <see cref="SmallStep"/>: the grid the value snaps to, and one arrow key.</summary>
    public static readonly StyledProperty<double> SmallStepProperty =
        AvaloniaProperty.Register<Fader, double>(nameof(SmallStep), 0.01);

    /// <summary>Backs <see cref="LargeStep"/>: one arrow key with shift held.</summary>
    public static readonly StyledProperty<double> LargeStepProperty =
        AvaloniaProperty.Register<Fader, double>(nameof(LargeStep), 0.1);

    /// <summary>Backs <see cref="Label"/>, the name printed above the groove.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<Fader, string>(nameof(Label), "");

    /// <summary>Backs <see cref="Unit"/>, written straight after the number, as in "80ms".</summary>
    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<Fader, string>(nameof(Unit), "");

    /// <summary>
    /// Backs <see cref="Format"/>, the standard numeric format the reading is worded with.
    /// </summary>
    /// <remarks>
    /// Two decimals unless a panel says otherwise, which is right for a nought to one parameter
    /// and wrong for a level in decibels. A machine that means something else says so.
    /// </remarks>
    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<Fader, string>(nameof(Format), "0.00");

    /// <summary>
    /// The throw every fader has unless it is told otherwise.
    /// </summary>
    /// <remarks>
    /// One number for the whole app, so a panel does not end up with a different length of fader
    /// in each of its boxes. Long enough that the hand has somewhere to go: the three machines
    /// this was gathered from used seventy six, eighty six and ninety six, none of which was
    /// chosen, all of which were whatever fitted the box that was being drawn at the time.
    /// </remarks>
    public const double StandardTrackLength = 120;

    /// <summary>
    /// How long the throw is. Longer means finer control for the same range. Zero means take
    /// whatever height the fader is given, for a strip that should fill its panel.
    /// </summary>
    public static readonly StyledProperty<double> TrackLengthProperty =
        AvaloniaProperty.Register<Fader, double>(nameof(TrackLength), StandardTrackLength);

    /// <summary>
    /// The scale beside the groove, as the values to mark: "6,0,-6,-12,-24,-40,-60". Empty for
    /// no scale at all.
    /// </summary>
    public static readonly StyledProperty<string> TicksProperty =
        AvaloniaProperty.Register<Fader, string>(nameof(Ticks), "");

    /// <summary>Whether each mark is written out as well as drawn.</summary>
    public static readonly StyledProperty<bool> ShowTickLabelsProperty =
        AvaloniaProperty.Register<Fader, bool>(nameof(ShowTickLabels), true);

    /// <summary>
    /// Backs <see cref="DefaultValue"/>: where a double click puts the fader back to, and
    /// nothing happens when it is not set.
    /// </summary>
    public static readonly StyledProperty<double?> DefaultValueProperty =
        AvaloniaProperty.Register<Fader, double?>(nameof(DefaultValue));

    /// <summary>One cursor for every fader: each instance would otherwise hold a platform handle.</summary>
    private static readonly Cursor DragCursor = new(StandardCursorType.SizeNorthSouth);

    /// <summary>
    /// The scale, already read out of <see cref="Ticks"/>.
    /// </summary>
    /// <remarks>
    /// Parsed when the written form is set rather than on every frame of a drag: the marks are
    /// wanted twice per paint, once for the drawing and once for the width, and a fader being
    /// dragged paints as fast as the window will let it.
    /// </remarks>
    private double[] _ticks = Array.Empty<double>();

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

    /// <summary>
    /// How far down the cap the hand took hold of it.
    /// </summary>
    /// <remarks>
    /// Kept so the cap stays under the point that grabbed it rather than jumping its middle to
    /// the pointer. Nought when the press landed off the cap, since the cap has already been
    /// sent to the pointer in that case.
    /// </remarks>
    private double _grabOffset;

    /// <summary>Where the drag began, and what the value was there, for the fine drag.</summary>
    private double _dragStartY;

    /// <inheritdoc cref="_dragStartY"/>
    private double _dragStartValue;

    /// <summary>Whether the pointer is over it, which lights the cap's rim.</summary>
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
    /// The ends are in the measure list because they decide how long a reading can be. The value
    /// is deliberately not, because it no longer decides anything about the width: see
    /// <see cref="INumericInput.Widest"/> and <see cref="MeasureOverride"/>.
    /// </remarks>
    static Fader()
    {
        MinimumProperty.Changed.AddClassHandler<Fader>(EndsMoved);
        MaximumProperty.Changed.AddClassHandler<Fader>(EndsMoved);

        AffectsRender<Fader>(LinkGlow.LitProperty);

        AffectsRender<Fader>(
            ValueProperty, MinimumProperty, MaximumProperty, LabelProperty,
            UnitProperty, FormatProperty, TrackLengthProperty, TicksProperty, ShowTickLabelsProperty);

        AffectsMeasure<Fader>(
            LabelProperty, UnitProperty, FormatProperty, TrackLengthProperty,
            TicksProperty, ShowTickLabelsProperty, MinimumProperty, MaximumProperty);
    }

    /// <summary>Takes the keyboard, and wears the up-and-down cursor so the drag is discoverable.</summary>
    public Fader()
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

    /// <summary>The value at the bottom of the throw, which is where a level fader's silence is.</summary>
    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>The value at the top of the throw.</summary>
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

    /// <summary>What the fader is called, printed above the groove.</summary>
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

    /// <inheritdoc cref="TrackLengthProperty"/>
    public double TrackLength
    {
        get => GetValue(TrackLengthProperty);
        set => SetValue(TrackLengthProperty, value);
    }

    /// <summary>Where a double click puts it back to, or nothing when it has no detent.</summary>
    public double? DefaultValue
    {
        get => GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    /// <inheritdoc cref="TicksProperty"/>
    public string Ticks
    {
        get => GetValue(TicksProperty);
        set => SetValue(TicksProperty, value);
    }

    /// <inheritdoc cref="ShowTickLabelsProperty"/>
    public bool ShowTickLabels
    {
        get => GetValue(ShowTickLabelsProperty);
        set => SetValue(ShowTickLabelsProperty, value);
    }

    /// <summary>Reads the scale out of its written form when that is set, and not again.</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TicksProperty) _ticks = _marks.Parse(Ticks);
    }

    /// <summary>The reading printed under the groove: the number and its unit.</summary>
    public string ValueText => _number.Format(Value, Format) + Unit;

    /// <summary>
    /// Room for the name, the throw, the cap, the reading, and the scale beside all of it.
    /// </summary>
    /// <remarks>
    /// The width is taken at the longest the reading can ever be rather than at what it says
    /// now, so the control does not change width as the value moves. A fader measured off its
    /// current reading is as wide as the number under it: on the mixer, "-10.0 dB" is a
    /// character wider than "0.0 dB", so the strips turned down far enough to need it came out
    /// wider inside than the others and pushed their meters into the card's own border. See
    /// <see cref="INumericInput.Widest"/>.
    ///
    /// A stretching fader asks for the shortest throw it can live with rather than for what it
    /// wants, since a panel handing out the space left over gives it the whole area anyway.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        var label = BuildText(Label, LabelFontSize, FontFamily.Default, Brushes.Black);
        var value = BuildText(ValueText, ValueFontSize, PatternFont.Family, Brushes.Black);

        double throwLength = TrackLength > 0 ? TrackLength : MinimumTrackLength;

        var widest = BuildText(
            _number.Widest(Value, Minimum, Maximum, Format, Unit),
            ValueFontSize, PatternFont.Family, Brushes.Black);

        double width = Math.Max(CapWidth, Math.Max(label.Width, widest.Width)) + ScaleWidth();
        double height = label.Height + TextGap + throwLength + CapHeight + TextGap + value.Height;

        return new Size(width, height);
    }

    /// <summary>
    /// Paints the name, the scale, the groove with its cap, and the reading.
    /// </summary>
    /// <remarks>
    /// The link glow goes on last, over everything else, because while a controller is being
    /// pointed at something the control being offered has to say so itself and nothing may paint
    /// over that. See <see cref="LinkGlow"/>.
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);

        var label = BuildText(Label, LabelFontSize, FontFamily.Default, palette.MutedBrush);
        var value = BuildText(ValueText, ValueFontSize, PatternFont.Family, palette.TextBrush);

        context.DrawText(label, new Point((Bounds.Width - label.Width) / 2, 0));

        var (trackTop, trackLength) = Track();
        DrawScale(context, palette, trackTop, trackLength);
        DrawTrack(context, palette, trackTop, trackLength);

        context.DrawText(value, new Point(
            (Bounds.Width - value.Width) / 2,
            trackTop + trackLength + CapHeight / 2 + TextGap));

        if (LinkGlow.GetLit(this)) LinkGlow.Paint(context, new Rect(Bounds.Size));
    }

    /// <summary>
    /// The groove, the part of it the cap has travelled past, and the cap itself.
    /// </summary>
    /// <remarks>
    /// Both parts of the groove are drawn with rounded ends, so where the travelled part meets
    /// the rest the ends do not look cut. The cap's face is a gradient from light at the top to
    /// the page colour at the bottom, which is what a real cap does under a light above it, and
    /// its rim takes the accent colour while the fader is hovered or holds the keyboard.
    ///
    /// The grip line across the middle of the cap is what the eye actually reads the value off,
    /// which is why it is drawn in the accent rather than in the rim's colour.
    /// </remarks>
    private void DrawTrack(DrawingContext context, ThemePalette palette, double trackTop, double trackLength)
    {
        double centerX = TrackCenterX();
        double capY = _track.CapCenterY(Value, trackTop, trackLength, Minimum, Maximum);

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

        context.DrawLine(
            new Pen(palette.AccentBrush, 1.5),
            new Point(cap.X + 4, capY),
            new Point(cap.Right - 4, capY));
    }

    /// <summary>Where the cap sits for a given middle, which the press test uses as well as the paint.</summary>
    private Rect CapRect(double capY) =>
        new(TrackCenterX() - CapWidth / 2, capY - CapHeight / 2, CapWidth, CapHeight);

    /// <summary>
    /// The groove keeps to the left of the scale, so adding marks moves the numbers in rather
    /// than sliding the fader out from under the pointer.
    /// </summary>
    private double TrackCenterX() => (Bounds.Width - ScaleWidth()) / 2;

    /// <summary>How much room the scale takes beside the groove, including its labels.</summary>
    private double ScaleWidth()
    {
        if (_ticks.Length == 0) return 0;

        double width = TickGap + TickLength;
        if (!ShowTickLabels) return width;

        double widest = 0;
        foreach (double mark in _ticks)
            widest = Math.Max(widest, BuildText(TickText(mark), TickFontSize, FontFamily.Default, Brushes.Black).Width);

        return width + 2 + widest;
    }

    /// <summary>
    /// The scale beside the groove. Unity is drawn in the accent: on a level fader that is the
    /// mark you aim for, and it should be findable without reading the numbers.
    /// </summary>
    /// <remarks>
    /// Which numbers there is room for is decided before any of them is drawn, since the answer
    /// for one mark depends on every other. See <see cref="Room"/>.
    /// </remarks>
    private void DrawScale(DrawingContext context, ThemePalette palette, double trackTop, double trackLength)
    {
        if (_ticks.Length == 0) return;

        double x = TrackCenterX() + CapWidth / 2 + TickGap;

        var written = ShowTickLabels ? Room(trackTop, trackLength) : null;

        foreach (double mark in _ticks)
        {
            if (mark < Minimum || mark > Maximum) continue;

            double y = _track.CapCenterY(mark, trackTop, trackLength, Minimum, Maximum);
            bool unity = Math.Abs(mark) < 0.0001;

            context.DrawLine(
                new Pen(unity ? palette.AccentBrush : palette.BorderBrush, 1),
                new Point(x, y),
                new Point(x + TickLength, y));

            if (!ShowTickLabels) continue;

            var text = BuildText(TickText(mark), TickFontSize, FontFamily.Default,
                unity ? palette.TextBrush : palette.MutedBrush);

            if (written?.Contains(mark) == false) continue;

            context.DrawText(text, new Point(x + TickLength + 2, y - text.Height / 2));
        }
    }

    /// <summary>
    /// Which of the marks there is room to print a number beside.
    /// </summary>
    /// <remarks>
    /// A scale in decibels crowds at the top, where six and nought are a tenth of the throw
    /// apart: printed anyway they sit on each other and the whole scale reads as crooked.
    ///
    /// Unity goes down first and keeps its place whatever else wants it. On a level fader that
    /// is the one you aim for, and dropping its number to make room for the number at the very
    /// end of the travel would be losing the one that is read for the one that is not.
    /// </remarks>
    private HashSet<double> Room(double trackTop, double trackLength)
    {
        var taken = new List<(double Top, double Bottom)>();
        var kept = new HashSet<double>();

        bool Fits(double mark)
        {
            double y = _track.CapCenterY(mark, trackTop, trackLength, Minimum, Maximum);
            double half = BuildText(TickText(mark), TickFontSize, FontFamily.Default, Brushes.Black).Height / 2;

            foreach (var (top, bottom) in taken)
                if (y + half > top && y - half < bottom) return false;

            taken.Add((y - half, y + half));

            return true;
        }

        foreach (double mark in _ticks)
            if (mark >= Minimum && mark <= Maximum && Math.Abs(mark) < 0.0001 && Fits(mark))
                kept.Add(mark);

        foreach (double mark in _ticks)
            if (mark >= Minimum && mark <= Maximum && Math.Abs(mark) >= 0.0001 && Fits(mark))
                kept.Add(mark);

        return kept;
    }

    /// <summary>
    /// How a mark is written: whole numbers plain, one decimal where there is one.
    /// </summary>
    /// <remarks>
    /// Invariant culture, so a scale reads the same on somebody else's machine and the widths
    /// worked out here match what is drawn.
    /// </remarks>
    private static string TickText(double mark) =>
        mark.ToString("0.#", CultureInfo.InvariantCulture);

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

    /// <summary>A piece of text laid out for measuring or for drawing, with no width limit.</summary>
    private FormattedText BuildText(string? text, double size, FontFamily family, IBrush brush) =>
        new(text ?? "",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(family),
            size,
            brush);

    /// <summary>Lights the cap's rim, so a fader under the hand is visibly the one that will move.</summary>
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
    /// Starts a drag. On the cap it is picked up where it is; anywhere else on the track sends
    /// it there first, which is what a mixer fader does.
    /// </summary>
    /// <remarks>
    /// The pointer is captured, so a hand that runs off the side of the fader goes on moving it
    /// rather than losing it half way through a movement.
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        Focus();

        double y = e.GetPosition(this).Y;
        var (trackTop, trackLength) = Track();
        double capY = _track.CapCenterY(Value, trackTop, trackLength, Minimum, Maximum);

        if (CapRect(capY).Contains(new Point(TrackCenterX(), y)))
        {
            _grabOffset = y - capY;
        }
        else
        {
            _grabOffset = 0;
            Value = _track.ValueAt(y, trackTop, trackLength, Minimum, Maximum, SmallStep);
        }

        _dragging = true;
        _fineDrag = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _dragStartY = y;
        _dragStartValue = Value;

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <summary>
    /// Moves the cap with the hand, or a quarter as fast as it when shift is in play.
    /// </summary>
    /// <remarks>
    /// The fine drag is measured from where the press landed rather than from the last move, so
    /// a hand that goes down and comes back up ends on the value it began with. The ordinary
    /// drag keeps the cap under the point that grabbed it instead, which is what makes it feel
    /// like a cap rather than like a value being nudged.
    ///
    /// Shift is read live as well as remembered from the press, so it can be taken up part way
    /// through a movement for the last few units.
    /// </remarks>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_dragging) return;

        double y = e.GetPosition(this).Y;
        var (trackTop, trackLength) = Track();

        bool fine = _fineDrag || e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        Value = fine
            ? _range.FromDrag(_dragStartValue, _dragStartY - y, Minimum, Maximum, SmallStep, trackLength, fine: true)
            : _track.ValueAt(y - _grabOffset, trackTop, trackLength, Minimum, Maximum, SmallStep);

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
    /// The base is deliberately not called and the event is marked handled: over a fader the
    /// wheel moves the fader rather than scrolling the panel it sits in, and a panel that
    /// scrolled underneath the hand would take the fader out from under it mid-movement.
    /// </remarks>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        StepBy(Math.Sign(e.Delta.Y), e.KeyModifiers);
        e.Handled = true;
    }

    /// <summary>
    /// Arrow keys step it, Home takes it to the top and End to the bottom.
    /// </summary>
    /// <remarks>
    /// Home is the maximum here and the minimum on a knob, because the two ends are up and down
    /// on a fader and the beginning and end of a sweep on a dial: Home is the top of a fader's
    /// travel in the same way it is the top of a page.
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
                Value = _range.Quantize(Maximum, Minimum, Maximum, SmallStep);
                break;

            case Key.End:
                Value = _range.Quantize(Minimum, Minimum, Maximum, SmallStep);
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

        Value = _range.Quantize(reset, Minimum, Maximum, SmallStep);
        e.Handled = true;
    }

    /// <summary>One step up or down, large if shift is held, landing on the small step's grid.</summary>
    private void StepBy(int direction, KeyModifiers modifiers)
    {
        if (direction == 0) return;

        double step = modifiers.HasFlag(KeyModifiers.Shift) ? LargeStep : SmallStep;
        Value = _range.Quantize(Value + direction * step, Minimum, Maximum, SmallStep);
    }

    /// <summary>A colour taken towards white, keeping its transparency, for the lit top of the cap.</summary>
    private static Color Lighten(Color color, double amount) => Color.FromArgb(
        color.A,
        (byte)Math.Clamp(color.R + 255 * amount, 0, 255),
        (byte)Math.Clamp(color.G + 255 * amount, 0, 255),
        (byte)Math.Clamp(color.B + 255 * amount, 0, 255));
}
