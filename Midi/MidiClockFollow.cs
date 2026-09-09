using System;
using System.Threading;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <inheritdoc/>
public sealed class MidiClockFollow : IMidiClockFollow
{
    /// <summary>
    /// How long a waiting thread sleeps before looking again of its own accord.
    /// </summary>
    /// <remarks>
    /// A pulse wakes it the moment a tick arrives, so this is only about the case where nothing
    /// arrives: it is how long a stop takes to be noticed while the master is silent. Short
    /// enough that pressing stop feels immediate, long enough that a held transport is not a
    /// thread spinning.
    /// </remarks>
    private const int WakeMs = 20;

    /// <summary>
    /// Held by both threads, and what a waiting one is pulsed on.
    /// </summary>
    /// <remarks>
    /// One lock over the count, the flags and the pointer, because they are read together: a
    /// waiting thread that saw a fresh count against a stale stopped flag would carry on past a
    /// stop by one line.
    /// </remarks>
    private readonly object _gate = new();

    /// <summary>How many ticks have arrived since the pass began.</summary>
    private long _ticks;

    /// <summary>Where the last pointer said to be, in sixteenths.</summary>
    private int _pointer;

    /// <summary>Whether this is the clock the transport is on.</summary>
    private bool _following;

    /// <summary>Whether the master has said go and not yet said stop.</summary>
    private bool _going;

    /// <inheritdoc/>
    public bool IsFollowing
    {
        get { lock (_gate) return _following; }
    }

    /// <inheritdoc/>
    public long Ticks
    {
        get { lock (_gate) return _ticks; }
    }

    /// <inheritdoc/>
    public int Pointer
    {
        get { lock (_gate) return _pointer; }
    }

    /// <inheritdoc/>
    public event Action<bool>? Began;

    /// <inheritdoc/>
    public event Action? Ended;

    /// <inheritdoc/>
    /// <remarks>
    /// Everything that had arrived is forgotten either way, since a count is only meaningful
    /// against the pass it was counted in. Whoever is waiting is woken, because turning following
    /// off has to let a held thread go rather than leave it there for ever.
    /// </remarks>
    public void Follow(bool following)
    {
        lock (_gate)
        {
            if (_following == following) return;

            _following = following;
            _ticks = 0;
            _pointer = 0;
            _going = false;

            Monitor.PulseAll(_gate);
        }

        Log.Write(LogArea.Midi, () =>
            following ? "clock: following somebody else's" : "clock: on its own again");
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Counted whether or not the master has said go, deliberately. Plenty of gear ticks
    /// continuously and starts and stops on top of it, and a tick dropped because no start had
    /// arrived would put the whole pass one tick behind for its length.
    /// </remarks>
    public void Tick()
    {
        lock (_gate)
        {
            if (!_following) return;

            _ticks++;

            Monitor.PulseAll(_gate);
        }
    }

    /// <inheritdoc/>
    public void Start()
    {
        lock (_gate)
        {
            if (!_following) return;

            _ticks = 0;
            _pointer = 0;
            _going = true;

            Monitor.PulseAll(_gate);
        }

        Log.Write(LogArea.Midi, () => "clock: the master said start");

        Began?.Invoke(true);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The count goes back to nought and the pointer is left alone, which is the difference
    /// between this and <see cref="Start"/>: a continue means from where the pointer said, so the
    /// ticks that follow are counted from there rather than from the top of the song.
    /// </remarks>
    public void Resume()
    {
        int at;

        lock (_gate)
        {
            if (!_following) return;

            _ticks = 0;
            _going = true;
            at = _pointer;

            Monitor.PulseAll(_gate);
        }

        Log.Write(LogArea.Midi, () => "clock: the master said continue from " + at + " sixteenths");

        Began?.Invoke(false);
    }

    /// <inheritdoc/>
    public void Cease()
    {
        lock (_gate)
        {
            if (!_following) return;

            _going = false;

            Monitor.PulseAll(_gate);
        }

        Log.Write(LogArea.Midi, () => "clock: the master said stop");

        Ended?.Invoke();
    }

    /// <inheritdoc/>
    public void Placed(int pointer)
    {
        lock (_gate)
        {
            if (!_following) return;

            _pointer = Math.Max(0, pointer);
        }
    }

    /// <inheritdoc/>
    public bool WaitFor(double until, CancellationToken token)
    {
        lock (_gate)
        {
            while (true)
            {
                if (!_following || !_going) return false;
                if (token.IsCancellationRequested) return false;
                if (_ticks >= until) return true;

                Monitor.Wait(_gate, WakeMs);
            }
        }
    }
}
