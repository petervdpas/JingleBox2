using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Windows.Input;

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

    private static readonly Key[] Order = { Key.Record, Key.Play, Key.Pause, Key.Stop };

    public static readonly StyledProperty<bool> IsRecordingProperty =
        AvaloniaProperty.Register<Transport, bool>(nameof(IsRecording));

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<Transport, bool>(nameof(IsPlaying));

    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<Transport, bool>(nameof(IsPaused));

    /// <summary>False greys the pause cap, for a transport that is not running.</summary>
    public static readonly StyledProperty<bool> CanPauseProperty =
        AvaloniaProperty.Register<Transport, bool>(nameof(CanPause), true);

    public static readonly StyledProperty<ICommand?> RecordCommandProperty =
        AvaloniaProperty.Register<Transport, ICommand?>(nameof(RecordCommand));

    public static readonly StyledProperty<ICommand?> PlayCommandProperty =
        AvaloniaProperty.Register<Transport, ICommand?>(nameof(PlayCommand));

    public static readonly StyledProperty<ICommand?> PauseCommandProperty =
        AvaloniaProperty.Register<Transport, ICommand?>(nameof(PauseCommand));

    public static readonly StyledProperty<ICommand?> StopCommandProperty =
        AvaloniaProperty.Register<Transport, ICommand?>(nameof(StopCommand));

    public static readonly StyledProperty<double> CapWidthProperty =
        AvaloniaProperty.Register<Transport, double>(nameof(CapWidth), 46);

    public static readonly StyledProperty<double> CapHeightProperty =
        AvaloniaProperty.Register<Transport, double>(nameof(CapHeight), 34);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<Transport, double>(nameof(Gap), 6);

    /// <summary>The red a record button is, everywhere.</summary>
    private static readonly Color Armed = Color.FromRgb(0xE5, 0x39, 0x35);

    private const double Corner = 3;

    /// <summary>How much of the cap the symbol on it takes up.</summary>
    private const double SymbolShare = 0.30;

    private Key _down = Key.None;
    private Key _over = Key.None;

    static Transport()
    {
        AffectsRender<Transport>(
            IsRecordingProperty, IsPlayingProperty, IsPausedProperty, CanPauseProperty,
            CapWidthProperty, CapHeightProperty, GapProperty);

        AffectsMeasure<Transport>(CapWidthProperty, CapHeightProperty, GapProperty);
    }

    public bool IsRecording
    {
        get => GetValue(IsRecordingProperty);
        set => SetValue(IsRecordingProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public bool IsPaused
    {
        get => GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    public bool CanPause
    {
        get => GetValue(CanPauseProperty);
        set => SetValue(CanPauseProperty, value);
    }

    public ICommand? RecordCommand
    {
        get => GetValue(RecordCommandProperty);
        set => SetValue(RecordCommandProperty, value);
    }

    public ICommand? PlayCommand
    {
        get => GetValue(PlayCommandProperty);
        set => SetValue(PlayCommandProperty, value);
    }

    public ICommand? PauseCommand
    {
        get => GetValue(PauseCommandProperty);
        set => SetValue(PauseCommandProperty, value);
    }

    public ICommand? StopCommand
    {
        get => GetValue(StopCommandProperty);
        set => SetValue(StopCommandProperty, value);
    }

    public double CapWidth
    {
        get => GetValue(CapWidthProperty);
        set => SetValue(CapWidthProperty, value);
    }

    public double CapHeight
    {
        get => GetValue(CapHeightProperty);
        set => SetValue(CapHeightProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(Order.Length * CapWidth + (Order.Length - 1) * Gap, CapHeight);

    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);

        foreach (var key in Order) DrawCap(context, palette, key);
    }

    private void DrawCap(DrawingContext context, ThemePalette palette, Key key)
    {
        var cap = Seat(key);

        if (cap.Width <= 0 || cap.Height <= 0) return;

        bool down = _down == key;
        bool dead = key == Key.Pause && !CanPause;

        var seat = palette.Surface;

        // Hovering lifts the seat a little, so a cap says it can be pressed before it is.
        if (_over == key && !down && !dead) seat = ThemePalette.Shade(seat, 0.10);

        // Lit from above when it is up, from below when it is down: a pressed cap sits in its
        // own shadow, which is the whole of what makes a button look pressed. The same moulding
        // the machine panels' caps are drawn with.
        var moulding = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = down
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

        var pen = new Pen(new SolidColorBrush(ThemePalette.Shade(seat, -0.35)), 1);

        context.DrawRectangle(moulding, pen, cap, Corner, Corner);

        var symbol = new SolidColorBrush(Ink(palette, key), dead ? 0.35 : 1);

        DrawSymbol(context, key, cap, symbol, down);
    }

    /// <summary>
    /// What colour the symbol goes: red for record whatever it is doing, the accent for a
    /// transport that is running, and the ordinary text colour otherwise.
    /// </summary>
    private Color Ink(ThemePalette palette, Key key) => key switch
    {
        Key.Record => Armed,
        Key.Play => IsPlaying ? palette.Accent : palette.Text,
        Key.Pause => IsPaused ? palette.Accent : palette.Text,
        _ => palette.Text
    };

    private void DrawSymbol(DrawingContext context, Key key, Rect cap, IBrush ink, bool down)
    {
        double size = Math.Min(cap.Width, cap.Height) * SymbolShare;
        var middle = new Point(cap.Center.X, cap.Center.Y + (down ? 0.5 : 0));

        switch (key)
        {
            case Key.Record:
                // Filled while armed, an outline while not: a record button that looks the same
                // armed and idle is one you have to read the colour of to know.
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

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var key = At(e.GetPosition(this));

        if (key == Key.None || (key == Key.Pause && !CanPause)) return;

        _down = key;

        e.Pointer.Capture(this);
        InvalidateVisual();

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_down == Key.None) return;

        var key = _down;

        _down = Key.None;

        e.Pointer.Capture(null);
        InvalidateVisual();

        // Released somewhere else means the press was thought better of, which is what every
        // button everywhere does.
        if (At(e.GetPosition(this)) == key) Fire(key);

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var over = At(e.GetPosition(this));

        if (over == _over) return;

        _over = over;

        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (_over == Key.None) return;

        _over = Key.None;

        InvalidateVisual();
    }

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
