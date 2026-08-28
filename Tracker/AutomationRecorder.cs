using JingleBox2.Diagnostics;
using JingleBox2.Midi;
using System;
using System.Collections.Generic;

namespace JingleBox2.Tracker;

/// <summary>
/// A knob turned while the song plays, written down as points.
/// </summary>
/// <remarks>
/// The cheapest thing in the whole plan and the one that will feel like the most, because
/// everything it needs was built for something else. The link already resolves to a parameter,
/// takeover has already stopped the value lurching when your hand arrives, the sensing has
/// already worked out whether that control reports a position or a movement, and the router
/// already announces every value it writes. All that is left is to put the number somewhere.
///
/// It is told about a move rather than watching for one, so it holds no timer and no thread.
/// What it needs from the tracker is four questions, asked as functions rather than held as
/// state, because all four of them change underneath it: the transport starts and stops, the
/// playing line moves thirty times a second, the song is closed and another opened.
/// </remarks>
public sealed class AutomationRecorder
{
    private readonly Func<Song?> _song;
    private readonly Func<bool> _running;
    private readonly Func<TrackerPosition> _position;
    private readonly Func<int> _focused;
    private readonly Action<Action> _onto;

    /// <summary>Lanes already written to in this pass, so a pass leaves one step per lane.</summary>
    private readonly HashSet<AutomationLane> _touched = new();

    private bool _passing;

    /// <param name="onto">
    /// Where the writing is done, which is not where the deciding is.
    /// </param>
    /// <remarks>
    /// A message arrives on the MIDI thread and a pattern is read by the thread things are drawn
    /// on, so the write is handed across, the same way every other change here reaches a list
    /// somebody is looking at. What cannot be handed across is the moment: which line the song
    /// is on has to be read as the message lands, or a sweep would be written wherever the
    /// drawing thread happened to wake up and a hand moving quickly would pile several values
    /// onto one line. So the instant is captured here and only the writing is posted.
    ///
    /// Run in place when nothing is given, which is what every test wants and what a program
    /// with no window would want.
    /// </remarks>
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

    /// <summary>
    /// Whether a hand on a knob is being written down.
    /// </summary>
    /// <remarks>
    /// Off by default and switched on deliberately, because the alternative is a controller
    /// nudged while a song plays quietly editing it. Renoise arms recording the same way and
    /// for the same reason.
    /// </remarks>
    public bool Armed { get; set; }

    /// <summary>Told before a lane is first written in a pass, so undo has somewhere to go.</summary>
    /// <remarks>
    /// Once per lane per pass rather than once per point. A hand sweeping a filter across a
    /// pattern is one thing a person did and a hundred and twenty points, and a hundred and
    /// twenty steps would be a hundred and twenty presses of Ctrl+Z to get back to where they
    /// started. The same rule the instrument knobs already use, arrived at from the same
    /// direction.
    /// </remarks>
    public Action<Pattern, string>? Taking;

    /// <summary>Told after a point is written, since the song now has something unsaved in it.</summary>
    /// <remarks>
    /// Separate from the pattern's own <c>Changed</c> on purpose. Recording happens on whatever
    /// pattern is playing, which is not always the one being looked at, and the pattern being
    /// looked at is the only one anything is subscribed to. Without this a sweep recorded into
    /// the next pattern of the song would leave it looking saved.
    /// </remarks>
    public Action? Dirtied;

    /// <summary>Ends the pass, so the next one takes its own steps. Called when the clock stops.</summary>
    public void Stopped()
    {
        _passing = false;
        _touched.Clear();
    }

    /// <summary>
    /// A parameter moved. Writes a point when it should, and says whether it will.
    /// </summary>
    /// <remarks>
    /// The value arrives in the parameter's own units, since that is what was written to it, and
    /// goes into the lane normalised. That conversion is the whole reason a lane can be pointed
    /// at anything: the lane holds nought to one and the target holds hertz.
    ///
    /// Everything that has to be true of this instant is settled here and the writing is handed
    /// over, so the answer is a promise rather than a report. There is nothing between the two
    /// that can decide otherwise.
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

        // Read here rather than in the posted work, for the same reason the line is: a knob
        // moved twice before the drawing thread wakes would otherwise write its later value
        // twice and its earlier value never.
        double normalised = Math.Clamp((value - target.Min) / range, 0, 1);
        int line = position.Line;
        string said = target.Name;

        // Whether a lane can be made at all is a question about the mapping, so it is answered
        // now: a promise that turned out to be nothing would be a promise worth nothing.
        if (pattern.LaneFor(mapping, track) is null && AutomationLane.For(mapping, track) is null)
            return false;

        _onto(() => Write(pattern, mapping, track, line, normalised, said, position.OrderIndex));

        return true;
    }

    private void Write(Pattern pattern, ControlMapping mapping, int track, int line,
                       double value, string said, int order)
    {
        var lane = pattern.LaneFor(mapping, track);

        if (lane is null)
        {
            var made = AutomationLane.For(mapping, track);
            if (made is null) return;

            // The step is taken before the lane exists, so undo takes the lane away as well as
            // its points. A lane created and then emptied would be a parameter that still stops
            // moving where the recording stopped.
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

    private void Take(Pattern pattern, string what)
    {
        _passing = true;
        Taking?.Invoke(pattern, "recording " + what);
    }

    /// <summary>True while a pass has written something. For a light that says so.</summary>
    public bool Writing => _passing && Armed && _running();
}
