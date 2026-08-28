using JingleBox2.Midi;
using System;
using System.Collections.Generic;
using JingleBox2.Midi.Enums;
using JingleBox2.Tracker.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker;

/// <summary>
/// The movement of one parameter, across one track, within one pattern.
/// </summary>
/// <remarks>
/// It names its destination the way a <see cref="ControlMapping"/> names one, and for the same
/// reason: the clock writing at line 32 and a knob writing from CC 74 are the same act against
/// the same interface, so they should be resolved by the same code. <see cref="Mapping"/> is
/// that correspondence, and it is the only place that knows it. What a lane does not carry is
/// the other half of a mapping, the device and the controller number, because those say where a
/// value came from and a lane is only about where one is going.
///
/// The scope is always fixed. A link means "the track you are looking at" and a lane cannot:
/// it is written down inside a pattern beside the notes of one particular track.
///
/// Per pattern, per track, one lane per parameter, which is Renoise's shape read off its own
/// schema (<c>PatternTrack/Automations/Envelopes/Envelope</c>, each one a device, a parameter
/// and an envelope). Copying a pattern copies its movement with it, which is the only behaviour
/// that makes sense in a pattern sequencer.
/// </remarks>
public sealed class AutomationLane
{
    /// <summary>Which track's parameter this is about, counted from zero.</summary>
    public int Track { get; set; }

    /// <summary>How it gets from one point to the next. Straight lines unless somebody says otherwise.</summary>
    public AutomationPlay Play { get; set; } = AutomationPlay.Lines;

    /// <summary>
    /// What kind of thing is being moved. Only the three that are values.
    /// </summary>
    /// <remarks>
    /// <see cref="ControlKind.Action"/> is a button, which is a thing done rather than a value
    /// held, and there is nothing for a curve between two lines to mean. <see cref="Automatable"/>
    /// is where that is said once.
    /// </remarks>
    public ControlKind Kind { get; set; } = ControlKind.Instrument;

    /// <summary>The machine, by its slot id, for a parameter on the track's instrument.</summary>
    public string Machine { get; set; } = "";

    /// <summary>Which parameter of it, by the key it is stored under rather than its name.</summary>
    public string Key { get; set; } = "";

    /// <summary>The plugin, by the id the scanner gave it, for a parameter on an insert.</summary>
    public string Plugin { get; set; } = "";

    /// <summary>Which insert, counted from zero, for a chain where the plugin is not named.</summary>
    public int Slot { get; set; }

    /// <summary>Which parameter of it, as the plugin numbers them.</summary>
    public uint Parameter { get; set; }

    /// <summary>Which strip control, for <see cref="ControlKind.Mix"/>.</summary>
    public MixControl Mix { get; set; } = MixControl.Volume;

    /// <summary>
    /// The points, in time order and with no two at one time.
    /// </summary>
    /// <remarks>
    /// Both of those are Renoise's rules and both are worth keeping. Sorted, because the
    /// evaluator walks them and a scan for the surrounding pair would otherwise be a search.
    /// One per time, because a point at a time that already has one is somebody moving that
    /// point: there is nowhere for a second to go and no way to draw them apart.
    ///
    /// Kept private so those two facts cannot be broken from outside. <see cref="Put"/> is the
    /// only way in.
    /// </remarks>
    private readonly List<AutomationPoint> _points = new();

    /// <summary>The points, to be read. <see cref="Put"/> is the only way one goes in.</summary>
    public IReadOnlyList<AutomationPoint> Points => _points;

    /// <summary>
    /// True for a lane with nothing in it, which says nothing and moves nothing.
    /// </summary>
    /// <remarks>
    /// A lane is never left in this state deliberately: adding one gives it a point holding
    /// where the parameter already stands, since a lane that listed as automated and moved
    /// nothing would be a control nobody could account for.
    /// </remarks>
    public bool IsEmpty => _points.Count == 0;

    /// <summary>Which kinds can be a lane at all.</summary>
    public static bool Automatable(ControlKind kind) =>
        kind is ControlKind.Instrument or ControlKind.Insert or ControlKind.Mix;

