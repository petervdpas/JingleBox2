using System;
using JingleBox2.Waveform.Interfaces;

namespace JingleBox2.Tests;

/// <summary>A playhead clock that reads only when a test says so.</summary>
internal sealed class HandClock : IPlayheadClock
{
    /// <summary>The reading while one is running, or nothing.</summary>
    private Action? _tick;

    /// <summary>How many times a reading has been started.</summary>
    public int Starts { get; private set; }

    /// <summary>Whether a reading is running.</summary>
    public bool Running => _tick != null;

    /// <inheritdoc/>
    public void Start(TimeSpan every, Action tick)
    {
        Starts++;
        _tick = tick;
    }

    /// <inheritdoc/>
    public void Stop() => _tick = null;

    /// <summary>Takes one reading, where one is running.</summary>
    public void Tick() => _tick?.Invoke();
}
