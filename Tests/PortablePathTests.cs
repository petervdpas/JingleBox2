using System.IO;
using System.Linq;
using JingleBox2.Config;
using JingleBox2.Config.Enums;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Every path this application stores under its own folder is stored so it can be found again.
/// </summary>
/// <remarks>
/// The songs kept this rule from the day they were written and nothing else did, so the rack
/// wrote a kit's sixteen pads as full paths into somebody's home directory and the settings wrote
/// every pad the same way. Carried to another machine, or opened after the account was renamed,
/// those are paths to nothing, and nothing reports it: the pads are simply silent and the kit
/// plays nothing.
///
/// The rule itself is <see cref="IPortablePath"/> and it is one rule, in <c>Files/</c> where the
/// other questions about a file on this machine live. What differs per caller is only where the
/// paths are: an instrument names its own, a settings file names one per pad.
/// </remarks>
public class PortablePathTests
{
    /// <summary>An application folder of this test's own.</summary>
    private sealed class Somewhere(string path) : IAppFolder
    {
        public string Name => "JingleBox2";

        public string Path(string appName) => path;

        public string Path() => path;
    }

    /// <summary>A folder nothing else is looking at.</summary>
    /// <param name="named">A name no other test uses.</param>
    private static string Fresh(string named)
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jinglebox2-portable-" + named);

        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);

        Directory.CreateDirectory(root);

        return root;
    }

    /// <summary>A path under the application folder is stored as a name that travels.</summary>
    [Fact]
    public void A_path_inside_the_folder_is_packed()
    {
        string app = Fresh("inside");

        var portable = new PortablePath(folder: new Somewhere(app));

        string real = System.IO.Path.Combine(app, "recordings", "Take.wav");

        Assert.Equal("{app}/recordings/Take.wav", portable.Pack(real));
        Assert.Equal(real, portable.Unpack(portable.Pack(real)));
    }

    /// <summary>A path somewhere else is left exactly as it was.</summary>
    /// <remarks>
    /// Somebody's own file, or somebody else's plugin. Guessing at it would be worse than keeping
    /// it, since a path outside the folder is a path the user chose.
    /// </remarks>
    [Fact]
    public void A_path_outside_the_folder_is_left_alone()
    {
        var portable = new PortablePath(folder: new Somewhere(Fresh("outside")));

        string elsewhere = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "elsewhere", "Serum2.vst3");

        Assert.Equal(elsewhere, portable.Pack(elsewhere));
    }

    /// <summary>Nothing at all reads as nothing, both ways.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Nothing_reads_as_nothing(string? path)
    {
        var portable = new PortablePath(folder: new Somewhere(Fresh("empty" + (path?.Length ?? 9))));

        Assert.Equal("", portable.Pack(path!));
        Assert.Equal("", portable.Unpack(path!));
    }

    /// <summary>
    /// A stored name uses forward slashes and comes back with this machine's own separator.
    /// </summary>
    /// <remarks>
    /// The whole of what makes it cross-platform. A separator has to be chosen and written down,
    /// and it is the forward slash, which is what a zip entry already uses and what both systems
    /// understand. Read back, it becomes whatever this machine spells a path with.
    /// </remarks>
    [Fact]
    public void A_stored_name_is_read_with_this_machines_separator()
    {
        string app = Fresh("slashes");

        var portable = new PortablePath(folder: new Somewhere(app));

        string real = portable.Unpack("{app}/rack/machines/machine.kit/presets/Kick.wav");

        Assert.Equal(
            System.IO.Path.Combine(app, "rack", "machines", "machine.kit", "presets", "Kick.wav"),
            real);
        Assert.DoesNotContain('/', real[app.Length..].Replace(System.IO.Path.DirectorySeparatorChar, '|'));
    }

    /// <summary>A path already stored whole still reads, which is every file already on disc.</summary>
    [Fact]
    public void A_path_stored_the_old_way_still_reads()
    {
        var portable = new PortablePath(folder: new Somewhere(Fresh("old")));

        string whole = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "somewhere", "Old.wav");

        Assert.Equal(whole, portable.Unpack(whole));
    }

    /// <summary>The rack writes a device's recordings as names that travel.</summary>
    [Fact]
    public void The_rack_stores_a_devices_recordings_portably()
    {
        string app = Fresh("rack");

        var rack = new SoundMachineRack("JingleBox2", new Somewhere(app));

        string take = System.IO.Path.Combine(app, "recordings", "Kick.wav");

        var instrument = new TrackerInstrument { Id = "machine.kit", Name = "Kit", Kind = TrackerInstrumentKind.Sample };

        instrument.FilePath = take;

        rack.Save(instrument);

        Assert.Contains("{app}/recordings/Kick.wav", File.ReadAllText(rack.PathFor("machine.kit")));
        Assert.Equal(take, rack.Load("machine.kit")!.FilePath);
    }

    /// <summary>
    /// And the instrument it was handed is not left holding the stored name.
    /// </summary>
    /// <remarks>
    /// The rack hands out the instruments it holds and the pages on screen are looking at those
    /// very objects, so one left holding <c>{app}/</c> after a save is a device that plays nothing
    /// until something reads it off disc again. Which would look exactly like saving having broken
    /// the sound.
    /// </remarks>
    [Fact]
    public void Saving_does_not_leave_the_instrument_packed()
    {
        string app = Fresh("notpacked");

        var rack = new SoundMachineRack("JingleBox2", new Somewhere(app));

        string take = System.IO.Path.Combine(app, "recordings", "Kick.wav");

        var instrument = new TrackerInstrument { Id = "machine.kit", Name = "Kit", Kind = TrackerInstrumentKind.Sample };

        instrument.FilePath = take;

        rack.Save(instrument);

        Assert.Equal(take, instrument.FilePath);
    }

    /// <summary>A pad's take is stored the same way, and read back as a path this machine has.</summary>
    [Fact]
    public void The_settings_store_a_pads_take_portably()
    {
        string app = Fresh("pads");

        var folder = new Somewhere(app);
        var store = new ConfigStore("JingleBox2", folder);

        var cfg = store.LoadOrCreateDefault();

        string take = System.IO.Path.Combine(app, "recordings", "Jingle.wav");

        var pad = cfg.Profiles.First(one => one.Name == cfg.SelectedProfile).Pads[0];

        pad.Source = take;
        pad.Kind = PadSourceKind.Recording;

        store.Save(cfg);

        Assert.Contains("{app}/recordings/Jingle.wav", File.ReadAllText(System.IO.Path.Combine(app, "config.json")));
        Assert.Equal(take, new ConfigStore("JingleBox2", folder).LoadOrCreateDefault().Pads[0].Source);
        Assert.Equal(take, pad.Source);
    }

    /// <summary>A pad playing a stream is left alone, since a URL is not under any folder.</summary>
    [Fact]
    public void A_stream_is_not_touched()
    {
        string app = Fresh("stream");

        var folder = new Somewhere(app);
        var store = new ConfigStore("JingleBox2", folder);

        var cfg = store.LoadOrCreateDefault();

        const string url = "http://stream.example.com/live.mp3";

        var pad = cfg.Profiles.First(one => one.Name == cfg.SelectedProfile).Pads[0];

        pad.Source = url;
        pad.Kind = PadSourceKind.Stream;

        store.Save(cfg);

        Assert.Equal(url, new ConfigStore("JingleBox2", folder).LoadOrCreateDefault().Pads[0].Source);
    }
}
