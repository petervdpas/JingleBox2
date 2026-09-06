using System;
using System.Linq;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Records;
using JingleBox2.UI;
using JingleBox2.UI.Enums;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The three rules the patchbay is made of: what may be joined, how the channels line up, where
/// everything sits, and what picture the machine's own sources make.
/// </summary>
/// <remarks>
/// The blocks are controls and the cables are painted, and neither is tested here: what is
/// tested is everything those two ask, because a control can only be looked at and a rule can be
/// put a question to. That split is the reason the geometry is not written inside the block in
/// the first place, since the surface has to land a cable exactly on a dot the block drew.
///
/// Most of what is below is the unhappy half. A patchbay is a picture of somebody else's machine
/// and every list in it can be empty, doubled, or hold a thing that is being used and is not
/// offered.
/// </remarks>
public class PatchbayTests
{
    private readonly IPatchWiring _wiring = new PatchWiring();
    private readonly IPatchGeometry _shape = new PatchGeometry();
    private readonly IPatchGraph _graph = new PatchGraph();

    /// <summary>One port on a block, for the wiring rules to be asked about.</summary>
    private static PatchPort Port(string node, PatchSide side, PatchChannels channels = PatchChannels.Stereo) =>
        new(node, side == PatchSide.In ? "input" : "out", side, channels);

    /// <summary>A cable runs from something's output to something else's input.</summary>
    [Fact]
    public void An_output_may_be_joined_to_an_input()
    {
        Assert.True(_wiring.Allowed(Port("firefox", PatchSide.Out), Port("us", PatchSide.In)));
    }

    /// <summary>And it does not matter which end the hand started from.</summary>
    /// <remarks>
    /// A cable is dragged in whichever direction is convenient, so the rule cannot depend on the
    /// order the two ends arrive in. Answering yes one way and no the other would be a drag that
    /// works or does not depending on which block you touched first.
    /// </remarks>
    [Fact]
    public void Dragging_the_other_way_is_the_same_cable()
    {
        Assert.True(_wiring.Allowed(Port("us", PatchSide.In), Port("firefox", PatchSide.Out)));
    }

    /// <summary>Two inputs cannot be joined, and neither can two outputs.</summary>
    [Fact]
    public void Two_of_a_kind_cannot_be_joined()
    {
        Assert.False(_wiring.Allowed(Port("us", PatchSide.In), Port("them", PatchSide.In)));
        Assert.False(_wiring.Allowed(Port("us", PatchSide.Out), Port("them", PatchSide.Out)));
    }

    /// <summary>A block cannot be joined to itself.</summary>
    /// <remarks>
    /// Feedback, and the one connection anybody can make by accident: our own block has an input
    /// and will one day have an output, and they are two dots a few inches apart.
    /// </remarks>
    [Fact]
    public void A_block_cannot_be_joined_to_itself()
    {
        Assert.False(_wiring.Allowed(Port("us", PatchSide.Out), Port("us", PatchSide.In)));
    }

    /// <summary>A port belonging to nothing is refused rather than joined to whatever matches.</summary>
    [Fact]
    public void A_port_with_no_block_is_refused()
    {
        Assert.False(_wiring.Allowed(Port("", PatchSide.Out), Port("us", PatchSide.In)));
        Assert.False(_wiring.Allowed(Port("them", PatchSide.Out), Port("", PatchSide.In)));
    }

    /// <summary>Stereo into stereo is the pair it looks like.</summary>
    [Fact]
    public void Stereo_meets_stereo_side_for_side()
    {
        var pairs = _wiring.Pairs(PatchChannels.Stereo, PatchChannels.Stereo);

        Assert.Equal(new[] { (0, 0), (1, 1) }, pairs);
    }

    /// <summary>A mono source feeds both sides of a stereo input.</summary>
    /// <remarks>
    /// The case that matters most, since the alternative is a take that is silent on one side and
    /// has to be fixed afterwards. A headset in its telephone profile is a mono source.
    /// </remarks>
    [Fact]
    public void Mono_feeds_both_sides()
    {
        var pairs = _wiring.Pairs(PatchChannels.Mono, PatchChannels.Stereo);

        Assert.Equal(new[] { (0, 0), (0, 1) }, pairs);
    }

