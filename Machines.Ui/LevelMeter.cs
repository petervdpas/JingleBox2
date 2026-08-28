using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using JingleBox2.UI;
using System;
using System.Diagnostics;

namespace JingleBox2.Machines.Ui;

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
    /// <summary>Above this, the signal is close enough to the ceiling to warn about.</summary>
    private const double WarnAmplitude = 0.5;      // -6 dB

    private const double HotAmplitude = 0.89;      // -1 dB

    private const double PeakHoldSeconds = 1.2;
    private const double PeakFallDecibelsPerSecond = 20;

    private static readonly Color Warn = Color.FromRgb(0xFD, 0xD8, 0x35);
    private static readonly Color Hot = Color.FromRgb(0xE5, 0x39, 0x35);

    public static readonly StyledProperty<double> LeftProperty =
        AvaloniaProperty.Register<LevelMeter, double>(nameof(Left));

    public static readonly StyledProperty<double> RightProperty =
        AvaloniaProperty.Register<LevelMeter, double>(nameof(Right));

    /// <summary>Two bars instead of one. A mono source leaves this off and uses Left alone.</summary>
    public static readonly StyledProperty<bool> StereoProperty =
        AvaloniaProperty.Register<LevelMeter, bool>(nameof(Stereo));

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<LevelMeter, Orientation>(nameof(Orientation), Orientation.Vertical);

    public static readonly StyledProperty<double> MinimumDecibelsProperty =
        AvaloniaProperty.Register<LevelMeter, double>(nameof(MinimumDecibels), MeterScale.DefaultMinimumDecibels);

    /// <summary>A mark riding the loudest recent moment, so a transient is readable.</summary>
    public static readonly StyledProperty<bool> ShowPeakProperty =
        AvaloniaProperty.Register<LevelMeter, bool>(nameof(ShowPeak), true);

    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private double _leftPeak;
    private double _rightPeak;
    private double _leftPeakAt;
    private double _rightPeakAt;

    /// <summary>Whether a frame has already been asked for, so one is not asked for twice.</summary>
    private bool _waiting;

    static LevelMeter()
    {
        AffectsRender<LevelMeter>(
            LeftProperty, RightProperty, StereoProperty, OrientationProperty,
            MinimumDecibelsProperty, ShowPeakProperty);
    }

    public double Left
    {
        get => GetValue(LeftProperty);
        set => SetValue(LeftProperty, value);
    }

    public double Right
    {
        get => GetValue(RightProperty);
        set => SetValue(RightProperty, value);
    }

    public bool Stereo
    {
        get => GetValue(StereoProperty);
        set => SetValue(StereoProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double MinimumDecibels
    {
        get => GetValue(MinimumDecibelsProperty);
        set => SetValue(MinimumDecibelsProperty, value);
    }

    public bool ShowPeak
    {
        get => GetValue(ShowPeakProperty);
        set => SetValue(ShowPeakProperty, value);
    }

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

        if (!Stereo)
        {
            DrawBar(context, palette, new Rect(0, 0, width, height), left, _leftPeak);
            return;
        }

        // Two bars with a hair between them, so a lopsided mix is obvious at a glance.
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

        if (Falling(left, _leftPeak) || (Stereo && Falling(right, _rightPeak))) NextFrame();
    }

    /// <summary>Whether the mark is above the bar, and so has somewhere left to fall to.</summary>
    private bool Falling(double level, double peak) =>
        ShowPeak
        && MeterScale.Position(peak, MinimumDecibels) > MeterScale.Position(level, MinimumDecibels);

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

        return MeterScale.DecayPeak(peak, level, now - peakAt, PeakHoldSeconds, PeakFallDecibelsPerSecond);
    }

    private void DrawBar(DrawingContext context, ThemePalette palette, Rect area, double level, double peak)
    {
        if (area.Width <= 0 || area.Height <= 0) return;

        double radius = Math.Min(3, Math.Min(area.Width, area.Height) / 2);

        context.DrawRectangle(
            new SolidColorBrush(palette.Background),
            new Pen(palette.BorderBrush, 1),
            new RoundedRect(area, radius));

        double filled = MeterScale.Position(level, MinimumDecibels);
        if (filled > 0)
            context.DrawRectangle(Fill(palette, level), null, new RoundedRect(Portion(area, filled), radius));

        if (!ShowPeak || peak <= 0) return;

        double at = MeterScale.Position(peak, MinimumDecibels);
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
                new GradientStop(palette.Accent, MeterScale.Position(WarnAmplitude, MinimumDecibels)),
                new GradientStop(top, 1)
            }
        };
    }

    private static Color ColourFor(ThemePalette palette, double level) =>
        level >= MeterScale.ClipAmplitude || level >= HotAmplitude
            ? Hot
            : level >= WarnAmplitude ? Warn : palette.Accent;
}
