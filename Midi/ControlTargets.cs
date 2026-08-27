using Avalonia.Threading;
using JingleBox2.Audio.Plugins;
using JingleBox2.Machines;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Diagnostics;

namespace JingleBox2.Midi;

/// <summary>
/// Turns a mapping into the thing in the program it names, as things stand this second.
/// </summary>
/// <remarks>
/// The third adapter, and the same job as the other two: the router knows mappings, this knows
/// the application. See <see cref="PadTriggerAdapter"/> and <see cref="TrackerNoteAdapter"/>.
///
/// Two things happen here that do not happen there. A mapping about a machine is only answered
/// when the track really is playing that machine, which is what stops Zampler's filter knob
/// moving something arbitrary on a drum machine. And every write is put on the drawing thread
/// and coalesced on the way, because a hand on three knobs is three hundred messages a second
/// and the panel only needs the last one of each.
/// </remarks>
public sealed class ControlTargets : IControlTargets
{
    private readonly TrackerViewModel _tracker;
    private readonly MachineRackViewModel? _rack;

    public ControlTargets(TrackerViewModel tracker, MachineRackViewModel? rack = null)
    {
        _tracker = tracker;
        _rack = rack;
    }

    public IControlTarget? Find(ControlMapping mapping)
    {
        if (mapping is null) return null;

        int track = mapping.Scope == ControlScope.Fixed ? mapping.Track : _tracker.FocusedTrack;

        if (track < 0 || track >= _tracker.Song.TrackCount) return null;

        var found = mapping.Kind switch
        {
            ControlKind.Instrument => OnMachine(mapping, track) ?? OnRack(mapping),
            ControlKind.Insert => OnPlugin(mapping, track),
            ControlKind.Mix => OnStrip(mapping, track),
            ControlKind.Action => OnButton(mapping),
            _ => null
        };

        // Also asked first: a link whose machine is not on the track you are looking at answers
        // nothing on every message that arrives, which is a perfectly ordinary thing for it to
        // do and not worth an allocation each time.
        if (found == null && Log.On(LogArea.Midi))
            Log.Write(LogArea.Midi, () =>
                "controls: CC " + mapping.Cc + " names " + mapping.Kind + " '" + mapping.Key + "'"
                + " of machine '" + mapping.Machine + "' but nothing here answers: track " + track
                + " plays '" + _tracker.MachineOn(track) + "'"
                + ", the rack has '" + (_rack?.Editor?.MachineId ?? "nothing") + "' open");

        return found;
    }

    /// <summary>
    /// The same knob on the machine open on the rack, when no track answered for it.
    /// </summary>
    /// <remarks>
    /// The rack is where a controller gets laid out, because it is where the machines are: you
    /// open Zampler, point at its filter, turn a knob. Resolved against tracks alone, that link
    /// reaches nothing at all until some song puts a Zampler on a track, so the knob you just
    /// assigned does nothing and looks broken.
    ///
    /// A track first and the rack second, in that order, because a track is the instrument that
    /// is making sound and the rack is the one being worked on. In practice they do not compete:
    /// a song with a Zampler on a track is a song you are working in, and the rack is where you
    /// go when you are not.
    /// </remarks>
    private IControlTarget? OnRack(ControlMapping mapping)
    {
        if (mapping.Key.Length == 0) return null;
        if (_rack?.Editor is not { } editor) return null;

        string machine = editor.MachineId;
        if (machine.Length == 0) return null;

        if (mapping.Machine.Length > 0
            && !string.Equals(mapping.Machine, machine, StringComparison.Ordinal))
            return null;

        if (Tracker.Machines.MachineProjects.For(machine) is not { } project) return null;

        var parameter = project.Parameters.FirstOrDefault(one => one.Key == mapping.Key);
        if (parameter is null) return null;

        var values = editor.Values;
        if (values is null) return null;

        string said = parameter.Name.Length > 0 ? parameter.Name : parameter.Key;

        return new Target(
            project.Name + " " + said + " on the rack",
            parameter.Min,
            parameter.Max,
            () => values.Get(mapping.Key),
            value => Written(values, mapping.Key, value),
            this,
            mapping,
            parameter.Unit);
    }

