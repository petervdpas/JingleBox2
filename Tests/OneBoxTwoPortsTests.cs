using System.Collections.Generic;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A box on two ports is one desk, which nothing in a port name says.
/// </summary>
/// <remarks>
/// **One control does one job, and that rule was being kept per port rather than per box.** A
/// MiniLab 3 arrives on this machine as <c>Minilab3 MIDI</c>, <c>Minilab3 ALV</c>,
/// <c>Minilab3 MCU/HUI</c> and <c>Minilab3 DIN THRU</c>, and it is one thing under the hand. A
/// knob learned while a second of those was delivering left the first link where it was, so one
/// knob ended up with two links: two rows under one card, and both of them firing.
///
/// What a profile calls the device is the only thing here that knows the ports are one box, so it
/// is asked. Nothing is claimed where nobody knows: a device with no profile is compared by its
/// port name exactly as before, which is the case this whole layer is built to work in.
/// </remarks>
public sealed class OneBoxTwoPortsTests
{
    /// <summary>A knob on a machine, learned on a port.</summary>
    private static ControlMapping Knob(string port, int cc = 86, string key = "tune") => new()
    {
        Device = port,
        Channel = 1,
        Cc = cc,
        Kind = ControlKind.SoundDevice,
        Machine = "machine.oddskilla",
        Key = key,
        Owner = "OddSkilla",
        Name = "OddSkilla " + key,
    };

    /// <summary>What a profile calls the MiniLab's ports, and leaves everything else alone.</summary>
    private static string Called(string port) =>
        port.StartsWith("Minilab3", System.StringComparison.OrdinalIgnoreCase) ? "MiniLab 3" : port;

    /// <summary>A desk holding one link, with the profile naming handed in.</summary>
    private static ControlLink Over(List<ControlMapping> desk, bool named = true)
    {
        var link = new ControlLink(desk, () => { });

        if (named) link.Called = Called;

        return link;
    }

    /// <summary>
    /// **The same knob arriving on the box's other port takes the first link off.**
    /// </summary>
    [Fact]
    public void The_same_knob_on_another_port_of_one_box_displaces()
    {
        var desk = new List<ControlMapping> { Knob("Minilab3 ALV") };

        Over(desk).Take(new[] { Knob("Minilab3 MIDI") });

        var one = Assert.Single(desk);

        Assert.Equal("Minilab3 MIDI", one.Device);
    }

    /// <summary>And a different box pointed at the same control keeps both, as it always did.</summary>
    /// <remarks>
    /// The rule this must not break: two desks pointed at one machine can never both fire, since
    /// a link answers only its own controller's messages, so they are two templates rather than a
    /// fight and neither displaces the other.
    /// </remarks>
    [Fact]
    public void Another_box_on_the_same_control_keeps_both()
    {
        var desk = new List<ControlMapping> { Knob("Minilab3 MIDI") };

        Over(desk).Take(new[] { Knob("MPD218 Port A") });

        Assert.Equal(2, desk.Count);
    }

    /// <summary>With nobody to say the ports are one box, they are two, exactly as before.</summary>
    [Fact]
    public void With_no_profile_two_ports_are_two_desks()
    {
        var desk = new List<ControlMapping> { Knob("Minilab3 ALV") };

        Over(desk, named: false).Take(new[] { Knob("Minilab3 MIDI") });

        Assert.Equal(2, desk.Count);
    }

    /// <summary>A control pointed somewhere else on one port takes its twin off the other.</summary>
    /// <remarks>
    /// The other half of one control doing one job: it is the control that is displaced, wherever
    /// it was learned, not merely the target.
    /// </remarks>
    [Fact]
    public void Pointing_it_elsewhere_takes_the_twin_off_too()
    {
        var desk = new List<ControlMapping> { Knob("Minilab3 ALV") };

        Over(desk).Take(new[] { Knob("Minilab3 MIDI", key: "cutoff") });

        var one = Assert.Single(desk);

        Assert.Equal("cutoff", one.Key);
    }
}
