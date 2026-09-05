using System;
using System.IO;
using System.Linq;
using JingleBox2.SoundDevices;
using JingleBox2.SoundDevices.Interfaces;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundMachines;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Every device this program ships carries its own page, and every page reads.
/// </summary>
/// <remarks>
/// A device's help is content this repository is answerable for, exactly as its presets are, and
/// it goes wrong the way content goes wrong: a file renamed, a folder added without one, a page
/// that was written and then emptied. None of that is a fault a compiler can see, and the only
/// symptom is a Menu line that has quietly gone grey on somebody else's installation.
///
/// What is checked is that there is a page, that it is a page rather than a line, and that it
/// begins by saying which device it is about, since the window puts the device's name above it
/// and a page that starts with somebody else's name is a file that was copied and not edited.
/// The length is not pinned beyond a floor: how much a device needs saying about it is the
/// author's business.
/// </remarks>
public class ShippedHelpTests
{
    /// <summary>The page rule, which is what the application reads them with.</summary>
    private readonly ISoundDeviceHelp _help = new SoundDeviceHelp();

    /// <summary>Where what ships lives, walking up out of the test's own output.</summary>
    /// <param name="world">The folder under <c>rack</c>, which is machines or effects.</param>
    private static string Shipped(string world)
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at != null && !Directory.Exists(Path.Combine(at.FullName, RackRegistry<SoundMachineProject>.RackFolder, world)))
            at = at.Parent;

        return at is null ? "" : Path.Combine(at.FullName, RackRegistry<SoundMachineProject>.RackFolder, world);
    }

    /// <summary>
    /// Both shipped folders are where the test expects them.
    /// </summary>
    /// <remarks>
    /// Said out loud rather than skipped past, the same as the shipped presets: a test that
    /// quietly passes where its subject is missing reports nothing for the rest of its life.
    /// </remarks>
    [Fact]
    public void The_shipped_devices_are_where_they_are_looked_for()
    {
        foreach (string world in new[] { "machines", "effects" })
        {
            Assert.True(Directory.Exists(Shipped(world)),
                "rack/" + world + " was not found above " + AppContext.BaseDirectory);

            Assert.NotEmpty(Directory.GetDirectories(Shipped(world)));
        }
    }

    /// <summary>Every shipped machine carries a page, and it is about that machine.</summary>
    [Theory]
    [InlineData("machines")]
    [InlineData("effects")]
    public void Every_shipped_device_carries_a_page(string world)
    {
        foreach (string folder in Directory.GetDirectories(Shipped(world)))
        {
            string name = world == "machines"
                ? SoundMachineProject.Open(folder)?.Name ?? ""
                : SoundEffectProject.Open(folder)?.Name ?? "";

            Assert.False(name.Length == 0, folder + " holds no device this build can read");

            string page = _help.Read(folder);

            Assert.False(page.Length == 0, name + " carries no help page");

            Assert.StartsWith("# " + name, page, StringComparison.Ordinal);

            Assert.True(page.Split('\n').Length >= 10,
                name + "'s help page is shorter than anything worth opening a window for");
        }
    }

    /// <summary>A page is read as the device is, which is off the folder and into the project.</summary>
    /// <remarks>
    /// The other half of the same fact: the file being there is no use if what the rack hands
    /// about does not carry it, since that is what the Menu asks and what the window shows.
    /// </remarks>
    [Fact]
    public void A_shipped_effects_page_arrives_on_the_project()
    {
        var read = Directory.GetDirectories(Shipped("effects"))
            .Select(SoundEffectProject.Open)
            .Where(one => one != null)
            .ToList();

        Assert.NotEmpty(read);
        Assert.All(read, one => Assert.NotEqual("", one!.Help));
    }
}
