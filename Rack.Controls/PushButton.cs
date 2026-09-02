using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Globalization;
using System.Windows.Input;
using JingleBox2.Rack.Controls.Enums;
using JingleBox2.Rack.Controls.Records;

namespace JingleBox2.Rack.Controls;

/// <summary>
/// A small panel button: a moulded cap you press, with what it does written under it and a
/// lamp above it when it has something to say.
/// </summary>
/// <remarks>
/// The other thing a front panel is made of. A knob is for a value and a switch is for a
/// choice; this is for a thing that happens, or for one of a row of positions where a switch
/// would need too many. A machine's step buttons, its transport and its octave row are all
/// this control, which is why it is worth having rather than styling a button each time.
///
/// Momentary by default: pressed, it does the thing and comes back up. Latching, it stays down
/// until pressed again, and the lamp follows it unless something else is driving the lamp.
/// </remarks>
public class PushButton : ThemedControl
{
    /// <summary>The air between the lamp and the cap, and between the cap and the writing under it.</summary>
    private const double LampGap = 2;

    /// <inheritdoc cref="LampGap"/>
    private const double LabelGap = 2;

    /// <summary>
    /// How far the triangle is drawn inside the cap it fills.
    /// </summary>
    /// <remarks>
    /// A geometry drawn on the cap's own edge has half its outline clipped away by the control's
    /// bounds, which reads as a triangle with two thin sides and one thick one.
    /// </remarks>
    private const double TriangleInset = 1;

    /// <summary>
    /// How bright a cap has to be before what is written on it turns from white to black.
    /// </summary>
    /// <remarks>
    /// Weighted for how the eye reads the three channels, not a plain average, or a saturated
    /// green cap would come out darker than a blue one of the same brightness and take the wrong
    /// ink. Out of 255.
    /// </remarks>
    private const double CapTextFlipsAt = 140;

    /// <summary>Backs <see cref="Label"/>, what is written under the cap.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<PushButton, string?>(nameof(Label));

    /// <summary>
    /// Backs <see cref="CapText"/>: what is written on the cap itself, when that reads better
    /// than under it.
    /// </summary>
    public static readonly StyledProperty<string?> CapTextProperty =
        AvaloniaProperty.Register<PushButton, string?>(nameof(CapText));

    /// <summary>
    /// Backs <see cref="IsLatching"/>: the cap stays down when pressed rather than coming back
    /// up.
    /// </summary>
    public static readonly StyledProperty<bool> IsLatchingProperty =
        AvaloniaProperty.Register<PushButton, bool>(nameof(IsLatching));

    /// <summary>
    /// Whether this is the one of its group being worked on, drawn as a ring round the cap.
    /// </summary>
    /// <remarks>
    /// A grid of pads needs to say which one the settings underneath are about, and that is not
    /// the same as which one is sounding: the lamp says that, and it goes out.
    /// </remarks>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<PushButton, bool>(nameof(IsSelected));

