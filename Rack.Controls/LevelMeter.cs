using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Diagnostics;
using JingleBox2.Rack.Controls.Records;
using JingleBox2.Rack.Controls.Interfaces;

namespace JingleBox2.Rack.Controls;

/// <summary>
/// How loud something is, right now. One bar for a mono signal, two for a stereo one, either
/// way up. Used for a track, a recording input, or anything else with a level.
/// </summary>
/// <remarks>
/// The bar is on a decibel scale, because a linear one spends most of its length on the top
/// few decibels. The colours are the meter convention rather than the theme's: green, amber
/// and red mean the same thing in every studio, and a theme should not be able to say
/// otherwise. Only the quiet end follows the accent.
/// </remarks>
public class LevelMeter : ThemedControl
{
    /// <summary>Where a level sits on a meter, which is decibels rather than amplitude.</summary>
    private readonly IMeterScale _scale = new MeterScale();

    /// <summary>Minus six decibels, above which the signal is close enough to the ceiling to warn about.</summary>
    private const double WarnAmplitude = 0.5;

    /// <summary>Minus one decibel: near enough to the top that the next transient will be over it.</summary>
    private const double HotAmplitude = 0.89;

    /// <summary>
    /// How long the peak mark sits still before it starts to come down.
    /// </summary>
    /// <remarks>
    /// Long enough to read a transient that was gone before the eye reached the meter, short
    /// enough that the mark is still describing the present.
    /// </remarks>
    private const double PeakHoldSeconds = 1.2;

    /// <summary>How fast the mark falls once the hold is over.</summary>
    private const double PeakFallDecibelsPerSecond = 20;

    /// <summary>
    /// The two warning colours, fixed rather than taken from the theme.
    /// </summary>
    /// <remarks>
    /// Amber and red mean the same thing on every meter in every studio, and a theme should not
    /// be able to say otherwise. Only the quiet end of the bar follows the accent.
    /// </remarks>
    private static readonly Color Warn = Color.FromRgb(0xFD, 0xD8, 0x35);

    /// <inheritdoc cref="Warn"/>
    private static readonly Color Hot = Color.FromRgb(0xE5, 0x39, 0x35);

    /// <summary>Backs <see cref="Left"/>, and is the only bar a mono meter draws.</summary>
    public static readonly StyledProperty<double> LeftProperty =
        AvaloniaProperty.Register<LevelMeter, double>(nameof(Left));

    /// <summary>Backs <see cref="Right"/>, read only when <see cref="Stereo"/> is set.</summary>
    public static readonly StyledProperty<double> RightProperty =
        AvaloniaProperty.Register<LevelMeter, double>(nameof(Right));

    /// <summary>
    /// Backs <see cref="Stereo"/>: two bars instead of one. A mono source leaves this off and
    /// uses <see cref="Left"/> alone.
    /// </summary>
    public static readonly StyledProperty<bool> StereoProperty =
        AvaloniaProperty.Register<LevelMeter, bool>(nameof(Stereo));

    /// <summary>
    /// Backs <see cref="Orientation"/>: upright, which is a mixer strip, or on its side, which
    /// is a status bar.
    /// </summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<LevelMeter, Orientation>(nameof(Orientation), Orientation.Vertical);

    /// <summary>
    /// Backs <see cref="MinimumDecibels"/>, the bottom of the scale.
    /// </summary>
    /// <remarks>
    /// Minus sixty unless a panel says otherwise, which is quiet enough to be a floor without
    /// hiding a soft take under it.
    /// </remarks>
    public static readonly StyledProperty<double> MinimumDecibelsProperty =
        AvaloniaProperty.Register<LevelMeter, double>(nameof(MinimumDecibels), MeterScale.DefaultMinimumDecibels);

    /// <summary>
    /// Whether the meter carries a clip light: a small mark at the loud end, lit when what it
    /// was shown went past full scale.
    /// </summary>
    /// <remarks>
    /// On the meter rather than beside it, and drawn by the meter rather than dropped next to
    /// one, because a meter that shows level and not overload is half a meter, and because there
    /// is more than one meter on a desk: a light per strip assembled by hand is the same three
    /// lines written three times and one of them eventually forgotten.
    ///
    /// It can be turned off for a meter where it would be noise, which is any meter reading
    /// something that cannot clip.
    /// </remarks>
    public static readonly StyledProperty<bool> ShowClipProperty =
        AvaloniaProperty.Register<LevelMeter, bool>(nameof(ShowClip), true);

