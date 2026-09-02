using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Windows.Input;
using JingleBox2.Rack.Controls;
using JingleBox2.Rack.Controls.Records;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;

namespace JingleBox2.Views;

/// <summary>
/// Record, play, pause and stop, drawn as four caps with the symbols cut into them.
/// </summary>
/// <remarks>
/// Every machine's face in this app is drawn, and this is the bar you look at while you are
/// actually playing, so it is drawn too. What it replaces was four text buttons wearing glyph
/// characters and three style rules deciding what colour they went: the armed red, the playing
/// accent, the dimmed disable. Those three states now live in one place and are painted rather
/// than styled.
///
/// It holds the transport and nothing else. The tempo, the step and the rest are settings that
/// happen to stand next to it, and a control that swallowed them would be a control that has to
/// know what a bar per beat is.
/// </remarks>
public class Transport : ThemedControl
{
    /// <summary>Which of the four the pointer is on, or none.</summary>
    private enum Key
    {
        None,
        Record,
        Play,
        Pause,
        Stop
    }

    /// <summary>
    /// Left to right, and the same order everywhere: it is where the caps are drawn, how wide
    /// the control asks to be, and which one a click landed on.
    /// </summary>
    private static readonly Key[] Order = { Key.Record, Key.Play, Key.Pause, Key.Stop };

    /// <summary>Whether the record cap is armed, which fills its circle rather than outlining it.</summary>
    public static readonly StyledProperty<bool> IsRecordingProperty =
        AvaloniaProperty.Register<Transport, bool>(nameof(IsRecording));

    /// <summary>Whether the transport is running, which puts the play symbol in the accent.</summary>
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<Transport, bool>(nameof(IsPlaying));

    /// <summary>And whether it is held, which does the same to the pause symbol.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<Transport, bool>(nameof(IsPaused));

    /// <summary>False greys the pause cap, for a transport that is not running.</summary>
    public static readonly StyledProperty<bool> CanPauseProperty =
        AvaloniaProperty.Register<Transport, bool>(nameof(CanPause), true);

    /// <summary>
    /// False greys the record cap, for a page with nothing to arm or take.
    /// </summary>
    public static readonly StyledProperty<bool> CanRecordProperty =
        AvaloniaProperty.Register<Transport, bool>(nameof(CanRecord), true);

    /// <summary>
    /// False greys the play cap, for a page with nothing the transport can start.
    /// </summary>
    /// <remarks>
    /// FIRE is the one that needs it: pads are fired by pads, so the only cap there with
    /// anything behind it is stop.
    /// </remarks>
    public static readonly StyledProperty<bool> CanPlayProperty =
        AvaloniaProperty.Register<Transport, bool>(nameof(CanPlay), true);

    /// <summary>What the record cap does, and nothing at all when it is not given one.</summary>
    public static readonly StyledProperty<ICommand?> RecordCommandProperty =
        AvaloniaProperty.Register<Transport, ICommand?>(nameof(RecordCommand));

    /// <inheritdoc cref="RecordCommandProperty"/>
    public static readonly StyledProperty<ICommand?> PlayCommandProperty =
        AvaloniaProperty.Register<Transport, ICommand?>(nameof(PlayCommand));

    /// <inheritdoc cref="RecordCommandProperty"/>
    public static readonly StyledProperty<ICommand?> PauseCommandProperty =
        AvaloniaProperty.Register<Transport, ICommand?>(nameof(PauseCommand));

    /// <inheritdoc cref="RecordCommandProperty"/>
    public static readonly StyledProperty<ICommand?> StopCommandProperty =
        AvaloniaProperty.Register<Transport, ICommand?>(nameof(StopCommand));

    /// <summary>How wide one cap is. The bar is four of these and three gaps, always.</summary>
    public static readonly StyledProperty<double> CapWidthProperty =
        AvaloniaProperty.Register<Transport, double>(nameof(CapWidth), 46);

    /// <summary>And how tall, which is the whole height the control asks for.</summary>
    public static readonly StyledProperty<double> CapHeightProperty =
        AvaloniaProperty.Register<Transport, double>(nameof(CapHeight), 34);

    /// <summary>Between the caps, so four of them read as four and not as one long strip.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<Transport, double>(nameof(Gap), 6);

    /// <summary>
    /// Seats the caps on the page rather than on a panel, for a transport that is not standing
    /// on one.
    /// </summary>
    /// <remarks>
    /// The caps are drawn as a panel's caps are: a surface lit from above, which is right when
    /// there is a panel under them and wrong when there is not. On the bare page the same caps
    /// read as four bright blocks stuck to the top of the window, because they are lighter than
    /// everything around them and nothing explains why. Quiet takes the seat down to the page's
    /// own colour and flattens the moulding, so they read as part of the frame rather than as
    /// something dropped on it.
    /// </remarks>
    public static readonly StyledProperty<bool> QuietProperty =
        AvaloniaProperty.Register<Transport, bool>(nameof(Quiet));

