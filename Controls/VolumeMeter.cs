using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace JingleBox2.Controls;

public class VolumeMeter : Control
{
    public static readonly StyledProperty<float> LevelProperty =
        AvaloniaProperty.Register<VolumeMeter, float>(nameof(Level));

    public float Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
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
        AffectsRender<VolumeMeter>(LevelProperty);
    }

    private static double LinearToDb(float linear)
    {
        if (linear <= 0.0001f) return DbMin;
        return 20.0 * Math.Log10(linear);
    }

    private static double DbToFraction(double db)
    {
        // Map dB range to 0-1 for meter display
        return Math.Clamp((db - DbMin) / (DbMax - DbMin), 0, 1);
    }

    private static Color GetMeterColor(double dbFraction)
    {
        // dbFraction 0=bottom(-60dB), 1=top(0dB)
        // Map to standard meter ranges:
        // Green:  -60dB to -12dB  => fraction 0.0  to 0.8
        // Yellow: -12dB to -6dB   => fraction 0.8  to 0.9
        // Orange:  -6dB to -3dB   => fraction 0.9  to 0.95
        // Red:     -3dB to  0dB   => fraction 0.95 to 1.0
        if (dbFraction < 0.8)
            return Green;
        if (dbFraction < 0.9)
            return Yellow;
        if (dbFraction < 0.95)
            return Orange;
        return Red;
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;

        // Track background
        context.DrawRectangle(TrackBrush, null, new Rect(0, 0, w, h), 2, 2);

        var linear = Math.Clamp(Level, 0f, 1f);
        if (linear < 0.0001f) return;

        var db = LinearToDb(linear);
        var meterFraction = DbToFraction(db);
        var fillH = h * meterFraction;

        if (fillH < 1) return;

        // Draw in 2px segments for the banded look
        var segH = 2.0;
        var segments = (int)(fillH / segH);

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