    /// <summary>A stereo source into a mono input arrives on the one channel.</summary>
    [Fact]
    public void Stereo_into_mono_arrives_on_the_one_channel()
    {
        var pairs = _wiring.Pairs(PatchChannels.Stereo, PatchChannels.Mono);

        Assert.Equal(new[] { (0, 0), (1, 0) }, pairs);
    }

    /// <summary>Mono to mono is one wire.</summary>
    [Fact]
    public void Mono_to_mono_is_one_wire()
    {
        Assert.Equal(new[] { (0, 0) }, _wiring.Pairs(PatchChannels.Mono, PatchChannels.Mono));
    }

    /// <summary>No pairing is ever empty, whatever the two shapes are.</summary>
    /// <remarks>
    /// A cable drawn with no wires in it is a cable that says a connection was made and shows
    /// nothing, which is worse than refusing the connection.
    /// </remarks>
    [Fact]
    public void Every_pairing_carries_at_least_one_wire()
    {
        foreach (var from in new[] { PatchChannels.Mono, PatchChannels.Stereo })
            foreach (var to in new[] { PatchChannels.Mono, PatchChannels.Stereo })
                Assert.NotEmpty(_wiring.Pairs(from, to));
    }

    /// <summary>A block with no ports still stands tall enough to be taken hold of.</summary>
    [Fact]
    public void A_block_with_nothing_on_it_is_still_a_block()
    {
        Assert.True(_shape.BlockHeight(0) > _shape.HeaderHeight);
    }

    /// <summary>Each row of ports adds its own height and no more.</summary>
    [Fact]
    public void Rows_add_their_own_height()
    {
        Assert.Equal(_shape.RowHeight, _shape.BlockHeight(3) - _shape.BlockHeight(2), 3);
    }

    /// <summary>A row's middle is under the title bar and inside the block.</summary>
    [Fact]
    public void A_row_sits_under_the_title_bar()
    {
        double centre = _shape.RowCentre(0);

        Assert.True(centre > _shape.HeaderHeight);
        Assert.True(centre < _shape.BlockHeight(1));
    }

    /// <summary>A place on the title bar is on no row at all.</summary>
    /// <remarks>
    /// The bar is where a block is dragged from, so a press there that answered with a port
    /// would start a cable instead of moving the block.
    /// </remarks>
    [Fact]
    public void The_title_bar_is_on_no_row()
    {
        Assert.Equal(-1, _shape.RowAt(_shape.HeaderHeight / 2, 2));
    }

    /// <summary>A place under the last row is on no row either.</summary>
    [Fact]
    public void Below_the_last_row_is_on_no_row()
    {
        Assert.Equal(-1, _shape.RowAt(_shape.BlockHeight(2), 2));
    }

    /// <summary>A block with no ports answers no row wherever it is pressed.</summary>
    [Fact]
    public void A_block_with_no_ports_answers_no_row()
    {
        Assert.Equal(-1, _shape.RowAt(_shape.RowCentre(0), 0));
        Assert.Equal(-1, _shape.RowAt(-40, 0));
    }

    /// <summary>Each row answers itself when asked at its own middle.</summary>
    [Fact]
    public void Each_row_answers_itself()
    {
        for (int row = 0; row < 4; row++)
            Assert.Equal(row, _shape.RowAt(_shape.RowCentre(row), 4));
    }

    /// <summary>A mono port is one dot, in the middle of its row.</summary>
    [Fact]
    public void A_mono_port_is_one_dot_on_the_line()
    {
        var centres = _shape.ChannelCentres(50, 1);

        Assert.Equal(new[] { 50d }, centres);
    }

    /// <summary>A stereo port is two dots, evenly either side of the middle.</summary>
    [Fact]
    public void A_stereo_port_is_a_pair_about_the_line()
    {
        var centres = _shape.ChannelCentres(50, 2);

        Assert.Equal(2, centres.Count);
        Assert.Equal(50, (centres[0] + centres[1]) / 2, 3);
        Assert.True(centres[0] < centres[1]);
    }

    /// <summary>A port claiming no channels is still drawn, as one dot.</summary>
    /// <remarks>
    /// Nought and negative cannot arrive from the enum, and this is asked anyway: what is drawn
    /// comes from somebody else's machine, and a port with no dot at all is a connection nobody
    /// can make and nothing on the screen to say why.
    /// </remarks>
    [Fact]
    public void A_port_claiming_no_channels_still_has_a_dot()
    {
        Assert.Single(_shape.ChannelCentres(50, 0));
        Assert.Single(_shape.ChannelCentres(50, -3));
    }

