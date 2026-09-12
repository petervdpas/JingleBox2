using System;
using System.Collections.Generic;
using System.Timers;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Hints.Interfaces;

namespace JingleBox2.Hints;

/// <inheritdoc/>
public sealed class HintClock : IHintClock
{
    /// <summary>How often every hint is asked whether it is due.</summary>
    /// <remarks>
    /// A twentieth of a second, which is under what a hand notices and is the resolution the
    /// shortest wait here wants. A tick costs a comparison per hint, and there are six of them.
    /// </remarks>
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(50);

    /// <summary>Every hint in the application.</summary>
    private readonly List<Hint> _hints = new();

    /// <summary>The clock, which runs for as long as this does.</summary>
    /// <remarks>
    /// Deliberately not the drawing thread's. What hangs off these is writing files and reading
    /// plugins, and neither belongs in front of a window that is being drawn: a caller whose work
    /// really does belong there says so in its own answer.
    /// </remarks>
    private readonly Timer _clock;

    /// <summary>Starts the clock.</summary>
    public HintClock()
    {
        _clock = new Timer(Tick.TotalMilliseconds) { AutoReset = true };
        _clock.Elapsed += (_, _) => Round();
        _clock.Start();
    }

    /// <inheritdoc/>
    public IHint Gathered(string name, TimeSpan settles, TimeSpan atMost, Action answer)
    {
        var hint = new Hint(name, settles, atMost, answer);

        lock (_hints) _hints.Add(hint);

        return hint;
    }

    /// <inheritdoc/>
    public IHint Often(string name, TimeSpan every, Action answer)
    {
        var hint = new Hint(name, every, every, answer, always: true);

        lock (_hints) _hints.Add(hint);

        return hint;
    }

    /// <summary>Answers whatever is due, and costs one answer where one throws.</summary>
    private void Round()
    {
        foreach (var hint in Every())
        {
            if (!hint.Due()) continue;

            Answer(hint);
        }
    }

    /// <summary>The hints as they stand, so the clock is not walking a list being added to.</summary>
    private Hint[] Every()
    {
        lock (_hints) return _hints.ToArray();
    }

    /// <summary>One answer, with whatever it throws written down rather than let out.</summary>
    /// <param name="hint">The hint that is due.</param>
    private static void Answer(Hint hint)
    {
        try
        {
            hint.Now();
        }
        catch (Exception bad)
        {
            Log.Fault(LogArea.App, "the hint about " + hint.Name + " could not be answered", bad);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// **Everything owed is answered on the way out**, which is the one moment that cannot be
    /// waited through, and it is the whole reason the hints are held here rather than each being
    /// let go of wherever it was made.
    /// </remarks>
    public void Dispose()
    {
        _clock.Stop();
        _clock.Dispose();

        foreach (var hint in Every()) Answer(hint);
    }
}
