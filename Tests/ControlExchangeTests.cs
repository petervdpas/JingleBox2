using System.Collections.Generic;
using JingleBox2.Controllers;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The hook a template is exchanged through, which the three places share.
/// </summary>
/// <remarks>
/// A sound device's face, the mixer and the pads are the whole list, and each was answering these
/// three questions its own way or not at all. The menu on a face read the block and laid a
/// template down; the mixer and the pads did the same through a menu of their own; and what is
/// already wired was answered only by a drawn face, so a template applied to the mixer marked
/// nothing whatever and read exactly like one that had not been applied.
///
/// So the rule is here and the pages are faces over it. What these say is the whole contract: what
/// there is for this thing, what choosing one does, and what is wired now.
/// </remarks>
public sealed class ControlExchangeTests
{
    /// <summary>The machine the sound device links in here are about.</summary>
    private const string Machine = "machine.oddskilla";

    /// <summary>A knob on that machine, learned on a desk.</summary>
    private static ControlMapping OnDevice(string key, int cc, string desk = "nanoKONTROL2") => new()
    {
        Kind = ControlKind.SoundDevice,
        Machine = Machine,
        Key = key,
        Owner = "OddSkilla",
        Name = "OddSkilla " + key,
        Device = desk,
        Channel = 1,
        Cc = cc
    };

    /// <summary>A fader on a mixer strip, which names a strip and never a machine.</summary>
    private static ControlMapping OnStrip(int track, int cc, string desk = "nanoKONTROL2") => new()
    {
        Kind = ControlKind.Mix,
        Mix = MixControl.Volume,
        Scope = ControlScope.Fixed,
        Track = track,
        Owner = "Mixer",
        Name = "track " + (track + 1) + " level",
        Device = desk,
        Channel = 1,
        Cc = cc
    };

    /// <summary>A desk holding those links, with the templates block filled from them.</summary>
    /// <param name="links">What is on the desk to begin with.</param>
    private static ControlLink Desk(List<ControlMapping> links)
    {
        var made = new ControlLink(links, () => { });

        var block = new ControlTemplateBlock();

        new ControlTemplatesFromLinks(block, () => links, new ControllerProfiles()).Fill(said: false);

        made.Templates = block;

        return made;
    }

    /// <summary>A hook over that desk.</summary>
    /// <param name="link">The desk.</param>
    /// <param name="kind">Which sort of thing it is about.</param>
    /// <param name="id">Which one, or nothing for a whole kind.</param>
    private static IControlExchange Hook(ControlLink link, string kind = LinkTargets.SoundDevice, string id = Machine) =>
        new ControlExchange(() => id, kind, () => link);

    /// <summary>A hook offers the templates for the thing it is about and no others.</summary>
    [Fact]
    public void It_offers_only_what_is_pointed_at_this()
    {
        var links = new List<ControlMapping> { OnDevice("attack", 14), OnStrip(0, 30) };

        var template = Assert.Single(Hook(Desk(links)).Offered());

        Assert.Equal(LinkTargets.SoundDevice, template.Target.Kind);
        Assert.Equal(Machine, template.Target.Id);
    }

    /// <summary>And the mixer's hook, which names no particular strip, takes the mixer's.</summary>
    /// <remarks>
    /// The whole desk is one thing to point a controller at, so a mixer template is every strip
    /// at once rather than one card per fader.
    /// </remarks>
    [Fact]
    public void The_mixer_names_nothing_and_takes_every_strip()
    {
        var links = new List<ControlMapping> { OnDevice("attack", 14), OnStrip(0, 30), OnStrip(1, 31) };

        var template = Assert.Single(Hook(Desk(links), LinkTargets.Mixer, "").Offered());

        Assert.Equal(2, template.Controls.Count);
    }

