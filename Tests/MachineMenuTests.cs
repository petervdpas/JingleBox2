using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Machines;
using JingleBox2.Machines.Interfaces;
using JingleBox2.Machines.Records;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The Links part on a machine's face: the desks pointed at it, and the way to point another.
/// </summary>
/// <remarks>
/// The part is drawn by the machine library and filled by the application, and this is the
/// filling. It is answerable without a window because what a machine offers is a list of lines
/// rather than a menu, which is the whole reason <see cref="MachineMenuItem"/> is a shape and
/// not a toolkit type.
///
/// What is worth checking is that it lists exactly the templates the MIDI CC page lists, cut the
/// same way: one controller against this machine. A machine with nothing pointed at it lists
/// nothing, and the desks pointed at other machines are somebody else's business.
/// </remarks>
public class MachineMenuTests
{
    /// <summary>The machine every test here is about.</summary>
    private const string Id = "machine.linkstest";

    /// <summary>What that machine is called on the front of it.</summary>
    private const string Named = "LinksTest";

    /// <summary>What the line that turns the mode over says while it is off.</summary>
    private const string Learn = "Learn a control";

    /// <summary>And while it is on.</summary>
    private const string Stop = "Stop learning";

    /// <summary>A knob on that machine, as the panel writes one down.</summary>
    /// <param name="key">Which parameter.</param>
    /// <param name="cc">Which controller number.</param>
    /// <param name="device">Which desk it was learned on.</param>
    private static ControlMapping OnMachine(string key, int cc, string device = "Desk One") => new()
    {
        Kind = ControlKind.Instrument,
        Machine = Id,
        Key = key,
        Owner = Named,
        Name = Named + " " + key,
        Device = device,
        Channel = 1,
        Cc = cc
    };

    /// <summary>The part over a desk holding those links, with that machine on the panel.</summary>
    /// <param name="desk">The links, kept so a test can count them.</param>
    /// <param name="link">The desk itself, for the tests about the mode.</param>
    /// <param name="said">The last line the part asked to have said.</param>
    /// <param name="links">What is on the desk to begin with.</param>
    private static IMachineMenu Part(
        out List<ControlMapping> desk,
        out ControlLink link,
        out Func<string> said,
        params ControlMapping[] links)
    {
        var kept = new List<ControlMapping>(links);
        var made = new ControlLink(kept, () => { });
        string last = "";

        desk = kept;
        link = made;
        said = () => last;

        return new MachineLinks(() => Id, () => Named, () => made) { Told = line => last = line };
    }

    /// <summary>
    /// A machine nobody has pointed anything at offers the one thing there is to do.
    /// </summary>
    /// <remarks>
    /// Which is the case every machine starts in, and the case most machines on a rack are in.
    /// The answer is not a line saying there is nothing: it is the line that does the thing you
    /// would have to do next anyway.
    /// </remarks>
    [Fact]
    public void A_machine_nothing_is_pointed_at_offers_only_learning()
    {
        var only = Assert.Single(Part(out _, out _, out _).Read());

        Assert.Equal(Learn, only.Said);
        Assert.True(only.Live);
        Assert.Equal(MachineMenuOptions.Learn, only.Option);
    }

    /// <summary>And a panel with no machine on it offers nothing to do at all.</summary>
    [Fact]
    public void A_panel_with_no_machine_offers_nothing_at_all()
    {
        var part = new MachineLinks(() => "", desk: () => new ControlLink(new List<ControlMapping>(), () => { }));

        Assert.Empty(part.Read());
    }

    /// <summary>And a machine whose id comes back as nothing at all, rather than as empty.</summary>
    [Fact]
    public void A_machine_named_nothing_offers_nothing_at_all()
    {
        Assert.Empty(new MachineLinks(() => null!, desk: () => new ControlLink(new List<ControlMapping>(), () => { })).Read());
    }

    /// <summary>
    /// And a panel shown with no desk behind it at all.
    /// </summary>
    /// <remarks>
    /// Which is what something that is not this application would meet. It is a question rather
    /// than the door itself precisely so this can be asked: a static cannot be stood in front of.
    /// </remarks>
    [Fact]
    public void With_no_desk_at_all_there_is_nothing_at_all()
    {
        Assert.Empty(new MachineLinks(() => Id, () => Named, () => null).Read());
    }