    /// <summary>A cable leaves to the right and arrives from the left.</summary>
    /// <remarks>
    /// What makes a picture of wires readable when they cross. It holds even when the blocks are
    /// the wrong way round, which is what happens the moment somebody drags one across another.
    /// </remarks>
    [Fact]
    public void A_cable_leaves_rightwards_and_arrives_leftwards()
    {
        var (x1, _, x2, _) = _shape.Curve(0, 0, 300, 40);

        Assert.True(x1 > 0);
        Assert.True(x2 < 300);
    }

    /// <summary>Even with the blocks reversed, and even with no gap at all.</summary>
    [Fact]
    public void The_bend_survives_blocks_the_wrong_way_round()
    {
        var (x1, _, x2, _) = _shape.Curve(300, 10, 0, 10);

        Assert.True(x1 > 300);
        Assert.True(x2 < 0);

        var (a1, _, a2, _) = _shape.Curve(100, 10, 100, 10);

        Assert.True(a1 > 100);
        Assert.True(a2 < 100);
    }

    /// <summary>The cable's ends stay at the height they were given.</summary>
    [Fact]
    public void A_cable_leaves_and_arrives_at_its_own_height()
    {
        var (_, y1, _, y2) = _shape.Curve(0, 12, 200, 90);

        Assert.Equal(12, y1, 3);
        Assert.Equal(90, y2, 3);
    }

    /// <summary>A machine offering nothing still draws us.</summary>
    /// <remarks>
    /// The picture has to say "here is this program and nothing is feeding it" rather than being
    /// blank, which reads as a page that has not loaded.
    /// </remarks>
    [Fact]
    public void Nothing_on_offer_still_draws_our_own_block()
    {
        var scene = _graph.Read(Array.Empty<AudioRoute>(), null);

        Assert.Single(scene.Nodes);
        Assert.Equal(_graph.OwnNode, scene.Nodes[0].Id);
        Assert.True(scene.Nodes[0].IsOurs);
        Assert.Empty(scene.Links);
    }

    /// <summary>Every source on offer gets a block, and ours is the only one that is ours.</summary>
    [Fact]
    public void Every_source_gets_a_block()
    {
        var scene = _graph.Read(new[]
        {
            new AudioRoute("firefox", "Firefox", AudioRouteKind.Application),
            new AudioRoute("mic", "Built-in", AudioRouteKind.Input)
        }, null);

        Assert.Equal(3, scene.Nodes.Count);
        Assert.Single(scene.Nodes, n => n.IsOurs);
        Assert.Empty(scene.Links);
    }

    /// <summary>What is feeding the input is drawn as a cable into our own block.</summary>
    [Fact]
    public void What_is_feeding_us_is_a_cable()
    {
        var firefox = new AudioRoute("firefox", "Firefox", AudioRouteKind.Application);

        var scene = _graph.Read(new[] { firefox }, firefox);

        var link = Assert.Single(scene.Links);

        Assert.Equal("firefox", link.From.Node);
        Assert.Equal(PatchSide.Out, link.From.Side);
        Assert.Equal(_graph.OwnInput, link.To);
    }

    /// <summary>A source that is feeding us and is not on offer is drawn all the same.</summary>
    /// <remarks>
    /// This really happens: the sound server can wire something into the capture that the picker
    /// would not list. A picture that left it out would say the input is unconnected while it is
    /// recording that very thing.
    /// </remarks>
    [Fact]
    public void A_source_that_is_not_offered_is_still_drawn()
    {
        var hidden = new AudioRoute("mystery", "Mystery", AudioRouteKind.Application);

        var scene = _graph.Read(Array.Empty<AudioRoute>(), hidden);

        Assert.Equal(2, scene.Nodes.Count);
        Assert.Single(scene.Links);
        Assert.Equal("mystery", scene.Nodes[0].Id);
    }

    /// <summary>And it is drawn once when it is also on offer.</summary>
    [Fact]
    public void The_chosen_source_is_not_drawn_twice()
    {
        var firefox = new AudioRoute("firefox", "Firefox", AudioRouteKind.Application);

        var scene = _graph.Read(new[] { firefox, firefox }, firefox);

        Assert.Equal(2, scene.Nodes.Count);
        Assert.Single(scene.Links);
    }

