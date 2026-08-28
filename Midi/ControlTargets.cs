using Avalonia.Threading;
using JingleBox2.Audio.Plugins;
using JingleBox2.Machines;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Enums;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Machines.Interfaces;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Midi.Records;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Machines;
using JingleBox2.Tracker.Machines.Interfaces;

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
    /// <summary>The order a panel reads in. Holds nothing, so one is enough.</summary>
    private readonly IPanelOrder _order = new PanelOrder();

    /// <summary>The machines this run has.</summary>
    private readonly IMachineProjects _machines;

    private readonly TrackerViewModel _tracker;
    private readonly MachineRackViewModel? _rack;

    /// <param name="tracker">
    /// The song and everything in it: the tracks, their mixer strips, their instruments and the
    /// plugins on their chains. Almost every mapping is answered out of this.
    /// </param>
    /// <param name="rack">
    /// Where a controller actually gets laid out, and optional because the tracker alone is a
    /// complete answer for a song that is playing. See <see cref="OnRack"/>.
    /// </param>
    /// <param name="machines">
    /// The machines this run has, the one instance everything shares. Required rather than
    /// defaulted: a fresh one is empty, so a default would draw blank panels and report every
    /// machine missing, without an error anywhere to say why.
    /// </param>
    public ControlTargets(TrackerViewModel tracker, IMachineProjects machines, MachineRackViewModel? rack = null)
    {
        _tracker = tracker;
        _machines = machines;
        _rack = rack;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The master is a strip without being a track, so it is the one thing here that answers from
    /// outside the track numbers. Only the mixer kinds reach it: nothing is played on it, so it
    /// has no machine and no instrument's plugin to be pointed at.
    ///
    /// A link that reaches nothing writes a line naming what it asked for and what the track and
    /// the rack were actually holding, since that is the only way to tell "the mapping is wrong"
    /// from "the mapping is right and you are on the wrong track". The log is asked before the
    /// line is built, because a link whose machine is not on the track you are looking at answers
    /// nothing on every message that arrives, which is perfectly ordinary and not worth an
    /// allocation each time.
    /// </remarks>
    public IControlTarget? Find(ControlMapping mapping)
    {
        if (mapping is null) return null;

        int track = mapping.Scope == ControlScope.Fixed ? mapping.Track : _tracker.FocusedTrack;

        if (track == Tracker.TrackerPlayer.MasterStrip)
            return mapping.Kind switch
            {
                ControlKind.Mix => OnStrip(mapping, track),
                ControlKind.Insert => OnPlugin(mapping, track),
                _ => null
            };

        if (track < 0 || track >= _tracker.Song.TrackCount) return null;

        var found = mapping.Kind switch
        {
            ControlKind.Instrument => OnMachine(mapping, track) ?? OnRack(mapping),
            ControlKind.Insert => OnPlugin(mapping, track),
            ControlKind.Mix => OnStrip(mapping, track),
            ControlKind.Action => OnButton(mapping),
            _ => null
        };

        if (found == null && Log.On(LogArea.Midi))
            Log.Write(LogArea.Midi, () =>
                "controls: CC " + mapping.Cc + " names " + mapping.Kind + " '" + mapping.Key + "'"
                + " of machine '" + mapping.Machine + "' but nothing here answers: track " + track
                + " plays '" + _tracker.MachineOn(track) + "'"
                + ", the rack has '" + (_rack?.Editor?.MachineId ?? "nothing") + "' open");

        return found;
    }

    /// <summary>
    /// What a track has on it that a lane could be about, in the order the eye reads it.
    /// </summary>
    /// <remarks>
    /// The machine first, then the inserts in the order they are in the chain, then the strip.
    /// That is the order a track is read in on the screen, and a list in any other order would
    /// be a list somebody has to search rather than scan.
    ///
    /// A machine's parameters come in panel order rather than file order, the same rule the
    /// default layout follows, so the third one down this list is the third knob your eye lands
    /// on rather than the third line somebody happened to type. The ones the machine says are
    /// not part of the sound are left out: how much of the wave the picture shows is a knob and
    /// turns like the others, and a lane driving it would be a song insisting on somebody's
    /// zoom level.
    ///
    /// A plugin's read-only parameters are left out for a harder reason: a compressor's gain
    /// reduction meter is a parameter that reports rather than accepts, and a lane pointed at
    /// one would write into a value the plugin overwrites on the next block.
    ///
    /// The master has a strip and nothing else, since no machine plays through it and no
    /// instrument's plugin sits on it: everything has been played by the time it is reached. Its
    /// own inserts are offered, because those are the one thing it does have.
    /// </remarks>
    public IEnumerable<ControlChoice> On(int track)
    {
        if (track == Tracker.TrackerPlayer.MasterStrip)
        {
            foreach (var choice in OnInserts(track)) yield return choice;
            foreach (var choice in OnMixer(track)) yield return choice;

            yield break;
        }

        if (track < 0 || track >= _tracker.Song.TrackCount) yield break;

        string machine = _tracker.MachineOn(track);

        if (machine.Length > 0 && _machines.For(machine) is { } project)
        {
            var byKey = project.Parameters.ToDictionary(one => one.Key, StringComparer.Ordinal);

            foreach (var key in _order.Of(project.Panel))
            {
                if (!byKey.TryGetValue(key, out var parameter) || !parameter.Saved) continue;

                yield return new ControlChoice(
                    new ControlMapping
                    {
                        Kind = ControlKind.Instrument,
                        Scope = ControlScope.Fixed,
                        Track = track,
                        Machine = machine,
                        Key = key,
                        Ordinal = -1
                    },
                    project.Name,
                    parameter.Name.Length > 0 ? parameter.Name : parameter.Key,
                    parameter.Unit);
            }
        }

        foreach (var choice in OnInserts(track)) yield return choice;

        foreach (var choice in OnMixer(track)) yield return choice;
    }

    /// <summary>Every parameter of every plugin on a strip's chain, in the order they run.</summary>
    /// <remarks>
    /// A plugin's read-only parameters are left out: a compressor's gain reduction meter reports
    /// rather than accepts, and a lane pointed at one would write into a value the plugin
    /// overwrites on the next block.
    /// </remarks>
    private IEnumerable<ControlChoice> OnInserts(int track)
    {
        if (_tracker.InsertsOn(track) is not { } chain) yield break;

        int slot = 0;

        foreach (var device in chain.Devices)
        {
            if (device.Insert is not IPluginParameters plugin) continue;

            foreach (var parameter in plugin.Parameters())
            {
                if (parameter.IsReadOnly) continue;

                yield return new ControlChoice(
                    new ControlMapping
                    {
                        Kind = ControlKind.Insert,
                        Scope = ControlScope.Fixed,
                        Track = track,
                        Plugin = plugin.Info.Id,
                        Slot = slot,
                        Parameter = parameter.Id,
                        Ordinal = -1
                    },
                    plugin.Info.Name,
                    parameter.Name,
                    parameter.Units);
            }

            slot++;
        }

    }

    /// <summary>The handful of things every strip has, the master included.</summary>
    private IEnumerable<ControlChoice> OnMixer(int track)
    {
        foreach (var (control, said) in new[]
                 {
                     (MixControl.Volume, "Level"), (MixControl.Pan, "Pan"),
                     (MixControl.Mute, "Mute"), (MixControl.Solo, "Solo"),
                     (MixControl.Duck, "Duck")
                 })
        {
            yield return new ControlChoice(
                new ControlMapping
                {
                    Kind = ControlKind.Mix,
                    Scope = ControlScope.Fixed,
                    Track = track,
                    Mix = control,
                    Ordinal = -1
                },
                "Mixer",
                said);
        }
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

        if (_machines.For(machine) is not { } project) return null;

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
    ///
    /// A link that names no parameter at all is one nobody made: it means the third knob on
    /// whatever face is in front of you, and which parameter that is depends on the face. See
    /// <see cref="ControlMapping.Ordinal"/>.
    /// </remarks>
    private IControlTarget? OnMachine(ControlMapping mapping, int track)
    {
        if (mapping.Key.Length == 0 && mapping.Ordinal < 0) return null;

        string machine = _tracker.MachineOn(track);
        if (machine.Length == 0) return null;

        if (mapping.Machine.Length > 0
            && !string.Equals(mapping.Machine, machine, StringComparison.Ordinal))
            return null;

        if (_machines.For(machine) is not { } project) return null;

        string key = mapping.Key.Length > 0
            ? mapping.Key
            : _order.At(project.Panel, mapping.Ordinal);

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
    ///
    /// The master is asked for by name rather than found among the tracks, because it is kept
    /// apart from them rather than on the end of them: nothing that walks the tracks by counting
    /// reaches it, and it does not move when they are reordered.
    /// </remarks>
    private IControlTarget? OnStrip(ControlMapping mapping, int track)
    {
        var strip = track == Tracker.TrackerPlayer.MasterStrip
            ? _tracker.MasterStrip
            : _tracker.Strips.FirstOrDefault(one => one.Track == track);

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

            MixControl.Release => ("Duck release", TrackMix.MinDuckReleaseMs, TrackMix.MaxDuckReleaseMs,
                () => strip.DuckReleaseMs, value => strip.DuckReleaseMs = value),

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

        string said = _machines.For(mapping.Machine)?.Name ?? mapping.Machine;

        return new Target(
            said + " " + mapping.Key.Replace('_', ' '),
            0,
            1,
            () => 0,
            _ => ControlActions.Current.Fire(mapping.Machine, mapping.Key),
            this,
            mapping);
    }

    /// <summary>What a track is called in a target's name, which ends in the track it is on.</summary>
    private static string Named(int track) =>
        "TR-" + (track + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>One thing a knob is driving, wrapped so the write lands on the drawing thread.</summary>
    private sealed class Target : IControlTarget
    {
        private readonly Func<double> _read;
        private readonly Action<double> _write;
        private readonly ControlTargets _desk;
        private readonly ControlMapping _mapping;

        /// <param name="name">What to call it, ending in the track it is on so a status line reads.</param>
        /// <param name="min">The bottom of the parameter's own range, which a mapping is scaled into.</param>
        /// <param name="max">And the top of it.</param>
        /// <param name="read">Where it stands now, for a control that has to pick the value up.</param>
        /// <param name="write">Where a new value goes, called on the drawing thread and not here.</param>
        /// <param name="desk">The one that queues the write, so it lands on the drawing thread.</param>
        /// <param name="mapping">What was pointed at this, kept so a write can say what moved.</param>
        /// <param name="unit">
        /// What the number is measured in, where the thing that owns it said. Empty otherwise,
        /// and then a reading is the number on its own.
        /// </param>
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

        /// <summary>What it is measured in, when the thing that owns it said. Empty otherwise.</summary>
        private readonly string _unit;

        /// <inheritdoc/>
        /// <remarks>The number, and what it is measured in when the machine said.</remarks>
        public string Reads(double value)
        {
            string said = value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

            return _unit.Length > 0 ? said + " " + _unit : said;
        }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public double Min { get; }

        /// <inheritdoc/>
        public double Max { get; }

        /// <inheritdoc/>
        /// <remarks>
        /// Where the parameter is, or where it is about to be when something is on its way.
        ///
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

        /// <inheritdoc/>
        /// <remarks>Queued rather than written, so it lands on the drawing thread and coalesces.</remarks>
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

    /// <summary>Whether a trip to the drawing thread is already booked.</summary>
    private bool _posted;

    /// <summary>The value on its way to a parameter, or nothing when none is.</summary>
    private double? Waiting(ControlMapping mapping)
    {
        lock (_waiting)
            return _waiting.TryGetValue(mapping, out var held) ? held.Value : null;
    }

    /// <summary>
    /// Puts a value in the queue, and asks for one trip to the drawing thread if none is booked.
    /// </summary>
    /// <remarks>
    /// At <c>DispatcherPriority.Input</c>, so a hand on three knobs cannot starve the drawing of
    /// the panels it is moving.
    /// </remarks>
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

    /// <summary>
    /// Puts everything that is waiting where it goes, on the drawing thread.
    /// </summary>
    /// <remarks>
    /// Each write is swallowed on its own: one parameter that will not take a value is one knob
    /// gone quiet, and letting it throw here would take the rest of the desk with it.
    /// </remarks>
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
            try { held.Write(held.Value); }
            catch (Exception) { }
        }
    }
}
