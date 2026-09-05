using System;
using System.IO;
using JingleBox2.Files.Interfaces;
using JingleBox2.SoundDevices.SoundMachines;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What the pass on every start may touch, and what it may never touch.
/// </summary>
/// <remarks>
/// A device that ships is brought up to date file by file against the copy beside the program, so
/// a new version of the application arrives without anybody copying folders about. That pass runs
/// unattended, on every start, over the one folder holding somebody's own work, which makes it
/// the most dangerous thing in this half of the program: there is nobody watching it and no undo
/// behind it.
///
/// Three rules, and each is a way somebody could lose an afternoon.
///
/// **Nothing is ever deleted.** The walk is over the files in the shipped folder, so anything in
/// the installed folder that does not ship is not looked at, let alone removed. That is what
/// keeps a preset you saved onto a device, and a whole device you made yourself.
///
/// **What ships is overwritten, and only when it is newer.** A file you have edited since is
/// newer than the shipped one and is kept. The other way round it is replaced, deliberately:
/// what ships is the device, and that is how a fixed manifest reaches anybody.
///
/// **The pass is about the rack and nothing else.** Your settings for a device live in
/// <c>instruments/</c>, a folder this never opens.
/// </remarks>
public class RefreshKeepsYoursTests
{
    /// <summary>An application folder of this test's own.</summary>
    private sealed class Somewhere(string path) : IAppFolder
    {
        public string Name => "JingleBox2";

        public string Path(string appName) => path;

        public string Path() => path;
    }

    /// <summary>A shipped soundmachine, written where the program keeps what it ships.</summary>
    /// <param name="root">This test's stand-in for the folder beside the program.</param>
    /// <param name="named">The folder name its author gave it.</param>
    /// <param name="id">Its id.</param>
    /// <param name="version">Which version this copy claims to be.</param>
    private static string Ships(string root, string named, string id, string version)
    {
        string folder = Path.Combine(root, "rack", "machines", named);

        Directory.CreateDirectory(folder);

        File.WriteAllText(Path.Combine(folder, "machine.json"),
            "{\"Id\":\"" + id + "\",\"Name\":\"" + named + "\",\"Version\":\"" + version + "\",\"Engine\":\"Kit\"}");

        return folder;
    }

    /// <summary>A bench with one shipped device already registered here.</summary>
    /// <param name="named">A name no other test uses.</param>
    private static (string Shipped, string App, SoundMachineRegistry Registry) Set(string named)
    {
        string root = Path.Combine(Path.GetTempPath(), "jinglebox2-keep-" + named);

        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);

        string shipped = Path.Combine(root, "shipped");
        string app = Path.Combine(root, "app");

        Directory.CreateDirectory(app);

        Ships(shipped, "Thumper", "machine.thumper", "1.0");

        var registry = new SoundMachineRegistry(
            folder: new Somewhere(app), shipped: Path.Combine(shipped, "rack", "machines"));

        registry.Load();

