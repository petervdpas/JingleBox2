using System;
using System.IO;
using System.Linq;
using JingleBox2.SoundDevices.SoundMachines;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A kit preset brings its recordings onto the pads, whatever the kit's face draws.
/// </summary>
/// <remarks>
/// Which settings in a preset are recordings was read off the pickers drawn on the face, so the
/// day BongaBong's picker came off every preset landed with its pad names and nothing to play.
/// Both kits that ship have no picker per pad, which is exactly the case.
/// </remarks>
public sealed class KitPresetTakeTests
{
    /// <summary>A shipped machine's folder, walking up out of the test's own output.</summary>
    private static string Shipped(string name)
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at != null && !Directory.Exists(Path.Combine(at.FullName, "rack", "machines", name))) at = at.Parent;

        return at is null ? "" : Path.Combine(at.FullName, "rack", "machines", name);
    }

    /// <summary>Every BongaBong preset puts a recording on each pad it names.</summary>
    [Fact]
    public void Every_BongaBong_preset_puts_recordings_on_its_pads()
    {
        string folder = Shipped("BongaBong");
        var machine = SoundMachineProject.Open(folder);

        Assert.NotNull(machine);

        var files = new SoundMachinePresetFile();
        var presets = Directory.GetFiles(Path.Combine(folder, "presets"), "*.json");

        Assert.NotEmpty(presets);

        foreach (string file in presets)
        {
            var sound = files.Read(file, machine!);

            Assert.NotNull(sound?.Kit);

            var named = sound!.Kit!.Pads.Where(pad => pad.Name.Length > 0).ToList();

            Assert.NotEmpty(named);
            Assert.All(named, pad => Assert.False(string.IsNullOrEmpty(pad.FilePath),
                Path.GetFileName(file) + " put nothing on " + pad.Name));
        }
    }
}