    /// <summary>
    /// Backs <see cref="IsChecked"/>, which is where a latching button holds whether it is down.
    /// </summary>
    /// <remarks>
    /// Two way, because a button is pressed by hand and whatever it is bound to has to hear
    /// about it. Meaningless on a momentary button, which is down only while a finger is on it.
    /// </remarks>
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<PushButton, bool>(nameof(IsChecked), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Backs <see cref="Lit"/>: whether the lamp burns, and null to let it follow the button.
    /// </summary>
    /// <remarks>
    /// Nullable so that "nobody has said" is a different answer from "off". A latching button
    /// lights its own lamp, which is right for a mute; a transport button's lamp is driven by
    /// whether the thing is actually running, which is not the same as whether it was pressed.
    /// </remarks>
    public static readonly StyledProperty<bool?> LitProperty =
        AvaloniaProperty.Register<PushButton, bool?>(nameof(Lit));

    /// <summary>
    /// Backs <see cref="HasLamp"/>: whether there is a lamp at all. A transport button has one;
    /// a step does not.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="Lit"/> because the lamp takes room whether or not it is
    /// burning, and a row of buttons where only the lit ones were the right height would be a
    /// row that moved as it was used.
    /// </remarks>
    public static readonly StyledProperty<bool> HasLampProperty =
        AvaloniaProperty.Register<PushButton, bool>(nameof(HasLamp));

    /// <summary>
    /// Backs <see cref="CapHeight"/>. The width follows what is on the cap unless one is given.
    /// </summary>
    public static readonly StyledProperty<double> CapHeightProperty =
        AvaloniaProperty.Register<PushButton, double>(nameof(CapHeight), 22.0);

    /// <summary>
    /// Backs <see cref="CapWidth"/>: a width to hold to, and zero measures it from what is
    /// written on the cap.
    /// </summary>
    /// <remarks>
    /// A row of buttons whose captions are different lengths is a row of different sized caps,
    /// which no panel is built with. Giving them all one width puts them back in a row.
    /// </remarks>
    public static readonly StyledProperty<double> CapWidthProperty =
        AvaloniaProperty.Register<PushButton, double>(nameof(CapWidth));

    /// <summary>
    /// What the cap is moulded as: an oblong, a disc, or a triangle pointing somewhere.
    /// </summary>
    /// <remarks>
    /// A panel uses the shape to say what kind of thing a button is before you have read the
    /// writing on it. The oblongs are the ordinary ones, the disc is the one it wants you to
    /// find in a hurry, and a triangle is a direction: it points at what pressing it will do.
    /// </remarks>
    public static readonly StyledProperty<ButtonShape> ShapeProperty =
        AvaloniaProperty.Register<PushButton, ButtonShape>(nameof(Shape));

    /// <summary>
    /// Backs <see cref="Points"/>: which way a triangular cap points, ignored by the other
    /// shapes.
    /// </summary>
    public static readonly StyledProperty<Pointing> PointsProperty =
        AvaloniaProperty.Register<PushButton, Pointing>(nameof(Points), Pointing.Right);

    /// <summary>
    /// What colour the cap is moulded in. Left alone, it is the panel's own colour.
    /// </summary>
    /// <remarks>
    /// The shading is worked out from whatever this is, so a red cap is lit and shadowed as a
    /// red cap rather than being a red rectangle with somebody else's highlight on it.
    /// </remarks>
    public static readonly StyledProperty<Color> ColourProperty =
        AvaloniaProperty.Register<PushButton, Color>(nameof(Colour), Colors.Transparent);

    /// <summary>
    /// Backs <see cref="FontSize"/>, which sizes what is on the cap and what is under it alike.
    /// </summary>
    /// <remarks>
    /// One size for both, because a cap's caption and its label are the same word said in two
    /// places and a panel never prints them at two sizes. It also sets the padding either side
    /// of a caption, so a button made bigger gets wider as well as taller.
    /// </remarks>
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<PushButton, double>(nameof(FontSize), 10.0);

    /// <summary>Backs <see cref="LampSize"/>, the diameter of the lamp.</summary>
    public static readonly StyledProperty<double> LampSizeProperty =
        AvaloniaProperty.Register<PushButton, double>(nameof(LampSize), 5.0);

    /// <summary>
    /// True to put the lamp under the cap rather than over it.
    /// </summary>
    /// <remarks>
    /// Which side the lamp sits on is not decoration: it is how a panel tells a row of buttons
    /// apart. On the Mother-32 the transport buttons carry their lamp above, and the eight step
    /// buttons carry theirs below, so a glance at the row tells you which row you are looking
    /// at before you have read a word of it.
    /// </remarks>
    public static readonly StyledProperty<bool> LampBelowProperty =
        AvaloniaProperty.Register<PushButton, bool>(nameof(LampBelow));

    /// <summary>
    /// Backs <see cref="LampColour"/>, red unless a panel says otherwise.
    /// </summary>
    /// <remarks>
    /// Not the theme's alarm colour, deliberately: this is what colour the lamp is moulded in,
    /// which is a fact about the hardware being drawn rather than about the page it is on.
    /// </remarks>
    public static readonly StyledProperty<Color> LampColourProperty =
        AvaloniaProperty.Register<PushButton, Color>(nameof(LampColour), Color.FromRgb(0xE5, 0x39, 0x35));

    /// <summary>
    /// Backs <see cref="Command"/>, for a panel put together in XAML where a command is what
    /// there is to bind.
    /// </summary>
    /// <remarks>
    /// Beside <see cref="Pressed"/> rather than instead of it: a panel built from a description
    /// has no bindings and only wants to be told.
    /// </remarks>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PushButton, ICommand?>(nameof(Command));

    /// <summary>Backs <see cref="CommandParameter"/>, which says which button of a row this is.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<PushButton, object?>(nameof(CommandParameter));

    /// <summary>
    /// Backs <see cref="Pressed"/>, raised when the button has actually been worked.
    /// </summary>
    /// <remarks>
    /// Bubbling, so a grid of pads can listen once at the top rather than wiring every cap it
    /// builds.
    /// </remarks>
    public static readonly RoutedEvent<RoutedEventArgs> PressedEvent =
        RoutedEvent.Register<PushButton, RoutedEventArgs>(nameof(Pressed), RoutingStrategies.Bubble);

    /// <summary>Whether a finger is on the cap now, which is not the same as a latch being down.</summary>
    private bool _down;

    /// <summary>Whether the focus this holds arrived by tabbing rather than by being clicked.</summary>
    private bool _byKeyboard;

    /// <summary>
    /// Says which properties change the picture and which change the size, and makes a button
    /// take the keyboard.
    /// </summary>
    /// <remarks>
    /// Focusable is overridden rather than set in the constructor so a style can still take it
    /// back off a button that is only there to be looked at.
    /// </remarks>
    static PushButton()
    {
        AffectsRender<PushButton>(
            LabelProperty, CapTextProperty, IsCheckedProperty, IsSelectedProperty, LitProperty, HasLampProperty,
            CapHeightProperty, CapWidthProperty, ShapeProperty, PointsProperty, ColourProperty,
            FontSizeProperty, LampSizeProperty, LampBelowProperty, LampColourProperty, MarkProperty);

        AffectsMeasure<PushButton>(
            LabelProperty, CapTextProperty, HasLampProperty,
            CapHeightProperty, CapWidthProperty, ShapeProperty, FontSizeProperty, LampSizeProperty,
            LampBelowProperty);

        FocusableProperty.OverrideDefaultValue<PushButton>(true);
    }

    /// <summary>
    /// A mark drawn on the cap instead of a word, or nothing.
    /// </summary>
    /// <remarks>
    /// Drawn to the cap's own size rather than set in a font size somebody has to keep in step
    /// with it, which is what makes a button that says nothing still say something at any size.
    /// A cap that has a word on it as well draws the word: the mark is for the button whose
    /// meaning is a picture.
    /// </remarks>
    public static readonly StyledProperty<CapMark> MarkProperty =
        AvaloniaProperty.Register<PushButton, CapMark>(nameof(Mark));

    /// <inheritdoc cref="MarkProperty"/>
    public CapMark Mark
    {
        get => GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }

    /// <summary>What is written under the cap.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="CapTextProperty"/>
    public string? CapText
    {
        get => GetValue(CapTextProperty);
        set => SetValue(CapTextProperty, value);
    }

    /// <inheritdoc cref="IsLatchingProperty"/>
    public bool IsLatching
    {
        get => GetValue(IsLatchingProperty);
        set => SetValue(IsLatchingProperty, value);
    }

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <inheritdoc cref="IsCheckedProperty"/>
    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <inheritdoc cref="LitProperty"/>
    public bool? Lit
    {
        get => GetValue(LitProperty);
        set => SetValue(LitProperty, value);
    }

    /// <inheritdoc cref="HasLampProperty"/>
    public bool HasLamp
    {
        get => GetValue(HasLampProperty);
        set => SetValue(HasLampProperty, value);
    }

    /// <summary>How tall the cap is, and how wide too for the shapes that are square.</summary>
    public double CapHeight
    {
        get => GetValue(CapHeightProperty);
        set => SetValue(CapHeightProperty, value);
    }

    /// <inheritdoc cref="CapWidthProperty"/>
    public double CapWidth
    {
        get => GetValue(CapWidthProperty);
        set => SetValue(CapWidthProperty, value);
    }

    /// <inheritdoc cref="ShapeProperty"/>
    public ButtonShape Shape
    {
        get => GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    /// <inheritdoc cref="PointsProperty"/>
    public Pointing Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <summary>True for the shapes that are as wide as they are tall.</summary>
    private bool Square => Shape is ButtonShape.Round or ButtonShape.Triangle;

    /// <inheritdoc cref="ColourProperty"/>
    public Color Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    /// <inheritdoc cref="FontSizeProperty"/>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>How wide across the lamp is.</summary>
    public double LampSize
    {
        get => GetValue(LampSizeProperty);
        set => SetValue(LampSizeProperty, value);
    }

    /// <inheritdoc cref="LampBelowProperty"/>
    public bool LampBelow
    {
        get => GetValue(LampBelowProperty);
        set => SetValue(LampBelowProperty, value);
    }

    /// <inheritdoc cref="LampColourProperty"/>
    public Color LampColour
    {
        get => GetValue(LampColourProperty);
        set => SetValue(LampColourProperty, value);
    }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>Handed to the command, which is how one handler serves a whole row of caps.</summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>
    /// The button was worked: released inside its own bounds, or driven from the keyboard.
    /// </summary>
    /// <remarks>
    /// Raised before the command is run, so a listener that reads state set up by the command
    /// has to use the command instead.
    /// </remarks>
    public event EventHandler<RoutedEventArgs> Pressed
    {
        add => AddHandler(PressedEvent, value);
        remove => RemoveHandler(PressedEvent, value);
    }

    /// <summary>True while the cap is down, whether held or latched.</summary>
    private bool Down => _down || (IsLatching && IsChecked);

    /// <summary>
    /// Whether the lamp burns: what it was told, or the latch when nothing has told it.
    /// </summary>
    /// <remarks>
    /// A momentary button nobody drives has an unlit lamp, which is right: it is down for a
    /// tenth of a second and a lamp that flickered with the finger would say nothing.
    /// </remarks>
    private bool Lamp => Lit ?? (IsLatching && IsChecked);

    /// <summary>
    /// Room for the lamp, the cap, and whatever is written under it.
    /// </summary>
    /// <remarks>
    /// A round or triangular cap is as wide as it is tall, since neither shape means anything
    /// stretched. An oblong is the wider of what it was told to be and what its caption needs
    /// with a little padding either side, and never narrower than thirty, which is the width
    /// below which a cap stops reading as something to press.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        var cap = Text(CapText, Brushes.Black);
        var label = Text(Label, Brushes.Black);

        double width = Square
            ? CapHeight
            : Math.Max(CapWidth > 0 ? CapWidth : 30, cap.Width + FontSize * 1.8);

        width = Math.Max(width, label.Width);

        double height = CapHeight;
        if (HasLamp) height += LampSize + LampGap;
        if (!string.IsNullOrEmpty(Label)) height += LabelGap + label.Height;

        return new Size(width, height);
    }

    /// <summary>
    /// Paints the lamp, the moulded cap, and the writing on and under it.
    /// </summary>
    /// <remarks>
    /// The cap is lit from above when it is up and from below when it is down, so a pressed cap
    /// sits in its own shadow. That is the whole of what makes a button look pressed, and it is
    /// worked out from whatever colour the cap is moulded in rather than painted over it, so a
    /// red cap is lit as a red cap.
    ///
    /// A ring round the cap means this is the one being worked on. Focus draws one too, but only
    /// when the keyboard put it there: a button that kept a ring because it was clicked once
    /// looked like a selection nobody made, and beside a real selection it read as two.
    ///
    /// What is written on the cap is black or white by how bright the cap is, and it drops half
    /// a pixel while the cap is down, which is the writing going down with the moulding it is
    /// printed on.
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);

