using JingleBox2.Diagnostics;
using JingleBox2.Midi;
using System;
using System.Collections.Generic;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
/// <remarks>
/// The four questions it puts to the tracker are held as functions, and the fifth thing it is
/// given is where the writing is done, which is not where the deciding is.
/// </remarks>
public sealed class AutomationRecorder : IAutomationRecorder
{
    /// <summary>The song being played, asked per message because it is closed and reopened.</summary>
    private readonly Func<Song?> _song;

    /// <summary>Whether the transport is running, which is what makes a move a recording.</summary>
    private readonly Func<bool> _running;

    /// <summary>Where the clock is, asked as the message lands rather than when the write happens.</summary>
    private readonly Func<TrackerPosition> _position;

    /// <summary>Which track a link with no track of its own means.</summary>
    private readonly Func<int> _focused;

    /// <summary>
    /// Where the writing is done.
    /// </summary>
    /// <remarks>
    /// A message arrives on the MIDI thread and a pattern is read by the thread things are drawn
    /// on, so the write is handed across, the same way every other change here reaches a list
    /// somebody is looking at. Run in place when nothing was given, which is what every test
    /// wants and what a program with no window would want.
    /// </remarks>
    private readonly Action<Action> _onto;

    /// <summary>Lanes already written to in this pass, so a pass leaves one step per lane.</summary>
    private readonly HashSet<AutomationLane> _touched = new();

    /// <summary>Whether this pass has written anything yet, which is what lights the indicator.</summary>
    private bool _passing;

    /// <param name="song">The song being played, asked per message.</param>
    /// <param name="running">Whether the transport is running.</param>
    /// <param name="position">Where the clock is, read as a message lands.</param>
    /// <param name="focused">Which track a link with no track of its own means.</param>
    /// <param name="onto">
    /// Where the writing is done, which is not where the deciding is. Null runs it in place.
    /// </param>
    public AutomationRecorder(Func<Song?> song, Func<bool> running,
                              Func<TrackerPosition> position, Func<int> focused,
                              Action<Action>? onto = null)
    {
        _song = song;
        _running = running;
        _position = position;
        _focused = focused;
        _onto = onto ?? (work => work());
    }

    /// <inheritdoc/>
    public bool Armed { get; set; }

    /// <inheritdoc/>
    public Action<Pattern, string>? Taking { get; set; }

    /// <inheritdoc/>
    public Action? Dirtied { get; set; }

    /// <inheritdoc/>
    public void Stopped()
    {
        _passing = false;
        _touched.Clear();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The order of the tests is the whole of it. The normalised value and the line are read
    /// here, before anything is posted, for the same reason: a knob moved twice before the
    /// drawing thread wakes would otherwise write its later value twice and its earlier value
    /// never. And whether a lane can be made at all is a question about the mapping, so it is
    /// answered now rather than in the posted work, since a promise that turned out to be
    /// nothing would be a promise worth nothing.
    /// </remarks>
    public bool Moved(ControlMapping? mapping, IControlTarget? target, double value)
    {
        if (!Armed || mapping is null || target is null) return false;
        if (!_running()) return false;

        if (!AutomationLane.Automatable(mapping.Kind)) return false;

        var song = _song();
        if (song is null) return false;

        var position = _position();

        var pattern = song.PatternAt(position.OrderIndex);
        if (pattern is null || position.Line < 0 || position.Line >= pattern.Lines) return false;

        int track = mapping.Scope == ControlScope.Fixed ? mapping.Track : _focused();
        if (track < 0 || track >= pattern.TrackCount) return false;

        double range = target.Max - target.Min;
        if (range == 0) return false;

        double normalised = Math.Clamp((value - target.Min) / range, 0, 1);
        int line = position.Line;
        string said = target.Name;

        if (pattern.LaneFor(mapping, track) is null && AutomationLane.For(mapping, track) is null)
            return false;

        _onto(() => Write(pattern, mapping, track, line, normalised, said, position.OrderIndex));

        return true;
    }

    /// <summary>
    /// Puts one point in, taking a step first if this is the lane's first point of the pass.
    /// </summary>
    /// <remarks>
    /// Run on the thread patterns are drawn from, with the instant already decided, so nothing
    /// here reads the clock or the transport.
    ///
    /// A lane that has to be made takes its step before it exists, so undo takes the lane away
    /// along with its points; a lane already there takes one the first time this pass touches it.
    /// </remarks>
    private void Write(Pattern pattern, ControlMapping mapping, int track, int line,
                       double value, string said, int order)
    {
        var lane = pattern.LaneFor(mapping, track);

        if (lane is null)
        {
            var made = AutomationLane.For(mapping, track);
            if (made is null) return;

            Take(pattern, said);

            lane = pattern.Lane(made);
            _touched.Add(lane);

            Log.Write(LogArea.Tracker, () =>
                "automation: recording " + said + " into pattern " + order
                + ", track " + (track + 1));
        }
        else if (_touched.Add(lane))
        {
            Take(pattern, said);
        }

        lane.Put(line, value);
        pattern.LaneChanged();
        Dirtied?.Invoke();
    }

    /// <summary>Takes one undo step and marks the pass as having written something.</summary>
    private void Take(Pattern pattern, string what)
    {
        _passing = true;
        Taking?.Invoke(pattern, "recording " + what);
    }

    /// <inheritdoc/>
    public bool Writing => _passing && Armed && _running();
}
