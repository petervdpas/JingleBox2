using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using JingleBox2.Audio.Plugins;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Files.Interfaces;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.SoundDevices.SoundMachines.Records;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using JingleBox2.ViewModels.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The whole flow, walked in one test: designer, registry, rack, song, track.
/// </summary>
/// <remarks>
/// Every step of it was covered somewhere and the walk was covered nowhere, which is how the
/// thing that made it impossible survived: a soundmachine's id was worked out from its engine by
/// a switch of five strings written into the application, so a device made in the designer under
/// any other id was read off disc, refused in silence, and never reached the rack or a song.
/// Each layer's own tests passed throughout, because each layer was right about the question it
/// was asked.
///
/// Nothing is faked below the view model. The registry reads real folders, the rack writes real
/// files, and <see cref="RackViewModel"/> is the real one, which builds without a window.
/// </remarks>
public class DeviceFlowTests
{
    /// <summary>The audio the rack borrows, which this asks nothing of.</summary>
    private sealed class Silent : IInstrumentAudition
    {
        public double Audition(TrackerInstrument instrument, Note note, int volume) => 0;

        public void Let(TrackerInstrument instrument, Note note) { }

        public void Silence(TrackerInstrument instrument) { }

        public double SamplePosition(int track) => 0;

        public IPluginParameters? PluginFor(TrackerInstrument instrument) => null;
    }

    /// <summary>An application folder of this test's own.</summary>
    private sealed class Somewhere(string path) : IAppFolder
    {
        public string Name => "JingleBox2";

        public string Path(string appName) => path;

        public string Path() => path;
    }

    /// <summary>What the designer leaves behind: a folder, a manifest, an id and an engine.</summary>
    /// <param name="root">Where the shipped devices live for this test.</param>
    /// <param name="named">What the author called the folder.</param>
    /// <param name="id">The id the designer gave it.</param>
    /// <param name="engine">The engine its manifest names.</param>
    private static void Designed(string root, string named, string id, string engine)
    {
        string folder = System.IO.Path.Combine(root, "rack", "machines", named);

        Directory.CreateDirectory(folder);

        File.WriteAllText(System.IO.Path.Combine(folder, "machine.json"),
            "{\"Id\":\"" + id + "\",\"Name\":\"" + named + "\",\"Version\":\"1.0\",\"Engine\":\"" + engine + "\"}");
    }

    /// <summary>Everything the flow needs, over folders nothing else is looking at.</summary>
    private sealed record Bench(
        string Shipped, string App, SoundMachineRegistry Registry,
        SoundMachineArchive Crates, SoundMachineRack Rack, SoundMachineProjects Projects);

