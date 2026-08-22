using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;

namespace JingleBox2.Views;

/// <summary>
/// The keyboard on a front panel: the keys, and the octave they are showing.
/// </summary>
/// <remarks>
/// One control rather than a keyboard with a row of lamps wired to it, because they are not
/// two things. The lamps do not report a separate setting that has to be kept in step with the
/// keys: they are where the keys are. Splitting them would mean every panel that wanted a
/// keyboard had to rebuild that agreement, and get it right.
///
/// Three octaves of keys, which is what a panel has room for, and ten lamps for the ten octaves
/// a note can be in, so the lit one says which part of the range is on show. Press a key to
/// sound it. A key lights while its note is sounding, whoever played it: by hand here, or by
/// the pattern on the track this panel's instrument is standing in.
/// </remarks>
public class Clavier : ThemedControl
{
    /// <summary>Which semitones of an octave are the raised keys.</summary>
    private static readonly bool[] Raised = { false, true, false, true, false, false, true, false, true, false, true, false };

    private const double RaisedWidth = 0.62;
    private const double RaisedHeight = 0.62;

    private const double HeadGap = 8;
    private const double CaptionGap = 4;
    private const double NumberGap = 3;
    private const double ArrowGap = 9;

    /// <summary>
    /// The four faces a key can wear when it is not sounding, made once.
    /// </summary>
    /// <remarks>
    /// Thirty-seven keys is thirty-seven gradients if each one builds its own, and a keyboard
    /// repaints every time a note starts or stops. Only the sounding keys need a brush of their
    /// own, and there are never many of those, so the rest share these four.
    /// </remarks>
    private static readonly IBrush WhiteUp = Face(Color.FromRgb(0xE8, 0xE8, 0xE4), Color.FromRgb(0xB4, 0xB4, 0xAE));
    private static readonly IBrush WhiteDown = Face(Color.FromRgb(0xB4, 0xB4, 0xAE), Color.FromRgb(0xE8, 0xE8, 0xE4));
    private static readonly IBrush RaisedUp = Face(Color.FromRgb(0x2A, 0x2C, 0x30), Color.FromRgb(0x14, 0x15, 0x18));
    private static readonly IBrush RaisedDown = Face(Color.FromRgb(0x14, 0x15, 0x18), Color.FromRgb(0x2A, 0x2C, 0x30));

    private static readonly IPen Edge = new Pen(new SolidColorBrush(Color.FromRgb(0x0C, 0x0D, 0x0F)), 1);

