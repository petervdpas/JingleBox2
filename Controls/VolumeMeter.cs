using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace JingleBox2.Controls;

public class VolumeMeter : Control
{
    public static readonly StyledProperty<float> LevelProperty =
        AvaloniaProperty.Register<VolumeMeter, float>(nameof(Level));

    public static readonly StyledProperty<bool> UseDbScaleProperty =
        AvaloniaProperty.Register<VolumeMeter, bool>(nameof(UseDbScale), true);

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<VolumeMeter, IBrush?>(nameof(FillBrush));

    public float Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public bool UseDbScale
    {
        get => GetValue(UseDbScaleProperty);
        set => SetValue(UseDbScaleProperty, value);
    }

    public IBrush? FillBrush
    {
        get => GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    private static readonly Color Green = Color.Parse("#43A047");
    private static readonly Color Yellow = Color.Parse("#FDD835");
    private static readonly Color Orange = Color.Parse("#FB8C00");
    private static readonly Color Red = Color.Parse("#E53935");

    private static readonly IBrush TrackBrush = new SolidColorBrush(Colors.White, 0.1);

    private const double DbMin = -60.0;
    private const double DbMax = 0.0;

    static VolumeMeter()
    {
        AffectsRender<VolumeMeter>(LevelProperty, UseDbScaleProperty, FillBrushProperty);
    }

    private static double LinearToDb(float linear)
    {
        if (linear <= 0.0001f) return DbMin;
        return 20.0 * Math.Log10(linear);
    }

    private static double DbToFraction(double db)
    {
        return Math.Clamp((db - DbMin) / (DbMax - DbMin), 0, 1);
    }

    private static Color GetMeterColor(double fraction)
    {
        if (fraction < 0.8) return Green;
        if (fraction < 0.9) return Yellow;
        if (fraction < 0.95) return Orange;
        return Red;
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;

        context.DrawRectangle(TrackBrush, null, new Rect(0, 0, w, h), 2, 2);

        var linear = Math.Clamp(Level, 0f, 1f);
        if (linear < 0.005f) return;

        // Solid fill mode (volume bar)
        if (FillBrush != null)
        {
            var fillH = h * linear;
            context.DrawRectangle(FillBrush, null, new Rect(0, h - fillH, w, fillH), 2, 2);
            return;
        }

        // VU meter mode (dB scale, segmented, colored)
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

        var segH = 2.0;
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
