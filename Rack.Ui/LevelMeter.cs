using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Diagnostics;
using JingleBox2.Rack.Ui.Records;
using JingleBox2.Rack.Ui.Interfaces;

namespace JingleBox2.Rack.Ui;

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

    /// <summary>Says which properties change the picture. None of them changes the size.</summary>
    static LevelMeter()
    {
        AffectsRender<LevelMeter>(
            LeftProperty, RightProperty, StereoProperty, OrientationProperty,
            MinimumDecibelsProperty, ShowPeakProperty);
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

        var palette = ThemePalette.From(this);
        double now = _clock.Elapsed.TotalSeconds;

        double left = Math.Clamp(double.IsNaN(Left) ? 0 : Left, 0, 1);
        double right = Stereo ? Math.Clamp(double.IsNaN(Right) ? 0 : Right, 0, 1) : 0;

        _leftPeak = Track(left, ref _leftPeak, ref _leftPeakAt, now);
        if (Stereo) _rightPeak = Track(right, ref _rightPeak, ref _rightPeakAt, now);

        if (Falling(left, _leftPeak) || (Stereo && Falling(right, _rightPeak))) NextFrame();

        if (!Stereo)
        {
            DrawBar(context, palette, new Rect(0, 0, width, height), left, _leftPeak);
            return;
        }

        double gap = 2;

        if (Orientation == Orientation.Vertical)
        {
            double each = (width - gap) / 2;
            DrawBar(context, palette, new Rect(0, 0, each, height), left, _leftPeak);
            DrawBar(context, palette, new Rect(each + gap, 0, each, height), right, _rightPeak);
        }
        else
        {
            double each = (height - gap) / 2;
            DrawBar(context, palette, new Rect(0, 0, width, each), left, _leftPeak);
            DrawBar(context, palette, new Rect(0, each + gap, width, each), right, _rightPeak);
        }
    }

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
        if (_waiting || TopLevel.GetTopLevel(this) is not { } top) return;

        _waiting = true;

        top.RequestAnimationFrame(_ =>
        {
            _waiting = false;
            InvalidateVisual();
        });
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

        context.DrawRectangle(
            new SolidColorBrush(palette.Background),
            new Pen(palette.BorderBrush, 1),
            new RoundedRect(area, radius));

        double filled = _scale.Position(level, MinimumDecibels);
        if (filled > 0)
            context.DrawRectangle(Fill(palette, level), null, new RoundedRect(Portion(area, filled), radius));

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
        var brush = new SolidColorBrush(ColourFor(palette, peak));

        if (Orientation == Orientation.Vertical)
        {
            double y = Math.Clamp(area.Bottom - area.Height * at, area.Y, area.Bottom - 2);
            context.FillRectangle(brush, new Rect(area.X, y, area.Width, 2));
            return;
        }

        double x = Math.Clamp(area.X + area.Width * at, area.X, area.Right - 2);
        context.FillRectangle(brush, new Rect(x, area.Y, 2, area.Height));
    }

    /// <summary>
    /// A gradient rather than one flat colour, so the top of a loud bar reddens while the
    /// quiet part stays where it was: the eye reads the change, not just the height.
    /// </summary>
    private IBrush Fill(ThemePalette palette, double level)
    {
        var top = ColourFor(palette, level);
        if (level < WarnAmplitude) return new SolidColorBrush(top);

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
