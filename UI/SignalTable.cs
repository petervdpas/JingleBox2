using System;
using System.Collections.Generic;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;

namespace JingleBox2.UI;

/// <inheritdoc/>
/// <remarks>
/// Handed what a point reads rather than knowing: a table of levels has no business knowing what
/// a pad bus is, and handed in it can be put a question to without an engine, a sound card or a
/// window.
///
/// A point that has been let go of by everybody is dropped, so a page opened and closed all
/// afternoon leaves nothing behind.
/// </remarks>
/// <param name="measure">What one point is carrying, asked at most once per reading.</param>
public sealed class SignalTable(Func<SignalPoint, PatchLevel> measure) : ISignalTable
{
    /// <summary>What is being listened to, and what it last read.</summary>
    private readonly Dictionary<SignalPoint, Watchers> _points = new();

    /// <summary>The listeners on one point, and the last reading they were told.</summary>
    private sealed class Watchers
    {
        /// <summary>Everybody following this point.</summary>
        public List<Action<PatchLevel>> Told { get; } = new();

        /// <summary>What the point last read, which is what <see cref="At"/> answers.</summary>
        public PatchLevel Last { get; set; }
    }

    /// <summary>Lets go of one listener when it is done, and of the point with the last of them.</summary>
    /// <remarks>
    /// A class rather than a lambda over a disposable helper, since letting go twice has to be
    /// harmless: a page that is closed and then thrown away does exactly that.
    /// </remarks>
    private sealed class Watching(SignalTable table, SignalPoint point, Action<PatchLevel> told) : IDisposable
    {
        /// <summary>Whether this has already been let go of.</summary>
        private bool _done;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_done) return;

            _done = true;

            table.Drop(point, told);
        }
    }

    /// <inheritdoc/>
    public PatchLevel At(SignalPoint point) =>
        _points.TryGetValue(point, out var watchers) ? watchers.Last : default;

    /// <inheritdoc/>
    public IDisposable Watch(SignalPoint point, Action<PatchLevel> told)
    {
        if (!_points.TryGetValue(point, out var watchers))
        {
            watchers = new Watchers();

            _points[point] = watchers;
        }

        watchers.Told.Add(told);

        return new Watching(this, point, told);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The points are copied before they are walked, since a listener told about one is entitled
    /// to start or stop watching another.
    /// </remarks>
    public void Read()
    {
        var points = new SignalPoint[_points.Count];

        _points.Keys.CopyTo(points, 0);

        foreach (var point in points)
        {
            if (!_points.TryGetValue(point, out var watchers)) continue;

            var now = measure(point);

            if (now.Equals(watchers.Last)) continue;

            watchers.Last = now;

            foreach (var told in watchers.Told.ToArray()) told(now);
        }
    }

    /// <summary>Takes one listener off a point, and the point off the table with the last of them.</summary>
    private void Drop(SignalPoint point, Action<PatchLevel> told)
    {
        if (!_points.TryGetValue(point, out var watchers)) return;

        watchers.Told.Remove(told);

        if (watchers.Told.Count == 0) _points.Remove(point);
    }
}