    /// <summary>Which octave the leftmost key is the C of, and which lamp is lit.</summary>
    public static readonly StyledProperty<int> OctaveProperty =
        AvaloniaProperty.Register<Clavier, int>(
            nameof(Octave), 4, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>How many octaves the lamps count, which is how far the keyboard can travel.</summary>
    public static readonly StyledProperty<int> OctaveCountProperty =
        AvaloniaProperty.Register<Clavier, int>(nameof(OctaveCount), 10);

    /// <summary>How many keys there are, counted in semitones. Thirty-seven is three octaves.</summary>
    public static readonly StyledProperty<int> KeyCountProperty =
        AvaloniaProperty.Register<Clavier, int>(nameof(KeyCount), 37);

    /// <summary>
    /// The semitones sounding now, as absolute note numbers.
    /// </summary>
    /// <remarks>
    /// A collection rather than one number, because auditions pile up rather than cutting one
    /// another: hold four keys on the computer keyboard and four voices sound. If it notifies
    /// when it changes, this follows it without being told again.
    /// </remarks>
    public static readonly StyledProperty<IEnumerable?> LitProperty =
        AvaloniaProperty.Register<Clavier, IEnumerable?>(nameof(Lit));

    /// <summary>Run with the pressed key's absolute semitone as its argument.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<Clavier, ICommand?>(nameof(Command));

    public static readonly StyledProperty<double> KeyHeightProperty =
        AvaloniaProperty.Register<Clavier, double>(nameof(KeyHeight), 52.0);

    public static readonly StyledProperty<double> KeyWidthProperty =
        AvaloniaProperty.Register<Clavier, double>(nameof(KeyWidth), 18.0);

    /// <summary>What colour a key burns when it is sounding.</summary>
    public static readonly StyledProperty<Color> LitColourProperty =
        AvaloniaProperty.Register<Clavier, Color>(nameof(LitColour), Color.FromRgb(0xE5, 0xB3, 0x39));

    /// <summary>What colour the octave lamps burn.</summary>
    public static readonly StyledProperty<Color> LampColourProperty =
        AvaloniaProperty.Register<Clavier, Color>(nameof(LampColour), Color.FromRgb(0xE5, 0xB3, 0x39));

    public static readonly StyledProperty<double> LampSizeProperty =
        AvaloniaProperty.Register<Clavier, double>(nameof(LampSize), 9.0);

    public static readonly StyledProperty<double> LampGapProperty =
        AvaloniaProperty.Register<Clavier, double>(nameof(LampGap), 9.0);

    /// <summary>Written over the lamps, the way a panel names a section.</summary>
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<Clavier, string?>(nameof(Caption), "OCTAVE");

    /// <summary>True to write the octave's number on each C, the way a keyboard is marked.</summary>
    public static readonly StyledProperty<bool> MarksOctavesProperty =
        AvaloniaProperty.Register<Clavier, bool>(nameof(MarksOctaves), true);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<Clavier, double>(nameof(FontSize), 8.5);

    /// <summary>The key under the pointer while it is held down, so it can be drawn pressed.</summary>
    private int _pressed = -1;

    /// <summary>Which arrow is held down: -1 for the left, 1 for the right, 0 for neither.</summary>
    private int _arrow;

    /// <summary>
    /// Where the keys are, worked out once and kept until something about them changes.
    /// </summary>
    /// <remarks>
    /// A repaint does not move the keys and neither does the pointer, so laying them out on
    /// every paint and again on every pointer move is thirty-seven of everything for nothing.
    /// </remarks>
    private (double Left, double Width, bool Raised, int Note)[]? _laid;

    private (int First, int Count, double Width) _laidFor;

    /// <summary>The lit faces and the halo, remade only when the colour itself changes.</summary>
    private IBrush? _litWhite;
    private IBrush? _litRaised;
    private IBrush? _halo;
    private Color _litFor;

    private INotifyCollectionChanged? _watching;

    static Clavier()
    {
        AffectsRender<Clavier>(
            OctaveProperty, OctaveCountProperty, KeyCountProperty, LitProperty,
            KeyHeightProperty, KeyWidthProperty, LitColourProperty, LampColourProperty,
            LampSizeProperty, LampGapProperty, CaptionProperty, MarksOctavesProperty,
            FontSizeProperty);

        AffectsMeasure<Clavier>(
            OctaveCountProperty, KeyCountProperty, KeyHeightProperty, KeyWidthProperty,
            LampSizeProperty, LampGapProperty, CaptionProperty, MarksOctavesProperty,
            FontSizeProperty);

        FocusableProperty.OverrideDefaultValue<Clavier>(true);
    }

    public int Octave
    {
        get => GetValue(OctaveProperty);
        set => SetValue(OctaveProperty, value);
    }

    public int OctaveCount
    {
        get => GetValue(OctaveCountProperty);
        set => SetValue(OctaveCountProperty, value);
    }

    public int KeyCount
    {
        get => GetValue(KeyCountProperty);
        set => SetValue(KeyCountProperty, value);
    }

    public IEnumerable? Lit
    {
        get => GetValue(LitProperty);
        set => SetValue(LitProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public double KeyHeight
    {
        get => GetValue(KeyHeightProperty);
        set => SetValue(KeyHeightProperty, value);
    }

    public double KeyWidth
    {
        get => GetValue(KeyWidthProperty);
        set => SetValue(KeyWidthProperty, value);
    }

    public Color LitColour
    {
        get => GetValue(LitColourProperty);
        set => SetValue(LitColourProperty, value);
    }

    public Color LampColour
    {
        get => GetValue(LampColourProperty);
        set => SetValue(LampColourProperty, value);
    }

    public double LampSize
    {
        get => GetValue(LampSizeProperty);
        set => SetValue(LampSizeProperty, value);
    }

    public double LampGap
    {
        get => GetValue(LampGapProperty);
        set => SetValue(LampGapProperty, value);
    }

    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public bool MarksOctaves
    {
        get => GetValue(MarksOctavesProperty);
        set => SetValue(MarksOctavesProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>The note the leftmost key sounds.</summary>
    private int FirstNote => Octave * 12;

    /// <summary>How many white keys the keyboard has, which is how wide it is.</summary>
    private int Whites
    {
        get
        {
            int whites = 0;

            for (int i = 0; i < Math.Max(1, KeyCount); i++)
                if (!Raised[Mod12(FirstNote + i)]) whites++;

            return whites;
        }
    }

    private double KeysWidth => Whites * KeyWidth;

    private double ArrowWidth => LampSize * 2.4;

    private double ArrowHeight => LampSize * 2.0;

    /// <summary>How tall the arrows, lamps and their numbers are together.</summary>
    private double HeadHeight
    {
        get
        {
            double height = Math.Max(ArrowHeight, LampSize + NumberGap + LineHeight);

            if (!string.IsNullOrEmpty(Caption)) height += LineHeight + CaptionGap;

            return height;
        }
    }

    private double LineHeight => Text("0", Brushes.Black).Height;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Follow the collection itself, not only the property holding it: notes are added to
        // the same collection rather than a new one being handed over each time.
        if (change.Property != LitProperty) return;

        if (_watching != null) _watching.CollectionChanged -= OnLitChanged;

        _watching = change.NewValue as INotifyCollectionChanged;

        if (_watching != null) _watching.CollectionChanged += OnLitChanged;
    }

    private void OnLitChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    protected override Size MeasureOverride(Size availableSize) =>
        new(KeysWidth, HeadHeight + HeadGap + KeyHeight + (MarksOctaves ? NumberGap + LineHeight : 0));

    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);

        DrawHead(context, palette);
        DrawKeys(context, palette);
    }

    /// <summary>The arrows and the lamps: where on the range the keys below are looking.</summary>
    private void DrawHead(DrawingContext context, ThemePalette palette)
    {
        double top = 0;
        double middle = KeysWidth / 2;

        if (!string.IsNullOrEmpty(Caption))
        {
            var caption = Text(Caption, palette.MutedBrush);
            context.DrawText(caption, new Point(middle - caption.Width / 2, top));
            top += caption.Height + CaptionGap;
        }

        double lampsWidth = LampsWidth();
        double left = middle - lampsWidth / 2;
        double lampTop = top + Math.Max(0, (ArrowHeight - LampSize) / 2);

        for (int i = 0; i < OctaveCount; i++)
        {
            double at = left + LampPitch() * i + LampSize / 2;

            Led.DrawLamp(context, new Point(at, lampTop + LampSize / 2), LampSize / 2, LampColour, i == Octave);

            var number = Text(i.ToString(CultureInfo.CurrentCulture), palette.MutedBrush);
            context.DrawText(number, new Point(at - number.Width / 2, lampTop + LampSize + NumberGap));
        }

        DrawArrow(context, LeftArrow(), pointsRight: false, held: _arrow < 0);
        DrawArrow(context, RightArrow(), pointsRight: true, held: _arrow > 0);
    }

    private void DrawArrow(DrawingContext context, Rect at, bool pointsRight, bool held)
    {
        var seat = Color.FromRgb(0xB0, 0xB3, 0xB8);

        var moulding = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = held
                ? new GradientStops
                {
                    new GradientStop(Lighten(seat, -0.30), 0),
                    new GradientStop(Lighten(seat, 0.10), 1)
                }
                : new GradientStops
                {
                    new GradientStop(Lighten(seat, 0.20), 0),
                    new GradientStop(Lighten(seat, -0.14), 1)
                }
        };

        var shape = new StreamGeometry();

        using (var draw = shape.Open())
        {
            if (pointsRight)
            {
                draw.BeginFigure(at.TopLeft, true);
                draw.LineTo(new Point(at.Right, at.Center.Y));
                draw.LineTo(at.BottomLeft);
            }
            else
            {
                draw.BeginFigure(at.TopRight, true);
                draw.LineTo(new Point(at.Left, at.Center.Y));
                draw.LineTo(at.BottomRight);
            }

            draw.EndFigure(true);
        }

        context.DrawGeometry(moulding, new Pen(new SolidColorBrush(Lighten(seat, -0.45)), 1), shape);
    }

    private void DrawKeys(DrawingContext context, ThemePalette palette)
    {
        double top = HeadHeight + HeadGap;
        double height = KeyHeight;

        var laid = Keys();
        var sounding = Sounding();

        // The white keys first, whole, then the raised ones over them. Drawn the other way
        // round a raised key would be sitting under its neighbours instead of on top.
        for (int i = 0; i < laid.Length; i++)
        {
            var (left, width, raised, note) = laid[i];
            if (raised) continue;

            var key = new Rect(left, top, width, height);

            Draw(context, key, raised: false, sounding.Contains(note), pressed: _pressed == i);

            if (!MarksOctaves || Mod12(note) != 0) continue;

            var mark = Text((note / 12).ToString(CultureInfo.CurrentCulture), palette.MutedBrush);
            context.DrawText(mark, new Point(key.Center.X - mark.Width / 2, key.Bottom + NumberGap));
        }

        for (int i = 0; i < laid.Length; i++)
        {
            var (left, width, raised, note) = laid[i];
            if (!raised) continue;

            var key = new Rect(left, top, width, height * RaisedHeight);

            Draw(context, key, raised: true, sounding.Contains(note), pressed: _pressed == i);
        }
    }

    private void Draw(DrawingContext context, Rect key, bool raised, bool lit, bool pressed)
    {
        double round = raised ? 2 : 3;

        // A lit key is the panel's colour rather than a coloured rectangle: it is the same key,
        // with a light behind it. Pressed, it is lit from below, which is the whole of what
        // makes a key look struck.
        var face = lit
            ? Burning(raised)
            : raised
                ? pressed ? RaisedDown : RaisedUp
                : pressed ? WhiteDown : WhiteUp;

        context.DrawRectangle(face, Edge, key, round, round);

        // A lit key spills onto the ones beside it, the same way a lamp does.
        if (lit) context.DrawRectangle(Halo(), null, key.Inflate(3), round, round);
    }

    /// <summary>The sounding face, kept until the colour it is made from changes.</summary>
    private IBrush Burning(bool raised)
    {
        Refresh();

        return raised ? _litRaised! : _litWhite!;
    }

    private IBrush Halo()
    {
        Refresh();

        return _halo!;
    }

    private void Refresh()
    {
        if (_litWhite != null && _litFor == LitColour) return;

        _litFor = LitColour;
        _litWhite = Face(Lighten(LitColour, 0.34), Lighten(LitColour, -0.10));
        _litRaised = Face(Lighten(LitColour, 0.10), Lighten(LitColour, -0.34));
        _halo = new SolidColorBrush(LitColour, 0.22).ToImmutable();
    }

    /// <summary>
    /// Where every key's left edge is, and how wide it is, in order.
    /// </summary>
    /// <remarks>
    /// A raised key is centred on the join between the two white keys it sits over, which is
    /// what makes a keyboard read as a keyboard whichever note it begins on.
    /// </remarks>
    private (double Left, double Width, bool Raised, int Note)[] Keys()
    {
        int keys = Math.Max(1, KeyCount);
        int first = FirstNote;
        double width = KeyWidth;

        if (_laid != null && _laidFor == (first, keys, width)) return _laid;

        double raisedWidth = width * RaisedWidth;

        var laid = new (double, double, bool, int)[keys];
        int whites = 0;

        for (int i = 0; i < keys; i++)
        {
            int note = first + i;
            bool raised = Raised[Mod12(note)];

            laid[i] = raised
                ? (whites * width - raisedWidth / 2, raisedWidth, true, note)
                : (whites * width, width, false, note);

            if (!raised) whites++;
        }

        _laid = laid;
        _laidFor = (first, keys, width);

        return laid;
    }

    /// <summary>Which of the twelve a note is, never negative however far below zero it is.</summary>
    private static int Mod12(int note) => ((note % 12) + 12) % 12;

    private double LampPitch() => LampSize + LampGap;

    private double LampsWidth() => LampPitch() * OctaveCount - LampGap;

    private double HeadTop => string.IsNullOrEmpty(Caption) ? 0 : LineHeight + CaptionGap;

    private Rect LeftArrow() =>
        new(KeysWidth / 2 - LampsWidth() / 2 - ArrowGap - ArrowWidth, HeadTop, ArrowWidth, ArrowHeight);

    private Rect RightArrow() =>
        new(KeysWidth / 2 + LampsWidth() / 2 + ArrowGap, HeadTop, ArrowWidth, ArrowHeight);

    /// <summary>The semitones sounding, read once so the keys do not walk it apiece.</summary>
    private HashSet<int> Sounding()
    {
        var sounding = new HashSet<int>();

        if (Lit == null) return sounding;

        foreach (var item in Lit)
            if (item is int semitone) sounding.Add(semitone);

        return sounding;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var point = e.GetPosition(this);
        Focus();

        if (LeftArrow().Contains(point))
        {
            _arrow = -1;
            Step(-1);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (RightArrow().Contains(point))
        {
            _arrow = 1;
            Step(1);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        // A lamp is a place to go rather than a report: pressing one takes the keys there,
        // which is quicker than pressing an arrow five times.
        int lamp = LampAt(point);
        if (lamp >= 0)
        {
            Octave = lamp;
            e.Handled = true;
            return;
        }

        _pressed = KeyAt(point);
        if (_pressed < 0) return;

        Play(_pressed);
        InvalidateVisual();

        e.Handled = true;
    }

    /// <summary>Dragging across the keys plays them, the way a finger down a keyboard does.</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_pressed < 0 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        int now = KeyAt(e.GetPosition(this));
        if (now < 0 || now == _pressed) return;

        _pressed = now;
        Play(now);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_pressed < 0 && _arrow == 0) return;

        _pressed = -1;
        _arrow = 0;
        InvalidateVisual();

        e.Handled = true;
    }

    /// <summary>The arrow keys walk the octave, for a keyboard that has the focus.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Left) Step(-1);
        else if (e.Key == Key.Right) Step(1);
        else return;

        e.Handled = true;
    }