        double top = 0;
        double middle = Bounds.Width / 2;

        if (HasLamp && !LampBelow)
        {
            Led.DrawLamp(context, new Point(middle, top + LampSize / 2), LampSize / 2, LampColour, Lamp);
            top += LampSize + LampGap;
        }

        double capWidth = Square ? CapHeight : Bounds.Width;
        var cap = new Rect((Bounds.Width - capWidth) / 2, top, capWidth, CapHeight);

        var seat = Colour.A == 0 ? palette.Surface : Colour;

        var moulding = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = Down
                ? new GradientStops
                {
                    new GradientStop(ThemePalette.Shade(seat, -0.10), 0),
                    new GradientStop(ThemePalette.Shade(seat, 0.20), 1)
                }
                : new GradientStops
                {
                    new GradientStop(ThemePalette.Shade(seat, 0.38), 0),
                    new GradientStop(ThemePalette.Shade(seat, 0.08), 1)
                }
        };

        bool ringed = IsSelected || (IsFocused && _byKeyboard);

        var edge = ringed ? palette.Accent : ThemePalette.Shade(seat, -0.35);

        var pen = new Pen(new SolidColorBrush(edge), ringed ? 1.5 : 1);

        switch (Shape)
        {
            case ButtonShape.Round:
                context.DrawEllipse(moulding, pen, cap.Center, capWidth / 2, CapHeight / 2);
                break;

            case ButtonShape.Triangle:
                context.DrawGeometry(moulding, pen, Triangle(cap, Points));
                break;

            default:
                context.DrawRectangle(moulding, pen, cap, 3, 3);
                break;
        }

        bool dark = seat.R * 0.299 + seat.G * 0.587 + seat.B * 0.114 > CapTextFlipsAt;

        if (!string.IsNullOrEmpty(CapText))
        {
            var text = Text(CapText, new SolidColorBrush(dark ? Colors.Black : Colors.White));
            context.DrawText(text,
                new Point(middle - text.Width / 2, cap.Center.Y - text.Height / 2 + (Down ? 0.5 : 0)));
        }
        else if (Mark == CapMark.Bars)
        {
            DrawBars(context, cap, new SolidColorBrush(dark ? Colors.Black : Colors.White), Down ? 0.5 : 0);
        }

        double under = cap.Bottom;

        if (HasLamp && LampBelow)
        {
            under += LampGap;
            Led.DrawLamp(context, new Point(middle, under + LampSize / 2), LampSize / 2, LampColour, Lamp);
            under += LampSize;
        }

        if (!string.IsNullOrEmpty(Label))
        {
            var text = Text(Label, palette.MutedBrush);
            context.DrawText(text, new Point(middle - text.Width / 2, under + LabelGap));
        }
    }

    /// <summary>
    /// Remembers how the focus arrived, because only a tab draws a ring.
    /// </summary>
    /// <remarks>
    /// Directional counts as the keyboard too: arrowing around a grid of pads is somebody
    /// navigating, and the ring is what tells them where they have got to.
    /// </remarks>
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);

        _byKeyboard = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        InvalidateVisual();
    }

    /// <summary>Takes the ring away with the focus.</summary>
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);

        _byKeyboard = false;
        InvalidateVisual();
    }

    /// <summary>
    /// Puts the cap down.
    /// </summary>
    /// <remarks>
    /// Nothing happens yet: a button fires on the release, so that sliding off it is how you
    /// change your mind. The pointer is deliberately not captured, since the release has to be
    /// able to land somewhere else for that to work.
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        Focus();
        _down = true;
        InvalidateVisual();

        e.Handled = true;
    }

    /// <summary>
    /// Lets the cap up, and works the button if the release landed on it.
    /// </summary>
    /// <remarks>
    /// Only a release inside the button counts, so sliding off it is how you change your mind
    /// about a press you have already begun.
    /// </remarks>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_down) return;

        _down = false;
        InvalidateVisual();

        if (new Rect(Bounds.Size).Contains(e.GetPosition(this))) Fire();

        e.Handled = true;
    }

    /// <summary>
    /// Space and enter work the button.
    /// </summary>
    /// <remarks>
    /// The cap is not drawn down for these: a key press has no length worth animating, and a cap
    /// that flashed would be a frame nobody sees. Any other key is left unhandled and carries on
    /// out to the panel.
    /// </remarks>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key is not (Key.Space or Key.Enter)) return;

        Fire();
        e.Handled = true;
    }

    /// <summary>
    /// The button was worked: throw the latch if it has one, tell whoever is listening, run the
    /// command if there is one.
    /// </summary>
    /// <remarks>
    /// Both the event and the command, and in that order. A panel built in XAML binds a command
    /// and a panel built from a description listens, and one control serves both rather than
    /// there being two spellings of a button.
    /// </remarks>
    private void Fire()
    {
        if (IsLatching) IsChecked = !IsChecked;

        RaiseEvent(new RoutedEventArgs(PressedEvent));

        if (Command?.CanExecute(CommandParameter) == true) Command.Execute(CommandParameter);
    }

    /// <summary>A piece of text laid out at the button's own size, for the cap or for the label.</summary>
    private FormattedText Text(string? text, IBrush brush) =>
        new(text ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), FontSize, brush);

    /// <summary>
    /// Three bars across the middle of the cap, the mark every menu button wears.
    /// </summary>
    /// <remarks>
    /// Sized from the cap rather than from a font, so a machine that asks for a bigger button
    /// gets a bigger mark and nobody has to keep two numbers in step. Rounded at the ends,
    /// because square ends at this size read as pixels rather than as a drawing.
    ///
    /// The bars take a little over half the width and a little over half the height between
    /// them, which is what the character version looked like on a font that had it. Thin enough
    /// to stay three bars at the smallest cap anybody would draw: a thickness under a pixel is
    /// held up so the mark never fades out.
    /// </remarks>
    /// <param name="context">What to draw into.</param>
    /// <param name="cap">The cap to draw on.</param>
    /// <param name="ink">What to draw them in.</param>
    /// <param name="down">How far the cap has sunk, so the mark sinks with it.</param>
    private static void DrawBars(DrawingContext context, Rect cap, IBrush ink, double down)
    {
        double wide = Math.Max(cap.Width * BarsWidth, 2);
        double thick = Math.Max(cap.Height * BarThickness, 1);
        double gap = thick * BarGap;
        double tall = thick * 3 + gap * 2;

        double left = cap.Center.X - wide / 2;
        double top = cap.Center.Y - tall / 2 + down;

        for (int at = 0; at < 3; at++)
        {
            var bar = new Rect(left, top + at * (thick + gap), wide, thick);

            context.DrawRectangle(ink, null, bar, thick / 2, thick / 2);
        }
    }

    /// <summary>How much of the cap's width the bars take.</summary>
    private const double BarsWidth = 0.55;

    /// <summary>How thick each bar is, as a share of the cap's height.</summary>
    private const double BarThickness = 0.09;

    /// <summary>And how far apart they are, as a share of their own thickness.</summary>
    private const double BarGap = 1.4;

    /// <summary>A triangle filling the cap, with its point in the direction asked for.</summary>
    private static StreamGeometry Triangle(Rect cap, Pointing points)
    {
        var box = cap.Deflate(TriangleInset);

        var geometry = new StreamGeometry();

        using (var draw = geometry.Open())
        {
            var corners = points switch
            {
                Pointing.Left => new[]
                {
                    new Point(box.Left, box.Center.Y),
                    new Point(box.Right, box.Top),
                    new Point(box.Right, box.Bottom)
                },
                Pointing.Up => new[]
                {
                    new Point(box.Center.X, box.Top),
                    new Point(box.Right, box.Bottom),
                    new Point(box.Left, box.Bottom)
                },
                Pointing.Down => new[]
                {
                    new Point(box.Center.X, box.Bottom),
                    new Point(box.Left, box.Top),
                    new Point(box.Right, box.Top)
                },
                _ => new[]
                {
                    new Point(box.Right, box.Center.Y),
                    new Point(box.Left, box.Bottom),
                    new Point(box.Left, box.Top)
                }
            };

            draw.BeginFigure(corners[0], true);
            draw.LineTo(corners[1]);
            draw.LineTo(corners[2]);
            draw.EndFigure(true);
        }

        return geometry;
    }
}
