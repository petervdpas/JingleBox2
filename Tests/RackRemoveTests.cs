using System.IO;
using System.Linq;
using JingleBox2.Files.Interfaces;
using JingleBox2.SoundDevices.SoundMachines;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Taking a registered machine off this installation, and what happens on the next start.
/// </summary>
/// <remarks>
/// Two halves of one act, and the second is the one nobody had checked. Removing is a deliberate
/// act and so the box must not come back on the next start, which is what <c>offered.txt</c> is
/// for; and a machine that ships must not be brought up to date into a folder that is no longer
/// there, which is what would put it back without anybody deciding to.
/// </remarks>
public class RackRemoveTests
{
    /// <summary>An application folder somewhere nothing else is looking.</summary>
    private sealed class Somewhere(string path) : IAppFolder
    {
        public string Name => "JingleBox2";

        public string Path(string appName) => path;

        public string Path() => path;
    }

    /// <summary>A shipped machine on disc, under a folder named the way an author names one.</summary>
    private static string Ships(string root, string named, string id)
    {
        string folder = System.IO.Path.Combine(root, "rack", "machines", named);

        Directory.CreateDirectory(folder);

        File.WriteAllText(System.IO.Path.Combine(folder, "machine.json"),
            "{\"Id\":\"" + id + "\",\"Name\":\"" + named + "\",\"Version\":\"1.0\"}");

        return folder;
    }

    /// <summary>A registry over two folders of this test's own.</summary>
    private static (SoundMachineRegistry Registry, SoundMachineArchive Crates, string App) Rack(string named)
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jinglebox2-remove-" + named);

        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);

        string shipped = System.IO.Path.Combine(root, "shipped");
        string app = System.IO.Path.Combine(root, "app");

        Ships(shipped, "OddSkilla", "machine.oddskilla");

        Directory.CreateDirectory(app);

        var registry = new SoundMachineRegistry(
            folder: new Somewhere(app),
            shipped: System.IO.Path.Combine(shipped, "rack", "machines"));

        return (registry, new SoundMachineArchive(registry), app);
    }

    /// <summary>A shipped machine this installation has never seen arrives on the first start.</summary>
    [Fact]
    public void A_shipped_machine_is_taken_on_the_first_start()
    {
        var (registry, _, _) = Rack("first");

        Assert.Contains(registry.Load(), one => one.Id == "machine.oddskilla");
    }

    /// <summary>And removing it really takes the folder off the disc.</summary>
    [Fact]
    public void Removing_a_machine_deletes_its_folder()
    {
        var (registry, crates, _) = Rack("gone");

        var machine = registry.Load().First(one => one.Id == "machine.oddskilla");

        string folder = machine.Folder;

        Assert.True(Directory.Exists(folder));
        Assert.True(crates.Remove(machine));
        Assert.False(Directory.Exists(folder));
    }

    /// <summary>
    /// And it stays removed on the next start, which is the whole of what the record is for.
    /// </summary>
    [Fact]
    public void A_removed_machine_does_not_come_back_on_the_next_start()
    {
        var (registry, crates, app) = Rack("stays");

        crates.Remove(registry.Load().First(one => one.Id == "machine.oddskilla"));

        var again = new SoundMachineRegistry(
            folder: new Somewhere(app),
            shipped: registry.Shipped);

        Assert.DoesNotContain(again.Load(), one => one.Id == "machine.oddskilla");
    }

    /// <summary>And it is offered back, since the shipped copy is still where it was.</summary>
    [Fact]
    public void A_removed_machine_is_offered_back()
    {
        var (registry, crates, _) = Rack("offered");

        crates.Remove(registry.Load().First(one => one.Id == "machine.oddskilla"));

        Assert.Contains(registry.Available(), one => one.Id == "machine.oddskilla");
    }

    /// <summary>
    /// An archive made the way the application makes one can remove a machine.
    /// </summary>
    /// <remarks>
    /// The regression. Every test above hands the archive its registry, which is what anything
    /// wiring these up on purpose does and is exactly why they all passed while the Remove button
    /// in SETTINGS did nothing: the shelf built the archive with no registry, the constructor put
    /// a null straight into the field <c>Remove</c> dereferences, and the catch around it reported
    /// a null reference as "Could not remove". The archive's own documentation had said all along
    /// that one made without a registry builds one and hands itself over, and the code said
    /// <c>registry!</c>, which builds nothing.
    ///
    /// So this one takes the default constructor, which is the production wiring, and the
    /// application folder is the sandbox's for the length of the run.
    /// </remarks>
    [Fact]
    public void An_archive_made_with_no_registry_can_still_remove()
    {
        var registry = new SoundMachineRegistry();

        string folder = System.IO.Path.Combine(registry.Installed, "machine.testbox");

        Directory.CreateDirectory(folder);

        File.WriteAllText(System.IO.Path.Combine(folder, "machine.json"),
            "{\"Id\":\"machine.testbox\",\"Name\":\"TestBox\",\"Version\":\"1.0\"}");

        var project = SoundMachineProject.Open(folder);

        Assert.NotNull(project);
        Assert.True(new SoundMachineArchive().Remove(project), "the default archive could not remove");
        Assert.False(Directory.Exists(folder));
    }
}
