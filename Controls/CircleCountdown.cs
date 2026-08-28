using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace JingleBox2.Controls;

/// <summary>
/// A disc that empties as something runs down, drawn as a pie going clockwise from the top.
/// </summary>
/// <remarks>
/// What is left is drawn rather than what has gone, so a countdown reads as something being used
/// up. A full circle is drawn as an ellipse rather than as an arc of very nearly 360 degrees,
/// which leaves a hairline seam where the two ends almost meet.
/// </remarks>
public class CircleCountdown : Control
{
    /// <summary>How far through it is, 0 for just started and 1 for finished.</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<CircleCountdown, double>(nameof(Progress));

    /// <summary>What the disc is painted with. Nothing is drawn without one.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<CircleCountdown, IBrush?>(nameof(Fill),
            new SolidColorBrush(Colors.White, 0.6));

    /// <inheritdoc cref="ProgressProperty"/>
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <inheritdoc cref="FillProperty"/>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>Says which properties make the disc need drawing again.</summary>
    static CircleCountdown()
    {
        AffectsRender<CircleCountdown>(ProgressProperty, FillProperty);
    }

    /// <summary>Draws what is left of the disc.</summary>
    /// <param name="context">Where to draw.</param>
    public override void Render(DrawingContext context)
    {
        var progress = Math.Clamp(Progress, 0, 1);
        var remaining = 1.0 - progress;

        if (remaining <= 0.001) return;

        var size = Math.Min(Bounds.Width, Bounds.Height);
        var radius = size / 2;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);

        var brush = Fill;
        if (brush == null) return;

        if (remaining >= 0.999)
        {
            context.DrawEllipse(brush, null, center, radius, radius);
            return;
        }

        var sweepAngle = remaining * 360.0;
        var startAngleRad = -90.0 * Math.PI / 180.0;
        var endAngleRad = (-90.0 + sweepAngle) * Math.PI / 180.0;

        var startPoint = new Point(
            center.X + radius * Math.Cos(startAngleRad),
            center.Y + radius * Math.Sin(startAngleRad));

        var endPoint = new Point(
            center.X + radius * Math.Cos(endAngleRad),
            center.Y + radius * Math.Sin(endAngleRad));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(center, true);
            ctx.LineTo(startPoint);
            ctx.ArcTo(endPoint, new Size(radius, radius), 0, sweepAngle > 180, SweepDirection.Clockwise);
            ctx.LineTo(center);
            ctx.EndFigure(true);
        }

        context.DrawGeometry(brush, null, geometry);
    }
}