    /// <summary>
    /// A knob on the machine a track plays, when that is the machine the mapping is about.
    /// </summary>
    /// <remarks>
    /// The check on the machine is the point of the whole design. Knob one is not "the first
    /// knob on this track", it is "Zampler's cutoff", so a track playing a drum machine is not
    /// driven by it at all and nothing has to be reassigned when a track changes hands.
    /// </remarks>
    private IControlTarget? OnMachine(ControlMapping mapping, int track)
    {
        if (mapping.Key.Length == 0 && mapping.Ordinal < 0) return null;

        string machine = _tracker.MachineOn(track);
        if (machine.Length == 0) return null;

        if (mapping.Machine.Length > 0
            && !string.Equals(mapping.Machine, machine, StringComparison.Ordinal))
            return null;

        if (Tracker.Machines.MachineProjects.For(machine) is not { } project) return null;

        // A link that names no parameter is one nobody made: it means the third knob on
        // whatever face is in front of you, and which parameter that is depends on the face.
        string key = mapping.Key.Length > 0
            ? mapping.Key
            : Machines.PanelOrder.At(project.Panel, mapping.Ordinal);

        if (key.Length == 0) return null;

        var parameter = project.Parameters.FirstOrDefault(one => one.Key == key);
        if (parameter is null) return null;

        var values = _tracker.MachineValuesOn(track);
        if (values is null) return null;

        string said = parameter.Name.Length > 0 ? parameter.Name : parameter.Key;

        return new Target(
            project.Name + " " + said + " on " + Named(track),
            parameter.Min,
            parameter.Max,
            () => values.Get(key),
            value => Written(values, key, value),
            this,
            mapping,
            parameter.Unit);
    }

    /// <summary>
    /// Writes a setting, and says so when the machine does not hold what it was given.
    /// </summary>
    /// <remarks>
    /// A setting written and read back should be the setting. When it is not, something other
    /// than the write has moved it, and no amount of looking at what arrived on the wire will
    /// show that: the wire was innocent. So the disagreement is caught here, where both halves
    /// are in one place, and named.
    /// </remarks>
    private static void Written(IMachineValues values, string key, double value)
    {
        values.Set(key, value);

        double back = values.Get(key);

        if (Math.Abs(back - value) <= 0.001) return;

        Log.Write(LogArea.Midi, () =>
            "controls: wrote " + value.ToString("0.####") + " to '" + key
            + "' AND IT READS BACK " + back.ToString("0.####"));
    }

    /// <summary>A knob on a plugin in a track's chain, when that is the plugin it is about.</summary>
    private IControlTarget? OnPlugin(ControlMapping mapping, int track)
    {
        var chain = _tracker.InsertsOn(track);
        if (chain is null) return null;

        var wanted = Insert(chain, mapping);
        if (wanted is null) return null;

        var parameter = wanted.Parameters().FirstOrDefault(one => one.Id == mapping.Parameter);
        if (parameter is null || parameter.IsReadOnly) return null;

        double min = parameter.Normalized ? 0 : parameter.Minimum;
        double max = parameter.Normalized ? 1 : parameter.Maximum;

        return new Target(
            wanted.Info.Name + " " + parameter.Name + " on " + Named(track),
            min,
            max,
            () => wanted.ValueOf(mapping.Parameter),
            value => wanted.SetValue(mapping.Parameter, value),
            this,
            mapping,
            parameter.Units);
    }

    /// <summary>
    /// Which plugin in the chain the mapping means.
    /// </summary>
    /// <remarks>
    /// By what it is rather than by where it sits, when the mapping says what it is. An insert
    /// moved up the chain is the same plugin, and a mapping that stopped working because a
    /// compressor was dragged above a delay would be a mapping nobody trusted.
    /// </remarks>
    private static IPluginParameters? Insert(PluginChain chain, ControlMapping mapping)
    {
        var loaded = chain.Devices
            .Select(device => device.Insert as IPluginParameters)
            .Where(one => one != null)
            .ToList();

        if (mapping.Plugin.Length > 0)
            return loaded.FirstOrDefault(one =>
                string.Equals(one!.Info.Id, mapping.Plugin, StringComparison.Ordinal));

        return mapping.Slot >= 0 && mapping.Slot < loaded.Count ? loaded[mapping.Slot] : null;
    }

    /// <summary>Something on a track's channel strip.</summary>
    /// <remarks>
    /// Written through the strip rather than into the song's <c>TrackMix</c>, so the fader on
    /// the screen moves with the fader in your hand. Writing underneath it would change the
    /// sound and leave the mixer showing the old value.
    /// </remarks>
    private IControlTarget? OnStrip(ControlMapping mapping, int track)
    {
        var strip = _tracker.Strips.FirstOrDefault(one => one.Track == track);
        if (strip is null) return null;

        var (name, min, max, read, write) = mapping.Mix switch
        {
            MixControl.Volume => ("Level", TrackMix.MinVolume, TrackMix.MaxVolume,
                (Func<double>)(() => strip.Volume), (Action<double>)(value => strip.Volume = value)),

            MixControl.Pan => ("Pan", -1.0, 1.0,
                () => strip.Pan, value => strip.Pan = value),

            MixControl.Mute => ("Mute", 0.0, 1.0,
                () => strip.Mute ? 1 : 0, value => strip.Mute = value >= 0.5),

            MixControl.Solo => ("Solo", 0.0, 1.0,
                () => strip.Solo ? 1 : 0, value => strip.Solo = value >= 0.5),

            MixControl.Duck => ("Duck", TrackMix.MinDuck, TrackMix.MaxDuck,
                () => strip.Duck, value => strip.Duck = value),

            _ => ("", 0.0, 0.0, () => 0.0, (Action<double>)(_ => { }))
        };

        if (name.Length == 0) return null;

        return new Target(name + " on " + Named(track), min, max, read, write, this, mapping);
    }

