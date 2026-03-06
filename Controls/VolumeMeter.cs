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

    static VolumeMeter()
    {
        AffectsRender<VolumeMeter>(LevelProperty);
    }

    private static Color LerpColor(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * t),
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private static Color GetMeterColor(double fraction)
    {
        // fraction 0=bottom, 1=top
        // 0.0-0.5 green, 0.5-0.75 yellow, 0.75-0.9 orange, 0.9-1.0 red
        if (fraction < 0.5)
            return LerpColor(Green, Green, 0);
        if (fraction < 0.75)
            return LerpColor(Yellow, Yellow, 0);
        if (fraction < 0.9)
            return LerpColor(Orange, Orange, 0);
        return Red;
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;

        // Track background
        context.DrawRectangle(TrackBrush, null, new Rect(0, 0, w, h), 2, 2);

        var level = Math.Clamp(Level, 0f, 1f);
        if (level < 0.005) return;

        var fillH = h * level;
        var segmentCount = Math.Max(1, (int)(fillH / 2));
        var segH = fillH / segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            var y = h - (i + 1) * segH;
            var fraction = (double)(i + 1) / (h / segH); // position 0-1 from bottom
            var color = GetMeterColor(Math.Clamp(fraction, 0, 1));
            var brush = new SolidColorBrush(color);
            context.DrawRectangle(brush, null, new Rect(0, y, w, segH));
        }
    }
}