    /// <summary>
    /// The mapping this lane would be, so the ordinary resolution can answer it.
    /// </summary>
    /// <remarks>
    /// Built per call rather than kept. It is asked once when a lane starts playing and again
    /// when the song changes underneath it, never per line, and a kept one would be a second
    /// copy of these fields with nothing keeping the two in step.
    /// </remarks>
    public ControlMapping Mapping() => new()
    {
        Kind = Kind,
        Scope = ControlScope.Fixed,
        Track = Track,
        Machine = Machine,
        Key = Key,
        Plugin = Plugin,
        Slot = Slot,
        Parameter = Parameter,
        Mix = Mix,
        Ordinal = -1
    };

    /// <summary>True when this lane and that mapping are about the same parameter.</summary>
    /// <remarks>
    /// Which is how a knob turned while recording finds the lane it belongs in, and how a
    /// second lane for one parameter is stopped from being made. The controller half of the
    /// mapping is not looked at: two knobs pointed at one parameter write into one lane, since
    /// what is being written down is the parameter's movement and not the hand's.
    /// </remarks>
    public bool About(ControlMapping mapping, int track)
    {
        if (mapping is null || mapping.Kind != Kind || track != Track) return false;

        return Kind switch
        {
            ControlKind.Instrument =>
                string.Equals(mapping.Machine, Machine, StringComparison.Ordinal)
                && string.Equals(mapping.Key, Key, StringComparison.Ordinal)
                && Key.Length > 0,

            ControlKind.Insert =>
                string.Equals(mapping.Plugin, Plugin, StringComparison.Ordinal)
                && mapping.Parameter == Parameter
                && (Plugin.Length > 0 || mapping.Slot == Slot),

            ControlKind.Mix => mapping.Mix == Mix,

            _ => false
        };
    }

    /// <summary>The lane a mapping would make, for a track, with nothing in it yet.</summary>
    /// <remarks>
    /// Two mappings answer null and both are refusals rather than failures.
    ///
    /// A track below nought is nowhere, with one exception: the master is a strip without being
    /// a track, and <see cref="TrackerPlayer.MasterStrip"/> is the only number down there that
    /// means anything. That is why a lane names a strip rather than a track, and why a master
    /// lane stays put when tracks are removed or reordered.
    ///
    /// And a link on an instrument that names no parameter means the third knob on whatever face
    /// is in front of you, which is a fact about a hand rather than about a song. There is
    /// nothing to write down, because the face will be a different one tomorrow.
    /// </remarks>
    public static AutomationLane? For(ControlMapping mapping, int track)
    {
        if (mapping is null || !Automatable(mapping.Kind)) return null;

        if (track < 0 && track != TrackerPlayer.MasterStrip) return null;

        if (mapping.Kind == ControlKind.Instrument && mapping.Key.Length == 0) return null;

        return new AutomationLane
        {
            Track = track,
            Kind = mapping.Kind,
            Machine = mapping.Machine,
            Key = mapping.Key,
            Plugin = mapping.Plugin,
            Slot = mapping.Slot,
            Parameter = mapping.Parameter,
            Mix = mapping.Mix
        };
    }

    /// <summary>
    /// Puts a point at a time, replacing whatever was there.
    /// </summary>
    /// <remarks>
    /// Replacing rather than adding beside, because there is no room for two: the list holds
    /// one per time on purpose. What that costs is that the value which was there is gone, and
    /// getting it back is the history's job rather than the lane's.
    /// </remarks>
    public void Put(double time, double value)
    {
        var point = new AutomationPoint(time, value).Clamped();

        int at = IndexOf(point.Time);
        if (at >= 0)
        {
            if (_points[at].Value == point.Value) return;

            _points[at] = point;
            return;
        }

        _points.Insert(~at, point);
    }

    /// <summary>Takes the point at a time away. False when there was not one.</summary>
    public bool Remove(double time)
    {
        int at = IndexOf(time);
        if (at < 0) return false;

        _points.RemoveAt(at);
        return true;
    }

    /// <summary>Takes away every point in [from, to), which is how a range is cleared.</summary>
    public int RemoveRange(double from, double to)
    {
        int gone = _points.RemoveAll(one => one.Time >= from && one.Time < to);

        return gone;
    }

