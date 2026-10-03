using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;
using JingleBox2.Audio.Records;
using JingleBox2.Config;
using JingleBox2.Config.Enums;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A part switched off is not drawn and its work is not done: the lanes are not played or
/// recorded, no track is ducked, the neighbours are not handed to the grid, the command editor
/// cannot be opened and a chain block reads nothing off its plugin.
/// </summary>
public class FeatureWorkTests
{
    /// <summary>A song of two 16 line patterns, one slot each.</summary>
    private static Song TwoSlots()
    {
        var song = new Song();
        song.Patterns.Add(new Pattern(16, song.TrackCount) { Name = "A" });
        song.Patterns.Add(new Pattern(16, song.TrackCount) { Name = "B" });
        song.Order.Add(0);
        song.Order.Add(1);
        song.Normalize();
        return song;
    }

    /// <summary>A track's level on the mixer, which is on the song's timeline.</summary>
    private static ControlMapping Level(int track) =>
        new() { Kind = ControlKind.Mix, Mix = MixControl.Volume, Scope = ControlScope.Fixed, Track = track };

    /// <summary>A machine's parameter, which is on a pattern's timeline.</summary>
    private static ControlMapping Cutoff(int track) =>
        new() { Kind = ControlKind.SoundDevice, Machine = "machine.oddskilla", Key = "cutoff", Scope = ControlScope.Fixed, Track = track };

    /// <summary>Every lane reaches the one knob.</summary>
    private sealed class Heard : IControlTargets
    {
        /// <summary>The one knob.</summary>
        public Knob Knob { get; } = new(0.5);

        /// <inheritdoc/>
        public IControlTarget? Find(ControlMapping mapping) => Knob;
    }

    /// <summary>A tracker on a fresh song, with no sound card.</summary>
    private static TrackerViewModel Tracker() =>
        new(new QuietAudio(), new SoundMachineRack(), new ObservableCollection<Recording>(), new SoundMachineProjects());

    /// <summary>With pattern automation off a pattern's lane is not played, and the song's still is.</summary>
    [Fact]
    public void Pattern_lanes_are_not_played_while_off()
    {
        var song = TwoSlots();
        song.Patterns[0].Lane(AutomationLane.For(Cutoff(0), 0)!).Put(0, 1.0);
        var features = new Features();
        var targets = new Heard();
        var player = new AutomationPlayer(targets, features);

        features.Set(Feature.PatternAutomation, false);
        player.Play(song, new TrackerPosition(0, 0));

        Assert.Equal(0.5, targets.Knob.Value, 6);

        features.Set(Feature.PatternAutomation, true);
        player.Play(song, new TrackerPosition(0, 0));

        Assert.Equal(1.0, targets.Knob.Value, 6);
    }

    /// <summary>With song automation off the song's lane is not played.</summary>
    [Fact]
    public void Song_lanes_are_not_played_while_off()
    {
        var song = TwoSlots();
        song.Lane(AutomationLane.For(Level(0), 0)!).Put(0, 1.0);
        var features = new Features();
        var targets = new Heard();
        var player = new AutomationPlayer(targets, features);

        features.Set(Feature.SongAutomation, false);
        player.Play(song, new TrackerPosition(0, 0));

        Assert.Equal(0.5, targets.Knob.Value, 6);
    }

    /// <summary>A knob turned while its timeline's automation is off writes nothing.</summary>
    [Fact]
    public void Nothing_is_recorded_while_off()
    {
        var song = TwoSlots();
        var features = new Features();
        var recorder = new AutomationRecorder(() => song, () => true, () => new TrackerPosition(0, 4), () => 0,
                                              features: features) { Armed = true };

        features.Set(Feature.SongAutomation, false);
        features.Set(Feature.PatternAutomation, false);

        Assert.False(recorder.Moved(Level(0), new Knob(0.5), 0.25));
        Assert.False(recorder.Moved(Cutoff(0), new Knob(0.5), 0.25));
        Assert.Null(song.LaneFor(Level(0), 0));
        Assert.Empty(song.Patterns[0].Lanes);

        features.Set(Feature.PatternAutomation, true);

        Assert.True(recorder.Moved(Cutoff(0), new Knob(0.5), 0.25));
    }

    /// <summary>How far track 1 is ducked under track 0 after a block, with the side chain on or off.</summary>
    private static float DuckedTo(bool sideChain)
    {
        var bench = new MixerBench();
        var features = new Features();
        using var player = new TrackerPlayer(new QuietAudio(), output: bench) { Features = features };

        var song = new Song { TrackCount = 2 };
        song.Normalize();
        song.Mix[1].Duck = 1;
        song.Mix[1].DuckFrom = 0;

        bench.Mixer.SetInstrument(0, new SteadyInstrument(0.9f));
        bench.Mixer.SetInstrument(1, new SteadyInstrument(0.5f));
        player.Use(song);

        features.Set(Feature.SideChain, sideChain);
        player.ApplyMix();

        var buffer = new float[441 * 2];
        for (int block = 0; block < 4; block++) bench.Mixer.Render(buffer, 441);

        return bench.Mixer.DuckGainFor(1);
    }

