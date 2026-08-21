using Avalonia;
using Avalonia.Threading;
using System;
using System.Diagnostics;

namespace JingleBox2.Views;

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

    public static readonly StyledProperty<int> TriggerProperty =
        AvaloniaProperty.Register<ScopeControl, int>(nameof(Trigger));

    private readonly DispatcherTimer _frames;
    private readonly Stopwatch _clock = new();

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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TriggerProperty) Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // A timer left running on a page nobody is looking at is just heat.
        Stop();
    }

    private void Start()
    {
        _clock.Restart();
        _frames.Start();
        InvalidateVisual();
    }

    private void Stop()
    {
        _frames.Stop();
        _clock.Reset();
    }

    private void Advance()
    {
        if (ElapsedSeconds > AnimationSeconds) Stop();

        InvalidateVisual();
    }
}
