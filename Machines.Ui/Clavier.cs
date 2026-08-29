using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
using JingleBox2.Machines.Ui.Records;

namespace JingleBox2.Machines.Ui;

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

    /// <summary>
    /// How wide and how tall a raised key is, as a fraction of a white one.
    /// </summary>
    /// <remarks>
    /// The two happen to be the same number and mean different things: the width is measured
    /// against a white key's width, the height against the keyboard's height.
    /// </remarks>
    private const double RaisedWidth = 0.62;

    /// <inheritdoc cref="RaisedWidth"/>
    private const double RaisedHeight = 0.62;

    /// <summary>The air between the head, which is the arrows and lamps, and the keys under it.</summary>
    private const double HeadGap = 8;

    /// <summary>Between the caption and the lamps it names.</summary>
    private const double CaptionGap = 4;

    /// <summary>Between a lamp and its number, and between a C and the octave written under it.</summary>
    private const double NumberGap = 3;

    /// <summary>Between the row of lamps and the arrow at either end of it.</summary>
    private const double ArrowGap = 9;

    /// <summary>
    /// The four faces a key can wear when it is not sounding, made once.
    /// </summary>
    /// <remarks>
    /// Thirty-seven keys is thirty-seven gradients if each one builds its own, and a keyboard
    /// repaints every time a note starts or stops. Only the sounding keys need a brush of their
    /// own, and there are never many of those, so the rest share these four.
    ///
    /// A pressed key is the same gradient turned over, which is lighting it from below and is
    /// the whole of what makes a key look struck.
    /// </remarks>
    private static readonly IBrush WhiteUp = Face(Color.FromRgb(0xE8, 0xE8, 0xE4), Color.FromRgb(0xB4, 0xB4, 0xAE));

    /// <inheritdoc cref="WhiteUp"/>
    private static readonly IBrush WhiteDown = Face(Color.FromRgb(0xB4, 0xB4, 0xAE), Color.FromRgb(0xE8, 0xE8, 0xE4));

    /// <inheritdoc cref="WhiteUp"/>
    private static readonly IBrush RaisedUp = Face(Color.FromRgb(0x2A, 0x2C, 0x30), Color.FromRgb(0x14, 0x15, 0x18));

    /// <inheritdoc cref="WhiteUp"/>
    private static readonly IBrush RaisedDown = Face(Color.FromRgb(0x14, 0x15, 0x18), Color.FromRgb(0x2A, 0x2C, 0x30));

    /// <summary>
    /// The line round every key, near black and the same on both kinds.
    /// </summary>
    /// <remarks>
    /// Not the theme's border colour. A keyboard is white and black plastic whatever colour the
    /// rest of the application is wearing, and a pale line between the keys in a light theme
    /// would lose the gaps that make it read as a keyboard at all.
    /// </remarks>
    private static readonly IPen Edge = new Pen(new SolidColorBrush(Color.FromRgb(0x0C, 0x0D, 0x0F)), 1);

    /// <summary>
    /// Backs <see cref="Octave"/>: which octave the leftmost key is the C of, and which lamp is
    /// lit.
    /// </summary>
    /// <remarks>
    /// Two way, because the keyboard moves it itself: the arrows and the lamps are both ways of
    /// setting it, so a panel that bound it one way would watch its own keyboard walk away from
    /// whatever it thought it was showing.
    /// </remarks>
    public static readonly StyledProperty<int> OctaveProperty =
        AvaloniaProperty.Register<Clavier, int>(
            nameof(Octave), 4, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Backs <see cref="OctaveCount"/>: how many octaves the lamps count, which is how far the
    /// keyboard can travel.
    /// </summary>
    public static readonly StyledProperty<int> OctaveCountProperty =
        AvaloniaProperty.Register<Clavier, int>(nameof(OctaveCount), 10);

    /// <summary>
    /// Backs <see cref="KeyCount"/>: how many keys there are, counted in semitones.
    /// Thirty-seven is three octaves, which is what a panel has room for.
    /// </summary>
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

    /// <summary>
    /// The semitones that have something on them, as absolute note numbers.
    /// </summary>
    /// <remarks>
    /// A kit answers sixteen keys out of a hundred and twenty, and without this the keyboard
    /// under it is a hundred and four keys that do nothing with no way of telling which. Marked
    /// along the bottom of the key rather than by painting the key, because a key with a drum on
    /// it is still an ordinary key: it is not sounding and it is not the one in hand.
    /// </remarks>
    public static readonly StyledProperty<IEnumerable?> FilledProperty =
        AvaloniaProperty.Register<Clavier, IEnumerable?>(nameof(Filled));

    /// <summary>
    /// The one key the controls beside the keyboard are about, or -1 for none.
    /// </summary>
    /// <remarks>
    /// What makes a pad and a key the same thing to look at. Pick the snare on the grid and its
    /// key is outlined here, which is the question a kit's keyboard is there to answer: which
    /// note fires this drum.
    /// </remarks>
    public static readonly StyledProperty<int> MarkedProperty =
        AvaloniaProperty.Register<Clavier, int>(nameof(Marked), -1);

    /// <summary>Backs <see cref="Command"/>, run with the pressed key's absolute semitone.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<Clavier, ICommand?>(nameof(Command));

    /// <summary>
    /// Backs <see cref="ReleaseCommand"/>, run with that same semitone when the key is let go.
    /// </summary>
    /// <remarks>
    /// A key is down while a hand is on it and up when the hand comes off, which is not the same
    /// as how long what it started goes on sounding. A cymbal rings for four seconds; the key was
    /// down for a tenth of one.
    /// </remarks>
    public static readonly StyledProperty<ICommand?> ReleaseCommandProperty =
        AvaloniaProperty.Register<Clavier, ICommand?>(nameof(ReleaseCommand));

    /// <summary>Backs <see cref="KeyHeight"/>: how far a white key runs down the panel.</summary>
    public static readonly StyledProperty<double> KeyHeightProperty =
        AvaloniaProperty.Register<Clavier, double>(nameof(KeyHeight), 52.0);

    /// <summary>Backs <see cref="KeyWidth"/>: how wide one white key is, which sets the whole width.</summary>
    public static readonly StyledProperty<double> KeyWidthProperty =
        AvaloniaProperty.Register<Clavier, double>(nameof(KeyWidth), 18.0);

    /// <summary>Backs <see cref="LitColour"/>: what a key burns when it is sounding.</summary>
    public static readonly StyledProperty<Color> LitColourProperty =
        AvaloniaProperty.Register<Clavier, Color>(nameof(LitColour), Color.FromRgb(0xE5, 0xB3, 0x39));

    /// <summary>Backs <see cref="LampColour"/>: what the octave lamps burn, and what a filled key is banded with.</summary>
    public static readonly StyledProperty<Color> LampColourProperty =
        AvaloniaProperty.Register<Clavier, Color>(nameof(LampColour), Color.FromRgb(0xE5, 0xB3, 0x39));

    /// <summary>Backs <see cref="LampSize"/>: how big one octave lamp is across.</summary>
    public static readonly StyledProperty<double> LampSizeProperty =
        AvaloniaProperty.Register<Clavier, double>(nameof(LampSize), 9.0);

    /// <summary>Backs <see cref="LampGap"/>: the space between one lamp and the next.</summary>
    public static readonly StyledProperty<double> LampGapProperty =
        AvaloniaProperty.Register<Clavier, double>(nameof(LampGap), 9.0);

    /// <summary>
    /// Backs <see cref="Caption"/>, written over the lamps the way a panel names a section.
    /// </summary>
    /// <remarks>
    /// OCTAVE unless a machine says otherwise, which is what the row is on every keyboard that
    /// has one.
    /// </remarks>
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<Clavier, string?>(nameof(Caption), "OCTAVE");

    /// <summary>
    /// Backs <see cref="MarksOctaves"/>: whether the octave's number is written on each C, the
    /// way a keyboard is marked.
    /// </summary>
    public static readonly StyledProperty<bool> MarksOctavesProperty =
        AvaloniaProperty.Register<Clavier, bool>(nameof(MarksOctaves), true);

    /// <summary>Backs <see cref="FontSize"/>, the size of the caption and every number here.</summary>
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

    /// <summary>What <see cref="_laid"/> was worked out for, so it is known when it has gone stale.</summary>
    private (int First, int Count, double Width) _laidFor;

    /// <summary>The lit faces and the halo, remade only when the colour itself changes.</summary>
    private IBrush? _litWhite;

    /// <inheritdoc cref="_litWhite"/>
    private IBrush? _litRaised;

    /// <inheritdoc cref="_litWhite"/>
    private IBrush? _halo;

    /// <summary>Which colour those three were made from.</summary>
    private Color _litFor;

    /// <summary>
    /// The sounding collection this is currently following, where it announces itself.
    /// </summary>
    /// <remarks>
    /// Kept because the subscription has to come off the collection it went on, and by the time
    /// a different one is handed over the property already holds the new one.
    /// </remarks>
    private INotifyCollectionChanged? _watching;

    /// <summary>
    /// Says which properties change the picture and which change the size, and makes the
    /// keyboard focusable.
    /// </summary>
    /// <remarks>
    /// Focusable so the arrow keys can walk the octave, which is the one thing here a hand wants
    /// without reaching for the mouse. <see cref="LitProperty"/> and <see cref="MarkedProperty"/>
    /// change nothing about the room the keyboard takes and so are in the render list alone.
    /// </remarks>
    static Clavier()
    {
        AffectsRender<Clavier>(
            OctaveProperty, OctaveCountProperty, KeyCountProperty, LitProperty,
            FilledProperty, MarkedProperty,
            KeyHeightProperty, KeyWidthProperty, LitColourProperty, LampColourProperty,
            LampSizeProperty, LampGapProperty, CaptionProperty, MarksOctavesProperty,
            FontSizeProperty);

        AffectsMeasure<Clavier>(
            OctaveCountProperty, KeyCountProperty, KeyHeightProperty, KeyWidthProperty,
            LampSizeProperty, LampGapProperty, CaptionProperty, MarksOctavesProperty,
            FontSizeProperty);

        FocusableProperty.OverrideDefaultValue<Clavier>(true);
    }

    /// <inheritdoc cref="OctaveProperty"/>
    public int Octave
    {
        get => GetValue(OctaveProperty);
        set => SetValue(OctaveProperty, value);
    }

    /// <summary>How many octaves the lamps count, which is how far the keys can travel.</summary>
    public int OctaveCount
    {
        get => GetValue(OctaveCountProperty);
        set => SetValue(OctaveCountProperty, value);
    }

    /// <summary>How many keys there are, counted in semitones rather than in white keys.</summary>
    public int KeyCount
    {
        get => GetValue(KeyCountProperty);
        set => SetValue(KeyCountProperty, value);
    }

    /// <inheritdoc cref="LitProperty"/>
    public IEnumerable? Lit
    {
        get => GetValue(LitProperty);
        set => SetValue(LitProperty, value);
    }

    /// <inheritdoc cref="FilledProperty"/>
    public IEnumerable? Filled
    {
        get => GetValue(FilledProperty);
        set => SetValue(FilledProperty, value);
    }

    /// <inheritdoc cref="MarkedProperty"/>
    public int Marked
    {
        get => GetValue(MarkedProperty);
        set => SetValue(MarkedProperty, value);
    }

    /// <inheritdoc cref="ReleaseCommandProperty"/>
    public ICommand? ReleaseCommand
    {
        get => GetValue(ReleaseCommandProperty);
        set => SetValue(ReleaseCommandProperty, value);
    }

    /// <summary>Run with the pressed key's absolute semitone, which is what sounds it.</summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>How far a white key runs down the panel.</summary>
    public double KeyHeight
    {
        get => GetValue(KeyHeightProperty);
        set => SetValue(KeyHeightProperty, value);
    }

    /// <summary>How wide one white key is, which is what sets the keyboard's whole width.</summary>
    public double KeyWidth
    {
        get => GetValue(KeyWidthProperty);
        set => SetValue(KeyWidthProperty, value);
    }

    /// <summary>What a key burns when it is sounding, and what the key in hand is ringed with.</summary>
    public Color LitColour
    {
        get => GetValue(LitColourProperty);
        set => SetValue(LitColourProperty, value);
    }

    /// <summary>What the octave lamps burn, and what a key with something on it is banded with.</summary>
    public Color LampColour
    {
        get => GetValue(LampColourProperty);
        set => SetValue(LampColourProperty, value);
    }

    /// <summary>How big one octave lamp is across.</summary>
    public double LampSize
    {
        get => GetValue(LampSizeProperty);
        set => SetValue(LampSizeProperty, value);
    }

    /// <summary>The space between one lamp and the next.</summary>
    public double LampGap
    {
        get => GetValue(LampGapProperty);
        set => SetValue(LampGapProperty, value);
    }

    /// <inheritdoc cref="CaptionProperty"/>
    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>Whether the octave's number is written on each C.</summary>
    public bool MarksOctaves
    {
        get => GetValue(MarksOctavesProperty);
        set => SetValue(MarksOctavesProperty, value);
    }

    /// <summary>How big the caption and every number here are.</summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>The note the leftmost key sounds, which is the C of whichever octave is showing.</summary>
    private int FirstNote => Octave * 12;

    /// <summary>
    /// How many white keys the keyboard has, which is how wide it is.
    /// </summary>
    /// <remarks>
    /// Counted rather than divided, because the answer depends on which note the keyboard starts
    /// on: thirty-seven keys from C is twenty-two whites and from F sharp is twenty-one.
    /// </remarks>
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

    /// <summary>How wide the keys are altogether, which is the whole control's width.</summary>
    private double KeysWidth => Whites * KeyWidth;

    /// <summary>
    /// How big the two octave arrows are, sized off a lamp rather than fixed.
    /// </summary>
    /// <remarks>
    /// A machine that draws its lamps larger draws its arrows larger with them, or the head goes
    /// out of proportion the moment anybody changes one number.
    /// </remarks>
    private double ArrowWidth => LampSize * 2.4;

    /// <inheritdoc cref="ArrowWidth"/>
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

    /// <summary>
    /// How tall one line of writing is, measured off a digit.
    /// </summary>
    /// <remarks>
    /// Off a digit rather than off the text actually being drawn, so every row in the head sits
    /// at the same height whether or not it happens to have anything in it.
    /// </remarks>
    private double LineHeight => Text("0", Brushes.Black).Height;

    /// <summary>
    /// Follows the sounding collection itself, not only the property holding it.
    /// </summary>
    /// <remarks>
    /// Notes are added to the same collection rather than a new one being handed over each time,
    /// so a keyboard watching the property alone would light on the first note it was given and
    /// then never move again.
    /// </remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != LitProperty) return;

        if (_watching != null) _watching.CollectionChanged -= OnLitChanged;

        _watching = change.NewValue as INotifyCollectionChanged;

        if (_watching != null) _watching.CollectionChanged += OnLitChanged;
    }

    /// <summary>A note started or stopped, so the keys have to be painted again.</summary>
    private void OnLitChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    /// <summary>
    /// The keys' width, and the head, the keys and the octave numbers stacked down the panel.
    /// </summary>
    /// <remarks>
    /// The halo a sounding key spills is not measured. It reaches three pixels past the key on
    /// every side and is painted over whatever is beside it, the same way a lamp's is.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize) =>
        new(KeysWidth, HeadHeight + HeadGap + KeyHeight + (MarksOctaves ? NumberGap + LineHeight : 0));

    /// <summary>The head, then the keys under it.</summary>
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

    /// <summary>
    /// One octave arrow, moulded so that a held one reads as pressed in.
    /// </summary>
    /// <remarks>
    /// Held, the gradient is turned over and darkened at the top, which is the same trick the
    /// keys use: a thing lit from below looks like a thing that has gone down.
    ///
    /// Its own grey rather than the theme's, since these are buttons on a machine's front panel
    /// and are the same colour on every machine and in every theme.
    /// </remarks>
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

    /// <summary>
    /// The keys, and the octave number under each C.
    /// </summary>
    /// <remarks>
    /// The white keys go down first, whole, and the raised ones over them. Drawn the other way
    /// round a raised key would be sitting under its neighbours instead of on top of them.
    /// </remarks>
    private void DrawKeys(DrawingContext context, ThemePalette palette)
    {
        double top = HeadHeight + HeadGap;
        double height = KeyHeight;

        var laid = Keys();
        var sounding = Sounding();
        var loaded = Occupied();

        for (int i = 0; i < laid.Length; i++)
        {
            var (left, width, raised, note) = laid[i];
            if (raised) continue;

            var key = new Rect(left, top, width, height);

            Draw(
                context, key, raised: false, sounding.Contains(note), pressed: _pressed == i,
                filled: loaded.Contains(note), marked: note == Marked);

            if (!MarksOctaves || Mod12(note) != 0) continue;

            var mark = Text((note / 12).ToString(CultureInfo.CurrentCulture), palette.MutedBrush);
            context.DrawText(mark, new Point(key.Center.X - mark.Width / 2, key.Bottom + NumberGap));
        }

        for (int i = 0; i < laid.Length; i++)
        {
            var (left, width, raised, note) = laid[i];
            if (!raised) continue;

            var key = new Rect(left, top, width, height * RaisedHeight);

            Draw(
                context, key, raised: true, sounding.Contains(note), pressed: _pressed == i,
                filled: loaded.Contains(note), marked: note == Marked);
        }
    }

    /// <summary>
    /// One key, in whichever of its states it is in.
    /// </summary>
    /// <remarks>
    /// A sounding key is the panel's own colour rather than a coloured rectangle: it is the same
    /// key with a light behind it, and it spills onto the ones beside it the way a lamp does.
    /// Pressed, it is lit from below instead, which is the whole of what makes a key look
    /// struck.
    ///
    /// A key with something on it says so along its bottom edge, where nothing else is drawn and
    /// where the eye reads a whole row of them at once, rather than by painting the key: a key
    /// with a drum on it is still an ordinary key, neither sounding nor in hand.
    ///
    /// The one in hand is outlined, the same way the pad it belongs to is.
    /// </remarks>
    private void Draw(
        DrawingContext context, Rect key, bool raised, bool lit, bool pressed,
        bool filled = false, bool marked = false)
    {
        double round = raised ? 2 : 3;

        var face = lit
            ? Burning(raised)
            : raised
                ? pressed ? RaisedDown : RaisedUp
                : pressed ? WhiteDown : WhiteUp;

        context.DrawRectangle(face, Edge, key, round, round);

        if (lit) context.DrawRectangle(Halo(), null, key.Inflate(3), round, round);

        if (filled)
        {
            double inset = raised ? 2 : 3;
            double band = raised ? 3 : 4;

            context.DrawRectangle(
                Marking(),
                null,
                new Rect(key.X + inset, key.Bottom - band - inset, key.Width - inset * 2, band),
                1, 1);
        }

        if (marked) context.DrawRectangle(null, Outline(), key.Deflate(0.5), round, round);
    }

    /// <summary>What a key with something on it is banded with, and what the one in hand is ringed with.</summary>
    /// <remarks>
    /// The machine's own colour, which is what the lamps and the lit keys already use. A kit
    /// painted red should not have its keyboard marked in somebody else's amber.
    /// </remarks>
    private IBrush Marking() => new SolidColorBrush(LampColour, 0.75);

    /// <summary>What the key in hand is ringed with: the colour a sounding key burns.</summary>
    private IPen Outline() => new Pen(new SolidColorBrush(LitColour), 2);

    /// <summary>The sounding face, kept until the colour it is made from changes.</summary>
    private IBrush Burning(bool raised)
    {
        Refresh();

        return raised ? _litRaised! : _litWhite!;
    }

    /// <summary>The wash a sounding key spills onto its neighbours.</summary>
    private IBrush Halo()
    {
        Refresh();

        return _halo!;
    }

    /// <summary>
    /// Makes the three lit brushes, and only when the colour they are made from has moved.
    /// </summary>
    /// <remarks>
    /// A keyboard repaints every time a note starts or stops, and the colour almost never
    /// changes, so building these per paint would be three gradients per frame for nothing.
    /// </remarks>
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

    /// <summary>
    /// Which of the twelve a note is, never negative however far below zero it is.
    /// </summary>
    /// <remarks>
    /// C# gives a negative remainder for a negative dividend, which would index the raised-key
    /// table from the wrong end. Nothing here should reach a negative note, but the keyboard is
    /// drawn from whatever octave it is handed.
    /// </remarks>
    private static int Mod12(int note) => ((note % 12) + 12) % 12;

    /// <summary>How far apart the lamps are, and how wide the row of them is altogether.</summary>
    private double LampPitch() => LampSize + LampGap;

    /// <inheritdoc cref="LampPitch"/>
    private double LampsWidth() => LampPitch() * OctaveCount - LampGap;

    /// <summary>Where the arrows and lamps begin, which is under the caption where there is one.</summary>
    private double HeadTop => string.IsNullOrEmpty(Caption) ? 0 : LineHeight + CaptionGap;

    /// <summary>
    /// Where the two octave arrows sit, one either side of the lamps.
    /// </summary>
    /// <remarks>
    /// Worked out in one place rather than at the drawing and again at the press, or the arrow
    /// somebody sees and the arrow they can hit would drift apart.
    /// </remarks>
    private Rect LeftArrow() =>
        new(KeysWidth / 2 - LampsWidth() / 2 - ArrowGap - ArrowWidth, HeadTop, ArrowWidth, ArrowHeight);

    /// <inheritdoc cref="LeftArrow"/>
    private Rect RightArrow() =>
        new(KeysWidth / 2 + LampsWidth() / 2 + ArrowGap, HeadTop, ArrowWidth, ArrowHeight);

    /// <summary>And the ones with something on them, read the same way and for the same reason.</summary>
    private HashSet<int> Occupied()
    {
        var held = new HashSet<int>();

        if (Filled == null) return held;

        foreach (var item in Filled)
            if (item is int semitone) held.Add(semitone);

        return held;
    }

    /// <summary>The semitones sounding, read once so the keys do not walk it apiece.</summary>
    private HashSet<int> Sounding()
    {
        var sounding = new HashSet<int>();

        if (Lit == null) return sounding;

        foreach (var item in Lit)
            if (item is int semitone) sounding.Add(semitone);

        return sounding;
    }

    /// <summary>
    /// An arrow, a lamp, or a key, in that order.
    /// </summary>
    /// <remarks>
    /// A lamp is a place to go rather than a report: pressing one takes the keys there, which is
    /// quicker than pressing an arrow five times.
    /// </remarks>
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

    /// <summary>
    /// Dragging across the keys plays them, the way a finger down a keyboard does.
    /// </summary>
    /// <remarks>
    /// Sliding off one key onto the next lets the first go before the second is played, so the
    /// two halves of a key press stay in order and nothing is left holding a note nobody is on.
    /// </remarks>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_pressed < 0 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        int now = KeyAt(e.GetPosition(this));
        if (now < 0 || now == _pressed) return;

        Let(_pressed);

        _pressed = now;
        Play(now);
        InvalidateVisual();
    }

    /// <summary>Lets go of whatever was held: a key is released, an arrow simply comes back up.</summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_pressed < 0 && _arrow == 0) return;

        Let(_pressed);

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

    /// <summary>
    /// Moves the keyboard by an octave, stopping at the ends.
    /// </summary>
    /// <remarks>
    /// Stopping rather than coming round, since an arrow held down that wrapped would carry the
    /// hand past the octave it was looking for.
    /// </remarks>
    private void Step(int by) => Octave = Math.Clamp(Octave + by, 0, Math.Max(0, OctaveCount - 1));

    /// <summary>Sounds a key, if anybody is listening for it.</summary>
    private void Play(int key)
    {
        int note = FirstNote + key;

        if (Command?.CanExecute(note) == true) Command.Execute(note);
    }

    /// <summary>Says that key is up again, if anybody asked to be told.</summary>
    private void Let(int key)
    {
        if (key < 0) return;

        int note = FirstNote + key;

        if (ReleaseCommand?.CanExecute(note) == true) ReleaseCommand.Execute(note);
    }

    /// <summary>
    /// Which lamp is under a point, or -1 for none.
    /// </summary>
    /// <remarks>
    /// The whole column under a lamp counts, its number included: nine pixels across is not a
    /// target anybody can hit.
    /// </remarks>
    private int LampAt(Point point)
    {
        double left = KeysWidth / 2 - LampsWidth() / 2;
        double top = HeadTop + Math.Max(0, (ArrowHeight - LampSize) / 2);

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

    /// <summary>The caption or a number, laid out for measuring or for drawing.</summary>
    private FormattedText Text(string? text, IBrush brush) =>
        new(text ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), FontSize, brush);

    /// <summary>
    /// A key face: a gradient down from the top colour to the bottom one.
    /// </summary>
    /// <remarks>
    /// Frozen, since the four unlit faces are shared by every keyboard in the application and a
    /// brush that can still be written to cannot be.
    /// </remarks>
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

    /// <summary>
    /// A colour taken towards white or towards black, for the lit faces and the arrows' moulding.
    /// </summary>
    /// <remarks>
    /// Its own copy rather than <see cref="ThemePalette.Shade"/>, because none of the colours it
    /// is used on here comes from the theme.
    /// </remarks>
    private static Color Lighten(Color colour, double amount)
    {
        double Mix(byte channel) => amount >= 0
            ? channel + (255 - channel) * amount
            : channel * (1 + amount);

        return Color.FromRgb((byte)Mix(colour.R), (byte)Mix(colour.G), (byte)Mix(colour.B));
    }
}
