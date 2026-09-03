using System.Collections.Generic;
using System.Linq;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A pad box pointed at the pads, which is the same gesture as everything else on the desk.
/// </summary>
/// <remarks>
/// The pads had a mapping table of their own, with its own storage, its own Learn button and its
/// own matching rules, which is a second way of doing the one thing the link layer is for. What
/// is worth pinning is the half that is genuinely new: a link can name a note now, and only the
/// press half of one, since every link before this was a knob or a button sending a controller.
///
/// The unhappy answers matter more than the happy one here. A release that fired a pad would
/// double every hit, a controller answering a note link would fire pads off a fader, and a link
/// naming a pad the matrix no longer has must do nothing rather than throw.
/// </remarks>
public class PadLinkTests
{
    /// <summary>A pad box hitting a note, or letting one go.</summary>
    private static MidiMessage Hit(int note, bool down = true, int channel = 10, int velocity = 100,
                                   string device = "MPD218 Port A") => new()
    {
        Device = device,
        Type = MidiMessageType.Note,
        Channel = channel,
        Value = note,
        Data = down ? velocity : 0,
        IsOn = down
    };

    /// <summary>The same button, sending a controller instead, which some pad boxes do.</summary>
    private static MidiMessage Sent(int cc, int value = 127, int channel = 10,
                                    string device = "MPD218 Port A") => new()
    {
        Device = device,
        Type = MidiMessageType.ControlChange,
        Channel = channel,
        Value = cc,
        Data = value,
        IsOn = value > 0
    };

    /// <summary>A link from that note to a pad, as the pointing gesture would leave it.</summary>
    private static ControlMapping Pointed(int pad, int note, int channel = 10,
                                          string device = "MPD218 Port A")
    {
        var link = PadLinks.On(pad);

        link.Device = device;
        link.Channel = channel;
        link.Cc = note;
        link.Sends = MidiMessageType.Note;

        return link;
    }

    /// <summary>A pad is pinned, since pad three is pad three from every page.</summary>
    [Fact]
    public void A_pad_link_names_the_pad_and_follows_nothing()
    {
        var link = PadLinks.On(2);

        Assert.Equal(ControlKind.Pad, link.Kind);
        Assert.Equal(ControlScope.Fixed, link.Scope);
        Assert.Equal(2, link.Pad);
        Assert.Equal("pad 3", link.Name);
    }

    /// <summary>The press is what fires it.</summary>
    [Fact]
    public void A_note_press_answers()
    {
        Assert.True(Pointed(0, 44).Answers(Hit(44)));
    }

    /// <summary>And the release is not a second press.</summary>
    [Fact]
    public void The_release_does_not()
    {
        Assert.False(Pointed(0, 44).Answers(Hit(44, down: false)));
    }

    /// <summary>A note link and a controller of the same number are different controls.</summary>
    [Fact]
    public void A_controller_of_the_same_number_is_not_that_note()
    {
        Assert.False(Pointed(0, 44).Answers(Sent(44)));
    }

    /// <summary>And the other way round, for a pad box that sends controllers.</summary>
    [Fact]
    public void A_pad_can_be_pointed_at_a_controller_instead()
    {
        var link = PadLinks.On(0);

        link.Channel = 10;
        link.Cc = 44;

        Assert.True(link.Answers(Sent(44)));
        Assert.False(link.Answers(Hit(44)));
    }

    /// <summary>Which matters when both are learned, since one button must not be two links.</summary>
    [Fact]
    public void A_note_and_a_controller_are_not_the_same_control()
    {
        var note = Pointed(0, 44);
        var controller = PadLinks.On(1);

        controller.Device = note.Device;
        controller.Channel = note.Channel;
        controller.Cc = note.Cc;

        Assert.False(note.SameControl(controller));
    }

    /// <summary>Two buttons pointed at one pad is the second replacing the first.</summary>
    [Fact]
    public void Two_links_on_one_pad_are_the_same_target()
    {
        Assert.True(Pointed(3, 44).SameTarget(Pointed(3, 51)));
        Assert.False(Pointed(3, 44).SameTarget(Pointed(4, 44)));
    }

    /// <summary>A pad hit gently is still a pad hit.</summary>
    /// <remarks>
    /// The press test the other two press kinds use reads a value under 64 as a button coming up,
    /// which is right for a button reporting its own state and wrong for a velocity: it would
    /// mean a pad played softly did nothing at all.
    /// </remarks>
    [Fact]
    public void A_quiet_hit_still_fires()
    {
        var pads = new CountingPads();
        var router = Router(pads, Pointed(0, 44));

        router.Pads(Hit(44, velocity: 1));

        Assert.Single(pads.Fired);
    }

