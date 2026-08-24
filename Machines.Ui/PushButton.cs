using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Globalization;
using System.Windows.Input;

namespace JingleBox2.Machines.Ui;

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
    private const double LampGap = 2;
    private const double LabelGap = 2;

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<PushButton, string?>(nameof(Label));

    /// <summary>What is written on the cap itself, when that reads better than under it.</summary>
    public static readonly StyledProperty<string?> CapTextProperty =
        AvaloniaProperty.Register<PushButton, string?>(nameof(CapText));

    /// <summary>Stays down when pressed rather than coming back up.</summary>
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

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<PushButton, bool>(nameof(IsChecked), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Whether the lamp is lit. Follows the button when it latches and nothing says otherwise.</summary>
    public static readonly StyledProperty<bool?> LitProperty =
        AvaloniaProperty.Register<PushButton, bool?>(nameof(Lit));

    /// <summary>True when this button has a lamp at all. A transport button has one; a step does not.</summary>
    public static readonly StyledProperty<bool> HasLampProperty =
        AvaloniaProperty.Register<PushButton, bool>(nameof(HasLamp));

    /// <summary>How tall the cap is. Its width follows what is on it unless one is given.</summary>
    public static readonly StyledProperty<double> CapHeightProperty =
        AvaloniaProperty.Register<PushButton, double>(nameof(CapHeight), 22.0);

    /// <summary>A width to hold to. Zero measures it from what is written on the cap.</summary>
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

    /// <summary>Which way a triangular cap points. Ignored by the other shapes.</summary>
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

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<PushButton, double>(nameof(FontSize), 10.0);

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

    /// <summary>What colour the lamp burns. Red unless a panel says otherwise.</summary>
    public static readonly StyledProperty<Color> LampColourProperty =
        AvaloniaProperty.Register<PushButton, Color>(nameof(LampColour), Color.FromRgb(0xE5, 0x39, 0x35));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PushButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<PushButton, object?>(nameof(CommandParameter));

    public static readonly RoutedEvent<RoutedEventArgs> PressedEvent =
        RoutedEvent.Register<PushButton, RoutedEventArgs>(nameof(Pressed), RoutingStrategies.Bubble);

    private bool _down;

    /// <summary>Whether the focus this holds arrived by tabbing rather than by being clicked.</summary>
    private bool _byKeyboard;

    static PushButton()
    {
        AffectsRender<PushButton>(
            LabelProperty, CapTextProperty, IsCheckedProperty, IsSelectedProperty, LitProperty, HasLampProperty,
            CapHeightProperty, CapWidthProperty, ShapeProperty, PointsProperty, ColourProperty,
            FontSizeProperty, LampSizeProperty, LampBelowProperty, LampColourProperty);

        AffectsMeasure<PushButton>(
            LabelProperty, CapTextProperty, HasLampProperty,
            CapHeightProperty, CapWidthProperty, ShapeProperty, FontSizeProperty, LampSizeProperty,
            LampBelowProperty);

        FocusableProperty.OverrideDefaultValue<PushButton>(true);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? CapText
    {
        get => GetValue(CapTextProperty);
        set => SetValue(CapTextProperty, value);
    }

    public bool IsLatching
    {
        get => GetValue(IsLatchingProperty);
        set => SetValue(IsLatchingProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public bool? Lit
    {
        get => GetValue(LitProperty);
        set => SetValue(LitProperty, value);
    }

    public bool HasLamp
    {
        get => GetValue(HasLampProperty);
        set => SetValue(HasLampProperty, value);
    }

    public double CapHeight
    {
        get => GetValue(CapHeightProperty);
        set => SetValue(CapHeightProperty, value);
    }

    public double CapWidth
    {
        get => GetValue(CapWidthProperty);
        set => SetValue(CapWidthProperty, value);
    }

    public ButtonShape Shape
    {
        get => GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    public Pointing Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <summary>True for the shapes that are as wide as they are tall.</summary>
    private bool Square => Shape is ButtonShape.Round or ButtonShape.Triangle;

    public Color Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public double LampSize
    {
        get => GetValue(LampSizeProperty);
        set => SetValue(LampSizeProperty, value);
    }

    public bool LampBelow
    {
        get => GetValue(LampBelowProperty);
        set => SetValue(LampBelowProperty, value);
    }

    public Color LampColour
    {
        get => GetValue(LampColourProperty);
        set => SetValue(LampColourProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public event EventHandler<RoutedEventArgs> Pressed
    {
        add => AddHandler(PressedEvent, value);
        remove => RemoveHandler(PressedEvent, value);
    }

    /// <summary>True while the cap is down, whether held or latched.</summary>
    private bool Down => _down || (IsLatching && IsChecked);

    private bool Lamp => Lit ?? (IsLatching && IsChecked);

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

        // Lit from above when it is up, from below when it is down: a pressed cap sits in its
        // own shadow, which is the whole of what makes a button look pressed.
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

        // A ring means this is the one being worked on. Focus draws one too, but only when the
        // keyboard put it there: a button that keeps a ring because it was clicked once looks
        // like a selection nobody made, and next to a real selection it reads as two.
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

        if (!string.IsNullOrEmpty(CapText))
        {
            var bright = seat.R * 0.299 + seat.G * 0.587 + seat.B * 0.114 > 140;
            var text = Text(CapText, new SolidColorBrush(bright ? Colors.Black : Colors.White));
            context.DrawText(text,
                new Point(middle - text.Width / 2, cap.Center.Y - text.Height / 2 + (Down ? 0.5 : 0)));
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

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);

        _byKeyboard = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);

        _byKeyboard = false;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        Focus();
        _down = true;
        InvalidateVisual();

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_down) return;

        _down = false;
        InvalidateVisual();

        // Only a release inside the button counts, so sliding off is how you change your mind.
        if (new Rect(Bounds.Size).Contains(e.GetPosition(this))) Fire();

        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key is not (Key.Space or Key.Enter)) return;

        Fire();
        e.Handled = true;
    }

    private void Fire()
    {
        if (IsLatching) IsChecked = !IsChecked;

        RaiseEvent(new RoutedEventArgs(PressedEvent));

        if (Command?.CanExecute(CommandParameter) == true) Command.Execute(CommandParameter);
    }

    private FormattedText Text(string? text, IBrush brush) =>
        new(text ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), FontSize, brush);

    /// <summary>A triangle filling the cap, with its point in the direction asked for.</summary>
    private static StreamGeometry Triangle(Rect cap, Pointing points)
    {
        // Slightly inside the cap, so the outline is not clipped by the control's own bounds.
        var box = cap.Deflate(1);

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

/// <summary>What a push button's cap is moulded as.</summary>
public enum ButtonShape
{
    /// <summary>The ordinary one: a rounded oblong, as wide as what is written on it.</summary>
    Oblong = 0,

    /// <summary>A disc, for the button a panel wants you to find without looking.</summary>
    Round = 1,

    /// <summary>A triangle, which points at what pressing it does.</summary>
    Triangle = 2
}

/// <summary>Which way a triangular cap points.</summary>
public enum Pointing
{
    Right = 0,
    Left = 1,
    Up = 2,
    Down = 3
}
