using System.Collections.Generic;
using System.Linq;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Links cut into templates, which is what fills the block at startup.
/// </summary>
/// <remarks>
/// **One template is one controller's layout for one thing.** So the cut is by what the link is
/// pointed at and then by which box it was learned on, and the interesting cases are the ones
/// where those two come apart: one desk on two machines, two desks on one machine, and a link
/// made before controllers were recorded, which names none.
///
/// It is the same rule the page cuts its cards by and the same one an export writes, which is
/// why it is one method: three spellings would eventually disagree, and the way that fails is a
/// block holding something the page does not list.
/// </remarks>
public sealed class ControlTemplateCutTests
{
    /// <summary>A knob on a machine, learned on a port.</summary>
    private static ControlMapping Knob(string port, string machine, int cc) => new()
    {
        Device = port,
        Channel = 1,
        Cc = cc,
        Kind = ControlKind.SoundDevice,
        Machine = machine,
        Key = "knob" + cc,
        Owner = machine,
        Name = machine + " knob " + cc,
    };

    /// <summary>Nothing pointed anywhere is no templates, which is a fresh installation.</summary>
    [Fact]
    public void Nothing_pointed_anywhere_is_no_templates()
    {
        Assert.Empty(new ControlTemplates().Cut(null));
        Assert.Empty(new ControlTemplates().Cut(new List<ControlMapping>()));
    }

    /// <summary>One desk on one machine is one template holding all of its controls.</summary>
    [Fact]
    public void One_desk_on_one_machine_is_one_template()
    {
        var cut = new ControlTemplates().Cut(new[]
        {
            Knob("nanoKONTROL2 _ CTRL", "machine.oddskilla", 16),
            Knob("nanoKONTROL2 _ CTRL", "machine.oddskilla", 17),
        });

        var one = Assert.Single(cut);

        Assert.Equal(2, one.Controls.Count);
        Assert.Equal("machine.oddskilla", one.Target.Id);
    }

    /// <summary>One desk on two machines is two templates, since a template is about one thing.</summary>
    [Fact]
    public void One_desk_on_two_machines_is_two_templates()
    {
        var cut = new ControlTemplates().Cut(new[]
        {
            Knob("nanoKONTROL2 _ CTRL", "machine.oddskilla", 16),
            Knob("nanoKONTROL2 _ CTRL", "machine.ouroboros", 17),
        });

        Assert.Equal(2, cut.Count);
        Assert.All(cut, one => Assert.Single(one.Controls));
    }

    /// <summary>
    /// **And two desks on one machine are two templates**, which is the case that is not obvious.
    /// </summary>
    /// <remarks>
    /// A link answers only its own controller's messages, so the two can never compete: they are
    /// two things somebody keeps, hands on or lays down apart, and rolling them into one would
    /// hand somebody a template for a box they have not got.
    /// </remarks>
    [Fact]
    public void Two_desks_on_one_machine_are_two_templates()
    {
        var cut = new ControlTemplates().Cut(new[]
        {
            Knob("nanoKONTROL2 _ CTRL", "machine.oddskilla", 16),
            Knob("MiniLab3 MIDI", "machine.oddskilla", 74),
        });

        Assert.Equal(2, cut.Count);
        Assert.Equal(
            new[] { "MiniLab3 MIDI", "nanoKONTROL2 _ CTRL" },
            cut.Select(one => one.Controller).OrderBy(one => one).ToArray());
    }

    /// <summary>
    /// The controller is what its profile calls it and never the port, which is the one thing in
    /// a template that does not travel.
    /// </summary>
    [Fact]
    public void The_controller_is_what_its_profile_calls_it()
    {
        var cut = new ControlTemplates().Cut(
            new[] { Knob("nanoKONTROL2 _ CTRL", "machine.oddskilla", 16) },
            port => port.Replace(" _ CTRL", ""));

        Assert.Equal("nanoKONTROL2", Assert.Single(cut).Controller);
    }