    /// <summary>Empties the lane without taking it out of the pattern.</summary>
    public void Clear() => _points.Clear();

    /// <summary>
    /// Where the parameter should be at a time, or null when the lane says nothing.
    /// </summary>
    /// <remarks>
    /// Null only for a lane with no points at all. A lane with one point says that value for
    /// the whole pattern, and a time before the first point or after the last reads as the
    /// nearest one rather than as silence: an envelope is a statement about the whole of the
    /// time it covers, and a hole at the top of the pattern would let whatever the knob happened
    /// to be at play through it.
    /// </remarks>
    public double? ValueAt(double time)
    {
        if (_points.Count == 0) return null;
        if (time <= _points[0].Time) return _points[0].Value;

        var last = _points[^1];
        if (time >= last.Time) return last.Value;

        int after = 0;
        while (after < _points.Count && _points[after].Time <= time) after++;

        var before = _points[after - 1];
        if (Play == AutomationPlay.Points) return before.Value;

        var next = _points[after];
        double span = next.Time - before.Time;
        if (span <= 0) return before.Value;

        double how = (time - before.Time) / span;

        return before.Value + (next.Value - before.Value) * how;
    }

    /// <summary>True for a lane about the whole mix rather than about one track.</summary>
    public bool IsMaster => Track == TrackerPlayer.MasterStrip;

    /// <summary>Takes away every point at or past a line, for a pattern that got shorter.</summary>
    public void FitTo(int lines)
    {
        _points.RemoveAll(one => one.Time >= lines);
    }

    /// <summary>
    /// A lane of its own, about the same parameter, holding the same points.
    /// </summary>
    /// <remarks>
    /// What a history step keeps. A lane is edited in place, so a step holding the live one
    /// would hold whatever it became rather than what it was, and undo would put the present
    /// back.
    /// </remarks>
    public AutomationLane Clone()
    {
        var copy = new AutomationLane
        {
            Track = Track,
            Play = Play,
            Kind = Kind,
            Machine = Machine,
            Key = Key,
            Plugin = Plugin,
            Slot = Slot,
            Parameter = Parameter,
            Mix = Mix
        };

        copy._points.AddRange(_points);

        return copy;
    }

    /// <summary>True when that lane is this one, point for point. For a history to compare.</summary>
    public bool Same(AutomationLane? other)
    {
        if (other is null) return false;

        if (other.Track != Track || other.Play != Play || other.Kind != Kind) return false;
        if (other.Slot != Slot || other.Parameter != Parameter || other.Mix != Mix) return false;

        if (!string.Equals(other.Machine, Machine, StringComparison.Ordinal)) return false;
        if (!string.Equals(other.Key, Key, StringComparison.Ordinal)) return false;
        if (!string.Equals(other.Plugin, Plugin, StringComparison.Ordinal)) return false;

        if (other._points.Count != _points.Count) return false;

        for (int at = 0; at < _points.Count; at++)
            if (other._points[at] != _points[at]) return false;

        return true;
    }

    /// <summary>Puts a list of points in, sorted and deduplicated, for a file being read.</summary>
    public void TakePoints(IEnumerable<AutomationPoint> points)
    {
        _points.Clear();

        foreach (var point in points)
        {
            var one = point.Clamped();

            int at = IndexOf(one.Time);
            if (at >= 0) _points[at] = one;
            else _points.Insert(~at, one);
        }
    }

    /// <summary>The point at a time, or the complement of where it would go.</summary>
    /// <remarks>
    /// A binary search, which is only correct because the list is kept sorted, and it is what
    /// keeps the list sorted: everything that puts a point in inserts at the place this names.
    /// Times are compared exactly, which is right here because a time comes off a line number
    /// rather than out of any arithmetic.
    /// </remarks>
    private int IndexOf(double time)
    {
        int low = 0;
        int high = _points.Count - 1;

        while (low <= high)
        {
            int middle = (low + high) / 2;
            double at = _points[middle].Time;

            if (at == time) return middle;
            if (at < time) low = middle + 1;
            else high = middle - 1;
        }

        return ~low;
    }
}
