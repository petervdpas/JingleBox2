using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace JingleBox2.Controls;

/// <summary>
/// A share of something as a slice of a disc, beside the figures it stands for.
/// </summary>
/// <remarks>
/// The slice is filled from twelve o'clock round, and the rest of the disc is only outlined, so
/// an empty disc and a full one can be told apart at a glance.
/// </remarks>
public class ShareDisc : Control
{
    /// <summary>How much of it is in use, nought to one.</summary>
    public static readonly StyledProperty<double> ShareProperty =
        AvaloniaProperty.Register<ShareDisc, double>(nameof(Share));

    /// <summary>The colour of the slice and the outline.</summary>
    public static readonly StyledProperty<Color> ColourProperty =
        AvaloniaProperty.Register<ShareDisc, Color>(nameof(Colour), Colors.Gray);

    /// <inheritdoc cref="ShareProperty"/>
    public double Share
    {
        get => GetValue(ShareProperty);
        set => SetValue(ShareProperty, value);
    }

    /// <inheritdoc cref="ColourProperty"/>
    public Color Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    /// <summary>Says which properties make the disc need drawing again.</summary>
    static ShareDisc()
    {
        AffectsRender<ShareDisc>(ShareProperty, ColourProperty);
    }

    /// <summary>Draws the outline, then the slice.</summary>
    /// <param name="context">Where to draw.</param>
    public override void Render(DrawingContext context)
    {
        double radius = Math.Min(Bounds.Width, Bounds.Height) / 2 - 1;

        if (radius < 2) return;

        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var brush = new SolidColorBrush(Colour);

        context.DrawEllipse(null, new Pen(brush, 1.5), centre, radius, radius);

        double share = Math.Clamp(Share, 0, 1);

        if (share <= 0) return;

        if (share >= 0.999)
        {
            context.DrawEllipse(brush, null, centre, radius, radius);
            return;
        }

        double angle = share * Math.PI * 2;
        var top = new Point(centre.X, centre.Y - radius);
        var end = new Point(centre.X + radius * Math.Sin(angle), centre.Y - radius * Math.Cos(angle));

        var slice = new StreamGeometry();

        using (var pen = slice.Open())
        {
            pen.BeginFigure(centre, true);
            pen.LineTo(top);
            pen.ArcTo(end, new Size(radius, radius), 0, share > 0.5, SweepDirection.Clockwise);
            pen.EndFigure(true);
        }

        context.DrawGeometry(brush, null, slice);
    }
}