    /// <summary>
    /// The list is the desks pointed at this machine, one line each, and the learn line last.
    /// </summary>
    /// <remarks>
    /// One controller against one thing it is pointed at is what a template is, so this is the
    /// same cut the MIDI CC page's cards are made by. Ten knobs from one desk are one line and
    /// not ten.
    /// </remarks>
    [Fact]
    public void The_list_is_the_desks_pointed_at_this_machine()
    {
        var offers = Part(
            out _,
            out _,
            out _,
            OnMachine("attack", 0),
            OnMachine("decay", 1),
            OnMachine("duty", 2, "Desk Two")).Read();

        Assert.Equal(3, offers.Count);
        Assert.StartsWith("Desk One", offers[0].Said, StringComparison.Ordinal);
        Assert.Contains("2 controls", offers[0].Said, StringComparison.Ordinal);
        Assert.StartsWith("Desk Two", offers[1].Said, StringComparison.Ordinal);
        Assert.Contains("1 control", offers[1].Said, StringComparison.Ordinal);
        Assert.Equal(Learn, offers[2].Said);
    }

    /// <summary>
    /// A desk pointed at another machine is that machine's business and not this one's.
    /// </summary>
    /// <remarks>
    /// The whole point of a link naming a machine rather than a track: one desk drives every
    /// machine you have pointed it at, and each of their faces answers for itself.
    /// </remarks>
    [Fact]
    public void A_desk_pointed_at_another_machine_is_not_listed_here()
    {
        var elsewhere = OnMachine("attack", 0, "Desk Two");

        elsewhere.Machine = "machine.elsewhere";

        var offers = Part(out _, out _, out _, OnMachine("attack", 0), elsewhere).Read();

        Assert.Equal(2, offers.Count);
        Assert.StartsWith("Desk One", offers[0].Said, StringComparison.Ordinal);
    }

