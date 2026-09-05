using System.Linq;
using JingleBox2.Rack.SoundDevices.Faces.Records;
using JingleBox2.SoundDevices.SoundMachines.Records;
using JingleBox2.Tracker.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A soundmachine carries its own id and names its own engine.
/// </summary>
/// <remarks>
/// It used to do neither. The id was worked out from the engine by a switch of five strings
/// written into the application, so there could only ever be five soundmachines, their names were
/// decided here rather than by whoever made them, and one designed in the designer under any
/// other id was read off disc, refused in silence, and never reached the rack or any song.
///
/// A device is made in the designer, registered, and put on the rack, and nothing in that
/// sentence is a fact about an engine. So the manifest says which engine it plays and the folder
/// says what it is called, which is what these hold it to.
/// </remarks>
public class DeviceIdentityTests
{
    /// <summary>A colour, since a machine needs one and none of this is about colours.</summary>
    private static PanelTheme Paint => new("#7B838C");

    /// <summary>Takes the registry back to the plugin heading it starts on.</summary>
    private static void Empty() => SoundMachine.Forget();

    /// <summary>A device made in the designer reaches the registry under its own name.</summary>
    [Fact]
    public void A_machine_made_in_the_designer_is_registered_under_its_own_id()
    {
        Empty();

        Assert.True(SoundMachine.Register("machine.mything", "MyThing", "Mine.", Paint, "Synth"));

        var mine = SoundMachine.Installed.Single(one => one.Id == "machine.mything");

        Assert.Equal("MyThing", mine.Name);
        Assert.Equal(TrackerInstrumentKind.Synth, mine.Kind);
    }

    /// <summary>And two of them on one engine are two devices, not one replacing the other.</summary>
    /// <remarks>
    /// The whole of what the old switch made impossible: one machine to an engine, so registering
    /// a second quietly threw the first away. Two kits are two devices.
    /// </remarks>
    [Fact]
    public void Two_machines_can_sit_on_one_engine()
    {
        Empty();

        SoundMachine.Register("machine.first", "First", "", Paint, "Kit");
        SoundMachine.Register("machine.second", "Second", "", Paint, "Kit");

        Assert.Equal(2, SoundMachine.Installed.Count(one => one.Kind == TrackerInstrumentKind.Kit));
    }

    /// <summary>Registering the same id twice is that device again, not a second of it.</summary>
    [Fact]
    public void The_same_id_registered_twice_is_one_machine()
    {
        Empty();

        SoundMachine.Register("machine.once", "Old name", "", Paint, "Synth");
        SoundMachine.Register("machine.once", "New name", "", Paint, "Synth");

        var only = SoundMachine.Installed.Single(one => one.Id == "machine.once");

        Assert.Equal("New name", only.Name);
    }

    /// <summary>The five that shipped name no engine and still work, which is every song on disc.</summary>
    /// <remarks>
    /// Their manifests say nothing in <c>Engine</c>, because there was nothing to say when they
    /// were written, and every song and rack file anybody has names them. So the old mapping is
    /// kept for exactly those five ids and consulted only where a manifest is silent.
    /// </remarks>
    [Theory]
    [InlineData("machine.oddskilla", TrackerInstrumentKind.Synth)]
    [InlineData("machine.zampler", TrackerInstrumentKind.Sampler)]
    [InlineData("machine.bongabong", TrackerInstrumentKind.Kit)]
    [InlineData("machine.ouroboros", TrackerInstrumentKind.MonoSynth)]
    [InlineData("machine.recording", TrackerInstrumentKind.Sample)]
    public void The_machines_that_shipped_still_register_naming_no_engine(string id, TrackerInstrumentKind kind)
    {
        Empty();

        Assert.True(SoundMachine.Register(id, "", "", Paint, ""));
        Assert.Equal(kind, SoundMachine.Installed.Single(one => one.Id == id).Kind);
    }

    /// <summary>An engine the manifest names beats the id, since the id decides nothing now.</summary>
    [Fact]
    public void The_named_engine_beats_the_old_mapping()
    {
        Empty();

        SoundMachine.Register("machine.oddskilla", "Odd", "", Paint, "Kit");

        Assert.Equal(TrackerInstrumentKind.Kit,
            SoundMachine.Installed.Single(one => one.Id == "machine.oddskilla").Kind);
    }

    /// <summary>A device naming an engine this build has not got is still passed over.</summary>
    /// <remarks>
    /// The gate that keeps a folder from a later version harmless, and the one thing about the
    /// old rule that was right: an engine is in the application, so a device asking for one that
    /// is not here has nothing to sound with and is left on the disc rather than put on the rack.
    /// </remarks>
    [Fact]
    public void A_machine_on_an_engine_this_build_has_not_got_is_refused()
    {
        Empty();

        Assert.False(SoundMachine.Register("machine.future", "Future", "", Paint, "Granular"));
        Assert.DoesNotContain(SoundMachine.Installed, one => one.Id == "machine.future");
    }

    /// <summary>And so is one that names no engine and is not one of the five.</summary>
    [Fact]
    public void A_machine_naming_no_engine_under_a_new_id_is_refused()
    {
        Empty();

        Assert.False(SoundMachine.Register("machine.nameless", "Nameless", "", Paint, ""));
    }

    /// <summary>The engine name is read the way somebody would write it, not as an enum.</summary>
    [Theory]
    [InlineData("Mono synth")]
    [InlineData("monosynth")]
    [InlineData("MONO SYNTH")]
    [InlineData("mono-synth")]
    public void The_engine_name_is_read_loosely(string written)
    {
        Assert.Equal(TrackerInstrumentKind.MonoSynth, SoundMachine.EngineNamed(written));
    }
}
