using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which outputs really get this machine's clock, once the one being followed is left out.
/// </summary>
/// <remarks>
/// **A setting rather than a wire, which is why it is a rule of its own.** Two answers are in
/// hand in exactly one place, whose clock the transport runs on and which outputs are ticked, and
/// the only honest reading of both together is that a machine does not send its own clock back to
/// the machine it is taking one from.
///
/// The case it exists for is not obscure. A device with a MIDI in and a MIDI out is one name in
/// each list, and where the two read the same the obvious way to set a chain up is to follow it
/// and tick it, at which point every tick goes straight back. A device that recognises clock as
/// well as sending it is then in a loop, and from a chair it is a tempo that will not settle with
/// nothing anywhere saying why.
///
/// **Nothing here names a real device and nothing looks a port up.** The names are strings, the
/// rule compares them, and every one of these answers the same on a machine with a rack of gear
/// and on one with no MIDI at all.
/// </remarks>
public class ClockDrivenPortsTests
{
    /// <summary>Settings following that port and driving those outputs.</summary>
    private static MidiConfig Following(string? port, params string[] outputs)
    {
        var cfg = new MidiConfig
        {
            ClockSource = MidiClockSource.Followed,
            ClockPort = port
        };

        cfg.ClockOutputs.AddRange(outputs);

        return cfg;
    }

    /// <summary>The port being followed is not driven, however it was ticked.</summary>
    [Fact]
    public void The_followed_port_is_not_driven()
    {
        var cfg = Following("clock box", "clock box", "drum machine");

        Assert.Equal(new[] { "drum machine" }, cfg.ClockDriven);
    }

    /// <summary>And the comparison ignores case, like every other port name here.</summary>
    /// <remarks>
    /// The two lists are filled from two different enumerations, so the same device can arrive
    /// spelled two ways and a case-sensitive test would let the echo through on some machines and
    /// not others, which is the worst of both.
    /// </remarks>
    [Fact]
    public void Case_does_not_let_the_echo_through()
    {
        var cfg = Following("Clock Box", "CLOCK BOX");

        Assert.Empty(cfg.ClockDriven);
    }

    /// <summary>Everything else that was ticked is still driven, in the order it was ticked.</summary>
    /// <remarks>
    /// The whole point of passing a clock on is the third device, so a rule that dropped more
    /// than the one port would take the feature away while looking like it was protecting it.
    /// </remarks>
    [Fact]
    public void Everything_else_is_still_driven_and_keeps_its_order()
    {
        var cfg = Following("clock box", "first", "clock box", "second", "third");

        Assert.Equal(new[] { "first", "second", "third" }, cfg.ClockDriven);
    }

    /// <summary>On its own clock, every ticked output is driven, followed port or not.</summary>
    /// <remarks>
    /// A machine that is the master is not taking a clock from anybody, so there is no echo to
    /// make: a port left named in <see cref="MidiConfig.ClockPort"/> is deliberately kept when
    /// following is turned off, and it must not go on being excluded because of it.
    /// </remarks>
    [Fact]
    public void On_its_own_clock_nothing_is_left_out()
    {
        var cfg = Following("clock box", "clock box", "drum machine");

        cfg.ClockSource = MidiClockSource.Own;

        Assert.Equal(new[] { "clock box", "drum machine" }, cfg.ClockDriven);
    }

    /// <summary>And following nothing in particular leaves the list exactly as it is.</summary>
    /// <remarks>
    /// Following is ticked and no port has been chosen yet, which is the state the settings page
    /// is in for as long as it takes somebody to open the picker.
    /// </remarks>
    [Fact]
    public void Following_with_no_port_chosen_leaves_the_list_alone()
    {
        Assert.Equal(new[] { "drum machine" }, Following(null, "drum machine").ClockDriven);
        Assert.Equal(new[] { "drum machine" }, Following("  ", "drum machine").ClockDriven);
    }

    /// <summary>Driving nothing is driving nothing, which is a fresh installation.</summary>
    [Fact]
    public void Driving_nothing_stays_nothing()
    {
        Assert.Empty(Following("clock box").ClockDriven);
    }

    /// <summary>Reading it does not change what was ticked.</summary>
    /// <remarks>
    /// What the settings page shows is <see cref="MidiConfig.ClockOutputs"/> and what is saved is
    /// the same list, so a read that quietly pruned it would lose somebody's tick the next time
    /// the file was written and would look like the page forgetting.
    /// </remarks>
    [Fact]
    public void Reading_it_leaves_the_ticks_where_they_were()
    {
        var cfg = Following("clock box", "clock box", "drum machine");

        _ = cfg.ClockDriven;
        _ = cfg.ClockDriven;

        Assert.Equal(new[] { "clock box", "drum machine" }, cfg.ClockOutputs);
    }
}