    /// <summary>A machine's buttons are part of its template, so they count with its knobs.</summary>
    /// <remarks>
    /// An action is a thing on that machine's face, which is what the naming rule already says. A
    /// part that listed only the knobs would report a desk as having less on it than it has.
    /// </remarks>
    [Fact]
    public void A_button_pointed_at_the_machine_counts_with_its_knobs()
    {
        var press = OnMachine("next_preset", 4);

        press.Kind = ControlKind.Action;

        Assert.Contains("2 controls", Part(out _, out _, out _, OnMachine("attack", 0), press).Read()[0].Said,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Picking a desk lays its template down again, and takes back what has moved since.
    /// </summary>
    /// <remarks>
    /// Through the one door a batch of links goes through, so it keeps the rules a link made by
    /// hand keeps: one control does one job. A template already in force comes back exactly as it
    /// was, which is why pressing it twice is safe.
    /// </remarks>
    [Fact]
    public void Picking_a_desk_takes_back_what_has_moved_since()
    {
        var part = Part(out var desk, out var link, out var said, OnMachine("attack", 0));

        var template = part.Read()[0];

        link.Take(new[] { OnMachine("decay", 0) });

        Assert.Equal("decay", Assert.Single(desk).Key);

        template.Chosen!();

        Assert.Equal("attack", Assert.Single(desk).Key);
        Assert.Contains("Pointed Desk One at " + Named, said(), StringComparison.Ordinal);
    }

    /// <summary>And picking one that is already in force leaves it exactly as it was.</summary>
    [Fact]
    public void Picking_one_that_is_already_in_force_changes_nothing()
    {
        var part = Part(out var desk, out _, out _, OnMachine("attack", 0), OnMachine("decay", 1));

        part.Read()[0].Chosen!();

        Assert.Equal(2, desk.Count);
        Assert.Equal(new[] { "attack", "decay" }, desk.Select(one => one.Key).OrderBy(one => one));
    }

    /// <summary>A link naming no controller is still listed, rather than dropped for want of a name.</summary>
    /// <remarks>
    /// Every link made before controllers were recorded is one of these, and a desk that has
    /// simply gone missing from the list is worse than one with an awkward name on it.
    /// </remarks>
    [Fact]
    public void A_link_that_names_no_controller_is_still_listed()
    {
        var nameless = OnMachine("attack", 0);

        nameless.Device = "";

        var offers = Part(out _, out _, out _, nameless).Read();

        Assert.Equal(2, offers.Count);
        Assert.Contains("1 control", offers[0].Said, StringComparison.Ordinal);
        Assert.Equal(MachineMenuOptions.Surfaces, offers[0].Option);
    }

    /// <summary>An id that differs by case is a different machine, since an id is exact.</summary>
    /// <remarks>
    /// How exact is the naming rule's business and not this one's, which is why nothing
    /// here compares an id itself. This says so out loud, because getting it wrong would have one
    /// machine listing another's desks.
    /// </remarks>
    [Fact]
    public void An_id_that_differs_by_case_is_another_machine()
    {
        var shouting = OnMachine("attack", 0);

        shouting.Machine = Id.ToUpperInvariant();

        Assert.Equal(Learn, Assert.Single(Part(out _, out _, out _, shouting).Read()).Said);
    }

    /// <summary>A plugin on a track's chain is not one of this machine's templates.</summary>
    /// <remarks>
    /// A plugin cannot be pointed at at all, and an old link that names one is a different kind
    /// of target however its id is spelled.
    /// </remarks>
    [Fact]
    public void A_plugin_on_a_chain_is_not_one_of_these()
    {
        var insert = OnMachine("attack", 0);

        insert.Kind = ControlKind.Insert;
        insert.Plugin = Id;

        Assert.Equal(Learn, Assert.Single(Part(out _, out _, out _, insert).Read()).Said);
    }

    /// <summary>And neither is a mixer strip or a transport key.</summary>
    [Fact]
    public void A_mixer_strip_and_a_transport_key_are_not_one_of_these()
    {
        var strip = MixLinks.On(MixControl.Volume, 0);

        strip.Device = "Desk One";

        var play = TransportLinks.For(TransportKey.Play);

        play.Device = "Desk One";

        Assert.Equal(Learn, Assert.Single(Part(out _, out _, out _, strip, play).Read()).Said);
    }

    /// <summary>
    /// A line read before its links were taken off puts them back when it is pressed.
    /// </summary>
    /// <remarks>
    /// A menu is read when it opens and pressed a moment later, and anything can have happened in
    /// between. Putting them back is right: pressing that line is asking for that template, and
    /// the template is what was read.
    /// </remarks>
    [Fact]
    public void A_line_read_before_its_links_went_puts_them_back()
    {
        var part = Part(out var desk, out var link, out _, OnMachine("attack", 0));

        var template = part.Read()[0];

        foreach (var one in desk.ToList()) link.Unlink(one);

        Assert.Empty(desk);

        template.Chosen!();

        Assert.Equal("attack", Assert.Single(desk).Key);
    }

    /// <summary>Reading it again does not pile up, however often the menu is opened.</summary>
    [Fact]
    public void Reading_it_again_does_not_pile_up()
    {
        var part = Part(out _, out _, out _, OnMachine("attack", 0));

        Assert.Equal(part.Read().Count, part.Read().Count);
        Assert.Equal(2, part.Read().Count);
    }

    /// <summary>A machine with no name of its own is spoken of by its id rather than by nothing.</summary>
    [Fact]
    public void A_machine_with_no_name_is_spoken_of_by_its_id()
    {
        var part = new MachineLinks(
            () => Id,
            () => "",
            () => new ControlLink(new List<ControlMapping>(), () => { }));

        Assert.Contains(Id, Assert.Single(part.Read()).Tip, StringComparison.Ordinal);
    }

    /// <summary>
    /// The learn line turns over the same mode Ctrl+Shift+M turns over, and says which way.
    /// </summary>
    /// <remarks>
    /// The same switch and not a second way of doing it. Two spellings of one mode would
    /// eventually disagree, and the way that fails is a menu saying the mode is off while the
    /// keystroke has it on.
    /// </remarks>
    [Fact]
    public void The_learn_line_is_the_keystroke_and_says_which_way_it_turns()
    {
        var part = Part(out _, out var link, out _);

        Assert.False(link.IsLinking);

        Assert.Single(part.Read()).Chosen!();

        Assert.True(link.IsLinking);
        Assert.Equal(Stop, Assert.Single(part.Read()).Said);

        Assert.Single(part.Read()).Chosen!();

        Assert.False(link.IsLinking);
        Assert.Equal(Learn, Assert.Single(part.Read()).Said);
    }
}
