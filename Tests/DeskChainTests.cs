using System.Collections.Generic;
using System.Collections.ObjectModel;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Midi;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The desk's MASTER has a master effect chain hung on the output bus, and picking MASTER on the
/// mixer shows that chain and no automation, since MASTER is not part of a song.
/// </summary>
public class DeskChainTests
{
    /// <summary>A bus that keeps what it is handed and makes no sound.</summary>
    private sealed class Bus : IOutputBus
    {
        /// <inheritdoc/>
        public Audio.Plugins.Interfaces.IAudioInsert? Insert { get; set; }

        /// <inheritdoc/>
        public int Rate { get; set; }

        /// <inheritdoc/>
        public float Level { get; set; } = 1f;

        /// <inheritdoc/>
        public double Pan { get; set; }

        /// <inheritdoc/>
        public bool Mute { get; set; }

        /// <inheritdoc/>
        public bool IsOpen => true;

        /// <inheritdoc/>
        public int Handle => 0;

        /// <inheritdoc/>
        public int BufferMs { get; set; }

        /// <inheritdoc/>
        public bool Present => true;

        /// <inheritdoc/>
        public int Sources => 0;

        /// <inheritdoc/>
        public (float Left, float Right) Reading => (0f, 0f);

        /// <inheritdoc/>
        public bool Add(int channel) => true;

        /// <inheritdoc/>
        public void Remove(int channel) { }

        /// <inheritdoc/>
        public bool Holds(int channel) => false;

        /// <inheritdoc/>
        public void HearOnly(IReadOnlyCollection<int> channels) { }

        /// <inheritdoc/>
        public bool Open(int rate, int channels, bool decoding) => true;

        /// <inheritdoc/>
        public void Close() { }

        /// <inheritdoc/>
        public void Dispose() { }
    }

    /// <summary>The chain is the one on the bus, at the bus's rate, or an ordinary rate before it opens.</summary>
    [Fact]
    public void The_chain_hangs_on_the_bus()
    {
        var bus = new Bus();
        var desk = new DeskPluginTarget(bus);

        Assert.Same(desk.Chain, bus.Insert);
        Assert.Equal(PadPluginTarget.AssumedSampleRate, desk.SampleRate);

        bus.Rate = 48000;
        Assert.Equal(48000, desk.SampleRate);
    }

    /// <summary>Picking MASTER shows its chain and no automation; picking a track brings the automation back.</summary>
    [Fact]
    public void Master_shows_its_chain_and_no_automation()
    {
        var tracker = new TrackerViewModel(new QuietAudio(), new SoundMachineRack(),
            new ObservableCollection<Recording>(), new SoundMachineProjects());
        tracker.UseAutomation(new ControlTargets(tracker, new SoundMachineProjects()));
        tracker.ShowsMixerLanes = true;

        tracker.PickTrack(TrackerViewModel.DeskStrip);

        Assert.True(tracker.MixerShowsDesk);
        Assert.False(tracker.MixerShowsLanes);
        Assert.False(tracker.MixerShowsSongChain);
        Assert.False(tracker.MasterStrip!.IsSelected);
        Assert.DoesNotContain(tracker.Strips, one => one.IsSelected);

        tracker.PickTrack(1);

        Assert.False(tracker.MixerShowsDesk);
        Assert.True(tracker.MixerShowsLanes);
        Assert.Equal(1, tracker.MixerLanes!.Track);

        tracker.PickTrack(TrackerPlayer.MasterStrip);

        Assert.True(tracker.MixerShowsSongChain);
        Assert.True(tracker.MixerShowsLanes);

        tracker.Finished();
    }
}
