using System.Collections.Generic;
using JingleBox2.SoundDevices.Interfaces;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The designer says so when what is open carries the id of a device that ships.
/// </summary>
/// <remarks>
/// A device is known by its id, so one of somebody's own under a shipped id is that device as far
/// as the registry is concerned: the start-up pass brings the shipped copy over the top of it and
/// the work is gone. That is deliberate, and it is also invisible, which is the whole reason this
/// exists. Where somebody chooses the id is the only place it can be caught.
///
/// A warning and not a refusal. Putting an edited device back over the copy that ships is a real
/// thing to want and is what Save as is for; what it may not do is happen quietly.
/// </remarks>
public class ShippedIdWarningTests
{
    /// <summary>A world that ships exactly one id and holds nothing on disc.</summary>
    /// <param name="ships">The id it claims to ship.</param>
    /// <param name="making">The id a fresh device comes out with.</param>
    private sealed class World(string ships, string making) : IDesignWorld
    {
        public IReadOnlyList<string> Parts => new List<string>();

        public bool Played => true;

        public string Word => "machine";

        public string ManifestName => "machine.json";

        public string Installed => "";

        public IDesignProject New() => new SoundMachineProject { Id = making, Name = "Fresh" };

        public IDesignProject? Open(string folder) => null;

        public bool CopyInto(IDesignProject project, string folder) => false;

        public bool Exports => false;

        public bool HasPresets => false;

        public void Export(IDesignProject project, string zipPath) { }

        public bool Ships(string? id) => id == ships;
    }

    /// <summary>A fresh device under an id of its own says nothing.</summary>
    [Fact]
    public void An_id_of_your_own_is_not_warned_about()
    {
        var page = new DesignerViewModel(new World("machine.taken", "machine.mine"));

        page.NewCommand.Execute(null);

        Assert.DoesNotContain("Careful", page.Status);
    }

    /// <summary>And one carrying a shipped id says what will happen to it.</summary>
    [Fact]
    public void A_shipped_id_is_warned_about_on_new()
    {
        var page = new DesignerViewModel(new World("machine.taken", "machine.taken"));

        page.NewCommand.Execute(null);

        Assert.Contains("Careful", page.Status);
        Assert.Contains("machine.taken", page.Status);
        Assert.Contains("next start", page.Status);
    }

    /// <summary>The real machines world knows what it ships, and what it does not.</summary>
    [Theory]
    [InlineData("machine.oddskilla", true)]
    [InlineData("machine.bongabong", true)]
    [InlineData("machine.something-of-mine", false)]
    [InlineData("", false)]
    public void The_machines_world_knows_what_ships(string id, bool ships)
    {
        Assert.Equal(ships, new SoundMachineWorld().Ships(id));
    }

    /// <summary>And so does the effects world, which is the same rule in the other half.</summary>
    [Theory]
    [InlineData("effect.echobox", true)]
    [InlineData("effect.roaster", true)]
    [InlineData("effect.something-of-mine", false)]
    [InlineData("", false)]
    public void The_effects_world_knows_what_ships(string id, bool ships)
    {
        Assert.Equal(ships, new SoundEffectWorld().Ships(id));
    }
}