    private void Step(int by) => Octave = Math.Clamp(Octave + by, 0, Math.Max(0, OctaveCount - 1));

    private void Play(int key)
    {
        int note = FirstNote + key;

        if (Command?.CanExecute(note) == true) Command.Execute(note);
    }

    private int LampAt(Point point)
    {
        double left = KeysWidth / 2 - LampsWidth() / 2;
        double top = HeadTop + Math.Max(0, (ArrowHeight - LampSize) / 2);

        // The whole column under a lamp counts, its number included: a nine pixel target is
        // not one anybody can hit.
        var row = new Rect(left, top, LampsWidth(), LampSize + NumberGap + LineHeight);
        if (!row.Contains(point)) return -1;

        int at = (int)((point.X - left) / LampPitch());

        return at >= 0 && at < OctaveCount ? at : -1;
    }

    /// <summary>
    /// Which key is under a point. The raised keys are asked first, since they lie over the
    /// white ones and the top of a white key is not where you meant to press.
    /// </summary>
    private int KeyAt(Point point)
    {
        double top = HeadHeight + HeadGap;
        var laid = Keys();

        for (int i = 0; i < laid.Length; i++)
        {
            var (left, width, raised, _) = laid[i];
            if (!raised) continue;

            if (new Rect(left, top, width, KeyHeight * RaisedHeight).Contains(point)) return i;
        }

        for (int i = 0; i < laid.Length; i++)
        {
            var (left, width, raised, _) = laid[i];
            if (raised) continue;

            if (new Rect(left, top, width, KeyHeight).Contains(point)) return i;
        }

        return -1;
    }

    private FormattedText Text(string? text, IBrush brush) =>
        new(text ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), FontSize, brush);

    private static IBrush Face(Color top, Color bottom) =>
        new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(top, 0),
                new GradientStop(bottom, 1)
            }
        }.ToImmutable();

    private static Color Lighten(Color colour, double amount)
    {
        double Mix(byte channel) => amount >= 0
            ? channel + (255 - channel) * amount
            : channel * (1 + amount);

        return Color.FromRgb((byte)Mix(colour.R), (byte)Mix(colour.G), (byte)Mix(colour.B));
    }
}