    /// <summary>And the legends are asked for by the port they are printed on.</summary>
    [Fact]
    public void The_legends_are_asked_for_by_port()
    {
        var cut = new ControlTemplates().Cut(
            new[] { Knob("nanoKONTROL2 _ CTRL", "machine.oddskilla", 16) },
            named: (port, channel, cc) => port + " ch" + channel + " cc" + cc);

        Assert.Equal("nanoKONTROL2 _ CTRL ch1 cc16", Assert.Single(cut).Controls[0].Control);
    }

    /// <summary>A link made before controllers were recorded names none, and is still a template.</summary>
    /// <remarks>
    /// Its own, rather than being folded in with a named desk's: it is the wildcard that answers
    /// every device, so it is not any one of them.
    /// </remarks>
    [Fact]
    public void A_link_naming_no_controller_is_its_own()
    {
        var cut = new ControlTemplates().Cut(new[]
        {
            Knob("", "machine.oddskilla", 16),
            Knob("nanoKONTROL2 _ CTRL", "machine.oddskilla", 17),
        });

        Assert.Equal(2, cut.Count);
        Assert.Contains(cut, one => one.Controller.Length == 0);
    }

    /// <summary>
    /// **A device on two ports is one desk, and so one template.**
    /// </summary>
    /// <remarks>
    /// The case this was got wrong on, and it is the ordinary case rather than a corner: a
    /// MiniLab arrives as <c>Minilab3 MIDI</c> and <c>Minilab3 ALV</c> on one machine, and both
    /// are the same box under the hand. Cut by the port it is two templates under one name, each
    /// covering the other's links as well as its own, which draws as two identical cards.
    /// </remarks>
    [Fact]
    public void A_device_on_two_ports_is_one_template()
    {
        var cut = new ControlTemplates().Cut(
            new[]
            {
                Knob("Minilab3 MIDI", "machine.oddskilla", 16),
                Knob("Minilab3 ALV", "machine.oddskilla", 17),
            },
            port => "MiniLab 3");

        var one = Assert.Single(cut);

        Assert.Equal("MiniLab 3", one.Controller);
        Assert.Equal(2, one.Controls.Count);
    }

    /// <summary>And what it covers is both of those ports' links, once each.</summary>
    /// <remarks>
    /// The other half of the same fault: the cut and <see cref="ControlTemplates.Covers"/> have
    /// to agree, or a card is headed by a template that does not carry the rows under it.
    /// </remarks>
    [Fact]
    public void And_it_covers_both_ports()
    {
        var links = new[]
        {
            Knob("Minilab3 MIDI", "machine.oddskilla", 16),
            Knob("Minilab3 ALV", "machine.oddskilla", 17),
        };

        var store = new ControlTemplates();

        var one = Assert.Single(store.Cut(links, port => "MiniLab 3"));

        Assert.Equal(2, links.Count(link => store.Covers(one, link, port => "MiniLab 3")));
    }

    /// <summary>The order does not move under the same links arriving in another order.</summary>
    /// <remarks>
    /// What that is worth is a writer that compares what it would write with what it wrote: a
    /// list that came out differently each time would report a change on every look.
    /// </remarks>
    [Fact]
    public void The_order_does_not_move()
    {
        var links = new[]
        {
            Knob("nanoKONTROL2 _ CTRL", "machine.ouroboros", 17),
            Knob("MiniLab3 MIDI", "machine.oddskilla", 74),
            Knob("nanoKONTROL2 _ CTRL", "machine.oddskilla", 16),
        };

        var store = new ControlTemplates();

        string Said(IReadOnlyList<ControlTemplate> cut) =>
            string.Join("|", cut.Select(one => one.Target.Id + "/" + one.Controller));

        Assert.Equal(Said(store.Cut(links)), Said(store.Cut(links.Reverse().ToArray())));
    }
}
