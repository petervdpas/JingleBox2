using System;
using Avalonia.Threading;
using JingleBox2.Waveform.Interfaces;

namespace JingleBox2.Waveform;

/// <inheritdoc/>
/// <remarks>
/// A dispatcher timer, so whoever is listening may touch controls directly: a pool thread raising
/// the reading would throw inside the toolkit and the timer would swallow it.
/// </remarks>
public sealed class PlayheadClock : IPlayheadClock
{
    /// <summary>The timer running, or nothing.</summary>
    private DispatcherTimer? _timer;

    /// <inheritdoc/>
    public void Start(TimeSpan every, Action tick)
    {
        Stop();

        _timer = new DispatcherTimer { Interval = every };
        _timer.Tick += (_, _) => tick();
        _timer.Start();
    }

    /// <inheritdoc/>
    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }
}
