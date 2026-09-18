using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using JingleBox2.Controls.Records;

namespace JingleBox2.Controls;

/// <summary>
/// Readings over the last minute as smooth coloured lines on a grid, the way a system monitor
/// draws them.
/// </summary>
/// <remarks>
/// Now is the right hand edge and a minute ago the left, with a line every ten seconds and the
/// time written under it. Five levels are ruled across and their values written down the right,
/// in whatever words the caller gives them, since a percentage and a rate of bytes are labelled
/// differently and the chart does not need to know which it is showing.
///
/// Its colours are its own and not the theme's: a chart like this is read by its lines, and a
/// dark ground under them reads the same whatever the rest of the program looks like.
/// </remarks>
public class HistoryChart : Control
{
    /// <summary>The lines, drawn in order, so the last is on top.</summary>
    public static readonly StyledProperty<IReadOnlyList<ChartLine>?> LinesProperty =
        AvaloniaProperty.Register<HistoryChart, IReadOnlyList<ChartLine>?>(nameof(Lines));

    /// <summary>The value at the top of the chart. One for a share, which is the default.</summary>
    public static readonly StyledProperty<double> TopProperty =
        AvaloniaProperty.Register<HistoryChart, double>(nameof(Top), 1.0);

    /// <summary>
    /// What the five levels are called, from the top down: all of it, four fifths, three fifths,
    /// two fifths and one fifth.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> LevelsProperty =
        AvaloniaProperty.Register<HistoryChart, IReadOnlyList<string>?>(nameof(Levels));

    /// <inheritdoc cref="LinesProperty"/>
    public IReadOnlyList<ChartLine>? Lines
    {
        get => GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    /// <inheritdoc cref="TopProperty"/>
    public double Top
    {
        get => GetValue(TopProperty);
        set => SetValue(TopProperty, value);
    }

    /// <inheritdoc cref="LevelsProperty"/>
    public IReadOnlyList<string>? Levels
    {
        get => GetValue(LevelsProperty);
        set => SetValue(LevelsProperty, value);
    }

    /// <summary>How many seconds the chart spans, left edge to right.</summary>
    private const int Span = 60;

    /// <summary>The room on the right for the levels' names, and under the chart for the times.</summary>
    private const double RightRoom = 56, BottomRoom = 16;

    /// <summary>The ground under the lines.</summary>
    private static readonly IBrush Ground = new SolidColorBrush(Color.Parse("#242424"));

    /// <summary>The ruled lines, across and down.</summary>
    private static readonly IPen Grid = new Pen(new SolidColorBrush(Color.Parse("#3a3a3a")), 1);

    /// <summary>The times and the levels.</summary>
    private static readonly IBrush Writing = new SolidColorBrush(Color.Parse("#9a9a9a"));

    /// <summary>The face they are written in.</summary>
    private static readonly Typeface Face = new(FontFamily.Default);

    /// <summary>What the times under the lines say, from the left.</summary>
    private static readonly string[] Times = ["1 min", "50 secs", "40 secs", "30 secs", "20 secs", "10 secs"];

    /// <summary>What the levels say when nobody has said: a share, in percent.</summary>
    private static readonly string[] Percents = ["100 %", "80 %", "60 %", "40 %", "20 %"];

    /// <summary>Says which properties make the chart need drawing again.</summary>
    static HistoryChart()
    {
        AffectsRender<HistoryChart>(LinesProperty, TopProperty, LevelsProperty);
    }

    /// <summary>Draws the ground, the grid and its words, then the lines.</summary>
    /// <param name="context">Where to draw.</param>
    public override void Render(DrawingContext context)
    {
        double width = Math.Max(0, Bounds.Width - RightRoom);
        double height = Math.Max(0, Bounds.Height - BottomRoom);

        if (width < 10 || height < 10) return;

        var plot = new Rect(0, 0, width, height);

        context.DrawRectangle(Ground, null, plot);

        var levels = Levels ?? Percents;

        for (int level = 0; level < 5; level++)
        {
            double y = Math.Round(height * level / 5) + 0.5;

            context.DrawLine(Grid, new Point(0, y), new Point(width, y));

            if (level < levels.Count) Write(context, levels[level], new Point(width + 6, y - 6));
        }

        for (int tick = 0; tick < Times.Length; tick++)
        {
            double x = Math.Round(width * tick / Times.Length) + 0.5;

            context.DrawLine(Grid, new Point(x, 0), new Point(x, height));
            Write(context, Times[tick], new Point(x + 3, height + 2));
        }

        context.DrawLine(Grid, new Point(0, height + 0.5), new Point(width, height + 0.5));

        if (Lines == null) return;

        using (context.PushClip(plot))
        {
            foreach (var line in Lines) Draw(context, line, plot);
        }
    }

    /// <summary>Writes one of the chart's small words.</summary>
    private static void Write(DrawingContext context, string text, Point at)
    {
        var words = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, 9, Writing);

        context.DrawText(words, at);
    }

    /// <summary>
    /// One line, smoothed, from its first reading to now.
    /// </summary>
    /// <remarks>
    /// Each step between two readings is a curve that leaves one level flat and arrives flat at the
    /// next, which rounds the corners off without overshooting either reading the way a spline
    /// through the points would.
    /// </remarks>
    private void Draw(DrawingContext context, ChartLine line, Rect plot)
    {
        var values = line.Values;

        if (values.Count < 2) return;

        double step = plot.Width / Span;
        double top = Top > 0 ? Top : 1;
        double left = plot.Width - (values.Count - 1) * step;

        int first = 0;
        while (first < values.Count && double.IsNaN(values[first])) first++;

        if (values.Count - first < 2) return;

        Point At(int index) => new(
            left + index * step,
            plot.Height - 1 - (plot.Height - 2) * Math.Clamp(values[index] / top, 0, 1));

        var shape = new StreamGeometry();

        using (var pen = shape.Open())
        {
            var from = At(first);

            pen.BeginFigure(from, false);

            for (int at = first + 1; at < values.Count; at++)
            {
                var to = At(at);
                double middle = (from.X + to.X) / 2;

                pen.CubicBezierTo(new Point(middle, from.Y), new Point(middle, to.Y), to);

                from = to;
            }

            pen.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(new SolidColorBrush(line.Colour), line.Thickness, lineJoin: PenLineJoin.Round), shape);
    }
}