        return (shipped, app, registry);
    }

    /// <summary>Where this installation keeps that device.</summary>
    /// <param name="app">The application folder.</param>
    private static string Mine(string app) =>
        Path.Combine(app, "rack", "machines", "machine.thumper");

    /// <summary>Makes a file look older or newer than another, since the pass reads the clock.</summary>
    /// <param name="path">The file to stamp.</param>
    /// <param name="days">How many days from now, negative for the past.</param>
    private static void Dated(string path, int days) =>
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(days));

    /// <summary>A preset you saved onto a shipped device is still there afterwards.</summary>
    [Fact]
    public void A_preset_you_saved_is_not_swept()
    {
        var bench = Set("preset");

        string presets = Path.Combine(Mine(bench.App), "presets");

        Directory.CreateDirectory(presets);
        File.WriteAllText(Path.Combine(presets, "My Sound.json"), "{\"mine\":true}");

        bench.Registry.Load();

        Assert.True(File.Exists(Path.Combine(presets, "My Sound.json")));
        Assert.Equal("{\"mine\":true}", File.ReadAllText(Path.Combine(presets, "My Sound.json")));
    }

    /// <summary>And so is a whole device you made yourself, which ships with nothing.</summary>
    [Fact]
    public void A_device_you_made_is_not_swept()
    {
        var bench = Set("mine");

        string mine = Path.Combine(bench.App, "rack", "machines", "machine.myown");

        Directory.CreateDirectory(mine);
        File.WriteAllText(Path.Combine(mine, "machine.json"),
            "{\"Id\":\"machine.myown\",\"Name\":\"MyOwn\",\"Version\":\"1.0\",\"Engine\":\"Kit\"}");

        bench.Registry.Load();

        Assert.True(File.Exists(Path.Combine(mine, "machine.json")));
    }

    /// <summary>A shipped file you have edited since is kept, because yours is the newer.</summary>
    [Fact]
    public void Your_own_edit_is_kept_when_it_is_the_newer()
    {
        var bench = Set("edited");

        string mine = Path.Combine(Mine(bench.App), "machine.json");

        File.WriteAllText(mine,
            "{\"Id\":\"machine.thumper\",\"Name\":\"Thumper\",\"Version\":\"9.9\",\"Engine\":\"Kit\"}");

        Dated(Path.Combine(bench.Shipped, "rack", "machines", "Thumper", "machine.json"), -2);
        Dated(mine, 0);

        bench.Registry.Load();

        Assert.Contains("9.9", File.ReadAllText(mine));
    }

    /// <summary>
    /// And it is replaced where the shipped one is the newer, which is how a fix arrives.
    /// </summary>
    /// <remarks>
    /// The one case where something of yours really does go, and it is the whole point of the
    /// pass: a new version of the application has to be able to correct the device it ships.
    /// Pinned here rather than left implied, because it is the sentence somebody has to read
    /// before editing an installed copy of a device that ships.
    ///
    /// Which is the hazard for anybody making devices of their own, and it is **by design**: a
    /// device given the id of one that ships is that device as far as any of this is concerned,
    /// so the next start brings the shipped manifest over the top of theirs and their work is
    /// gone. Make your own and give it your own id. <see cref="A_device_you_made_is_not_swept"/>
    /// is the other half of that sentence: under an id of its own, nothing here touches it ever.
    /// </remarks>
    [Fact]
    public void A_newer_shipped_file_replaces_yours()
    {
        var bench = Set("replaced");

        string mine = Path.Combine(Mine(bench.App), "machine.json");

        File.WriteAllText(mine,
            "{\"Id\":\"machine.thumper\",\"Name\":\"Thumper\",\"Version\":\"9.9\",\"Engine\":\"Kit\"}");

        Dated(mine, -2);
        Dated(Path.Combine(bench.Shipped, "rack", "machines", "Thumper", "machine.json"), 0);

        bench.Registry.Load();

        Assert.Contains("1.0", File.ReadAllText(mine));
    }

    /// <summary>Your settings for a device are in another folder, which the pass never opens.</summary>
    [Fact]
    public void Your_settings_for_a_device_are_never_touched()
    {
        var bench = Set("settings");

        string instruments = Path.Combine(bench.App, "instruments");

        Directory.CreateDirectory(instruments);

        string settings = Path.Combine(instruments, "machine.thumper.json");

        File.WriteAllText(settings, "{\"Id\":\"machine.thumper\",\"Volume\":0.25}");

        Dated(settings, -30);

        bench.Registry.Load();

        Assert.Equal("{\"Id\":\"machine.thumper\",\"Volume\":0.25}", File.ReadAllText(settings));
    }

    /// <summary>Nothing in the installed folder is removed, however many times it runs.</summary>
    [Fact]
    public void Nothing_is_ever_removed()
    {
        var bench = Set("nothing");

        string stray = Path.Combine(Mine(bench.App), "notes.txt");

        File.WriteAllText(stray, "mine");

        for (int again = 0; again < 3; again++) bench.Registry.Load();

        Assert.True(File.Exists(stray));
        Assert.Equal("mine", File.ReadAllText(stray));
    }

    /// <summary>
    /// A device of your own given a shipped device's id is overwritten on the next start.
    /// </summary>
    /// <remarks>
    /// Said in the words somebody would hit it in, because the mechanism above does not read as
    /// this from a chair: what they see is a device they spent an afternoon on, opening the next
    /// morning as the one that ships. It is deliberate and it cannot be otherwise while a device
    /// is known by its id, since there is nothing in a folder to say who wrote what is in it.
    ///
    /// The whole of the protection is the id. Everything about a device is yours except that.
    /// </remarks>
    [Fact]
    public void Naming_your_device_after_a_shipped_one_loses_it_on_the_next_start()
    {
        var bench = Set("collision");

        string mine = Path.Combine(Mine(bench.App), "machine.json");

        File.WriteAllText(mine,
            "{\"Id\":\"machine.thumper\",\"Name\":\"My Own Thumper\",\"Version\":\"1.0\",\"Engine\":\"Kit\"}");

        Dated(mine, -1);

        bench.Registry.Load();

        Assert.DoesNotContain("My Own Thumper", File.ReadAllText(mine));
    }
}