    /// <summary>
    /// Backs <see cref="ShowPeak"/>: a mark riding the loudest recent moment, so a transient is
    /// readable after it has gone.
    /// </summary>
    public static readonly StyledProperty<bool> ShowPeakProperty =
        AvaloniaProperty.Register<LevelMeter, bool>(nameof(ShowPeak), true);

    /// <summary>
    /// What the fall of the peak mark is measured against.
    /// </summary>
    /// <remarks>
    /// Real time rather than a count of frames, so the mark comes down at the same rate whatever
    /// the window is managing, and a dropped frame costs a frame rather than stretching the fall.
    /// </remarks>
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    /// <summary>Where each mark stands, and when it was last pushed up there.</summary>
    private double _leftPeak;

    /// <inheritdoc cref="_leftPeak"/>
    private double _rightPeak;

    /// <inheritdoc cref="_leftPeak"/>
    private double _leftPeakAt;

    /// <inheritdoc cref="_leftPeak"/>
    private double _rightPeakAt;

    /// <summary>Whether a frame has already been asked for, so one is not asked for twice.</summary>
    private bool _waiting;

    /// <summary>
    /// The colours the kept brushes were built from, or nothing while none are kept.
    /// </summary>
    /// <remarks>
    /// **Everything below is kept because this control is drawn many times a second.** A brush
    /// or a pen made inside <c>Render</c> becomes a native drawing object that is made on every
    /// frame and let go of by the finalizer afterwards: a mixer page carries a dozen of these
    /// meters, each one was making about ten of them per frame, and the cost turned up in a
    /// profile of the running application as the finalizer thread and the render loop.
    ///
    /// They are let go of in <see cref="ThemeMoved"/>, which is the one moment the colours they
    /// were built from stop being true, and rebuilt on the next frame. The reading itself is not
    /// in here: what moves is the length of the bar, not what it is painted with.
    /// </remarks>
    private ThemePalette? _painted;

    /// <summary>How far down the scale the kept gradients were built for.</summary>
    /// <remarks>
    /// The middle stop of the gradient sits where the warning level falls on the scale, which
    /// moves with the meter's own floor. Kept beside the colours so a strip with a different
    /// floor rebuilds rather than drawing somebody else's gradient.
    /// </remarks>
    private double _paintedFloor;

    /// <summary>The trough a bar is drawn in, and the line round it.</summary>
    private IBrush? _trough;

    /// <inheritdoc cref="_trough"/>
    private IPen? _rim;

    /// <summary>The three colours a level is worth, as brushes: quiet, warm, and over.</summary>
    private IBrush? _quiet;

    /// <inheritdoc cref="_quiet"/>
    private IBrush? _warm;

    /// <inheritdoc cref="_quiet"/>
    private IBrush? _over;

    /// <summary>The two gradients a bar is filled with once it is past the warning level.</summary>
    private IBrush? _warmFill;

    /// <inheritdoc cref="_warmFill"/>
    private IBrush? _overFill;

    /// <summary>
    /// Says which properties change the picture. None of them changes the size.
    /// </summary>
    /// <remarks>
    /// **The two readings are deliberately not in here.** They arrive twenty times a second and
    /// almost never move the bar: what changes is the fourth decimal of a level, and repainting
    /// for that is a dozen meters redrawn sixty times a second to show the same picture. They go
    /// through <see cref="Moved"/> instead, which asks whether the bar would land anywhere new.
    /// </remarks>
    static LevelMeter()
    {
        AffectsRender<LevelMeter>(
            StereoProperty, OrientationProperty, ShowClipProperty,
            MinimumDecibelsProperty, ShowPeakProperty);
    }

    /// <summary>Which pixel each bar was last drawn to, so a reading that moves none is dropped.</summary>
    private int _shownLeft = -1;