    /// <summary>The same node twice under two names is one block.</summary>
    [Fact]
    public void One_block_per_node_however_often_it_is_listed()
    {
        var scene = _graph.Read(new[]
        {
            new AudioRoute("firefox", "Firefox", AudioRouteKind.Application),
            new AudioRoute("firefox", "Firefox again", AudioRouteKind.Monitor)
        }, null);

        Assert.Equal(2, scene.Nodes.Count);
    }

    /// <summary>A source with no address at all is left out rather than drawn as a dead block.</summary>
    [Fact]
    public void A_source_with_no_address_is_left_out()
    {
        var scene = _graph.Read(new[] { new AudioRoute("", "Nameless", AudioRouteKind.Input) }, null);

        Assert.Single(scene.Nodes);
        Assert.True(scene.Nodes[0].IsOurs);
    }

    /// <summary>And so is anything claiming to be us, which would draw two of our block.</summary>
    [Fact]
    public void Nothing_may_pretend_to_be_us()
    {
        var scene = _graph.Read(
            new[] { new AudioRoute(_graph.OwnNode, "Not us", AudioRouteKind.Application) }, null);

        Assert.Single(scene.Nodes);
        Assert.True(scene.Nodes[0].IsOurs);
    }

    /// <summary>Every cable names ports that are on blocks that are really there.</summary>
    /// <remarks>
    /// The one rule that holds the two halves together: a cable to a block nobody drew is a
    /// cable to nowhere, and the picture would simply not draw it, which reads as a connection
    /// that failed.
    /// </remarks>
    [Fact]
    public void Every_cable_lands_on_a_block_that_is_drawn()
    {
        var firefox = new AudioRoute("firefox", "Firefox", AudioRouteKind.Application);

        var scene = _graph.Read(new[] { firefox }, firefox);

        foreach (var link in scene.Links)
        {
            Assert.Contains(scene.Nodes, n => n.Id == link.From.Node);
            Assert.Contains(scene.Nodes, n => n.Id == link.To.Node);
        }
    }

    /// <summary>Blocks are given places that do not sit on top of each other.</summary>
    [Fact]
    public void Blocks_start_somewhere_of_their_own()
    {
        var scene = _graph.Read(new[]
        {
            new AudioRoute("a", "A", AudioRouteKind.Input),
            new AudioRoute("b", "B", AudioRouteKind.Input)
        }, null);

        var places = scene.Nodes.Select(n => (n.X, n.Y)).ToList();

        Assert.Equal(places.Count, places.Distinct().Count());
    }

    /// <summary>A source's port is on the source, and ours is on ours.</summary>
    /// <remarks>
    /// A port names its block, and the whole hit test rests on that: a port carrying the wrong
    /// block name would draw its cable onto somebody else's dot.
    /// </remarks>
    [Fact]
    public void A_port_names_the_block_it_is_on()
    {
        var scene = _graph.Read(new[] { new AudioRoute("mic", "Built-in", AudioRouteKind.Input) }, null);

        foreach (var node in scene.Nodes)
        {
            foreach (var port in node.Ins) Assert.Equal(node.Id, port.Node);
            foreach (var port in node.Outs) Assert.Equal(node.Id, port.Node);
        }
    }

    /// <summary>Our block takes audio in and a source gives it out, never the other way about.</summary>
    [Fact]
    public void Sources_give_out_and_we_take_in()
    {
        var scene = _graph.Read(new[] { new AudioRoute("mic", "Built-in", AudioRouteKind.Input) }, null);

        var us = scene.Nodes.Single(n => n.IsOurs);
        var source = scene.Nodes.Single(n => !n.IsOurs);

        Assert.Single(us.Ins);
        Assert.Empty(us.Outs);
        Assert.Single(source.Outs);
        Assert.Empty(source.Ins);
    }

    /// <summary>A cable made by the rules may be drawn by the rules.</summary>
    /// <remarks>
    /// The two rules meeting, which is the seam where a picture and a wiring drift apart: what
    /// the graph reads has to be something the wiring would have allowed a hand to make.
    /// </remarks>
    [Fact]
    public void What_is_read_is_what_a_hand_could_have_made()
    {
        var firefox = new AudioRoute("firefox", "Firefox", AudioRouteKind.Application);

        var scene = _graph.Read(new[] { firefox }, firefox);

        foreach (var link in scene.Links)
            Assert.True(_wiring.Allowed(link.From, link.To));
    }
}
