using System;
using System.Linq;
using JingleBox2.Audio.Routing.Records;
using JingleBox2.UI;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The patchbay and the routing table are one model, walked against each other.
/// </summary>
/// <remarks>
/// **They shared a vocabulary and said so nowhere, which is a coupling that holds until it does
/// not.** A block on the picture is a node, a connection point on it is a port, and a
/// <see cref="SignalPoint"/> is that pair; the meters on the desk and the meter beside a picked
/// block are fed from those pairs. Nothing joined the two, so a port renamed on the picture left
/// the meter behind it reading nought, with nothing to say why and nothing that would fail. That
/// is not a hypothetical: the song's own point was called `master` on one side and `song` on the
/// other for exactly as long as it took somebody to look at the screen.
///
/// So the picture is built and the table is asked about it. No engine, no sound card and no
/// window: what the readings are is measured where the audio is, and what is walked here is which
/// points there are, which is the half that goes quietly wrong.
/// </remarks>
public sealed class OneRoutingModelTests
{
    /// <summary>The picture, as it is drawn for a song with tracks on it.</summary>
    private static PatchScene Drawn(params string[] moved) =>
        new PatchGraph().Read(
            Array.Empty<AudioRoute>(), null, null, new[] { "TR-01", "TR-02" }, moved);

    /// <summary>A table over readings nobody looks at, since what is asked here is the list.</summary>
    private static ISignalPoints Points()
    {
        PatchLevel Silent() => new(true, 0, 0);

        return new SignalPoints(Silent, Silent, Silent, Silent, Silent, Silent);
    }

    /// <summary>
    /// Every block of ours can be asked what it is putting out, since that is what the sidebar
    /// asks the moment anybody clicks one.
    /// </summary>
    [Fact]
    public void Every_block_of_ours_can_be_asked()
    {
        var points = Points();

        foreach (var node in Drawn().Nodes.Where(n => n.IsOurs))
        {
            Assert.True(
                points.At(new SignalPoint(node.Id, "")).Known,
                $"the picture draws '{node.Id}' and the routing table has nothing to say about it");
        }
    }

    /// <summary>
    /// And every port the table names is a port the picture really draws, which is the half that
    /// went wrong: a reading keyed on a name nobody uses is a meter that can never move.
    /// </summary>
    [Fact]
    public void Every_port_the_table_names_is_on_the_picture()
    {
        var scene = Drawn();

        foreach (var point in Points().Ours)
        {
            if (point.Port.Length == 0) continue;

            var node = scene.Nodes.SingleOrDefault(n => n.Id == point.Node);

            Assert.True(node != null, $"the routing table names a block '{point.Node}' that is not drawn");

            Assert.True(
                node!.Outs.Any(p => p.Name == point.Port) || node.Ins.Any(p => p.Name == point.Port),
                $"the routing table names '{point.Node}:{point.Port}', which the picture does not draw");
        }
    }

    /// <summary>
    /// A block on the machine is answered with nothing rather than nought, since those are two
    /// different answers: nought is silence and nothing is nobody can say.
    /// </summary>
    [Fact]
    public void A_block_on_the_machine_is_answered_with_nothing()
    {
        Assert.False(Points().At(new SignalPoint("Firefox", "")).Known);
    }

    /// <summary>
    /// The four points the desk's own strips are fed from are on the list, since those meters
    /// come off it every time the page is up.
    /// </summary>
    [Fact]
    public void The_desks_own_strips_have_their_points()
    {
        var points = Points();

        foreach (var point in new[]
        {
            new SignalPoint(PatchNodes.Record, PatchPorts.Capture),
            new SignalPoint(PatchNodes.Record, PatchPorts.Takes),
            new SignalPoint(PatchNodes.Fire, PatchPorts.Pads),
            new SignalPoint(PatchNodes.Mixer, PatchPorts.Master)
        })
        {
            Assert.True(points.At(point).Known, $"nothing feeds the strip over {point.Node}:{point.Port}");
        }
    }

    /// <summary>
    /// A source moved to the recorder is still a block the table answers for, since moving a
    /// cable is not moving a block.
    /// </summary>
    [Fact]
    public void Moving_a_source_leaves_its_point_where_it_was()
    {
        var points = Points();

        foreach (var node in Drawn(PatchNodes.Song, PatchNodes.Fire).Nodes.Where(n => n.IsOurs))
        {
            Assert.True(points.At(new SignalPoint(node.Id, "")).Known, $"'{node.Id}' lost its reading");
        }
    }

    /// <summary>
    /// Every cable between two of our own blocks can light, so the third place these words are
    /// spelled cannot drift either.
    /// </summary>
    /// <remarks>
    /// The one where being wrong is silent: a cable that never lights looks exactly like a cable
    /// carrying nothing.
    /// </remarks>
    [Fact]
    public void Every_cable_of_ours_can_be_lit()
    {
        var scene = Drawn();

        var everything = new PatchSignals(
            Input: true,
            Takes: true,
            Pads: true,
            Tracks: new System.Collections.Generic.HashSet<string>(
                new[] { "TR-01", "TR-02" }, StringComparer.Ordinal),
            Output: true);

        var live = new PatchFlow().Live(scene.Links, everything);

        foreach (var link in scene.Links)
        {
            if (!Ours(scene, link.From.Node) || !Ours(scene, link.To.Node)) continue;

            Assert.Contains(link, live);
        }
    }

    /// <summary>Whether that block is one of ours, according to the picture itself.</summary>
    private static bool Ours(PatchScene scene, string node) =>
        scene.Nodes.Any(n => n.Id == node && n.IsOurs);
}