    /// <summary>The red a record button is, everywhere.</summary>
    private static readonly Color Armed = Color.FromRgb(0xE5, 0x39, 0x35);

    /// <summary>The rounding on a cap, the same as the machine panels' caps have.</summary>
    private const double Corner = 3;

    /// <summary>How much of the cap the symbol on it takes up.</summary>
    private const double SymbolShare = 0.30;

    /// <summary>Which cap is being held down, so it can be drawn sitting in its own shadow.</summary>
    private Key _down = Key.None;

    /// <summary>And which the pointer is resting on, which lifts its seat a little.</summary>
    private Key _over = Key.None;

    /// <summary>The three sizes change the room asked for; everything else only changes the paint.</summary>
    static Transport()
    {
        AffectsRender<Transport>(
            IsRecordingProperty, IsPlayingProperty, IsPausedProperty,
            CanRecordProperty, CanPlayProperty, CanPauseProperty,
            CapWidthProperty, CapHeightProperty, GapProperty, QuietProperty);

        AffectsMeasure<Transport>(CapWidthProperty, CapHeightProperty, GapProperty);
    }

    /// <inheritdoc cref="IsRecordingProperty"/>
    public bool IsRecording
    {
        get => GetValue(IsRecordingProperty);
        set => SetValue(IsRecordingProperty, value);
    }

    /// <inheritdoc cref="IsPlayingProperty"/>
    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused
    {
        get => GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    /// <inheritdoc cref="CanPauseProperty"/>
    public bool CanPause
    {
        get => GetValue(CanPauseProperty);
        set => SetValue(CanPauseProperty, value);
    }

    /// <inheritdoc cref="CanRecordProperty"/>
    public bool CanRecord
    {
        get => GetValue(CanRecordProperty);
        set => SetValue(CanRecordProperty, value);
    }

    /// <inheritdoc cref="CanPlayProperty"/>
    public bool CanPlay
    {
        get => GetValue(CanPlayProperty);
        set => SetValue(CanPlayProperty, value);
    }

    /// <summary>Whether a cap has anything behind it here. A dead cap is drawn faint and
    /// does not press.</summary>
    private bool Dead(Key key) => key switch
    {
        Key.Record => !CanRecord,
        Key.Play => !CanPlay,
        Key.Pause => !CanPause,
        _ => false
    };

    /// <inheritdoc cref="QuietProperty"/>
    public bool Quiet
    {
        get => GetValue(QuietProperty);
        set => SetValue(QuietProperty, value);
    }

    /// <inheritdoc cref="RecordCommandProperty"/>
    public ICommand? RecordCommand
    {
        get => GetValue(RecordCommandProperty);
        set => SetValue(RecordCommandProperty, value);
    }

    /// <inheritdoc cref="PlayCommandProperty"/>
    public ICommand? PlayCommand
    {
        get => GetValue(PlayCommandProperty);
        set => SetValue(PlayCommandProperty, value);
    }

    /// <inheritdoc cref="PauseCommandProperty"/>
    public ICommand? PauseCommand
    {
        get => GetValue(PauseCommandProperty);
        set => SetValue(PauseCommandProperty, value);
    }

    /// <inheritdoc cref="StopCommandProperty"/>
    public ICommand? StopCommand
    {
        get => GetValue(StopCommandProperty);
        set => SetValue(StopCommandProperty, value);
    }

    /// <inheritdoc cref="CapWidthProperty"/>
    public double CapWidth
    {
        get => GetValue(CapWidthProperty);
        set => SetValue(CapWidthProperty, value);
    }

    /// <inheritdoc cref="CapHeightProperty"/>
    public double CapHeight
    {
        get => GetValue(CapHeightProperty);
        set => SetValue(CapHeightProperty, value);
    }

    /// <inheritdoc cref="GapProperty"/>
    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <summary>
    /// Four caps and three gaps wide, and one cap tall, whatever room it is offered: the bar is
    /// a fixed thing standing on a line, not something that stretches to fill one.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize) =>
        new(Order.Length * CapWidth + (Order.Length - 1) * Gap, CapHeight);

    /// <summary>
    /// Joins the tally of things worth entering the pointing mode for.
    /// </summary>
    /// <remarks>
    /// The transport is on every page that has anything to play, so this is what makes
    /// Ctrl+Shift+M mean something outside the mixer and the machine panels. Counted the same
    /// way they are, and by visibility as well as attachment, since the bar is hidden rather
    /// than removed on a page with no transport.
    /// </remarks>
    public Transport() => LinkKey.Watch(this);

    /// <summary>The four caps, in the order they are always in.</summary>
    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);

