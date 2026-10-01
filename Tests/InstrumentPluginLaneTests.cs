using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;
using JingleBox2.Audio.Records;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A track whose instrument is a plugin offers that plugin's parameters to a lane, and a lane on
/// one moves the instrument rather than an insert that happens to be the same plugin.
/// </summary>
public class InstrumentPluginLaneTests
{
    /// <summary>
    /// A plugin instrument with two knobs, a meter, a MIDI controller stand-in that is not for
    /// automating, and one it hides.
    /// </summary>
    private sealed class Synth : IPluginParameters
    {
        /// <summary>Where each knob stands.</summary>
        public readonly Dictionary<uint, double> Values = new() { [7] = 0.25, [8] = 0.5, [9] = 0, [10] = 0, [11] = 0 };

        /// <inheritdoc/>
        public PluginInfo Info { get; } = new("vst3:Dark707", "Dark707", "", "", "", IsInstrument: true);

        /// <inheritdoc/>
        public IReadOnlyList<PluginParameter> Parameters() =>
        [
            new(7, "Decay", 0, 1, 0, 0, false, false, false, true, "s"),
            new(8, "Tone", 0, 1, 0, 0, false, false, false, true, ""),
            new(9, "Meter", 0, 1, 0, 0, false, true, false, true, ""),
            new(10, "MIDI CC 0|1", 0, 1, 0, 0, false, false, false, true, CanAutomate: false),
            new(11, "Secret", 0, 1, 0, 0, true, false, false, true)
        ];

        /// <inheritdoc/>
        public double ValueOf(uint id) => Values[id];

        /// <inheritdoc/>
        public string TextFor(uint id, double value) => value.ToString();

        /// <inheritdoc/>
        public void SetValue(uint id, double value) => Values[id] = value;

        /// <inheritdoc/>
        public event Action<uint, double>? Edited { add { } remove { } }

        /// <inheritdoc/>
        public event Action? Reloaded { add { } remove { } }

        /// <inheritdoc/>
        public byte[] SaveState() => [];

        /// <inheritdoc/>
        public void LoadState(byte[]? state) { }
    }

    /// <summary>A tracker whose second track plays the synth, and nothing else plays a plugin.</summary>
    private static (TrackerViewModel Tracker, ControlTargets Targets, Synth Synth) Bench()
    {
        var tracker = new TrackerViewModel(
            new QuietAudio(), new SoundMachineRack(), new ObservableCollection<Recording>(), new SoundMachineProjects());
        var synth = new Synth();

        return (tracker, new ControlTargets(tracker, new SoundMachineProjects(),
            instruments: track => track == 1 ? synth : null), synth);
    }

    /// <summary>
    /// The instrument's knobs are offered, named for the plugin; a meter, a parameter the plugin
    /// does not offer for automating and one it hides are not.
    /// </summary>
    [Fact]
    public void A_plugin_instrument_offers_its_knobs()
    {
        var (tracker, targets, _) = Bench();

        var choices = targets.On(1).ToList();
        var mine = choices.Where(one => one.Device == "Dark707").ToList();

        Assert.Equal(["Decay", "Tone"], mine.Select(one => one.Name));
        Assert.All(mine, one => Assert.Equal(ControlMapping.InstrumentSlot, one.Mapping.Slot));
        Assert.True(choices.IndexOf(mine[0]) < choices.FindIndex(one => one.Device == "Mixer"));
        Assert.DoesNotContain(targets.On(0), one => one.Device == "Dark707");

        tracker.Finished();
    }

    /// <summary>A lane on the instrument reaches it and reads where the knob stands.</summary>
    [Fact]
    public void A_lane_on_the_instrument_reaches_it()
    {
        var (tracker, targets, synth) = Bench();
        var mapping = targets.On(1).First(one => one.Name == "Decay").Mapping;

        var target = targets.Find(mapping);

        Assert.NotNull(target);
        Assert.Equal(0.25, target!.Value);
        synth.Values[7] = 0.75;
        Assert.Equal(0.75, target.Value);

        tracker.Finished();
    }

    /// <summary>The track playing another plugin now answers nothing rather than a neighbour's knob.</summary>
    [Fact]
    public void A_lane_for_another_plugin_answers_nothing()
    {
        var (tracker, targets, _) = Bench();
        var mapping = targets.On(1).First(one => one.Name == "Decay").Mapping;
        mapping.Plugin = "vst3:Somebody";

        Assert.Null(targets.Find(mapping));

        tracker.Finished();
    }

    /// <summary>A track whose plugin has not loaded offers nothing of it and reaches nothing.</summary>
    [Fact]
    public void An_unloaded_instrument_offers_nothing()
    {
        var (tracker, targets, _) = Bench();
        var mapping = targets.On(1).First(one => one.Name == "Decay").Mapping;
        mapping.Track = 2;

        Assert.DoesNotContain(targets.On(2), one => one.Device == "Dark707");
        Assert.Null(targets.Find(mapping));

        tracker.Finished();
    }

    /// <summary>A lane on the instrument is not the lane for the same plugin used as an insert.</summary>
    [Fact]
    public void An_instrument_lane_is_not_an_insert_lane()
    {
        var instrument = new ControlMapping
        {
            Kind = ControlKind.Plugin, Scope = ControlScope.Fixed, Track = 1,
            Plugin = "vst3:Dark707", Slot = ControlMapping.InstrumentSlot, Parameter = 7
        };
        var insert = new ControlMapping
        {
            Kind = ControlKind.Plugin, Scope = ControlScope.Fixed, Track = 1,
            Plugin = "vst3:Dark707", Slot = 0, Parameter = 7
        };

        var lane = AutomationLane.For(instrument, 1)!;

        Assert.True(lane.About(instrument, 1));
        Assert.False(lane.About(insert, 1));
        Assert.False(AutomationLane.For(insert, 1)!.About(instrument, 1));
    }

    /// <summary>The instrument lane keeps saying it is about the instrument through a save.</summary>
    [Fact]
    public void An_instrument_lane_survives_a_save()
    {
        var song = new Song();
        song.Normalize();
        while (song.Mix.Count < song.TrackCount) song.Mix.Add(new TrackMix());
        song.Patterns[0].Lane(new AutomationLane
        {
            Track = 1, Kind = ControlKind.Plugin, Plugin = "vst3:Dark707",
            Slot = ControlMapping.InstrumentSlot, Parameter = 7
        }).Put(0, 0.5);

        var back = SongStore.Uncopy(SongStore.Copy(song))!.Patterns[0].Lanes[0];

        Assert.Equal(ControlMapping.InstrumentSlot, back.Slot);
    }
}