    /// <summary>
    /// A button on a machine's panel, which is a press rather than a position.
    /// </summary>
    /// <remarks>
    /// Wrapped as a target like everything else so the router needs no second path through it,
    /// with nought and one for a range and nothing to read back: a button has no position to be
    /// picked up from. Where the press comes out is <see cref="ControlActions"/>, and why it
    /// has to go there rather than be done here is written up on it.
    /// </remarks>
    private IControlTarget? OnButton(ControlMapping mapping)
    {
        if (mapping.Key.Length == 0) return null;

        string said = Tracker.Machines.MachineProjects.For(mapping.Machine)?.Name ?? mapping.Machine;

        return new Target(
            said + " " + mapping.Key.Replace('_', ' '),
            0,
            1,
            () => 0,
            _ => ControlActions.Current.Fire(mapping.Machine, mapping.Key),
            this,
            mapping);
    }

    private static string Named(int track) =>
        "TR-" + (track + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>One thing a knob is driving, wrapped so the write lands on the drawing thread.</summary>
    private sealed class Target : IControlTarget
    {
        private readonly Func<double> _read;
        private readonly Action<double> _write;
        private readonly ControlTargets _desk;
        private readonly ControlMapping _mapping;

        public Target(string name, double min, double max, Func<double> read, Action<double> write,
                      ControlTargets desk, ControlMapping mapping, string unit = "")
        {
            Name = name;
            Min = min;
            Max = max;
            _read = read;
            _write = write;
            _desk = desk;
            _mapping = mapping;
            _unit = unit;
        }

        private readonly string _unit;

        /// <summary>The number, and what it is measured in when the machine said.</summary>
        public string Reads(double value)
        {
            string said = value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

            return _unit.Length > 0 ? said + " " + _unit : said;
        }

        public string Name { get; }
        public double Min { get; }
        public double Max { get; }

        /// <summary>
        /// Where the parameter is, or where it is about to be when something is on its way.
        /// </summary>
        /// <remarks>
        /// The waiting value first, and this is not a nicety. Writes are coalesced onto the
        /// drawing thread, so between a message arriving and the panel being drawn the machine
        /// still holds the old value. Anything that works out its next value from this one then
        /// works it out from a number that is already out of date, and since only the last write
        /// survives the coalescing, a burst of them all say the same thing.
        ///
        /// For a knob that reports a position that costs nothing, because the new value comes
        /// from the message and not from here. For one that reports movement it costs almost
        /// everything: twenty notches of turning arrive in the time the drawing thread takes to
        /// wake up once, every one of them adds a notch to the same stale number, and the
        /// parameter moves one notch. The knob feels like it is stuck in treacle, which is
        /// exactly what it is.
        ///
        /// So the pending value is the answer while there is one. It is what the parameter will
        /// hold a millisecond from now, nothing else can be writing it in the meantime, and it
        /// makes a burst of relative movement add up to what the hand actually did.
        /// </remarks>
        public double Value
        {
            get
            {
                if (_desk.Waiting(_mapping) is { } coming) return coming;

                try { return _read(); } catch (Exception) { return Min; }
            }
        }

        public void Set(double value) => _desk.Queue(_mapping, _write, value);
    }

    /// <summary>
    /// What is waiting to be written, one value per mapping.
    /// </summary>
    /// <remarks>
    /// Per mapping and not a queue, so a knob swept from one end to the other writes once with
    /// where it ended up rather than a hundred and twenty eight times through where it passed.
    /// The sound is the same and the panel is drawn once.
    /// </remarks>
    private readonly Dictionary<ControlMapping, (Action<double> Write, double Value)> _waiting = new();

    private bool _posted;

    /// <summary>The value on its way to a parameter, or nothing when none is.</summary>
    private double? Waiting(ControlMapping mapping)
    {
        lock (_waiting)
            return _waiting.TryGetValue(mapping, out var held) ? held.Value : null;
    }

    private void Queue(ControlMapping mapping, Action<double> write, double value)
    {
        lock (_waiting)
        {
            _waiting[mapping] = (write, value);

            if (_posted) return;
            _posted = true;
        }

        Dispatcher.UIThread.Post(Write, DispatcherPriority.Input);
    }

    private void Write()
    {
        KeyValuePair<ControlMapping, (Action<double> Write, double Value)>[] due;

        lock (_waiting)
        {
            due = _waiting.ToArray();
            _waiting.Clear();
            _posted = false;
        }

        foreach (var (_, held) in due)
        {
            // One parameter that will not take a value is one knob, not a dead controller.
            try { held.Write(held.Value); }
            catch (Exception) { }
        }
    }
}
