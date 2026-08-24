using Avalonia;
using Avalonia.Media;
using JingleBox2.Tracker.Synth;
using System;
using JingleBox2.Machines.Ui;

namespace JingleBox2.Views;

/// <summary>
/// Draws the shape a synth voice makes: a couple of cycles of the wave, with the duty and the
/// drive applied, so what the knobs do is visible as well as audible.
/// </summary>
/// <remarks>
/// One cycle is computed the same way the voice computes it, through
/// <see cref="Oscillator"/> and <see cref="Saturation"/>, rather than being an artist's
/// impression of each wave. If the sound changes, the picture changes with it.
/// </remarks>
public class WaveScope : ScopeControl
{
    /// <summary>How fast the wave travels while a note sounds, in cycles per second.</summary>
    private const double ScrollCyclesPerSecond = 1.5;

    /// <summary>How long it keeps moving after a note, since a preview is about that long.</summary>
    private const double MotionSeconds = 0.6;

    /// <summary>Enough of the noise wave to read as noise, and stable between redraws.</summary>
    private const int NoiseSeed = 20260821;

    public static readonly StyledProperty<SynthPatch?> PatchProperty =
        AvaloniaProperty.Register<WaveScope, SynthPatch?>(nameof(Patch));

    /// <summary>
    /// Changes whenever the patch does. A patch is plain data and says nothing when it is
    /// edited, so this is what tells the view to redraw.
    /// </summary>
    public static readonly StyledProperty<int> RevisionProperty =
        AvaloniaProperty.Register<WaveScope, int>(nameof(Revision));

    /// <summary>
    /// How much of the wave to show, counted in cycles of the note before the instrument's own
    /// tuning. The window is a length of time, so tuning up fits more cycles into it.
    /// </summary>
    public static readonly StyledProperty<double> CyclesProperty =
        AvaloniaProperty.Register<WaveScope, double>(nameof(Cycles), 2);

    static WaveScope()
    {
        AffectsRender<WaveScope>(PatchProperty, RevisionProperty, CyclesProperty);
    }

    protected override double AnimationSeconds => MotionSeconds;

    public SynthPatch? Patch
    {
        get => GetValue(PatchProperty);
        set => SetValue(PatchProperty, value);
    }

    public int Revision
    {
        get => GetValue(RevisionProperty);
        set => SetValue(RevisionProperty, value);
    }

    public double Cycles
    {
        get => GetValue(CyclesProperty);
        set => SetValue(CyclesProperty, value);
    }

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

        double middle = height / 2;
        context.DrawLine(
            new Pen(palette.AccentTint(60), 1),
            new Point(2, middle),
            new Point(width - 2, middle));

        var patch = Patch;
        if (patch == null) return;

        // Brighter and thicker while it runs, so a played note is obvious at a glance.
        var pen = IsRunning
            ? new Pen(palette.TextBrush, 2, lineJoin: PenLineJoin.Round)
            : new Pen(palette.AccentBrush, 1.5, lineJoin: PenLineJoin.Round);

        context.DrawGeometry(null, pen, BuildWave(patch, width, height));
    }

    private StreamGeometry BuildWave(SynthPatch patch, double width, double height)
    {
        // One sample per pixel: any finer is invisible, any coarser and a square wave's edges
        // start to lean.
        int steps = (int)Math.Max(16, width);
        double amplitude = height / 2 - 4;
        double middle = height / 2;
        double makeup = Saturation.Makeup(patch.Drive);

        // The window is a length of time, not a number of cycles, so anything that moves the
        // pitch moves the picture: the instrument's tuning always, and while a note runs, its
        // vibrato and pitch envelope as well.
        double semitones = PitchMotion.Tuning(patch) + (IsRunning ? PitchMotion.MotionAt(patch, ElapsedSeconds) : 0);
        double cycles = Math.Max(0.25, Cycles) * PitchMotion.Ratio(semitones);

        // The wave travels while a note is sounding, the way a scope shows a running signal.
        double travel = IsRunning ? ElapsedSeconds * ScrollCyclesPerSecond : 0;

        // A fixed seed while it is still, so the noise wave does not crawl on every repaint;
        // while it runs, a new one per frame is exactly what noise should look like.
        var noise = new Random(IsRunning ? NoiseSeed + (int)(ElapsedSeconds * 1000) : NoiseSeed);

        var geometry = new StreamGeometry();
        using (var sink = geometry.Open())
        {
            for (int step = 0; step < steps; step++)
            {
                double across = step / (steps - 1.0);
                double phase = Oscillator.Wrap(across * cycles + travel);

                double sample = Oscillator.Sample(patch.Wave, phase, patch.Duty, noise.NextDouble() * 2.0 - 1.0);
                sample = Saturation.Apply(sample, patch.Drive, makeup);

                var point = new Point(across * width, middle - sample * amplitude);

                if (step == 0) sink.BeginFigure(point, false);
                else sink.LineTo(point);
            }

            sink.EndFigure(false);
        }

        return geometry;
    }
}
