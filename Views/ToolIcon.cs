using Avalonia;
using Avalonia.Media;
using JingleBox2.Rack.Controls;
using JingleBox2.Rack.Controls.Records;
using JingleBox2.Views.Enums;

namespace JingleBox2.Views;

/// <summary>
/// One tool's mark, drawn to the size it was given.
/// </summary>
/// <remarks>
/// Every mark is drawn inside a square of <see cref="MarkSize"/> in the middle of whatever room
/// the control was given, in fractions of that square, so one button is one number and nothing
/// has to be kept in step with anything else. The lines thicken with the square for the same
/// reason: a mark drawn at 28 with the strokes of a mark drawn at 14 is a wire frame.
///
/// The colour comes from the theme rather than from the drawing, and <see cref="Tint"/> is how
/// the button it sits on says which: the tool that is picked wears the accent, like every other
/// chosen thing in this application, and the rest wear the ordinary lettering colour. A mark
/// drawn in a fixed colour would be invisible on half the themes that ship.
/// </remarks>
public sealed class ToolIcon : ThemedControl
{
    /// <summary>Which mark this is.</summary>
    public static readonly StyledProperty<ToolMark> MarkProperty =
        AvaloniaProperty.Register<ToolIcon, ToolMark>(nameof(Mark));

    /// <summary>How big the square the mark is drawn in is.</summary>
    public static readonly StyledProperty<double> MarkSizeProperty =
        AvaloniaProperty.Register<ToolIcon, double>(nameof(MarkSize), 18);

    /// <summary>What colour to draw it, or nothing for the theme's own lettering.</summary>
    public static readonly StyledProperty<IBrush?> TintProperty =
        AvaloniaProperty.Register<ToolIcon, IBrush?>(nameof(Tint));

