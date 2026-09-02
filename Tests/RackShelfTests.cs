
using JingleBox2.Tracker;
using Xunit;
using JingleBox2.Devices.SoundMachines;

namespace JingleBox2.Tests;

/// <summary>
/// What the rack remembers about which machines have been put on it.
/// </summary>
/// <remarks>
/// The rack decides which machines a song can be given, so a machine can be taken off it, and
/// that is only possible if the rack is not rebuilt from the registry every time it is opened.
/// What has been offered is recorded rather than what is present, which is the rule the registry
/// itself keeps: deciding by absence would put a machine you took off back the next morning with
/// nothing to say why, and there would be no way to be without one.
///
/// Taking a machine off the rack is not losing it. The machine stays registered, so a song that
/// uses it still sounds and the picker offers it back. Losing one is unregistering it, which is
/// a different act on a different page, and `MissingMachineTests` is where that is checked.
/// </remarks>
public class RackShelfTests
{
    /// <summary>A rack of its own, under a name no other test uses.</summary>
    /// <param name="named">What to call the folder.</param>
    private static SoundMachineRack Rack(string named) => new("jinglebox2-shelf-" + named);

    /// <summary>A rack that has never been opened has been offered nothing.</summary>
    [Fact]
    public void A_new_rack_has_had_nothing_offered_to_it()
    {
        Assert.Empty(Rack("fresh").Shelved);
    }

    /// <summary>What was put on is remembered, and remembered by the next reader.</summary>
    [Fact]
    public void A_machine_put_on_the_rack_is_written_down()
    {
        Rack("written").Shelve("machine.zampler");

        Assert.Contains("machine.zampler", Rack("written").Shelved);
    }

    /// <summary>
    /// And it stays written down once the box has gone, which is the whole point.
    /// </summary>
    /// <remarks>
    /// The record is of what has been offered, not of what is there. A rack that decided by
    /// looking would put the box back the next time it was read.
    /// </remarks>
    [Fact]
    public void Taking_the_box_off_does_not_take_the_machine_off_the_record()
    {
        var rack = Rack("taken");

        var box = TrackerInstrument.CreateOn(JingleBox2.Devices.SoundMachines.Records.SoundMachine.For(
            JingleBox2.Tracker.Enums.TrackerInstrumentKind.Synth), "OddSkilla");

        box.Id = "machine.oddskilla";

        rack.Save(box);
        rack.Shelve(box.Id);

        Assert.True(rack.Delete(box.Id));
        Assert.Null(rack.Load(box.Id));
        Assert.Contains("machine.oddskilla", rack.Shelved);
    }

    /// <summary>Offering the same machine twice writes one line, not two.</summary>
    [Fact]
    public void A_machine_is_written_down_once()
    {
        var rack = Rack("twice");

        rack.Shelve("machine.kit");
        rack.Shelve("machine.kit");

        Assert.Single(rack.Shelved, one => one == "machine.kit");
    }

    /// <summary>Nothing is written down for nothing.</summary>
    [Fact]
    public void An_empty_id_is_not_a_machine()
    {
        var rack = Rack("empty");

        rack.Shelve("");
        rack.Shelve("   ");

        Assert.Empty(rack.Shelved);
    }
}
