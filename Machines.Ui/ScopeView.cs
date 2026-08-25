using Avalonia;
using Avalonia.Media;
using JingleBox2.Machines;
using System;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// The shape a machine makes, drawn: a couple of cycles of its wave, with everything the knobs
/// do to it already applied.
/// </summary>
/// <remarks>
/// Not an artist's impression of a sawtooth. The curve comes from the machine's own engine
/// through <see cref="IMachineScope"/>, so if the sound changes the picture changes with it, and
/// a duty cycle at a fifth looks like a duty cycle at a fifth.
///
/// It comes alive while a note sounds: brighter, thicker, and travelling, the way a scope shows a
/// running signal. That is <see cref="ScopeControl"/>'s doing, and all this adds is what to paint.
/// </remarks>
public class ScopeView : ScopeControl
{
    /// <summary>Where the curve comes from.</summary>
    public static readonly StyledProperty<IMachineScope?> ScopeProperty =
        AvaloniaProperty.Register<ScopeView, IMachineScope?>(nameof(Scope));

    /// <summary>
    /// How much of the wave the window shows, counted in cycles.
    /// </summary>
    /// <remarks>
    /// The window is a length of time rather than a number of cycles, so anything that moves the
    /// pitch moves the picture. Working that out is the machine's: this says how much was asked
    /// for and is handed back whatever that turns into.
    /// </remarks>
    public static readonly StyledProperty<double> CyclesProperty =
        AvaloniaProperty.Register<ScopeView, double>(nameof(Cycles), 2);

    static ScopeView()
    {
        AffectsRender<ScopeView>(ScopeProperty, CyclesProperty);
    }

    public ScopeView()
    {
        DetachedFromVisualTree += (_, _) => Unwatch();
    }

    public IMachineScope? Scope
    {
        get => GetValue(ScopeProperty);
        set => SetValue(ScopeProperty, value);
    }

    public double Cycles
    {
        get => GetValue(CyclesProperty);
        set => SetValue(CyclesProperty, value);
    }

    protected override double AnimationSeconds => Scope?.MotionSeconds ?? base.AnimationSeconds;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ScopeProperty) return;

        Unwatch();
        Watch();
    }

    private IMachineScope? _watching;
    private EventHandler? _listening;

    private void Watch()
    {
        if (Scope is not { } scope) return;

        _watching = scope;
        _listening = (_, _) => InvalidateVisual();

        scope.Changed += _listening;
    }

    private void Unwatch()
    {
        if (_watching != null && _listening != null) _watching.Changed -= _listening;

        _watching = null;
        _listening = null;
    }

    /// <summary>The points of the last curve, kept so a running note costs no allocation.</summary>
    private double[] _points = Array.Empty<double>();

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

        if (Scope is not { } scope) return;

        // Brighter and thicker while it runs, so a played note is obvious at a glance.
        var pen = IsRunning
            ? new Pen(palette.TextBrush, 2, lineJoin: PenLineJoin.Round)
            : new Pen(palette.AccentBrush, 1.5, lineJoin: PenLineJoin.Round);

        context.DrawGeometry(null, pen, Curve(scope, width, height));
    }

    private StreamGeometry Curve(IMachineScope scope, double width, double height)
    {
        // One sample per pixel: any finer is invisible, any coarser and a square wave's edges
        // start to lean.
        int steps = (int)Math.Max(16, width);

        if (_points.Length != steps) _points = new double[steps];

        scope.Trace(_points, Math.Max(0.25, Cycles), IsRunning ? ElapsedSeconds : 0, IsRunning);

        double amplitude = height / 2 - 4;
        double middle = height / 2;

        var geometry = new StreamGeometry();

        using (var sink = geometry.Open())
        {
            for (int step = 0; step < steps; step++)
            {
                double across = step / (steps - 1.0);

                var point = new Point(across * width, middle - _points[step] * amplitude);

                if (step == 0) sink.BeginFigure(point, false);
                else sink.LineTo(point);
            }

            sink.EndFigure(false);
        }

        return geometry;
    }
}
