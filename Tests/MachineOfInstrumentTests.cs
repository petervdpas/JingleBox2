using System.Text.Json;
using JingleBox2.Rack.SoundDevices.Faces.Records;
using JingleBox2.SoundDevices.SoundMachines.Records;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which machine an instrument wears the face of, when two machines share an engine.
/// </summary>
/// <remarks>
/// BongaBong and Chopper both play the kit engine. An instrument that knew only its engine would
/// open whichever of the two registered first, so an instrument says which machine it came off,
/// and one saved before it could say opens on the machine its engine has always meant.
///
/// The registry of machines is one list for the application, so each test puts it back the way
/// the others expect: empty of machines read off disc.
/// </remarks>
public sealed class MachineOfInstrumentTests
{
    /// <summary>A plain theme, which none of this is about.</summary>
    private static readonly PanelTheme Plain = new("#808080");

    /// <summary>Registers the two kits, the newer one first so registration order cannot be the answer.</summary>
    private static void TwoKits()
    {
        SoundMachine.Forget();
        SoundMachine.Register("machine.chopper", "Chopper", "", Plain, "Kit");
        SoundMachine.Register("machine.bongabong", "BongaBong", "", Plain, "Kit");
    }

    /// <summary>An instrument made on a machine wears that machine's face.</summary>
    [Fact]
    public void An_instrument_made_on_a_machine_is_on_that_machine()
    {
        TwoKits();

        try
        {
            var chopper = SoundMachine.For(TrackerInstrumentKind.Kit, "machine.chopper");
            var made = TrackerInstrument.CreateOn(chopper, "Beat");

            Assert.Equal("machine.chopper", made.MachineId);
            Assert.Equal("Chopper", made.Machine.Name);
            Assert.Equal("Chopper", made.Clone().Machine.Name);
        }
        finally
        {
            SoundMachine.Forget();
        }
    }

    /// <summary>One saved before it could say opens on the machine its engine always meant, not the first registered.</summary>
    [Fact]
    public void An_instrument_that_does_not_say_is_on_the_engines_own_machine()
    {
        TwoKits();

        try
        {
            var old = TrackerInstrument.CreateKit("Old kit");

            Assert.Null(old.MachineId);
            Assert.Equal("BongaBong", old.Machine.Name);
        }
        finally
        {
            SoundMachine.Forget();
        }
    }

    /// <summary>A machine's own slot on the rack wears that machine's face, by its id.</summary>
    [Fact]
    public void A_slot_on_the_rack_is_on_its_own_machine()
    {
        TwoKits();

        try
        {
            var slot = TrackerInstrument.CreateKit("Chopper");

            slot.Id = "machine.chopper";

            Assert.Equal("Chopper", slot.Machine.Name);
        }
        finally
        {
            SoundMachine.Forget();
        }
    }

    /// <summary>A machine named on another engine, or not registered at all, is not taken.</summary>
    [Fact]
    public void A_machine_on_another_engine_or_missing_is_not_taken()
    {
        TwoKits();

        try
        {
            var wrong = TrackerInstrument.CreateKit("Kit");

            wrong.MachineId = "machine.nowhere";

            Assert.Equal("BongaBong", wrong.Machine.Name);

            SoundMachine.Register("machine.operetta", "Operetta", "", Plain, "FM");

            wrong.MachineId = "machine.operetta";

            Assert.Equal("BongaBong", wrong.Machine.Name);
        }
        finally
        {
            SoundMachine.Forget();
        }
    }

    /// <summary>It is written into a file only when there is something to say, and read back.</summary>
    [Fact]
    public void It_travels_in_a_file_and_is_left_out_when_empty()
    {
        var old = TrackerInstrument.CreateKit("Old kit");

        Assert.DoesNotContain("MachineId", JsonSerializer.Serialize(old));

        old.MachineId = "machine.chopper";

        var back = JsonSerializer.Deserialize<TrackerInstrument>(JsonSerializer.Serialize(old))!;

        Assert.Equal("machine.chopper", back.MachineId);
    }
}
