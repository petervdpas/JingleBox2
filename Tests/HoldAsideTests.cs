using System;
using System.Threading;
using JingleBox2.Audio.Routing;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A source taken aside is kept there, on the clock that already keeps the capture standing.
/// </summary>
/// <remarks>
/// **Taking a source aside is not a thing that stays done.** The graph belongs to the machine
/// rather than to this application, and its session manager wires a stream back to the speakers
/// whenever the stream is remade: a new tab, a page reloaded, one video ending and the next
/// starting. The capture was already put back every couple of seconds for exactly that reason and
/// the other half was not, so the source came back onto its own output while it was still
/// arriving here. Two paths to the speakers, one of them a few tens of milliseconds late, which
/// is a slap rather than a level doubling and is how it was reported.
///
/// **A test here turns the switch off before it ends**, and that is not tidiness: while it is on
/// the page holds the input open and watches the graph, which is a clock that keeps running, and
/// a test that walked away from one would leave it ticking under every test after it.
///
/// The tools are not run, the rule <see cref="TakeAsideTests"/> keeps: `pw-link` rewires the
/// machine the suite is running on, and a test that silences somebody's browser while they work
/// is a worse thing than an untested line.
///
/// **What the page owns here is the reading and not the holding.** Keeping a source off its own
/// output belongs to <c>IInputArrangement</c>, which has a clock of its own for the length of the
/// session and is asked about in <see cref="InputArrangementTests"/>, including the sentence said
/// when something has crept back. What is covered here is what the page is still answerable for:
/// that the arrangement is made and held while the switches are on, that the switch being off
/// holds nothing, and that a routing which cannot see its own capture unplugs nothing whatever.
/// </remarks>
public sealed class HoldAsideTests
{
    /// <summary>Long enough for a double to answer, short enough to fail rather than hang.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>Waits for something the page does off the drawing thread, or gives up.</summary>
    private static bool Within(Func<bool> done)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();

        while (clock.Elapsed < Patience)
        {
            if (done()) return true;

            Thread.Sleep(10);
        }

        return done();
    }

    /// <summary>Reading the graph holds the arrangement while the switch is on.</summary>
    [Fact]
    public void A_reading_holds_a_source_that_is_supposed_to_be_aside()
    {
        var bench = new RecorderBench();

        bench.Page.SelectedRoute = RecorderBench.Firefox;

        Assert.True(
            Within(() =>
            {
                bench.Page.RefreshRoutes();

                return bench.Wiring.Held > 0;
            }),
            "the arrangement was never held, so a source that got back onto its own output stays there");
    }

    /// <summary>And touches nothing at all while the switch is off, which is every ordinary run.</summary>
    [Fact]
    public void A_reading_with_the_switch_off_holds_nothing()
    {
        var bench = new RecorderBench();

        bench.Page.RefreshRoutes();

        Assert.True(Within(() => bench.Page.Routes.Count > 0), "the graph was never read");

        Assert.Equal(0, bench.Wiring.Held);
    }

    /// <summary>
    /// A routing that cannot see its own capture unplugs nothing, which is the guard the whole
    /// thing turns on.
    /// </summary>
    /// <remarks>
    /// What makes taking a source aside safe is knowing which of its links is the one bringing it
    /// here, and that is decided by handing our capture's ports in. Handed none, every link looks
    /// like somebody else's and the one to keep is cut with the rest: the source goes silent
    /// everywhere at the moment somebody asked to hear it here. On a machine with no graph there
    /// are never any capture ports, so this is that case asked where it can be asked.
    /// </remarks>
    [Fact]
    public void A_routing_that_cannot_answer_unplugs_nothing()
    {
        var routing = new NoAudioRouting();
        var firefox = new AudioRoute("Firefox", "Firefox", AudioRouteKind.Application);

        Assert.False(routing.TakeAside(firefox));
        Assert.False(routing.HoldAside(firefox));
    }
}
