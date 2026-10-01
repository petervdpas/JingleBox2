using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio.Plugins;
using JingleBox2.Audio.Records;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using JingleBox2.ViewModels.Interfaces;
using JingleBox2.ViewModels.Records;
using JingleBox2.Views;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The count on a folding strip's tab: how many devices a chain holds and how many lanes move
/// something, so a folded strip still says whether there is anything under it.
/// </summary>
public class FoldCountTests
{
    /// <summary>A tracker with a default song in it.</summary>
    private static TrackerViewModel Tracker() =>
        new(new QuietAudio(), new SoundMachineRack(), new ObservableCollection<Recording>(), new SoundMachineProjects());

    /// <summary>A lane on a strip's level.</summary>
    private static AutomationLane LevelLane(int strip) =>
        AutomationLane.For(new ControlMapping { Kind = ControlKind.Mix, Mix = MixControl.Volume, Scope = ControlScope.Fixed, Track = strip }, strip)!;

    /// <summary>The tab shows no count until it is given one, and is lit only above nought.</summary>
    [Fact]
    public void The_tab_is_lit_only_with_something_under_it()
    {
        var fold = new FoldStrip();

        Assert.Null(fold.Count);
        Assert.DoesNotContain(":some", fold.Classes);

        fold.Count = 0;
        Assert.DoesNotContain(":some", fold.Classes);

        fold.Count = 3;
        Assert.Contains(":some", fold.Classes);

        fold.Count = null;
        Assert.DoesNotContain(":some", fold.Classes);
    }

    /// <summary>Lanes count only where they hold a point, and only on the cursor's track.</summary>
    [Fact]
    public void Lanes_count_where_they_hold_something_on_the_cursors_track()
    {
        var tracker = Tracker();
        var pattern = tracker.Song.Patterns[0];
        var said = new List<string?>();
        ((INotifyPropertyChanged)tracker).PropertyChanged += (_, e) => said.Add(e.PropertyName);

        Assert.Equal(0, tracker.LaneCount);

        var empty = pattern.Lane(LevelLane(0));
        Assert.Equal(0, tracker.LaneCount);

        empty.Put(0, 0.5);
        pattern.LaneChanged();

        Assert.Equal(1, tracker.LaneCount);
        Assert.Contains(nameof(TrackerViewModel.LaneCount), said);

        tracker.Cursor = new PatternCursor(0, 1, CellColumn.Note);
        Assert.Equal(0, tracker.LaneCount);

        tracker.Finished();
    }

    /// <summary>The master's lanes are counted apart from any track's.</summary>
    [Fact]
    public void The_masters_lanes_are_counted_apart()
    {
        var tracker = Tracker();
        var pattern = tracker.Song.Patterns[0];

        pattern.Lane(LevelLane(TrackerPlayer.MasterStrip)).Put(0, 0.5);
        pattern.LaneChanged();

        Assert.Equal(1, tracker.MasterLaneCount);
        Assert.Equal(0, tracker.LaneCount);

        tracker.Finished();
    }

    /// <summary>
    /// Automating a parameter from the panel counts at once, although the panel makes the lane
    /// before it puts the first point in it.
    /// </summary>
    [Fact]
    public void Automating_from_the_panel_counts_at_once()
    {
        var tracker = Tracker();
        tracker.UseAutomation(new ControlTargets(tracker, new SoundMachineProjects()));
        int seen = -1;
        ((INotifyPropertyChanged)tracker).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TrackerViewModel.MasterLaneCount)) seen = tracker.MasterLaneCount;
        };

        var panel = tracker.MasterLanes!;
        panel.Show(TrackerPlayer.MasterStrip);
        panel.Parameters[0].AddCommand.Execute(null);

        Assert.Equal(1, seen);

        tracker.Finished();
    }

    /// <summary>A chain counts its devices, and says so when one comes or goes.</summary>
    [Fact]
    public void A_chain_counts_its_devices()
    {
        var tracker = Tracker();
        var chain = tracker.TrackEffect;
        var said = new List<string?>();
        ((INotifyPropertyChanged)chain).PropertyChanged += (_, e) => said.Add(e.PropertyName);

        int before = chain.Count;

        chain.Devices.Add(new Slot());

        Assert.Equal(before + 1, chain.Count);
        Assert.Contains(nameof(PluginChainViewModel.Count), said);

        chain.Devices.Clear();

        Assert.Equal(chain.HasInstrument ? 1 : 0, chain.Count);

        tracker.Finished();
    }

    /// <summary>A device on a chain that is nothing but a name.</summary>
    private sealed class Slot : IChainSlot
    {
        /// <inheritdoc/>
        public string Name => "Test";

        /// <inheritdoc/>
        public string Format => "";

        /// <inheritdoc/>
        public string Vendor => "";

        /// <inheritdoc/>
        public string Colour => "";

        /// <inheritdoc/>
        public IReadOnlyList<ControlReading> Summary => System.Array.Empty<ControlReading>();

        /// <inheritdoc/>
        public bool HasSummary => false;

        /// <inheritdoc/>
        public PluginChain.Slot Device => null!;

        /// <inheritdoc/>
        public bool IsBypassed { get; set; }

        /// <inheritdoc/>
        public bool IsOpen { get; set; }

        /// <inheritdoc/>
        public IRelayCommand RemoveCommand { get; } = new RelayCommand(() => { });

        /// <inheritdoc/>
        public IRelayCommand MoveLeftCommand { get; } = new RelayCommand(() => { });

        /// <inheritdoc/>
        public IRelayCommand MoveRightCommand { get; } = new RelayCommand(() => { });

        /// <inheritdoc/>
        public void Reread() { }
    }
}
