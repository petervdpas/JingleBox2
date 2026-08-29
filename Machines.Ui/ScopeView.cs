using Avalonia;
using Avalonia.Media;
using System;
using JingleBox2.Machines.Interfaces;
using JingleBox2.Machines.Ui.Records;

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

    /// <summary>
    /// Says which properties change the picture. Neither changes the size: a scope is as big as
    /// the panel gives it, whatever is on it.
    /// </summary>
    static ScopeView()
    {
        AffectsRender<ScopeView>(ScopeProperty, CyclesProperty);
    }

    /// <summary>Lets go of the machine when the control leaves the tree.</summary>
    public ScopeView()
    {
        DetachedFromVisualTree += (_, _) => Unwatch();
    }

    /// <inheritdoc cref="ScopeProperty"/>
    public IMachineScope? Scope
    {
        get => GetValue(ScopeProperty);
        set => SetValue(ScopeProperty, value);
    }

    /// <inheritdoc cref="CyclesProperty"/>
    public double Cycles
    {
        get => GetValue(CyclesProperty);
        set => SetValue(CyclesProperty, value);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Taken from the machine, so the picture runs for as long as the sound does rather than for
    /// a length this control picked. A machine that says nothing gets the base's second.
    /// </remarks>
    protected override double AnimationSeconds => Scope?.MotionSeconds ?? base.AnimationSeconds;

    /// <summary>
    /// Moves the listening to whichever machine has just arrived.
    /// </summary>
    /// <remarks>
    /// The old one is let go of first, or a control handed two machines in a row would go on
    /// repainting for the first as long as anything else held it.
    /// </remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ScopeProperty) return;

        Unwatch();
        Watch();
    }

    /// <summary>
    /// The machine being listened to, and the handler doing it.
    /// </summary>
    /// <remarks>
    /// Both are kept so the subscription can be taken off again: the handler is a closure rather
    /// than a method, so it is not the same delegate twice and could not be unsubscribed without
    /// having been held on to.
    /// </remarks>
    private IMachineScope? _watching;

    /// <inheritdoc cref="_watching"/>
    private EventHandler? _listening;

    /// <summary>Starts listening, so a knob turned anywhere redraws the curve.</summary>
    private void Watch()
    {
        if (Scope is not { } scope) return;

        _watching = scope;
        _listening = (_, _) => InvalidateVisual();

        scope.Changed += _listening;
    }

    /// <summary>Stops listening, so nothing here keeps a machine alive after the panel has gone.</summary>
    private void Unwatch()
    {
        if (_watching != null && _listening != null) _watching.Changed -= _listening;

        _watching = null;
        _listening = null;
    }

    /// <summary>The points of the last curve, kept so a running note costs no allocation.</summary>
    private double[] _points = Array.Empty<double>();

    /// <summary>
    /// The face, the middle line, and the curve over both.
    /// </summary>
    /// <remarks>
    /// The curve is drawn brighter and thicker while a note is running, so a played note is
    /// obvious at a glance without anything having to move very far. Travelling is
    /// <see cref="ScopeControl"/>'s doing; this only paints what it is given.
    /// </remarks>
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

        var pen = IsRunning
            ? new Pen(palette.TextBrush, 2, lineJoin: PenLineJoin.Round)
            : new Pen(palette.AccentBrush, 1.5, lineJoin: PenLineJoin.Round);

        context.DrawGeometry(null, pen, Curve(scope, width, height));
    }

    /// <summary>
    /// The curve itself, asked of the machine and turned into a line across the face.
    /// </summary>
    /// <remarks>
    /// One sample per pixel: any finer is invisible, any coarser and a square wave's edges start
    /// to lean. The buffer is kept between frames, so a running note allocates nothing per frame.
    ///
    /// The window is held at a quarter cycle at the least. A window of nothing is a curve with
    /// no time in it, which draws as a flat line and reads as a machine that has stopped.
    /// </remarks>
    private StreamGeometry Curve(IMachineScope scope, double width, double height)
    {
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
