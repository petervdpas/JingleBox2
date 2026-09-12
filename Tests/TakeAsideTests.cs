using System;
using System.Linq;
using JingleBox2.Audio.Routing;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Audio.Routing.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which links come off when a source is taken aside, so it reaches this application alone.
/// </summary>
/// <remarks>
/// **Capturing a source and taking it aside are two different acts.** Every program that records
/// does the first; a browser captured is still playing out of the speakers, which is right for
/// streaming and wrong on air. This is the second, and it is the half that touches somebody
/// else's machine: what it decides is what gets unplugged.
///
/// The rule is asked here and the tools are not run, deliberately: `pw-link` rewires the machine
/// the suite is running on, and a test that silences somebody's browser while they work is a
/// worse thing than an untested line. Every awkward case below is ordinary rather than invented.
/// </remarks>
public class TakeAsideTests
{
    private readonly IPipeWireGraph _graph = new PipeWireGraph();

    /// <summary>This application's own capture, which is never unplugged.</summary>
    private static readonly PipeWirePort[] Ours =
    {
        new("JingleBox2", "input_FL"),
        new("JingleBox2", "input_FR")
    };

    /// <summary>One link, from a node's port to another's.</summary>
    private static PipeWireLink Link(string from, string fromPort, string to, string toPort) =>
        new(new PipeWirePort(from, fromPort), new PipeWirePort(to, toPort));

    /// <summary>What a browser feeding the speakers and us looks like.</summary>
    private static PipeWireLink[] Both() => new[]
    {
        Link("Firefox", "output_FL", "Speakers", "playback_FL"),
        Link("Firefox", "output_FR", "Speakers", "playback_FR"),
        Link("Firefox", "output_FL", "JingleBox2", "input_FL"),
        Link("Firefox", "output_FR", "JingleBox2", "input_FR")
    };

    /// <summary>What goes to the speakers comes off.</summary>
    [Fact]
    public void What_goes_elsewhere_comes_off()
    {
        var away = _graph.LinksAway(Both(), "Firefox", Ours);

        Assert.Equal(2, away.Count);
        Assert.All(away, l => Assert.Equal("Speakers", l.To.Node));
    }

    /// <summary>
    /// And what comes here never does.
    /// </summary>
    /// <remarks>
    /// The one that would be a disaster: unplugging the link that brought the source here is
    /// taking the source away from the very thing it was chosen for, and the meter would go
    /// quiet at the same moment somebody asked to hear it alone.
    /// </remarks>
    [Fact]
    public void What_comes_here_is_never_unplugged()
    {
        var away = _graph.LinksAway(Both(), "Firefox", Ours);

        Assert.DoesNotContain(away, l => l.To.Node == "JingleBox2");
    }

    /// <summary>Only that source's links are touched.</summary>
    [Fact]
    public void Nothing_elses_links_are_touched()
    {
        var links = Both().Append(Link("Music", "output_FL", "Speakers", "playback_FL"));

        var away = _graph.LinksAway(links, "Firefox", Ours);

        Assert.All(away, l => Assert.Equal("Firefox", l.From.Node));
    }

    /// <summary>A source feeding two places loses both.</summary>
    /// <remarks>
    /// Alone means alone. A browser wired to the speakers and to a meter somewhere else is
    /// still being heard, so leaving the second would make the switch a half truth.
    /// </remarks>
    [Fact]
    public void A_source_feeding_two_places_loses_both()
    {
        var links = Both()
            .Append(Link("Firefox", "output_FL", "Headphones", "playback_FL"))
            .ToArray();

        var away = _graph.LinksAway(links, "Firefox", Ours);

        Assert.Equal(3, away.Count);
    }

    /// <summary>A source that is already alone has nothing to take off.</summary>
    /// <remarks>
    /// Which is what happens when the switch is thrown twice, or when the machine had already
    /// wired the source only to us: an ordinary answer rather than a fault.
    /// </remarks>
    [Fact]
    public void A_source_already_alone_has_nothing_to_lose()
    {
        var links = new[]
        {
            Link("Firefox", "output_FL", "JingleBox2", "input_FL"),
            Link("Firefox", "output_FR", "JingleBox2", "input_FR")
        };

        Assert.Empty(_graph.LinksAway(links, "Firefox", Ours));
    }

    /// <summary>A source that is not in the graph has nothing either.</summary>
    [Fact]
    public void A_source_that_is_not_there_has_nothing()
    {
        Assert.Empty(_graph.LinksAway(Both(), "Chromium", Ours));
    }

    /// <summary>Nothing to read at all is an ordinary answer.</summary>
    [Fact]
    public void Nothing_to_read_is_an_ordinary_answer()
    {
        Assert.Empty(_graph.LinksAway(Array.Empty<PipeWireLink>(), "Firefox", Ours));
        Assert.Empty(_graph.LinksAway(null!, "Firefox", Ours));
        Assert.Empty(_graph.LinksAway(Both(), "", Ours));
        Assert.Empty(_graph.LinksAway(Both(), null!, Ours));
    }

    /// <summary>
    /// With our own capture unknown, everything that source feeds comes off.
    /// </summary>
    /// <remarks>
    /// Which is the honest answer rather than a safe one, and it is why the capture is read
    /// first: not knowing what is ours means every link out of that node is somewhere else as
    /// far as this can tell. It cannot happen in practice, since the input has to be open for a
    /// source to be feeding it at all.
    /// </remarks>
    [Fact]
    public void With_our_own_capture_unknown_everything_comes_off()
    {
        Assert.Equal(4, _graph.LinksAway(Both(), "Firefox", null!).Count);
    }

    /// <summary>The addresses are compared as they are written.</summary>
    /// <remarks>
    /// A node name is an address rather than a word: two programs on one machine can differ by
    /// nothing but case, and unplugging the wrong one is unplugging somebody's monitoring.
    /// </remarks>
    [Fact]
    public void Addresses_are_compared_as_they_are_written()
    {
        Assert.Empty(_graph.LinksAway(Both(), "firefox", Ours));
    }

    /// <summary>A machine with no graph says it cannot, and changes nothing.</summary>
    [Fact]
    public void A_machine_with_no_graph_says_it_cannot()
    {
        var none = new NoAudioRouting();

        Assert.False(none.CanTakeAside);
        Assert.False(none.TakeAside(new AudioRoute("firefox", "Firefox", AudioRouteKind.Application)));

        none.GiveBack();
    }
}
