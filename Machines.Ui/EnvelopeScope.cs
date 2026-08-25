using Avalonia;
using Avalonia.Media;
using System;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// Draws the envelope as a shape, and runs a playhead along it while a note sounds.
/// </summary>
/// <remarks>
/// The playhead is driven by a stopwatch rather than by counting frames, so it stays with the
/// note even when the UI thread is busy. The curve itself comes from
/// <see cref="EnvelopeShape"/>, which is the same arithmetic the voice's envelope follows.
/// <para>
/// It takes four numbers rather than a patch because it is drawn for whatever machine is in
/// front of it, and a machine described by data has parameters, not a synth patch. Four
/// bindings are all the view needs, and any machine with an attack and a release can supply
/// them.
/// </para>
/// </remarks>
public class EnvelopeScope : ScopeControl
{
    public static readonly StyledProperty<double> AttackMsProperty =
        AvaloniaProperty.Register<EnvelopeScope, double>(nameof(AttackMs), 2);

    public static readonly StyledProperty<double> DecayMsProperty =
        AvaloniaProperty.Register<EnvelopeScope, double>(nameof(DecayMs), 40);

    public static readonly StyledProperty<double> SustainProperty =
        AvaloniaProperty.Register<EnvelopeScope, double>(nameof(Sustain), 0.6);

    public static readonly StyledProperty<double> ReleaseMsProperty =
        AvaloniaProperty.Register<EnvelopeScope, double>(nameof(ReleaseMs), 80);

    /// <summary>How long the note is held before it is let go, matching what an audition does.</summary>
    public static readonly StyledProperty<double> HoldSecondsProperty =
        AvaloniaProperty.Register<EnvelopeScope, double>(nameof(HoldSeconds), 0.4);

    static EnvelopeScope()
    {
        AffectsRender<EnvelopeScope>(
            AttackMsProperty, DecayMsProperty, SustainProperty, ReleaseMsProperty, HoldSecondsProperty);
    }

    public double AttackMs
    {
        get => GetValue(AttackMsProperty);
        set => SetValue(AttackMsProperty, value);
    }

    public double DecayMs
    {
        get => GetValue(DecayMsProperty);
        set => SetValue(DecayMsProperty, value);
    }

    /// <summary>The level the note settles at, 0 to 1.</summary>
    public double Sustain
    {
        get => GetValue(SustainProperty);
        set => SetValue(SustainProperty, value);
    }

    public double ReleaseMs
    {
        get => GetValue(ReleaseMsProperty);
        set => SetValue(ReleaseMsProperty, value);
    }

    public double HoldSeconds
    {
        get => GetValue(HoldSecondsProperty);
        set => SetValue(HoldSecondsProperty, value);
    }

    private EnvelopeShape Shape =>
        EnvelopeShape.FromMilliseconds(AttackMs, DecayMs, Sustain, ReleaseMs, HoldSeconds);

    /// <summary>The playhead runs for exactly as long as the note it is drawing.</summary>
    protected override double AnimationSeconds => Shape.Length;

    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 2 || height <= 2) return;

        var palette = ThemePalette.From(this);
        var face = new Rect(0, 0, width, height);

        context.DrawRectangle(
            new SolidColorBrush(palette.Background),
            new Pen(palette.BorderBrush, 1),
            new RoundedRect(face, 4));

        var shape = Shape;
        double margin = 4;
        double floor = height - margin;
        double span = height - margin * 2;

        double X(double seconds) => margin + seconds / shape.Length * (width - margin * 2);
        double Y(double level) => floor - level * span;

        // The moment the key is let go, so the release is readable as a separate stage.
        if (shape.Sustain > 0 && shape.ReleaseSeconds > 0)
        {
            double release = X(shape.ReleaseStarts);
            context.DrawLine(
                new Pen(palette.AccentTint(70), 1, DashStyle.Dash),
                new Point(release, margin),
                new Point(release, floor));
        }

        context.DrawGeometry(
            palette.AccentTint(40),
            new Pen(palette.AccentBrush, 1.5, lineJoin: PenLineJoin.Round),
            BuildCurve(shape, width, X, Y, floor));

        DrawPlayhead(context, palette, shape, X, Y, margin, floor);
    }

    /// <summary>
    /// The curve, closed along the bottom so it can be filled. One point per pixel: the corners
    /// land where they should without a special case for each stage.
    /// </summary>
    private static StreamGeometry BuildCurve(
        EnvelopeShape shape, double width, Func<double, double> x, Func<double, double> y, double floor)
    {
        int steps = (int)Math.Max(16, width);

        var geometry = new StreamGeometry();
        using (var sink = geometry.Open())
        {
            sink.BeginFigure(new Point(x(0), floor), true);

            for (int step = 0; step <= steps; step++)
            {
                double seconds = step / (double)steps * shape.Length;
                sink.LineTo(new Point(x(seconds), y(shape.LevelAt(seconds))));
            }

            sink.LineTo(new Point(x(shape.Length), floor));
            sink.EndFigure(true);
        }

        return geometry;
    }

    private void DrawPlayhead(
        DrawingContext context,
        ThemePalette palette,
        EnvelopeShape shape,
        Func<double, double> x,
        Func<double, double> y,
        double margin,
        double floor)
    {
        if (!IsRunning) return;

        double seconds = Math.Min(ElapsedSeconds, shape.Length);
        double at = x(seconds);
        double level = shape.LevelAt(seconds);

        // The stretch already played, so the movement is a sweep and not just a moving line.
        context.FillRectangle(palette.AccentTint(70), new Rect(x(0), margin, Math.Max(0, at - x(0)), floor - margin));

        context.DrawLine(new Pen(palette.TextBrush, 2), new Point(at, margin), new Point(at, floor));
        context.DrawEllipse(palette.TextBrush, null, new Point(at, y(level)), 3.5, 3.5);
    }
}