    static ToolIcon()
    {
        AffectsRender<ToolIcon>(MarkProperty, MarkSizeProperty, TintProperty);
        AffectsMeasure<ToolIcon>(MarkSizeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ToolIcon>(false);
    }

    /// <inheritdoc cref="MarkProperty"/>
    public ToolMark Mark
    {
        get => GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }

    /// <inheritdoc cref="MarkSizeProperty"/>
    public double MarkSize
    {
        get => GetValue(MarkSizeProperty);
        set => SetValue(MarkSizeProperty, value);
    }

    /// <inheritdoc cref="TintProperty"/>
    public IBrush? Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    /// <summary>The square the mark is drawn in, whatever room the control was given.</summary>
    protected override Size MeasureOverride(Size available) => new(MarkSize, MarkSize);

    /// <summary>The mark, in fractions of that square.</summary>
    /// <remarks>
    /// One switch and no pictures. The strokes are rounded at their ends, since a mark this
    /// small drawn with square ends reads as a diagram rather than as an icon.
    /// </remarks>
    /// <param name="context">What it is drawn into.</param>
    public override void Render(DrawingContext context)
    {
        var brush = Tint ?? ThemePalette.From(this).TextBrush;

        double side = MarkSize;
        double left = (Bounds.Width - side) / 2;
        double top = (Bounds.Height - side) / 2;
        double thick = System.Math.Max(1.2, side / 13);

        var pen = new Pen(brush, thick, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        Point At(double x, double y) => new(left + x * side, top + y * side);

        void Line(double x1, double y1, double x2, double y2) =>
            context.DrawLine(pen, At(x1, y1), At(x2, y2));

        void Shape(bool filled, params double[] points)
        {
            var figure = new StreamGeometry();

            using (var draw = figure.Open())
            {
                draw.BeginFigure(At(points[0], points[1]), filled);

                for (int corner = 2; corner < points.Length; corner += 2)
                    draw.LineTo(At(points[corner], points[corner + 1]));

                draw.EndFigure(true);
            }

            context.DrawGeometry(filled ? brush : null, filled ? null : pen, figure);
        }

        switch (Mark)
        {
            case ToolMark.Select:
                context.DrawRectangle(
                    null,
                    new Pen(brush, thick, new DashStyle(new double[] { 2, 2 }, 0)),
                    new Rect(At(0.06, 0.14), At(0.94, 0.86)));
                break;

            case ToolMark.Trim:
                Line(0.3, 0.02, 0.3, 0.76);
                Line(0.24, 0.7, 0.98, 0.7);
                Line(0.7, 0.24, 0.7, 0.98);
                Line(0.02, 0.3, 0.76, 0.3);
                break;

            case ToolMark.Silence:
                Line(0.05, 0.08, 0.05, 0.92);
                Line(0.2, 0.3, 0.2, 0.7);
                Line(0.34, 0.5, 0.66, 0.5);
                Line(0.8, 0.3, 0.8, 0.7);
                Line(0.95, 0.08, 0.95, 0.92);
                break;

            case ToolMark.Reverse:
                Shape(true, 0.04, 0.2, 0.36, 0.5, 0.04, 0.8);
                Shape(false, 0.96, 0.2, 0.64, 0.5, 0.96, 0.8);
                context.DrawLine(
                    new Pen(brush, thick, new DashStyle(new double[] { 1.6, 1.6 }, 0)),
                    At(0.5, 0.06), At(0.5, 0.94));
                break;

            case ToolMark.FadeIn:
                Shape(true, 0.06, 0.9, 0.94, 0.1, 0.94, 0.9);
                break;

            case ToolMark.FadeOut:
                Shape(true, 0.06, 0.1, 0.94, 0.9, 0.06, 0.9);
                break;

            case ToolMark.Normalize:
                Line(0.06, 0.12, 0.94, 0.12);
                Line(0.5, 0.94, 0.5, 0.34);
                Shape(true, 0.5, 0.24, 0.74, 0.5, 0.26, 0.5);
                break;

            case ToolMark.ZoomIn:
            case ToolMark.ZoomOut:
                context.DrawEllipse(null, pen, At(0.42, 0.42), side * 0.3, side * 0.3);
                Line(0.64, 0.64, 0.96, 0.96);
                Line(0.24, 0.42, 0.6, 0.42);
                if (Mark == ToolMark.ZoomIn) Line(0.42, 0.24, 0.42, 0.6);
                break;

            case ToolMark.Fit:
                Line(0.04, 0.14, 0.04, 0.86);
                Line(0.96, 0.14, 0.96, 0.86);
                Line(0.16, 0.5, 0.84, 0.5);
                Shape(true, 0.16, 0.5, 0.36, 0.34, 0.36, 0.66);
                Shape(true, 0.84, 0.5, 0.64, 0.34, 0.64, 0.66);
                break;

            case ToolMark.Undo:
            case ToolMark.Redo:
            {
                bool back = Mark == ToolMark.Undo;
                double tip = back ? 0.16 : 0.84;
                double turn = back ? 1 : -1;

                var arc = new StreamGeometry();

                using (var draw = arc.Open())
                {
                    draw.BeginFigure(At(0.84, 0.7), false);
                    draw.ArcTo(
                        At(0.16, 0.7),
                        new Size(side * 0.34, side * 0.34),
                        0,
                        false,
                        back ? SweepDirection.CounterClockwise : SweepDirection.Clockwise);
                    draw.EndFigure(false);
                }

                context.DrawGeometry(null, pen, back ? arc : arc);
                Shape(true, tip, 0.86, tip + 0.2 * turn, 0.62, tip - 0.16 * turn, 0.58);
                break;
            }

            case ToolMark.Play:
                Shape(true, 0.16, 0.08, 0.9, 0.5, 0.16, 0.92);
                break;

            case ToolMark.Stop:
                context.FillRectangle(brush, new Rect(At(0.16, 0.16), At(0.84, 0.84)));
                break;
        }
    }
}