    /// <summary>And a release fires nothing, so one hit is one fire.</summary>
    [Fact]
    public void A_hit_and_its_release_fire_once()
    {
        var pads = new CountingPads();
        var router = Router(pads, Pointed(0, 44));

        router.Pads(Hit(44));
        router.Pads(Hit(44, down: false));

        Assert.Equal(new[] { 0 }, pads.Fired);
    }

    /// <summary>The knobs' door does not fire pads, since a port is given the two jobs apart.</summary>
    [Fact]
    public void The_other_door_leaves_pad_links_alone()
    {
        var pads = new CountingPads();

        var link = PadLinks.On(0);
        link.Channel = 10;
        link.Cc = 44;

        Router(pads, link).Handle(Sent(44));

        Assert.Empty(pads.Fired);
    }

    /// <summary>A pad the matrix has not got does nothing, and says nothing about it.</summary>
    [Fact]
    public void A_link_past_the_end_of_the_matrix_fires_nothing()
    {
        var pads = new CountingPads(count: 4);
        var router = Router(pads, Pointed(11, 55));

        router.Pads(Hit(55));

        Assert.Empty(pads.Fired);
    }

    /// <summary>A button pointed at a pad on another controller is not this one's.</summary>
    [Fact]
    public void Another_controller_does_not_answer()
    {
        var pads = new CountingPads();
        var router = Router(pads, Pointed(0, 44, device: "MPD218 Port A"));

        router.Pads(Hit(44, device: "nanoKONTROL2 _ CTRL"));

        Assert.Empty(pads.Fired);
    }

    /// <summary>Pointing at a pad learns the note, and only from the press.</summary>
    [Fact]
    public void The_gesture_learns_a_note()
    {
        var held = new List<ControlMapping>();
        var link = new ControlLink(held, () => { });

        link.IsLinking = true;
        link.Offer(PadLinks.On(5));

        Assert.Null(link.Handle(Hit(51, down: false)));

        var made = link.Handle(Hit(51));

        Assert.NotNull(made);
        Assert.Equal(MidiMessageType.Note, made!.Sends);
        Assert.Equal(51, made.Cc);
        Assert.Equal(10, made.Channel);
        Assert.Equal(5, made.Pad);
        Assert.Single(held);
    }

    /// <summary>Every pad is one card, the way the mixer is one card for every strip.</summary>
    [Fact]
    public void All_the_pads_are_one_template()
    {
        var targets = new LinkTargets();

        Assert.Equal(targets.KeyOf(PadLinks.On(0)), targets.KeyOf(PadLinks.On(9)));
        Assert.Equal("Pads", targets.TitleOf(new[] { PadLinks.On(0), PadLinks.On(1) }));
    }

    /// <summary>
    /// The pads are one thing to point a controller at, which the corner menu asks as well.
    /// </summary>
    /// <remarks>
    /// It is asked in two places that have to agree: how the cards and the files are cut, and
    /// whether the menu in FIRE's corner has anything to be about. The menu had its own answer
    /// first, which knew only about the mixer, and the whole of what that looked like was a
    /// hamburger that opened an empty flyout.
    /// </remarks>
    [Fact]
    public void The_pads_name_nothing_in_particular()
    {
        var targets = new LinkTargets();

        Assert.True(targets.Whole(LinkTargets.Pads));
        Assert.True(targets.Whole(LinkTargets.Mixer));
        Assert.False(targets.Whole(LinkTargets.SoundDevice));
    }

    /// <summary>So the menu on FIRE offers learning even with nothing pointed at a pad yet.</summary>
    [Fact]
    public void The_corner_menu_always_offers_learning()
    {
        var held = new List<ControlMapping>();
        var desk = new ControlLink(held, () => { });

        var menu = new ControlMenu(
            () => "", () => "the pads", desk: () => desk, kind: LinkTargets.Pads);

        Assert.NotEmpty(menu.Read());
    }

    /// <summary>A template writes the pad counting from one and reads it back the same way.</summary>
    [Fact]
    public void A_pad_survives_being_written_down()
    {
        var targets = new LinkTargets();

        Assert.Equal("4", targets.ParameterOf(PadLinks.On(3)));

        var read = targets.Point(LinkTargets.Pads, "", "4");

        Assert.NotNull(read);
        Assert.Equal(ControlKind.Pad, read!.Kind);
        Assert.Equal(3, read.Pad);
    }