    /// <summary>Lays the bench out fresh, with no device on it.</summary>
    /// <param name="named">A name no other test uses.</param>
    private static Bench Set(string named)
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jinglebox2-flow-" + named);

        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);

        string shipped = System.IO.Path.Combine(root, "shipped");
        string app = System.IO.Path.Combine(root, "app");

        Directory.CreateDirectory(app);

        var folder = new Somewhere(app);

        var registry = new SoundMachineRegistry(
            folder: folder, shipped: System.IO.Path.Combine(shipped, "rack", "machines"));

        return new Bench(shipped, app, registry, new SoundMachineArchive(registry),
            new SoundMachineRack("JingleBox2", folder), new SoundMachineProjects());
    }

    /// <summary>Reads the registry and hands the rack page what it found, as startup does.</summary>
    /// <param name="bench">The bench to read.</param>
    private static RackViewModel Opened(Bench bench)
    {
        bench.Projects.Keep(bench.Registry.Load());

        var page = new RackViewModel(
            bench.Rack, new Silent(), bench.Projects, new ObservableCollection<Recording>());

        page.Refresh();

        return page;
    }

    /// <summary>
    /// A device made in the designer walks the whole way to a track.
    /// </summary>
    /// <remarks>
    /// The one that could not have passed before: <c>machine.thumper</c> is an id no switch in
    /// the application has ever heard of, and it plays the Kit engine because its own manifest
    /// says so.
    /// </remarks>
    [Fact]
    public void A_device_made_in_the_designer_reaches_a_track()
    {
        var bench = Set("whole");

        Designed(bench.Shipped, "Thumper", "machine.thumper", "Kit");

        var page = Opened(bench);

        Assert.Contains(SoundMachine.Installed, one => one.Id == "machine.thumper");
        Assert.Contains(page.Machines, one => one.Id == "machine.thumper");

        var offRack = bench.Rack.Load("machine.thumper");

        Assert.NotNull(offRack);

        var song = new Song();

        song.Instruments.Add(offRack);
        song.SetTrackInstrument(0, song.Instruments.Count - 1);

        Assert.Equal("machine.thumper", song.InstrumentAt(song.GetTrackInstrument(0))!.Id);
    }

    /// <summary>Unregistering takes it off the rack, and the walk stops at the registry.</summary>
    [Fact]
    public void Removing_it_from_the_registry_takes_it_off_the_rack()
    {
        var bench = Set("removed");

        Designed(bench.Shipped, "Thumper", "machine.thumper", "Kit");

        Opened(bench);

        var machine = bench.Registry.Load().First(one => one.Id == "machine.thumper");

        Assert.True(bench.Crates.Remove(machine));

        var page = Opened(bench);

        Assert.DoesNotContain(SoundMachine.Installed, one => one.Id == "machine.thumper");
        Assert.DoesNotContain(page.Machines, one => one.Id == "machine.thumper");
    }

    /// <summary>
    /// And its settings are exactly where they were when it is registered again.
    /// </summary>
    /// <remarks>
    /// The half that is easy to get wrong. Unregistering must take the device off the rack and
    /// must not throw away what you set on it, so the settings file is left alone and the rack's
    /// own record still names it, which is what stops it being swept into <c>retired</c>.
    ///
    /// It comes back through Add and not by restarting, and that is the registry's own rule
    /// rather than an oversight: what is recorded is what has been offered, so a device you threw
    /// out stays thrown out however many times the application is started. Putting it back is a
    /// deliberate act, the same way taking it off was.
    /// </remarks>
    [Fact]
    public void What_was_set_on_it_survives_being_unregistered()
    {
        var bench = Set("kept");

        Designed(bench.Shipped, "Thumper", "machine.thumper", "Kit");

        Opened(bench);

        var mine = bench.Rack.Load("machine.thumper")!;

        mine.Volume = 0.25;

        bench.Rack.Save(mine);

        bench.Crates.Remove(bench.Registry.Load().First(one => one.Id == "machine.thumper"));

        Opened(bench);

        var offer = bench.Registry.Available().First(one => one.Id == "machine.thumper");

        Assert.NotNull(bench.Crates.Add(offer));

        var back = Opened(bench);

        Assert.Contains(back.Machines, one => one.Id == "machine.thumper");
        Assert.Equal(0.25, bench.Rack.Load("machine.thumper")!.Volume, 3);
    }

    /// <summary>Two devices on one engine both walk it, which is what one of each never allowed.</summary>
    [Fact]
    public void Two_devices_on_one_engine_both_reach_the_rack()
    {
        var bench = Set("two");

        Designed(bench.Shipped, "Thumper", "machine.thumper", "Kit");
        Designed(bench.Shipped, "Clatter", "machine.clatter", "Kit");

        var page = Opened(bench);

        Assert.Contains(page.Machines, one => one.Id == "machine.thumper");
        Assert.Contains(page.Machines, one => one.Id == "machine.clatter");
    }

    /// <summary>A device naming an engine this build has not got never leaves the registry.</summary>
    [Fact]
    public void A_device_on_an_unknown_engine_never_reaches_the_rack()
    {
        var bench = Set("unknown");

        Designed(bench.Shipped, "Granulator", "machine.granulator", "Granular");

        var page = Opened(bench);

        Assert.DoesNotContain(page.Machines, one => one.Id == "machine.granulator");
    }

    /// <summary>The rack lists devices by name, whatever order the folders were read in.</summary>
    /// <remarks>
    /// The reading order was the disc's, which is not an order. It only ever looked like one
    /// because the five that shipped had a curated list written into the application, and there
    /// is no such list for a device somebody makes and names themselves.
    /// </remarks>
    [Fact]
    public void The_rack_is_in_alphabetical_order()
    {
        var bench = Set("sorted");

        Designed(bench.Shipped, "Zither", "machine.zither", "Kit");
        Designed(bench.Shipped, "Anvil", "machine.anvil", "Kit");
        Designed(bench.Shipped, "Marimba", "machine.marimba", "Synth");

        var page = Opened(bench);

        var names = page.Machines.Select(one => one.Name).ToList();

        Assert.Equal(new[] { "Anvil", "Marimba", "Zither" }, names);
    }

    /// <summary>What the designer leaves behind for an effect: the same folder, the other manifest.</summary>
    /// <remarks>
    /// The one difference between the two worlds is what a song does with the device, and this is
    /// not that: making one, registering it and putting it on the rack is the same act, which is
    /// why the only thing that differs here is the name of the file.
    /// </remarks>
    /// <param name="root">Where the shipped devices live for this test.</param>
    /// <param name="named">What the author called the folder.</param>
    /// <param name="id">The id the designer gave it.</param>
    /// <param name="engine">The engine its manifest names.</param>
    private static void DesignedEffect(string root, string named, string id, string engine)
    {
        string folder = System.IO.Path.Combine(root, "rack", "effects", named);

        Directory.CreateDirectory(folder);

        File.WriteAllText(System.IO.Path.Combine(folder, "effect.json"),
            "{\"Id\":\"" + id + "\",\"Name\":\"" + named + "\",\"Version\":\"1.0\",\"Engine\":\"" + engine + "\"}");
    }

    /// <summary>An effect registry over this test's own folders.</summary>
    /// <param name="bench">The bench its folders belong to.</param>
    private static SoundEffectRegistry Effects(Bench bench) =>
        new(folder: new Somewhere(bench.App),
            shipped: System.IO.Path.Combine(bench.Shipped, "rack", "effects"));

    /// <summary>
    /// An effect made in the designer walks the whole way onto a track's chain.
    /// </summary>
    /// <remarks>
    /// The soundmachine half of this walk stops at the track, because that is where a
    /// soundmachine's journey ends: it is the instrument the track plays. An effect goes one step
    /// further, onto that track's chain, and that step is the whole of what makes it an effect
    /// rather than a soundmachine.
    ///
    /// <c>effect.rumbler</c> is an id no list in the application has ever heard of, and it is a
    /// delay because its own manifest says so.
    /// </remarks>
    [Fact]
    public void An_effect_made_in_the_designer_reaches_a_chain()
    {
        var bench = Set("chain");

        DesignedEffect(bench.Shipped, "Rumbler", "effect.rumbler", "delay");

        var registry = Effects(bench);
        var effects = new SoundEffectProjects();

        effects.Keep(registry.Load());

        Assert.True(effects.Has("effect.rumbler"));

        var written = new PluginChainConfig();

        written.Devices.Add(new PluginSlotConfig { Effect = "effect.rumbler", Name = "Rumbler" });

        var chain = new PluginChain();

        var missing = new PluginChainState(new SoundEffectEngines(effects))
            .Restore(chain, written, 48000, 512);

        Assert.Empty(missing);
        Assert.Equal(1, chain.Count);
    }

    /// <summary>Unregistering it takes it off the rack, and off any chain that names it.</summary>
    /// <remarks>
    /// Which is the same sentence as for a soundmachine, said one step further along. An effect
    /// this installation no longer has is named rather than passed over, so the rest of the chain
    /// still loads and somebody is told which slot went quiet.
    /// </remarks>
    [Fact]
    public void Removing_an_effect_from_the_registry_takes_it_off_the_chain()
    {
        var bench = Set("chaingone");

        DesignedEffect(bench.Shipped, "Rumbler", "effect.rumbler", "delay");

        var registry = Effects(bench);
        var crates = new SoundEffectArchive(registry);

        var effects = new SoundEffectProjects();

        effects.Keep(registry.Load());

        Assert.True(crates.Remove(registry.Load().First(one => one.Id == "effect.rumbler")));

        effects.Keep(registry.Load());

        Assert.False(effects.Has("effect.rumbler"));

        var written = new PluginChainConfig();

        written.Devices.Add(new PluginSlotConfig { Effect = "effect.rumbler", Name = "Rumbler" });

        var missing = new PluginChainState(new SoundEffectEngines(effects))
            .Restore(new PluginChain(), written, 48000, 512);

        Assert.Equal("effect.rumbler", Assert.Single(missing));
    }

    /// <summary>Two effects on one engine are two effects, which one of each never allowed.</summary>
    [Fact]
    public void Two_effects_on_one_engine_are_both_registered()
    {
        var bench = Set("twoeffects");

        DesignedEffect(bench.Shipped, "Rumbler", "effect.rumbler", "delay");
        DesignedEffect(bench.Shipped, "Slapper", "effect.slapper", "delay");

        var effects = new SoundEffectProjects();

        effects.Keep(Effects(bench).Load());

        Assert.True(effects.Has("effect.rumbler"));
        Assert.True(effects.Has("effect.slapper"));
    }

    /// <summary>An effect naming an engine this build has not got never leaves the registry.</summary>
    [Fact]
    public void An_effect_on_an_unknown_engine_is_passed_over()
    {
        var bench = Set("unknowneffect");

        DesignedEffect(bench.Shipped, "Convolver", "effect.convolver", "convolution");

        var effects = new SoundEffectProjects();

        effects.Keep(Effects(bench).Load());

        Assert.False(effects.Has("effect.convolver"));
    }
}