    /// <summary>A face with nothing open offers nothing, rather than everything of its kind.</summary>
    [Fact]
    public void A_face_showing_nothing_offers_nothing()
    {
        var links = new List<ControlMapping> { OnDevice("attack", 14) };

        Assert.Empty(Hook(Desk(links), LinkTargets.SoundDevice, "").Offered());
    }

    /// <summary>
    /// **Exchanging one in lays its links on the desk, where they stay.**
    /// </summary>
    /// <remarks>
    /// Which is the whole of what applying a template is: nothing anywhere remembers that a
    /// template was involved, and what is left is links like any other.
    /// </remarks>
    [Fact]
    public void Taking_one_lays_its_links_down()
    {
        var links = new List<ControlMapping> { OnDevice("attack", 14), OnDevice("decay", 15) };

        var link = Desk(links);
        var hook = Hook(link);

        var template = Assert.Single(hook.Offered());

        links.Clear();

        var reading = hook.Take(template);

        Assert.Equal(2, reading.Links.Count);
        Assert.Equal(2, links.Count);
        Assert.Contains(links, one => one.Key == "attack");
        Assert.Contains(links, one => one.Key == "decay");
    }

    /// <summary>Taking the same one twice leaves what it did the first time.</summary>
    [Fact]
    public void Taking_it_twice_leaves_what_it_did()
    {
        var links = new List<ControlMapping> { OnDevice("attack", 14), OnDevice("decay", 15) };

        var hook = Hook(Desk(links));

        var template = Assert.Single(hook.Offered());

        hook.Take(template);
        hook.Take(template);

        Assert.Equal(2, links.Count);
    }

    /// <summary>Nothing at all is asked of a hook with no desk behind it.</summary>
    [Fact]
    public void With_no_desk_it_offers_nothing_and_takes_nothing()
    {
        var hook = new ControlExchange(() => Machine, LinkTargets.SoundDevice, () => null);

        Assert.Empty(hook.Offered());
        Assert.Empty(hook.Take(null).Links);
        Assert.False(hook.Wired(OnDevice("attack", 14)));
    }

    /// <summary>
    /// **What is wired is answered with the very thing a control offers.**
    /// </summary>
    /// <remarks>
    /// The marking half, and the one that did not exist outside a drawn face. A page hands over
    /// the mapping it already hung on the control, so a fader, a pad and a knob on a face are one
    /// question rather than three rules that would drift.
    /// </remarks>
    [Fact]
    public void It_says_which_controls_are_already_pointed_at()
    {
        var links = new List<ControlMapping> { OnStrip(0, 30) };

        var hook = Hook(Desk(links), LinkTargets.Mixer, "");

        hook.Take(hook.Offered()[0]);

        Assert.True(hook.Wired(MixLinks.On(MixControl.Volume, 0)));
        Assert.False(hook.Wired(MixLinks.On(MixControl.Volume, 1)));
        Assert.False(hook.Wired(MixLinks.On(MixControl.Pan, 0)));
    }

    /// <summary>
    /// And it answers by the target rather than by which controller is on it.
    /// </summary>
    /// <remarks>
    /// The mark says this fader has something pointed at it, not which desk: two controllers on
    /// one fader is two links and one mark, which is right, since what the mark warns about is
    /// that pointing here takes something off.
    /// </remarks>
    [Fact]
    public void Two_controllers_on_one_fader_is_one_mark()
    {
        var links = new List<ControlMapping> { OnStrip(0, 30), OnStrip(0, 5, "MPD218 Port A") };

        var hook = Hook(Desk(links), LinkTargets.Mixer, "");

        Assert.Equal(2, hook.Offered().Count);

        hook.Take(hook.Offered()[0]);

        Assert.True(hook.Wired(MixLinks.On(MixControl.Volume, 0)));
    }

    /// <summary>A control offering nothing is not wired, whatever is on the desk.</summary>
    [Fact]
    public void A_control_offering_nothing_is_not_wired()
    {
        Assert.False(Hook(Desk(new List<ControlMapping> { OnStrip(0, 30) })).Wired(null));
    }

