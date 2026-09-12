using System;
using System.Diagnostics;
using JingleBox2.Hints.Interfaces;

namespace JingleBox2.Hints;

/// <inheritdoc/>
/// <remarks>
/// Holds no clock of its own: <see cref="HintClock"/> drives every hint in the application, and
/// this is what one of them knows about itself. Everything here is read and written from two
/// threads, whoever says it moved and the clock, so the whole of it is one lock.
/// </remarks>
public sealed class Hint : IHint
{
    /// <summary>How long the saying has to stop for.</summary>
    private readonly TimeSpan _settles;

    /// <summary>And how long an owed answer may be held back in all.</summary>
    private readonly TimeSpan _atMost;

    /// <summary>The work.</summary>
    private readonly Action _answer;

    /// <summary>What it is about, for a log line when the answer throws.</summary>
    public string Name { get; }

    /// <summary>Everything below is read by the clock and written by whoever moved.</summary>
    private readonly object _gate = new();

    /// <summary>How long since the last saying, or nothing when none is owed.</summary>
    private Stopwatch? _said;

    /// <summary>And how long since the first saying of the answer that is owed now.</summary>
    private Stopwatch? _owed;

    /// <summary>
    /// Whether an answer is owed without anybody having said so, which is a hint that is its own
    /// net rather than one that is waiting to be told.
    /// </summary>
    private readonly bool _always;

    /// <summary>Makes one. Built by <see cref="HintClock"/> rather than by hand.</summary>
    /// <param name="name">What it is about.</param>
    /// <param name="settles">How long the saying has to stop for.</param>
    /// <param name="atMost">How long an owed answer may be held back in all.</param>
    /// <param name="answer">The work.</param>
    /// <param name="always">
    /// Whether the answer is owed for ever rather than when somebody says so, which is
    /// <see cref="Interfaces.IHintClock.Often"/>'s half.
    /// </param>
    public Hint(string name, TimeSpan settles, TimeSpan atMost, Action answer, bool always = false)
    {
        Name = name;
        _settles = settles;
        _atMost = atMost;
        _answer = answer;
        _always = always;

        if (always) Moved();
    }

    /// <inheritdoc/>
    public bool Owed
    {
        get
        {
            lock (_gate) return _owed != null;
        }
    }

    /// <inheritdoc/>
    public void Moved()
    {
        lock (_gate)
        {
            _said = Stopwatch.StartNew();
            _owed ??= Stopwatch.StartNew();
        }
    }

    /// <summary>
    /// Whether this is the moment, which the clock asks of every hint on every tick.
    /// </summary>
    /// <remarks>
    /// The two rules, and the second is what stops a gesture that never ends holding the work
    /// back for as long as it goes on.
    /// </remarks>
    public bool Due()
    {
        lock (_gate)
        {
            if (_owed is not { } owed) return false;

            return _said is { } said && (said.Elapsed >= _settles || owed.Elapsed >= _atMost);
        }
    }

    /// <inheritdoc/>
    public void Forget()
    {
        lock (_gate)
        {
            _said = _always ? Stopwatch.StartNew() : null;
            _owed = _always ? Stopwatch.StartNew() : null;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// What is owed is forgotten before the work rather than after it, so a change arriving while
    /// the answer runs is owed again rather than one that has just been answered.
    /// </remarks>
    public void Now()
    {
        lock (_gate)
        {
            if (_owed == null) return;

            _said = _always ? Stopwatch.StartNew() : null;
            _owed = _always ? Stopwatch.StartNew() : null;
        }

        _answer();
    }
}
