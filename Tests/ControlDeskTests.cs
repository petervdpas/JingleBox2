using System.Collections.Generic;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What the desk keeps and what it gives up when a link arrives.
/// </summary>
/// <remarks>
/// The rule is written twice in this repository, once as prose and once as code, and the two had
/// drifted: a link is displaced by the same physical control being pointed somewhere else, or by
/// something else on the same controller being pointed at the same thing. The second half had
/// lost its controller, so pointing a second desk at a machine quietly deleted the first desk's
/// links on it.
///
/// That is one fault wearing two faces. The links vanish, which is "my templates get lost"; and
/// the surfaces line on a machine's face lists the links that are left, so what comes back is
/// what survived, which is "restoring only applies half of the knobs".
/// </remarks>
public class ControlDeskTests
{
    /// <summary>What a device's control offers. Holds nothing, so one serves every test here.</summary>
    private static readonly ISoundDeviceLinks Links = new SoundDeviceLinks();

    /// <summary>The Korg, as its port is called here.</summary>
    private const string Korg = "nanoKONTROL2 _ CTRL";

    /// <summary>The Arturia, which is a second desk in the same room.</summary>
    private const string Arturia = "MiniLab3 MIDI";

    /// <summary>
    /// Two desks pointed at one control of one device are two links, and both stay.
    /// </summary>
    /// <remarks>
    /// A link records the controller it was learned on, so at most one of the two can ever answer
    /// a given message and neither is competing with the other. Somebody with two boxes on the
    /// desk wants both of them driving the machine in front of them.
    /// </remarks>
    [Fact]
    public void Two_controllers_may_both_be_pointed_at_one_control()
    {
        var desk = new List<ControlMapping>();
        var link = new ControlLink(desk, () => { }) { IsLinking = true };

        Point(link, "machine.oddskilla", "OddSkilla", "cutoff", Korg, 20);
        Point(link, "machine.oddskilla", "OddSkilla", "cutoff", Arturia, 74);

        Assert.Equal(2, desk.Count);
        Assert.Contains(desk, one => one.Device == Korg && one.Cc == 20);
        Assert.Contains(desk, one => one.Device == Arturia && one.Cc == 74);
    }

    /// <summary>
    /// And a whole template survives a second desk being pointed at the same machine.
    /// </summary>
    /// <remarks>
    /// The shape of what was reported: four knobs learned on one box, two of them learned again
    /// on another, and the first box came back with two. Nothing said so, because a link taken
    /// off is what pointing at something is supposed to do.
    /// </remarks>
    [Fact]
    public void A_second_desk_does_not_eat_the_first_ones_template()
    {
        var desk = new List<ControlMapping>();
        var link = new ControlLink(desk, () => { }) { IsLinking = true };

        int cc = 20;

        foreach (var key in new[] { "time", "feedback", "damp", "mix" })
            Point(link, "effect.echobox", "EchoBox", key, Korg, cc++);

        Point(link, "effect.echobox", "EchoBox", "time", Arturia, 74);
        Point(link, "effect.echobox", "EchoBox", "damp", Arturia, 76);

        Assert.Equal(4, Count(desk, Korg));
        Assert.Equal(2, Count(desk, Arturia));
    }

    /// <summary>
    /// One knob does one job, so a second knob on the same desk still replaces the first.
    /// </summary>
    [Fact]
    public void One_desk_pointed_twice_at_one_control_keeps_the_second()
    {
        var desk = new List<ControlMapping>();
        var link = new ControlLink(desk, () => { }) { IsLinking = true };

        Point(link, "machine.oddskilla", "OddSkilla", "cutoff", Korg, 20);
        Point(link, "machine.oddskilla", "OddSkilla", "cutoff", Korg, 21);

        Assert.Single(desk);
        Assert.Equal(21, desk[0].Cc);
    }

    /// <summary>And the same knob pointed somewhere else takes its old job with it.</summary>
    [Fact]
    public void The_same_knob_pointed_elsewhere_gives_up_what_it_had()
    {
        var desk = new List<ControlMapping>();
        var link = new ControlLink(desk, () => { }) { IsLinking = true };

        Point(link, "machine.oddskilla", "OddSkilla", "cutoff", Korg, 20);
        Point(link, "machine.oddskilla", "OddSkilla", "decay", Korg, 20);

        Assert.Single(desk);
        Assert.Equal("decay", desk[0].Key);
    }

    /// <summary>
    /// A link naming no controller answers every message, so it is displaced by any of them.
    /// </summary>
    /// <remarks>
    /// The wildcard a link made before controllers were recorded reads as. It really would fire
    /// beside the arriving link, which is the case the rule exists for.
    /// </remarks>
    [Fact]
    public void A_link_naming_no_controller_is_displaced_by_any_of_them()
    {
        var desk = new List<ControlMapping> { Links.On("machine.oddskilla", "OddSkilla", "cutoff") };
        var link = new ControlLink(desk, () => { }) { IsLinking = true };

        Point(link, "machine.oddskilla", "OddSkilla", "cutoff", Korg, 20);

        Assert.Single(desk);
        Assert.Equal(Korg, desk[0].Device);
    }

    /// <summary>Two machines on one knob of one desk are still kept apart.</summary>
    [Fact]
    public void Two_devices_on_one_knob_are_both_kept()
    {
        var desk = new List<ControlMapping>();
        var link = new ControlLink(desk, () => { }) { IsLinking = true };

        Point(link, "machine.oddskilla", "OddSkilla", "cutoff", Korg, 20);
        Point(link, "effect.echobox", "EchoBox", "time", Korg, 20);

        Assert.Equal(2, desk.Count);
    }

    /// <summary>Points that control at that parameter, the way a hand does.</summary>
    /// <param name="link">The desk.</param>
    /// <param name="id">The device's id.</param>
    /// <param name="named">What it is called.</param>
    /// <param name="key">Which parameter.</param>
    /// <param name="device">Which controller the message comes from.</param>
    /// <param name="cc">Which continuous controller.</param>
    private static void Point(ControlLink link, string id, string named, string key, string device, int cc)
    {
        link.Offer(Links.On(id, named, key));

        link.Handle(new MidiMessage
        {
            Device = device,
            Type = MidiMessageType.ControlChange,
            Channel = 1,
            Value = cc,
            Data = 64
        });
    }

    /// <summary>How many of the desk's links were learned on that controller.</summary>
    /// <param name="desk">The links.</param>
    /// <param name="device">Which controller.</param>
    private static int Count(IEnumerable<ControlMapping> desk, string device)
    {
        int many = 0;

        foreach (var one in desk) if (MidiService.SameName(one.Device, device)) many++;

        return many;
    }
}