        foreach (var key in Order) DrawCap(context, palette, key);

        if (_offering != Key.None) LinkGlow.Paint(context, Seat(_offering));
    }

    /// <summary>
    /// One cap: the seat, the moulding over it, and the symbol cut into the middle.
    /// </summary>
    /// <remarks>
    /// The seat is the panel's own surface on a panel, and on the bare page a shade above the
    /// page, which is enough to be a cap and not enough to be a block of light. Hovering lifts it
    /// a little, so a cap says it can be pressed before it is.
    ///
    /// Lit from above when it is up and from below when it is down: a pressed cap sits in its own
    /// shadow, which is the whole of what makes a button look pressed, and it is the same
    /// moulding the machine panels' caps are drawn with.
    /// </remarks>
    private void DrawCap(DrawingContext context, ThemePalette palette, Key key)
    {
        var cap = Seat(key);

        if (cap.Width <= 0 || cap.Height <= 0) return;

        bool down = _down == key;
        bool dead = Dead(key);

        var seat = Quiet ? ThemePalette.Shade(palette.Background, 0.05) : palette.Surface;

        if (_over == key && !down && !dead) seat = ThemePalette.Shade(seat, 0.10);

        var moulding = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = down
                ? new GradientStops
                {
                    new GradientStop(ThemePalette.Shade(seat, Quiet ? -0.06 : -0.10), 0),
                    new GradientStop(ThemePalette.Shade(seat, Quiet ? 0.10 : 0.20), 1)
                }
                : new GradientStops
                {
                    new GradientStop(ThemePalette.Shade(seat, Quiet ? 0.08 : 0.38), 0),
                    new GradientStop(ThemePalette.Shade(seat, Quiet ? 0.00 : 0.08), 1)
                }
        };

        var pen = new Pen(new SolidColorBrush(ThemePalette.Shade(seat, Quiet ? -0.12 : -0.35)), 1);

        context.DrawRectangle(moulding, pen, cap, Corner, Corner);

        var symbol = new SolidColorBrush(Ink(palette, key), dead ? 0.35 : 1);

        DrawSymbol(context, key, cap, symbol, down);
    }

    /// <summary>
    /// What colour the symbol goes: red for record whatever it is doing, the accent for a
    /// transport that is running, and the ordinary text colour otherwise.
    /// </summary>
    /// <remarks>
    /// The resting colour is the muted one on the bare page. There the symbols keep company with
    /// the tab names beside them, which are muted until you are on one, and white marks read as
    /// four things shouting on a line that is otherwise quiet.
    /// </remarks>
    private Color Ink(ThemePalette palette, Key key)
    {
        var idle = Quiet ? palette.Muted : palette.Text;

        return key switch
        {
            Key.Record => Armed,
            Key.Play => IsPlaying ? palette.Accent : idle,
            Key.Pause => IsPaused ? palette.Accent : idle,
            _ => idle
        };
    }

    /// <summary>
    /// The mark on the cap, moved half a pixel down while it is pressed so it goes with the seat.
    /// </summary>
    /// <remarks>
    /// Record is filled while armed and an outline while not: a record button that looks the same
    /// armed and idle is one you have to read the colour of to know.
    /// </remarks>
    private void DrawSymbol(DrawingContext context, Key key, Rect cap, IBrush ink, bool down)
    {
        double size = Math.Min(cap.Width, cap.Height) * SymbolShare;
        var middle = new Point(cap.Center.X, cap.Center.Y + (down ? 0.5 : 0));

        switch (key)
        {
            case Key.Record:
                if (IsRecording) context.DrawEllipse(ink, null, middle, size, size);
                else context.DrawEllipse(null, new Pen(ink, 2), middle, size - 1, size - 1);
                break;

            case Key.Play:
                context.DrawGeometry(ink, null, Triangle(middle, size));
                break;

            case Key.Pause:
                double bar = size * 0.42;
                context.FillRectangle(ink, new Rect(middle.X - size, middle.Y - size, bar, size * 2));
                context.FillRectangle(ink, new Rect(middle.X + size - bar, middle.Y - size, bar, size * 2));
                break;

            case Key.Stop:
                context.FillRectangle(ink, new Rect(middle.X - size, middle.Y - size, size * 2, size * 2));
                break;
        }
    }

    /// <summary>A play triangle: pointing right, and set a hair left so it looks centred.</summary>
    private static StreamGeometry Triangle(Point middle, double size)
    {
        var geometry = new StreamGeometry();

        using var draw = geometry.Open();

        double left = middle.X - size * 0.8;
        double right = middle.X + size;

        draw.BeginFigure(new Point(left, middle.Y - size), true);
        draw.LineTo(new Point(right, middle.Y));
        draw.LineTo(new Point(left, middle.Y + size));
        draw.EndFigure(true);

        return geometry;
    }

    /// <summary>Where a cap sits, left to right in the order they are always in.</summary>
    private Rect Seat(Key key)
    {
        int at = Array.IndexOf(Order, key);

        if (at < 0) return default;

        return new Rect(at * (CapWidth + Gap), 0, CapWidth, CapHeight);
    }

    /// <summary>Which cap a point is on, or none.</summary>
    private Key At(Point point)
    {
        foreach (var key in Order)
            if (Seat(key).Contains(point)) return key;

        return Key.None;
    }

    /// <summary>
    /// Takes a cap down, and takes the pointer with it so the hand can wander off the control
    /// and come back without losing the press.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var key = At(e.GetPosition(this));

        if (key == Key.None || Dead(key)) return;

        _down = key;

        e.Pointer.Capture(this);
        InvalidateVisual();

        e.Handled = true;
    }

    /// <summary>
    /// Lets the cap up, and does the thing only if the hand let go on the cap it pressed.
    /// </summary>
    /// <remarks>
    /// Released somewhere else means the press was thought better of, which is what every button
    /// everywhere does.
    /// </remarks>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_down == Key.None) return;

        var key = _down;

        _down = Key.None;

        e.Pointer.Capture(null);
        InvalidateVisual();

        if (At(e.GetPosition(this)) == key) Fire(key);

        e.Handled = true;
    }

    /// <summary>Moves the lift from one cap to the next, and redraws only when it really moved.</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var over = At(e.GetPosition(this));

        if (over == _over) { Offer(over); return; }

        _over = over;

        Offer(over);

        InvalidateVisual();
    }

    /// <summary>
    /// Offers the cap under the pointer to whatever is being linked, and glows it.
    /// </summary>
    /// <remarks>
    /// Its own rather than <see cref="Pointable"/>'s, and for the reason the machine panel keeps
    /// its own: that hangs one mapping on one control, and this is four keys drawn inside one
    /// control, so only the control knows which of them the pointer is on.
    ///
    /// A fresh copy every time, because <see cref="ControlLink.Handle"/> fills the controller's
    /// half into the object it was handed and then keeps it: offering the template itself would
    /// have every link on the transport overwriting the last.
    ///
    /// Offered once per cap rather than once per movement, and offered again when the link has
    /// taken the last one, which is what a hand that has just pointed one button and reaches for
    /// the next expects.
    /// </remarks>
    private void Offer(Key over)
    {
        if (ControlLink.Current is not { IsLinking: true } link || over == Key.None)
        {
            if (_offering == Key.None) return;

            _offering = Key.None;

            InvalidateVisual();
            return;
        }

        if (_offering == over && link.Offered is not null) return;

        _offering = over;

        link.Offer(ControlMapping.Copy(TransportLinks.For(Named(over))));

        InvalidateVisual();
    }

    /// <summary>Which of the four a drawn cap is, for a link that names one.</summary>
    /// <remarks>
    /// Two enumerations for four keys, deliberately. The drawn one has a None, because a pointer
    /// is very often on no cap at all, and a link has no use for that.
    /// </remarks>
    private static TransportKey Named(Key key) => key switch
    {
        Key.Pause => TransportKey.Pause,
        Key.Stop => TransportKey.Stop,
        Key.Record => TransportKey.Record,
        _ => TransportKey.Play
    };

    /// <summary>Which cap is being offered to a link, or none.</summary>
    private Key _offering = Key.None;

    /// <summary>Puts every cap back down when the pointer leaves the bar.</summary>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (_over == Key.None) { Offer(Key.None); return; }

        _over = Key.None;

        Offer(Key.None);

        InvalidateVisual();
    }

    /// <summary>
    /// Runs whichever command that cap holds, and nothing when it holds none or refuses.
    /// </summary>
    private void Fire(Key key)
    {
        var command = key switch
        {
            Key.Record => RecordCommand,
            Key.Play => PlayCommand,
            Key.Pause => PauseCommand,
            Key.Stop => StopCommand,
            _ => null
        };

        if (command?.CanExecute(null) == true) command.Execute(null);
    }
}