    /// <inheritdoc cref="_shownLeft"/>
    private int _shownRight = -1;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LeftProperty || change.Property == RightProperty) Moved();
    }

    /// <summary>
    /// Asks to be drawn again, but only where the picture would be different.
    /// </summary>
    /// <remarks>
    /// Two things count as different. The bar landing on another pixel is the ordinary one; a
    /// reading at or over full scale is the other, and it is checked separately because the top
    /// of the bar is one pixel however far past it the reading goes, and the clip lamp latches
    /// on the reading rather than on the picture. A clip missed because the bar was already
    /// against the top is the one thing this may not do.
    /// </remarks>
    private void Moved()
    {
        if (!Seen()) return;

        var room = Bars(Bounds.Width, Bounds.Height);

        double along = Orientation == Orientation.Vertical ? room.Height : room.Width;

        if (ShowClip && Loudest() >= MeterScale.ClipAmplitude)
        {
            InvalidateVisual();

            return;
        }

        int left = _scale.Step(Left, MinimumDecibels, along);
        int right = Stereo ? _scale.Step(Right, MinimumDecibels, along) : 0;

        if (left == _shownLeft && right == _shownRight) return;

        _shownLeft = left;
        _shownRight = right;

        InvalidateVisual();
    }

    /// <inheritdoc cref="ShowClipProperty"/>
    public bool ShowClip
    {
        get => GetValue(ShowClipProperty);
        set => SetValue(ShowClipProperty, value);
    }

    /// <summary>Whether what this has been shown went past full scale, and for how long after.</summary>
    private readonly IClipHold _clip = new ClipHold();

    /// <summary>How wide across the clip lamp is drawn, at the loud end of the bar.</summary>
    /// <remarks>
    /// As wide as a narrow meter will take, and round rather than a cap across the bar: a cap reads as the bar having run out of room and a lamp reads as a lamp.
    /// Lit it is drawn through <see cref="Led.DrawLamp"/>, the same call the panels' own lamps
    /// go through, so it gets the halo they have. That is most of what makes a clip light work,
    /// since nobody is looking straight at it when it fires.
    /// </remarks>
    private const double ClipMark = 11;

    /// <summary>How far the lamp stands off the bar, so the two read as two things.</summary>
    private const double ClipGap = 2;

    /// <summary>
    /// Puts the clip light out. A press on the meter is how a desk does it.
    /// </summary>
    /// <remarks>
    /// Not marked handled, so a press that also means something to whatever the meter is
    /// standing on still reaches it: on the mixer, touching a strip anywhere picks its track,
    /// and a meter that swallowed that would make the light cost you the selection.
    /// </remarks>
    /// <param name="e">The press.</param>
    protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        _clip.Clear();

        InvalidateVisual();
    }

    /// <summary>How loud the left is, nought to one as amplitude rather than as decibels.</summary>
    public double Left
    {
        get => GetValue(LeftProperty);
        set => SetValue(LeftProperty, value);
    }

    /// <summary>The same for the right, and read only on a stereo meter.</summary>
    public double Right
    {
        get => GetValue(RightProperty);
        set => SetValue(RightProperty, value);
    }

    /// <inheritdoc cref="StereoProperty"/>
    public bool Stereo
    {
        get => GetValue(StereoProperty);
        set => SetValue(StereoProperty, value);
    }

    /// <inheritdoc cref="OrientationProperty"/>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <inheritdoc cref="MinimumDecibelsProperty"/>
    public double MinimumDecibels
    {
        get => GetValue(MinimumDecibelsProperty);
        set => SetValue(MinimumDecibelsProperty, value);
    }

    /// <inheritdoc cref="ShowPeakProperty"/>
    public bool ShowPeak
    {
        get => GetValue(ShowPeakProperty);
        set => SetValue(ShowPeakProperty, value);
    }

    /// <summary>
    /// Draws one bar, or two with a hair between them so a lopsided mix is obvious at a glance,
    /// and asks for another frame while a peak mark still has somewhere to fall to.
    /// </summary>
    /// <remarks>
    /// A NaN level is read as silence rather than being drawn. Levels arrive off the audio side
    /// and a bar drawn from a NaN is a bar that is neither empty nor full.
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 1 || height <= 1) return;

        var palette = Paint();
        double now = _clock.Elapsed.TotalSeconds;

        bool over = ShowClip && _clip.Saw(Loudest(), now);

        double left = Math.Clamp(double.IsNaN(Left) ? 0 : Left, 0, 1);
        double right = Stereo ? Math.Clamp(double.IsNaN(Right) ? 0 : Right, 0, 1) : 0;

        _leftPeak = Track(left, ref _leftPeak, ref _leftPeakAt, now);
        if (Stereo) _rightPeak = Track(right, ref _rightPeak, ref _rightPeakAt, now);

        if (Falling(left, _leftPeak) || (Stereo && Falling(right, _rightPeak))) NextFrame();

        var room = Bars(width, height);

        if (!Stereo)
        {
            DrawBar(context, palette, room, left, _leftPeak);
            DrawClip(context, palette, width, height, over);
            return;
        }

        double gap = 2;

        if (Orientation == Orientation.Vertical)
        {
            double each = (room.Width - gap) / 2;
            DrawBar(context, palette, new Rect(room.X, room.Y, each, room.Height), left, _leftPeak);
            DrawBar(context, palette, new Rect(room.X + each + gap, room.Y, each, room.Height), right, _rightPeak);
        }
        else
        {
            double each = (room.Height - gap) / 2;
            DrawBar(context, palette, new Rect(room.X, room.Y, room.Width, each), left, _leftPeak);
            DrawBar(context, palette, new Rect(room.X, room.Y + each + gap, room.Width, each), right, _rightPeak);
        }

        DrawClip(context, palette, width, height, over);
    }

    /// <summary>
    /// The loudest of what it was shown, before anything is clamped.
    /// </summary>
    /// <remarks>
    /// Before, deliberately: the drawing clamps to full scale because there is no more bar to
    /// fill, and a light worked out from the clamped number could never say anything, since
    /// everything over one arrives as one.
    /// </remarks>
    private double Loudest()
    {
        double left = Left;
        double right = Stereo ? Right : 0;

        if (double.IsNaN(left) || double.IsNaN(right)) return double.NaN;

        return Math.Max(left, right);
    }

    /// <summary>
    /// Draws the clip mark at the loud end of the meter.
    /// </summary>
    /// <remarks>
    /// At the end the bar fills towards, which is where an eye following the level already is.
    /// Over the bar rather than beside it, since the meter is often the narrowest thing on a
    /// strip and there is no room beside it. It is a lamp and is drawn as one, so it reads as a
    /// light rather than as the bar having run out of room: see <see cref="Lamp"/>.
    /// </remarks>
    /// <param name="context">Where it is drawn.</param>
    /// <param name="palette">The theme's colours.</param>
    /// <param name="width">How wide the meter is.</param>
    /// <param name="height">And how tall.</param>
    /// <param name="lit">Whether it is lit at this moment.</param>
    private void DrawClip(DrawingContext context, ThemePalette palette, double width, double height, bool lit)
    {
        if (!ShowClip) return;

        var shape = Lamp(width, height);

        if (shape.Width <= 0 || shape.Height <= 0) return;

        double radius = Math.Min(ClipMark, Math.Min(shape.Width, shape.Height)) / 2;

        var centre = new Point(shape.X + shape.Width / 2, shape.Y + shape.Height / 2);

        Led.DrawLamp(context, centre, radius, palette.Danger, lit);
    }

    /// <summary>
    /// Where the lamp sits: at the end the bar fills towards.
    /// </summary>
    /// <remarks>
    /// Both states go through <see cref="Led.DrawLamp"/>, which is what makes this the same lamp
    /// a machine's face carries rather than one that merely looks like it: the dome, the rim, the
    /// gloss and the halo are drawn once for the whole application, and the dark state is that
    /// same lamp with its colour dimmed rather than a circle drawn another way.
    ///
    /// **A lamp nobody can find while it is off is one nobody trusts when it is on**, which is
    /// why it is a round lamp with a halo rather than a few pixels at low opacity or a cap across
    /// the bar. The whole value of a clip light is knowing it was there and dark a moment ago.
    /// </remarks>
    /// <param name="width">How wide the meter is.</param>
    /// <param name="height">And how tall.</param>
    private Rect Lamp(double width, double height) =>
        Orientation == Orientation.Vertical
            ? new Rect(0, 0, width, ClipMark)
            : new Rect(width - ClipMark, 0, ClipMark, height);

    /// <summary>What is left for the bars once the lamp has had its room.</summary>
    /// <remarks>
    /// Taken out of the meter rather than drawn over it, or the loudest part of the bar, which
    /// is the part somebody is looking at when it matters, would be behind the lamp.
    /// </remarks>
    /// <param name="width">How wide the meter is.</param>
    /// <param name="height">And how tall.</param>
    private Rect Bars(double width, double height) =>
        !ShowClip
            ? new Rect(0, 0, width, height)
            : Orientation == Orientation.Vertical
                ? new Rect(0, ClipMark + ClipGap, width, Math.Max(0, height - ClipMark - ClipGap))
                : new Rect(0, 0, Math.Max(0, width - ClipMark - ClipGap), height);

    /// <summary>Whether the mark is above the bar, and so has somewhere left to fall to.</summary>
    private bool Falling(double level, double peak) =>
        ShowPeak
        && _scale.Position(peak, MinimumDecibels) > _scale.Position(level, MinimumDecibels);

    /// <summary>
    /// Asks to be drawn once more, because the mark is still coming down.
    /// </summary>
    /// <remarks>
    /// Everything else here is drawn because a value changed. The fall is the one thing that
    /// happens while nothing changes, and it is worked out during the drawing, so a meter that
    /// nobody invalidates simply stops: the bar empties when the last level arrives and the mark
    /// hangs where the loudest moment left it, for the rest of the session. That is what it
    /// looks like when a meter "does not go down".
    ///
    /// A frame rather than a timer of its own, so the fall runs at whatever rate the window is
    /// drawing at and costs nothing at all once the mark is on the floor.
    /// </remarks>
    private void NextFrame()
    {
        if (_waiting || !Seen() || TopLevel.GetTopLevel(this) is not { } top) return;

        _waiting = true;

        top.RequestAnimationFrame(_ =>
        {
            _waiting = false;
            InvalidateVisual();
        });
    }

    /// <summary>
    /// Whether any of this meter is somewhere somebody could see it.
    /// </summary>
    /// <remarks>
    /// **A meter that is not on the screen is still being told what the audio is doing**, and
    /// before this it asked for a frame about it. There are two ways to be out of sight and both
    /// happen constantly here: a page that is not the tab in front, and a strip scrolled off the
    /// side of the mixer, which is most of them on a song with a dozen tracks.
    ///
    /// It matters more than the drawing it saves. A profile of the running application says the
    /// render loop spends about two fifths of its time starting and finishing each frame, before
    /// anything is drawn at all, so a frame asked for by something nobody can see costs that
    /// whole fixed price to show nothing.
    ///
    /// The window's own edge is what it is measured against rather than any scroller in between,
    /// since a strip clipped away by one is outside the window too, and asking the window is one
    /// question rather than a walk up the tree.
    /// </remarks>
    /// <returns>True where some of it is inside the window.</returns>
    private bool Seen()
    {
        if (!IsEffectivelyVisible || Bounds.Width < 1 || Bounds.Height < 1) return false;

        if (TopLevel.GetTopLevel(this) is not { } window) return false;

        if (this.TransformToVisual(window) is not { } onto) return false;

        return new Rect(Bounds.Size).TransformToAABB(onto).Intersects(new Rect(window.Bounds.Size));
    }

    /// <summary>Follows the level up at once, and back down at a readable rate.</summary>
    private double Track(double level, ref double peak, ref double peakAt, double now)
    {
        if (level >= peak) peakAt = now;

        return _scale.DecayPeak(peak, level, now - peakAt, PeakHoldSeconds, PeakFallDecibelsPerSecond);
    }

    /// <summary>One bar: its trough, the part of it that is lit, and the mark over that.</summary>
    private void DrawBar(DrawingContext context, ThemePalette palette, Rect area, double level, double peak)
    {
        if (area.Width <= 0 || area.Height <= 0) return;

        double radius = Math.Min(3, Math.Min(area.Width, area.Height) / 2);

        context.DrawRectangle(_trough, _rim, new RoundedRect(area, radius));

        double filled = _scale.Position(level, MinimumDecibels);
        if (filled > 0)
            context.DrawRectangle(Fill(level), null, new RoundedRect(Portion(area, filled), radius));

        if (!ShowPeak || peak <= 0) return;

        double at = _scale.Position(peak, MinimumDecibels);
        DrawPeakMark(context, palette, area, at, peak);
    }

    /// <summary>The part of the bar that is lit, measured from the quiet end.</summary>
    private Rect Portion(Rect area, double fraction)
    {
        if (Orientation == Orientation.Vertical)
        {
            double tall = area.Height * fraction;
            return new Rect(area.X, area.Bottom - tall, area.Width, tall);
        }

        return new Rect(area.X, area.Y, area.Width * fraction, area.Height);
    }

    /// <summary>
    /// The two-pixel mark riding the loudest recent moment.
    /// </summary>
    /// <remarks>
    /// Held inside the bar's own ends, so a mark sitting at the top is drawn inside the trough
    /// rather than half over the border above it.
    /// </remarks>
    private void DrawPeakMark(DrawingContext context, ThemePalette palette, Rect area, double at, double peak)
    {
        if (Worth(peak) is not { } brush) return;

        if (Orientation == Orientation.Vertical)
        {
            double y = Math.Clamp(area.Bottom - area.Height * at, area.Y, area.Bottom - 2);
            context.FillRectangle(brush, new Rect(area.X, y, area.Width, 2));
            return;
        }

        double x = Math.Clamp(area.X + area.Width * at, area.X, area.Right - 2);
        context.FillRectangle(brush, new Rect(x, area.Y, 2, area.Height));
    }

    /// <summary>Which of the kept fills a level is worth.</summary>
    /// <param name="level">The reading, nought to one.</param>
    /// <returns>The brush, or nothing before the first painting.</returns>
    private IBrush? Fill(double level) =>
        level < WarnAmplitude ? _quiet
        : level >= MeterScale.ClipAmplitude || level >= HotAmplitude ? _overFill
        : _warmFill;

    /// <summary>Which of the three kept brushes a level is worth.</summary>
    /// <param name="level">The reading, nought to one.</param>
    /// <returns>The brush, or nothing before the first painting.</returns>
    private IBrush? Worth(double level) =>
        level >= MeterScale.ClipAmplitude || level >= HotAmplitude ? _over
        : level >= WarnAmplitude ? _warm
        : _quiet;

    /// <summary>
    /// The colours this is drawn in, built once and kept until the theme moves.
    /// </summary>
    /// <remarks>
    /// The gradient is the expensive one and is the reason this exists: three stops and a brush
    /// object per bar per frame, where the only thing that actually changes between frames is
    /// how much of the bar is filled in.
    /// </remarks>
    /// <returns>The palette, which the drawing still reads colours out of directly.</returns>
    private ThemePalette Paint()
    {
        var palette = ThemePalette.From(this);

        if (_painted is { } was && was.Equals(palette) && Math.Abs(_paintedFloor - MinimumDecibels) < 0.001)
            return palette;

        _painted = palette;
        _paintedFloor = MinimumDecibels;

        _trough = new SolidColorBrush(palette.Background);
        _rim = new Pen(new SolidColorBrush(palette.Border), 1);

        _quiet = new SolidColorBrush(palette.Accent);
        _warm = new SolidColorBrush(Warn);
        _over = new SolidColorBrush(Hot);

        _warmFill = Gradient(palette, Warn);
        _overFill = Gradient(palette, Hot);

        return palette;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Everything kept goes, since every one of them was built out of colours that have just
    /// stopped being true. The next frame builds them again.
    /// </remarks>
    protected override void ThemeMoved()
    {
        base.ThemeMoved();

        _painted = null;
    }

    /// <summary>
    /// A gradient rather than one flat colour, so the top of a loud bar reddens while the
    /// quiet part stays where it was: the eye reads the change, not just the height.
    /// </summary>
    /// <param name="palette">The theme's colours.</param>
    /// <param name="top">What the loud end of the bar goes.</param>
    /// <returns>The brush.</returns>
    private IBrush Gradient(ThemePalette palette, Color top)
    {
        var start = Orientation == Orientation.Vertical
            ? new RelativePoint(0.5, 1, RelativeUnit.Relative)
            : new RelativePoint(0, 0.5, RelativeUnit.Relative);

        var end = Orientation == Orientation.Vertical
            ? new RelativePoint(0.5, 0, RelativeUnit.Relative)
            : new RelativePoint(1, 0.5, RelativeUnit.Relative);

        return new LinearGradientBrush
        {
            StartPoint = start,
            EndPoint = end,
            GradientStops =
            {
                new GradientStop(palette.Accent, 0),
                new GradientStop(palette.Accent, _scale.Position(WarnAmplitude, MinimumDecibels)),
                new GradientStop(top, 1)
            }
        };
    }

    /// <summary>What a level is worth: the accent while it is quiet, then amber, then red.</summary>
    private static Color ColourFor(ThemePalette palette, double level) =>
        level >= MeterScale.ClipAmplitude || level >= HotAmplitude
            ? Hot
            : level >= WarnAmplitude ? Warn : palette.Accent;
}
