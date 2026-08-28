using Avalonia;
using Avalonia.Threading;
using System;
using System.Diagnostics;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// A drawn view that comes alive while a note sounds. Holds the clock and the frame timer, so
/// each scope above it is only about what to paint.
/// </summary>
/// <remarks>
/// A stopwatch rather than a frame count: the picture then stays with the note even when the
/// UI thread is busy, and a dropped frame costs a frame rather than shifting everything after
/// it. The trigger is a counter rather than an event, which keeps it a plain binding.
/// </remarks>
public abstract class ScopeControl : ThemedControl
{
    /// <summary>Fast enough to look continuous without asking much of the UI thread.</summary>
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

    /// <summary>Backs <see cref="Trigger"/>.</summary>
    public static readonly StyledProperty<int> TriggerProperty =
        AvaloniaProperty.Register<ScopeControl, int>(nameof(Trigger));

    /// <summary>Wakes the drawing while a note is running, and is stopped the moment it is not.</summary>
    private readonly DispatcherTimer _frames;

    /// <summary>How far into the note the picture has got, in real time rather than in frames.</summary>
    private readonly Stopwatch _clock = new();

    /// <summary>Builds the frame timer. It is not started until something plays.</summary>
    protected ScopeControl()
    {
        _frames = new DispatcherTimer { Interval = FrameInterval };
        _frames.Tick += (_, _) => Advance();
    }

    /// <summary>Bumped every time a note is played. Any change starts the animation.</summary>
    public int Trigger
    {
        get => GetValue(TriggerProperty);
        set => SetValue(TriggerProperty, value);
    }

    /// <summary>True while a note is running through the view.</summary>
    protected bool IsRunning => _clock.IsRunning;

    /// <summary>How far into the note the view is.</summary>
    protected double ElapsedSeconds => _clock.Elapsed.TotalSeconds;

    /// <summary>How long the animation lasts. Override to follow the sound.</summary>
    protected virtual double AnimationSeconds => 1.0;

    /// <summary>Starts the picture again whenever <see cref="Trigger"/> moves.</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TriggerProperty) Start();
    }

    /// <summary>
    /// Stops the clock and the timer on the way off the tree.
    /// </summary>
    /// <remarks>
    /// A timer left running on a page nobody is looking at is heat and nothing else, and this
    /// wakes sixty times a second.
    /// </remarks>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        Stop();
    }

    /// <summary>Puts the clock back to nought and runs the frames from there.</summary>
    private void Start()
    {
        _clock.Restart();
        _frames.Start();
        InvalidateVisual();
    }

    /// <summary>Ends the animation, leaving the last frame on screen.</summary>
    private void Stop()
    {
        _frames.Stop();
        _clock.Reset();
    }

    /// <summary>
    /// One frame: draw, and stop once the note has run its length.
    /// </summary>
    /// <remarks>
    /// The check comes before the draw so that the last frame is painted with the clock stopped,
    /// which is what leaves the picture resting at the end of the note rather than one frame
    /// short of it.
    /// </remarks>
    private void Advance()
    {
        if (ElapsedSeconds > AnimationSeconds) Stop();

        InvalidateVisual();
    }
}