    /// <summary>A line naming no pad at all is left out rather than read as pad nought.</summary>
    [Fact]
    public void A_template_line_with_no_pad_is_refused()
    {
        var targets = new LinkTargets();

        Assert.Null(targets.Point(LinkTargets.Pads, "", ""));
        Assert.Null(targets.Point(LinkTargets.Pads, "", "0"));
        Assert.Null(targets.Point(LinkTargets.Pads, "", "the third one"));
    }

    /// <summary>A pad template travels, and the note travels with it.</summary>
    /// <remarks>
    /// The whole reason the pads joined this layer rather than keeping a table of their own: the
    /// numbers that say which pad an MPD218's pads fire are the most device-specific thing in
    /// the application and could be handed to nobody. A file that lost the word note would arrive
    /// as sixteen links pointed at controllers nothing sends.
    /// </remarks>
    [Fact]
    public void A_pad_template_carries_the_note()
    {
        var templates = new ControlTemplates(new LinkTargets());

        var written = templates.Describe("MPD218", new[] { Pointed(0, 44), Pointed(1, 45) });

        Assert.NotNull(written);
        Assert.Equal("note", written!.Controls[0].Sends);

        var back = templates.Take(written).Links;

        Assert.Equal(2, back.Count);
        Assert.All(back, one => Assert.Equal(MidiMessageType.Note, one.Sends));
        Assert.Equal(new[] { 0, 1 }, back.Select(one => one.Pad));
        Assert.Equal(new[] { 44, 45 }, back.Select(one => one.Cc));
    }

    /// <summary>A router over those pads and those links, with none of the application behind it.</summary>
    private static MidiControlRouter Router(IPadTrigger pads, params ControlMapping[] links)
    {
        var list = links.ToList();

        return new MidiControlRouter(() => list, new PadDesk(pads));
    }
}

/// <summary>The pads, counting what was fired at them.</summary>
/// <remarks>
/// A pad past the end is silently nothing, which is what the real adapter does and why it is
/// worth having here: a link outliving the matrix being cut down is an ordinary state.
/// </remarks>
internal sealed class CountingPads : IPadTrigger
{
    /// <summary>How many pads there are, past which nothing is fired.</summary>
    private readonly int _count;

    /// <summary>Makes a bank of that many pads.</summary>
    public CountingPads(int count = 16) => _count = count;

    /// <summary>Which pads were fired, in order.</summary>
    public List<int> Fired { get; } = new();

    /// <inheritdoc/>
    public void TriggerPad(int padIndex, PadTriggerAction action)
    {
        if (padIndex < 0 || padIndex >= _count) return;

        Fired.Add(padIndex);
    }
}

/// <summary>Everything a pad link needs resolving, and nothing else.</summary>
internal sealed class PadDesk : IControlTargets
{
    /// <summary>Where a pad link comes out.</summary>
    private readonly IPadTrigger _pads;

    /// <summary>Takes the pads every pad link will be resolved against.</summary>
    public PadDesk(IPadTrigger pads) => _pads = pads;

    /// <inheritdoc/>
    /// <remarks>Only pads: anything else in a test using this is a link that should not fire.</remarks>
    public IControlTarget? Find(ControlMapping mapping) =>
        mapping.Kind == ControlKind.Pad ? new FiredPad(_pads, mapping.Pad) : null;
}

/// <summary>One pad, as something a control can be pointed at.</summary>
internal sealed class FiredPad : IControlTarget
{
    /// <summary>Where the press comes out.</summary>
    private readonly IPadTrigger _pads;

    /// <summary>Which pad this is.</summary>
    private readonly int _pad;

    /// <summary>Takes the pads and which of them this stands for.</summary>
    public FiredPad(IPadTrigger pads, int pad)
    {
        _pads = pads;
        _pad = pad;
    }

    /// <inheritdoc/>
    public string Name => "pad " + (_pad + 1);

    /// <inheritdoc/>
    public double Min => 0;

    /// <inheritdoc/>
    public double Max => 1;

    /// <inheritdoc/>
    public double Value => 0;

    /// <inheritdoc/>
    public void Set(double value) => _pads.TriggerPad(_pad, PadTriggerAction.Toggle);

    /// <inheritdoc/>
    public bool Switch => false;

    /// <inheritdoc/>
    public string Units => "";
}