    /// <summary>
    /// **Exchanging one in is what makes a control read as wired**, which is the pair.
    /// </summary>
    /// <remarks>
    /// The two halves said as one test, because separately each can pass while the thing somebody
    /// actually does still shows nothing: a template applied on the mixer marked no fader at all,
    /// and every test about applying and every test about marking was green throughout.
    /// </remarks>
    [Fact]
    public void A_fader_is_bare_until_a_template_is_exchanged_in()
    {
        var made = new List<ControlMapping> { OnStrip(0, 30), OnStrip(1, 31) };

        var template = Assert.Single(Hook(Desk(made), LinkTargets.Mixer, "").Offered());

        var empty = new List<ControlMapping>();
        var hook = Hook(Desk(empty), LinkTargets.Mixer, "");

        Assert.False(hook.Wired(MixLinks.On(MixControl.Volume, 0)));

        hook.Take(template);

        Assert.True(hook.Wired(MixLinks.On(MixControl.Volume, 0)));
        Assert.True(hook.Wired(MixLinks.On(MixControl.Volume, 1)));
    }

    /// <summary>
    /// **Nothing is live until somebody applies it**, however much is on the disc.
    /// </summary>
    /// <remarks>
    /// The links on the disc are the library: every template ever made, for hardware that is not
    /// on the desk this afternoon and for machines this song does not play. Live on start, the
    /// first knob touched would do whatever it was last pointed at months ago.
    /// </remarks>
    [Fact]
    public void Nothing_is_live_until_it_is_applied()
    {
        var links = new List<ControlMapping> { OnStrip(0, 30), OnStrip(1, 31) };

        var hook = Hook(Desk(links), LinkTargets.Mixer, "");

        Assert.False(hook.Wired(MixLinks.On(MixControl.Volume, 0)));
        Assert.False(hook.Wired(MixLinks.On(MixControl.Volume, 1)));
    }

    /// <summary>
    /// **Applying one controller's template puts the other controller's to sleep.**
    /// </summary>
    /// <remarks>
    /// The case the whole arrangement exists for: two boxes on the desk, both with a template for
    /// the mixer, both plugged in. Which of them is driving it is chosen by applying that one,
    /// and choosing means nothing if the one chosen before simply joins in. Nothing is lost,
    /// since the sleeping one is still on the desk and still in its own template, one press away.
    /// </remarks>
    [Fact]
    public void Applying_one_controller_puts_the_other_to_sleep()
    {
        var links = new List<ControlMapping>
        {
            OnStrip(0, 30),
            OnStrip(0, 5, "MPD218 Port A")
        };

        var link = Desk(links);
        var hook = Hook(link, LinkTargets.Mixer, "");

        var first = hook.Offered()[0];
        var second = hook.Offered()[1];

        hook.Take(first);

        Assert.Single(link.Live);

        hook.Take(second);

        var only = Assert.Single(link.Live);

        Assert.Equal(second.Controller, only.Device);
    }

    /// <summary>And a template on something else is left running, since it is not the same thing.</summary>
    /// <remarks>
    /// One controller against one target is one template, so what a second desk takes over is the
    /// mixer and never whatever the first is doing to a machine.
    /// </remarks>
    [Fact]
    public void Applying_a_mixer_template_leaves_a_machine_alone()
    {
        var links = new List<ControlMapping> { OnDevice("attack", 14), OnStrip(0, 30) };

        var link = Desk(links);

        var device = Hook(link);
        var mixer = Hook(link, LinkTargets.Mixer, "");

        device.Take(device.Offered()[0]);
        mixer.Take(mixer.Offered()[0]);

        Assert.Equal(2, link.Live.Count);
        Assert.True(device.Wired(OnDevice("attack", 14)));
        Assert.True(mixer.Wired(MixLinks.On(MixControl.Volume, 0)));
    }
}
