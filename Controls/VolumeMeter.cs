using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace JingleBox2.Controls;

/// <summary>
/// A vertical bar reading how loud something is, either as a plain fill or as a segmented meter
/// on a decibel scale.
/// </summary>
/// <remarks>
/// Which of the two it draws is decided by whether it was given a brush. A brush means somebody
/// wants a bar in a colour of their choosing, which is a volume control's read-out; without one
/// it is a meter, and a meter's colours belong to the meter, since green through red is what the
/// reading means rather than a decoration on it.
/// </remarks>
public class VolumeMeter : Control
{
    /// <summary>How loud it is, 0 to 1.</summary>
    public static readonly StyledProperty<float> LevelProperty =
        AvaloniaProperty.Register<VolumeMeter, float>(nameof(Level));

    /// <summary>Whether the bar rises on a decibel scale rather than in a straight line.</summary>
    /// <remarks>
    /// On by default, because a straight line spends almost all of its length on the loudest few
    /// decibels and shows nothing at all of a quiet signal.
    /// </remarks>
    public static readonly StyledProperty<bool> UseDbScaleProperty =
        AvaloniaProperty.Register<VolumeMeter, bool>(nameof(UseDbScale), true);

    /// <summary>A brush to fill the bar with, which turns it from a meter into a plain bar.</summary>
    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<VolumeMeter, IBrush?>(nameof(FillBrush));

    /// <inheritdoc cref="LevelProperty"/>
    public float Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    /// <inheritdoc cref="UseDbScaleProperty"/>
    public bool UseDbScale
    {
        get => GetValue(UseDbScaleProperty);
        set => SetValue(UseDbScaleProperty, value);
    }

    /// <inheritdoc cref="FillBrushProperty"/>
    public IBrush? FillBrush
    {
        get => GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    /// <summary>Well under, which is where a signal should be sitting.</summary>
    private static readonly Color Green = Color.Parse("#43A047");

    /// <summary>Getting near the top.</summary>
    private static readonly Color Yellow = Color.Parse("#FDD835");

    /// <summary>Nearly there.</summary>
    private static readonly Color Orange = Color.Parse("#FB8C00");

    /// <summary>At the top, where the next thing that happens is clipping.</summary>
    private static readonly Color Red = Color.Parse("#E53935");

    /// <summary>The empty bar behind the reading, so the meter is visible while it is silent.</summary>
    private static readonly IBrush TrackBrush = new SolidColorBrush(Colors.White, 0.1);

    /// <summary>The bottom of the scale. Below this a signal is not worth showing.</summary>
    private const double DbMin = -60.0;

    /// <summary>The top of it, which is full scale.</summary>
    private const double DbMax = 0.0;

    /// <summary>How tall one lit segment is, with half a pixel of it left dark as the gap.</summary>
    private const double SegmentHeight = 2.0;

    /// <summary>Says which properties make the bar need drawing again.</summary>
    static VolumeMeter()
    {
        AffectsRender<VolumeMeter>(LevelProperty, UseDbScaleProperty, FillBrushProperty);
    }

    /// <summary>An amplitude as decibels, with anything near silence reading as the bottom.</summary>
    private static double LinearToDb(float linear)
    {
        if (linear <= 0.0001f) return DbMin;
        return 20.0 * Math.Log10(linear);
    }

    /// <summary>Where a reading in decibels sits up the bar, 0 to 1.</summary>
    private static double DbToFraction(double db)
    {
        return Math.Clamp((db - DbMin) / (DbMax - DbMin), 0, 1);
    }

    /// <summary>What colour a segment is, by how far up the bar it sits.</summary>
    private static Color GetMeterColor(double fraction)
    {
        if (fraction < 0.8) return Green;
        if (fraction < 0.9) return Yellow;
        if (fraction < 0.95) return Orange;
        return Red;
    }

    /// <summary>Draws the empty bar, then the reading over it.</summary>
    /// <remarks>
    /// A reading below half a percent draws nothing at all, so a silent meter is the empty bar
    /// rather than the empty bar with a mark at the bottom of it.
    /// </remarks>
    /// <param name="context">Where to draw.</param>
    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;

        context.DrawRectangle(TrackBrush, null, new Rect(0, 0, w, h), 2, 2);

        var linear = Math.Clamp(Level, 0f, 1f);
        if (linear < 0.005f) return;

        if (FillBrush != null)
        {
            var fillH = h * linear;
            context.DrawRectangle(FillBrush, null, new Rect(0, h - fillH, w, fillH), 2, 2);
            return;
        }

        double meterFraction;
        if (UseDbScale)
        {
            var db = LinearToDb(linear);
            meterFraction = DbToFraction(db);
        }
        else
        {
            meterFraction = linear;
        }

        var fillHeight = h * meterFraction;
        if (fillHeight < 1) return;

        var segH = SegmentHeight;
        var segments = (int)(fillHeight / segH);

        for (int i = 0; i < segments; i++)
        {
            var segBottom = h - (i + 1) * segH;
            var segFraction = ((i + 1) * segH) / h;
            var color = GetMeterColor(segFraction);
            var brush = new SolidColorBrush(color);
            context.DrawRectangle(brush, null, new Rect(0, segBottom, w, segH - 0.5));
        }
    }
}
