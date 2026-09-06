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

    /// <summary>A mono port is one line.</summary>
    [Fact]
    public void A_mono_port_takes_one_line()
    {
        var rows = _shape.Rows(new[] { Port("us", PatchSide.In, PatchChannels.Mono) });

        Assert.Equal(new[] { new PatchRow(0, 0) }, rows);
    }

    /// <summary>A stereo port is two lines, one per channel, in order.</summary>
    /// <remarks>
    /// The whole of what "either stereo or mono depending on the type" comes to on the screen.
    /// Two dots sharing a line read as one fat point, which says nothing about how many wires
    /// there are.
    /// </remarks>
    [Fact]
    public void A_stereo_port_takes_a_line_each()
    {
        var rows = _shape.Rows(new[] { Port("us", PatchSide.In) });

        Assert.Equal(new[] { new PatchRow(0, 0), new PatchRow(0, 1) }, rows);
    }

    /// <summary>Several ports run on after each other, keeping their order.</summary>
    [Fact]
    public void Ports_keep_their_order_down_the_side()
    {
        var rows = _shape.Rows(new[]
        {
            Port("us", PatchSide.In, PatchChannels.Mono),
            Port("us", PatchSide.In)
        });

        Assert.Equal(
            new[] { new PatchRow(0, 0), new PatchRow(1, 0), new PatchRow(1, 1) },
            rows);
    }

    /// <summary>A side with no ports draws no lines, and does not throw about it.</summary>
    [Fact]
    public void A_side_with_no_ports_draws_nothing()
    {
        Assert.Empty(_shape.Rows(Array.Empty<PatchPort>()));
        Assert.Empty(_shape.Rows(null!));
    }

    /// <summary>A port claiming no channels still gets a line, since a dot nobody can see is worse.</summary>
    [Fact]
    public void A_port_claiming_no_channels_still_has_a_line()
    {
        var rows = _shape.Rows(new[] { new PatchPort("us", "in", PatchSide.In, 0) });

        Assert.Single(rows);
    }

    /// <summary>A stereo port's two lines are named apart, the way the sound server names them.</summary>
    [Fact]
    public void The_two_sides_are_named_apart()
    {
        var port = Port("firefox", PatchSide.Out);

        Assert.Equal("out_FL", port.Label(0));
        Assert.Equal("out_FR", port.Label(1));
    }

    /// <summary>A mono port says its own name and nothing else.</summary>
    [Fact]
    public void A_mono_port_says_its_own_name()
    {
        Assert.Equal("out", Port("mic", PatchSide.Out, PatchChannels.Mono).Label(0));
    }

    /// <summary>A fixed point refuses every cable, whichever end it is.</summary>
    /// <remarks>
    /// How the picture can show the way this application is wired inside itself without offering
    /// to take it apart: the pads reach the desk because that is what a desk is.
    /// </remarks>
    [Fact]
    public void A_fixed_point_refuses_the_hand()
    {
        var fixedIn = new PatchPort("mixer", "pads", PatchSide.In, PatchChannels.Stereo, Fixed: true);
        var free = Port("fire", PatchSide.Out);

        Assert.False(_wiring.Allowed(free, fixedIn));
        Assert.False(_wiring.Allowed(fixedIn, free));
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

    /// <summary>A machine offering nothing still draws this application.</summary>
    /// <remarks>
    /// The picture has to say "here is this program, and nothing is feeding it" rather than being
    /// blank, which reads as a page that has not loaded. What it draws then is our own signal
    /// path, which is true whether or not anything is plugged in.
    /// </remarks>
    [Fact]
    public void Nothing_on_offer_still_draws_our_own_path()
    {
        var scene = _graph.Read(Array.Empty<AudioRoute>(), null);

        Assert.Contains(scene.Nodes, n => n.Id == _graph.OwnNode);
        Assert.Contains(scene.Nodes, n => n.IsOurs && n.Title == "MIXER");
        Assert.DoesNotContain(scene.Links, l => l.To == _graph.OwnInput);
    }

    /// <summary>The tracker gives out a pair for every track the song has.</summary>
    /// <remarks>
    /// Which is what makes the patchbay say anything about the music: a single pair called "mix"
    /// is the sum, and the sum is one wire whether the song has one track or thirty two.
    /// </remarks>
    [Fact]
    public void Every_track_is_its_own_pair()
    {
        var scene = _graph.Read(
            Array.Empty<AudioRoute>(), null, null, new[] { "TR-01", "TR-02", "TR-03" });

        var tracker = scene.Nodes.Single(n => n.Id == "tracker");

        Assert.Equal(new[] { "TR-01", "TR-02", "TR-03" }, tracker.Outs.Select(p => p.Name));
    }

    /// <summary>And the desk takes one in for each of them, under its own name.</summary>
    [Fact]
    public void The_desk_takes_one_in_for_every_track()
    {
        var scene = _graph.Read(Array.Empty<AudioRoute>(), null, null, new[] { "TR-01", "TR-02" });

        var mixer = scene.Nodes.Single(n => n.Id == "mixer");

        Assert.Contains(mixer.Ins, p => p.Name == "TR-01");
        Assert.Contains(mixer.Ins, p => p.Name == "TR-02");
    }

    /// <summary>Each track's cable runs to its own point on the desk and no other.</summary>
    [Fact]
    public void A_track_runs_to_its_own_point()
    {
        var scene = _graph.Read(Array.Empty<AudioRoute>(), null, null, new[] { "TR-01", "TR-02" });

        foreach (var link in scene.Links)
        {
            if (link.From.Node != "tracker") continue;

            Assert.Equal(link.From.Name, link.To.Name);
            Assert.Equal("mixer", link.To.Node);
        }
    }

    /// <summary>The pads and a take go under the tracks rather than among them.</summary>
    /// <remarks>
    /// So adding a track to a song does not move the point a cable was drawn to, which on a
    /// picture somebody has arranged is the difference between a new row and everything shifting
    /// down one.
    /// </remarks>
    [Fact]
    public void The_pads_and_the_takes_go_under_the_tracks()
    {
        var mixer = _graph
            .Read(Array.Empty<AudioRoute>(), null, null, new[] { "TR-01" })
            .Nodes.Single(n => n.Id == "mixer");

        Assert.Equal("TR-01", mixer.Ins[0].Name);
        Assert.Equal("pads", mixer.Ins[^2].Name);
        Assert.Equal("takes", mixer.Ins[^1].Name);
    }

    /// <summary>A song with no tracks yet draws the whole mix as one pair.</summary>
    /// <remarks>
    /// Which is the moment before a song has been opened. A block with nothing on it would be a
    /// block nobody can read, and a picture that lost its middle until a song arrived would look
    /// broken rather than empty.
    /// </remarks>
    [Fact]
    public void No_tracks_yet_still_draws_the_mix()
    {
        foreach (var tracks in new[] { null, Array.Empty<string>() })
        {
            var tracker = _graph
                .Read(Array.Empty<AudioRoute>(), null, null, tracks)
                .Nodes.Single(n => n.Id == "tracker");

            Assert.Single(tracker.Outs);
        }
    }

    /// <summary>A track's pair is stereo, since a track has a pan and an insert chain.</summary>
    /// <remarks>
    /// Not a guess: a track is summed into a bus of its own and that bus is interleaved two
    /// channels, because a plugin on a track's chain places what it hears in the stereo field.
    /// See <c>TrackMixer.VoicesThenInsert</c>.
    /// </remarks>
    [Fact]
    public void A_track_carries_two_channels()
    {
        var tracker = _graph
            .Read(Array.Empty<AudioRoute>(), null, null, new[] { "TR-01" })
            .Nodes.Single(n => n.Id == "tracker");

        Assert.Equal(PatchChannels.Stereo, tracker.Outs[0].Channels);
    }

    /// <summary>The pads and the tracker reach the desk, and the desk reaches the machine.</summary>
    /// <remarks>
    /// **This is the routing table rather than a drawing of one.** What these cables say is how
    /// audio moves through this application, and they are here so that there is one place saying
    /// it rather than the shape being spread through the engine and nowhere written down.
    /// </remarks>
    [Fact]
    public void Our_own_path_is_drawn_end_to_end()
    {
        var scene = _graph.Read(Array.Empty<AudioRoute>(), null);

        Assert.Contains(scene.Links, l => l.From.Node == "record" && l.To.Node == "mixer");
        Assert.Contains(scene.Links, l => l.From.Node == "tracker" && l.To.Node == "mixer");
        Assert.Contains(scene.Links, l => l.From.Node == "fire" && l.To.Node == "mixer");
        Assert.Contains(scene.Links, l => l.From.Node == "mixer" && l.To.Node == "output");
    }

    /// <summary>Every one of those is fixed, since none of them is anybody's to move.</summary>
    [Fact]
    public void Our_own_path_cannot_be_pulled_apart()
    {
        var scene = _graph.Read(Array.Empty<AudioRoute>(), null);

        foreach (var link in scene.Links)
        {
            if (link.To == _graph.OwnInput) continue;

            Assert.True(link.From.Fixed);
            Assert.True(link.To.Fixed);
            Assert.False(_wiring.Allowed(link.From, link.To));
        }
    }

    /// <summary>Where the mix leaves is named when the machine has said, and stands there anyway.</summary>
    [Fact]
    public void The_output_is_named_where_it_is_known()
    {
        Assert.Contains(
            _graph.Read(Array.Empty<AudioRoute>(), null, "Scarlett 2i2").Nodes,
            n => n.Title == "Scarlett 2i2");

        Assert.Contains(
            _graph.Read(Array.Empty<AudioRoute>(), null).Nodes,
            n => n.Id == "output");
    }

    /// <summary>Every source on offer gets a block of its own.</summary>
    [Fact]
    public void Every_source_gets_a_block()
    {
        var scene = _graph.Read(new[]
        {
            new AudioRoute("firefox", "Firefox", AudioRouteKind.Application),
            new AudioRoute("mic", "Built-in", AudioRouteKind.Input)
        }, null);

        Assert.Contains(scene.Nodes, n => n.Id == "firefox");
        Assert.Contains(scene.Nodes, n => n.Id == "mic");
        Assert.DoesNotContain(scene.Links, l => l.To == _graph.OwnInput);
    }

    /// <summary>What is feeding the input is drawn as a cable into our own block.</summary>
    [Fact]
    public void What_is_feeding_us_is_a_cable()
    {
        var firefox = new AudioRoute("firefox", "Firefox", AudioRouteKind.Application);

        var scene = _graph.Read(new[] { firefox }, firefox);

        var link = Assert.Single(scene.Links, l => l.To == _graph.OwnInput);

        Assert.Equal("firefox", link.From.Node);
        Assert.Equal(PatchSide.Out, link.From.Side);
        Assert.False(link.From.Fixed);
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

        Assert.Contains(scene.Nodes, n => n.Id == "mystery");
        Assert.Single(scene.Links, l => l.To == _graph.OwnInput);
    }

    /// <summary>And it is drawn once when it is also on offer.</summary>
    [Fact]
    public void The_chosen_source_is_not_drawn_twice()
    {
        var firefox = new AudioRoute("firefox", "Firefox", AudioRouteKind.Application);

        var scene = _graph.Read(new[] { firefox, firefox }, firefox);

        Assert.Single(scene.Nodes, n => n.Id == "firefox");
        Assert.Single(scene.Links, l => l.To == _graph.OwnInput);
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

        Assert.Single(scene.Nodes, n => n.Id == "firefox");
    }

    /// <summary>A source with no address at all is left out rather than drawn as a dead block.</summary>
    [Fact]
    public void A_source_with_no_address_is_left_out()
    {
        var scene = _graph.Read(new[] { new AudioRoute("", "Nameless", AudioRouteKind.Input) }, null);

        Assert.All(scene.Nodes, n => Assert.NotEqual("", n.Id));
        Assert.DoesNotContain(scene.Nodes, n => n.Title == "Nameless");
    }

    /// <summary>And so is anything on the machine calling itself one of our own blocks.</summary>
    /// <remarks>
    /// A program on this computer can be called anything at all, and two blocks with one id would
    /// be one block with two meanings: a cable would land on whichever the walk found first.
    /// </remarks>
    [Fact]
    public void Nothing_may_pretend_to_be_one_of_ours()
    {
        foreach (string ours in new[] { "record", "tracker", "fire", "mixer", "output" })
        {
            var scene = _graph.Read(
                new[] { new AudioRoute(ours, "Not us", AudioRouteKind.Application) }, null);

            Assert.Single(scene.Nodes, n => n.Id == ours);
            Assert.DoesNotContain(scene.Nodes, n => n.Title == "Not us");
        }
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

        var scene = _graph.Read(new[] { firefox }, firefox, "Built-in");

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

    /// <summary>A source's port is on the source, and ours are on ours.</summary>
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

    /// <summary>A source gives out and nothing on the machine takes in but the output.</summary>
    [Fact]
    public void A_source_only_gives_out()
    {
        var scene = _graph.Read(new[] { new AudioRoute("mic", "Built-in", AudioRouteKind.Input) }, null);

        var source = scene.Nodes.Single(n => n.Id == "mic");

        Assert.Single(source.Outs);
        Assert.Empty(source.Ins);
    }

    /// <summary>
    /// The recorder takes the capture in and gives takes out, and only the second reaches the desk.
    /// </summary>
    /// <remarks>
    /// The two halves of RECORD are not the same signal. A take being auditioned is played
    /// through the desk and comes out of the master; what is arriving at the input reaches
    /// nothing, since that fader sets what a take will hold rather than what anybody hears. A
    /// picture that ran the capture into the mixer would be saying you can hear yourself, which
    /// you cannot.
    /// </remarks>
    [Fact]
    public void The_take_reaches_the_desk_and_the_capture_does_not()
    {
        var scene = _graph.Read(Array.Empty<AudioRoute>(), null);

        var recorder = scene.Nodes.Single(n => n.Id == _graph.OwnNode);

        Assert.Single(recorder.Ins);
        Assert.Single(recorder.Outs);

        Assert.Contains(scene.Links, l => l.From == recorder.Outs[0] && l.To.Node == "mixer");
        Assert.DoesNotContain(scene.Links, l => l.From == _graph.OwnInput);
    }

    /// <summary>A cable made by the rules may be drawn by the rules.</summary>
    /// <remarks>
    /// The two rules meeting, which is the seam where a picture and a wiring drift apart: what
    /// the graph reads has to be something the wiring would have allowed a hand to make.
    /// </remarks>
    [Fact]
    public void What_can_be_patched_is_what_a_hand_could_have_made()
    {
        var firefox = new AudioRoute("firefox", "Firefox", AudioRouteKind.Application);

        var scene = _graph.Read(new[] { firefox }, firefox);

        foreach (var link in scene.Links)
        {
            if (link.From.Fixed || link.To.Fixed) continue;

            Assert.True(_wiring.Allowed(link.From, link.To));
        }
    }
}