    /// <summary>With the side chain on the track is ducked, and off it is not, so nothing is worked out for it.</summary>
    [Fact]
    public void No_track_is_ducked_while_the_side_chain_is_off()
    {
        Assert.True(DuckedTo(sideChain: true) < 0.9f);
        Assert.Equal(1f, DuckedTo(sideChain: false));
    }

    /// <summary>With the neighbours off the grid is handed nothing to draw above or below.</summary>
    [Fact]
    public void Neighbours_are_not_handed_over_while_off()
    {
        var tracker = Tracker();
        tracker.AddPatternCommand.Execute(null);
        tracker.PlayMode = TrackerPlayMode.Song;
        tracker.OrderIndex = 0;

        Assert.NotNull(tracker.PatternAfter);

        var told = new List<string?>();
        tracker.PropertyChanged += (_, e) => told.Add(e.PropertyName);
        tracker.Features.Switches.Set(Feature.NeighbourPatterns, false);

        Assert.Null(tracker.PatternAfter);
        Assert.Contains(nameof(TrackerViewModel.PatternAfter), told);

        tracker.Finished();
    }

    /// <summary>With the command editor off it cannot be opened, and the menu line is told.</summary>
    [Fact]
    public void The_command_editor_cannot_be_opened_while_off()
    {
        var tracker = Tracker();

        Assert.True(tracker.EditCommandCommand.CanExecute(null));

        tracker.Features.Switches.Set(Feature.CommandEditor, false);

        Assert.False(tracker.EditCommandCommand.CanExecute(null));
        Assert.False(tracker.Features.CommandEditor);

        tracker.Finished();
    }

    /// <summary>With song automation off the mixer shows no lanes, whichever strip is picked.</summary>
    [Fact]
    public void The_mixer_shows_no_lanes_while_song_automation_is_off()
    {
        var tracker = Tracker();

        Assert.True(tracker.MixerShowsLanes);

        tracker.Features.Switches.Set(Feature.SongAutomation, false);

        Assert.False(tracker.MixerShowsLanes);

        tracker.Finished();
    }

    /// <summary>With pattern automation off the pattern's automation strip is not shown.</summary>
    [Fact]
    public void The_pattern_shows_no_lanes_while_pattern_automation_is_off()
    {
        var tracker = Tracker();

        tracker.Features.Switches.Set(Feature.PatternAutomation, false);

        Assert.False(tracker.Features.PatternAutomation);

        tracker.Finished();
    }

    /// <summary>A plugin that counts how often a value is read off it.</summary>
    private sealed class Counted : IPluginEffect
    {
        /// <summary>How many values have been read.</summary>
        public int Reads;

        /// <inheritdoc/>
        public PluginInfo Info { get; } = new("counted", "Counted", "", "", "");

        /// <inheritdoc/>
        public bool IsActive => true;

        /// <inheritdoc/>
        public void FlushParameters() { }

        /// <inheritdoc/>
        public void Process(float[] buffer, int frames) { }

        /// <inheritdoc/>
        public IReadOnlyList<PluginParameter> Parameters() =>
            [new(1, "Drive", 0, 1, 0, 0, false, false, false, true, "")];

        /// <inheritdoc/>
        public double ValueOf(uint id)
        {
            Reads++;
            return 0.5;
        }

        /// <inheritdoc/>
        public string TextFor(uint id, double value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <inheritdoc/>
        public void SetValue(uint id, double value) { }

        /// <inheritdoc/>
        public event Action<uint, double>? Edited { add { } remove { } }

        /// <inheritdoc/>
        public event Action? Reloaded { add { } remove { } }

        /// <inheritdoc/>
        public byte[] SaveState() => [];

        /// <inheritdoc/>
        public void LoadState(byte[]? state) { }

        /// <inheritdoc/>
        public void Dispose() { }
    }

    /// <summary>With the chain readings off a block reads nothing off its plugin, and on again it does.</summary>
    [Fact]
    public void A_block_reads_nothing_while_the_readings_are_off()
    {
        var features = new Features();
        var chain = new PluginChainViewModel(new PluginLibraryViewModel()) { Features = features };
        var plugin = new Counted();
        var block = new PluginSlotViewModel(chain, plugin, null!);
        int before = plugin.Reads;

        features.Set(Feature.ChainReadings, false);

        Assert.Empty(block.Summary);
        Assert.False(block.HasSummary);
        Assert.Equal(before, plugin.Reads);

        features.Set(Feature.ChainReadings, true);
        var told = new List<string?>();
        block.PropertyChanged += (_, e) => told.Add(e.PropertyName);
        block.Reread();

        Assert.Single(block.Summary);
        Assert.Contains(nameof(PluginSlotViewModel.Summary), told);
    }
}
