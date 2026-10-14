using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;
using JingleBox2.Audio.Records;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The SONG strip's chain on the mixer follows the plugins the player really holds, when the
/// player builds its chains again underneath it.
/// </summary>
/// <remarks>
/// The rows hold the plugins themselves. Left pointing at a chain that was rebuilt, a row reads
/// as the effect it was and its window asks a plugin that has been disposed, which a bridged
/// plugin answers as having no process, so the window shows the host's knobs instead of the
/// plugin's own face.
/// </remarks>
public class SongChainFollowsTests
{
    /// <summary>A tracker with a default song in it.</summary>
    private static TrackerViewModel Tracker() =>
        new(new QuietAudio(), new SoundMachineRack(), new ObservableCollection<Recording>(), new SoundMachineProjects());

    /// <summary>Cancelling the changes leaves the song chain showing what the player now holds.</summary>
    [Fact]
    public void Cancelling_the_changes_rereads_the_song_chain()
    {
        var tracker = Tracker();
        var plugin = new Held();

        tracker.MasterEffect.Target!.Chain.Add(plugin);
        tracker.MasterEffect.Reload();

        Assert.Single(tracker.MasterEffect.Devices);

        tracker.Restore(Song.CreateDefault());

        Assert.True(plugin.Disposed, "the player was supposed to take the plugin off the chain");
        Assert.NotNull(tracker.MasterEffect.Target);
        Assert.Empty(tracker.MasterEffect.Devices);

        tracker.Finished();
    }

    /// <summary>
    /// A row is let go of before its plugin is disposed, since letting go is what closes its
    /// window, and a window open over a disposed plugin is a crash inside the plugin's toolkit.
    /// </summary>
    [Fact]
    public void A_row_is_let_go_of_before_its_plugin_is_disposed()
    {
        var tracker = Tracker();
        var plugin = new Held();

        tracker.MasterEffect.Target!.Chain.Add(plugin);
        tracker.MasterEffect.Reload();

        var disposedWhenClosed = new List<bool>();
        tracker.MasterEffect.DeviceClosing += _ => disposedWhenClosed.Add(plugin.Disposed);

        tracker.Restore(Song.CreateDefault());

        Assert.Equal([false], disposedWhenClosed);

        tracker.Finished();
    }

    /// <summary>
    /// Coming back to a page with plugins that were never put down picks nothing up: the
    /// plugin on the chain is the one that was there, still loaded, and still on its row.
    /// </summary>
    [Fact]
    public void Coming_back_without_having_let_go_rebuilds_nothing()
    {
        var tracker = Tracker();
        var plugin = new Held();

        tracker.MasterEffect.Target!.Chain.Add(plugin);
        tracker.MasterEffect.Reload();

        tracker.TakeUpPlugins();

        Assert.False(plugin.Disposed, "coming back started a plugin that was never put down over again");
        Assert.Single(tracker.MasterEffect.Devices);

        tracker.Finished();
    }

    /// <summary>
    /// Put down on the way out and picked up on the way back, the song chain shows what the
    /// player holds afterwards rather than the plugin it had before.
    /// </summary>
    [Fact]
    public void Putting_down_and_picking_up_rereads_the_song_chain()
    {
        var tracker = Tracker();
        var plugin = new Held();

        tracker.MasterEffect.Target!.Chain.Add(plugin);
        tracker.MasterEffect.Reload();

        var row = Assert.Single(tracker.MasterEffect.Devices);

        tracker.LetGoOfPlugins();
        tracker.TakeUpPlugins();

        Assert.NotNull(tracker.MasterEffect.Target);
        Assert.DoesNotContain(row, tracker.MasterEffect.Devices);
        Assert.Equal(tracker.MasterEffect.Target!.Chain.Slots.Count, tracker.MasterEffect.Devices.Count);

        tracker.Finished();
    }

    /// <summary>A plugin that does nothing and says when it is disposed.</summary>
    internal sealed class Held : IPluginEffect
    {
        /// <summary>Whether it has been disposed.</summary>
        public bool Disposed;

        /// <inheritdoc/>
        public PluginInfo Info { get; } = new("held", "Held", "", "", "");

        /// <inheritdoc/>
        public bool IsActive => true;

        /// <inheritdoc/>
        public void FlushParameters() { }

        /// <inheritdoc/>
        public void Process(float[] buffer, int frames) { }

        /// <inheritdoc/>
        public IReadOnlyList<PluginParameter> Parameters() => [];

        /// <inheritdoc/>
        public double ValueOf(uint id) => 0;

        /// <inheritdoc/>
        public string TextFor(uint id, double value) => "";

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
        public void Dispose() => Disposed = true;
    }
}
