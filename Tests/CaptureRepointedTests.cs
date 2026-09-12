using System;
using System.Diagnostics;
using System.Threading;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The machine pointing the capture somewhere else, and the page putting it back.
/// </summary>
/// <remarks>
/// **The capture belongs to the machine and the choice belongs to whoever made it.** A session
/// manager re-points a capture whenever the stream is remade, so a reading of the graph really
/// can show a source nobody here chose. What somebody chose is still what they chose, so the
/// picker goes back to it and the arrangement is asked for again.
///
/// **The setting never moved, which is why asking again has to be a call of its own.** The page
/// writes what it wants into <c>IInputSetting</c> and the arrangement watches it; a setting that
/// still says the same thing raises nothing, and nothing is what should be raised, since the
/// ordinary reading of the graph is the arrangement that is already standing. So the one case
/// where a reading has learnt something is said out loud, through
/// <c>IInputArrangement.Again</c>.
///
/// **And asking again may not pull the source about.** Being off its own output is held on a
/// clock of its own and is not what came undone here: taking it aside afresh would put its links
/// back and pull them out again, which is the source out of the desk and back for a fraction of
/// a second, for a fault that was never about that half.
/// </remarks>
public sealed class CaptureRepointedTests
{
    /// <summary>Long enough for the reading to land, short enough to fail rather than hang.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Reads the graph and waits for the page to have finished with it.
    /// </summary>
    /// <remarks>
    /// The reading is <c>async void</c> over the pool, so what it settles on cannot be read on
    /// the line after asking for it. Waited for rather than slept through, so a slow machine
    /// takes longer rather than answering wrongly.
    /// </remarks>
    /// <param name="bench">The page and its doubles.</param>
    /// <param name="done">What is true once the reading has been answered.</param>
    private static bool Read(RecorderBench bench, Func<bool> done)
    {
        bench.Page.RefreshRoutes();

        var clock = Stopwatch.StartNew();

        while (clock.Elapsed < Patience)
        {
            if (done()) return true;

            Thread.Sleep(5);
        }

        return done();
    }

    /// <summary>A capture the machine re-pointed is pointed back at what was chosen.</summary>
    [Fact]
    public void A_capture_the_machine_moved_is_pointed_back()
    {
        var bench = new RecorderBench();

        bench.Page.SelectedRoute = RecorderBench.Firefox;

        int connected = bench.Wiring.Connected;

        bench.Wiring.Wired(RecorderBench.Microphone);

        Assert.True(
            Read(bench, () => bench.Wiring.Connected > connected),
            "the capture was left on what the machine had wired up");

        Assert.Equal(RecorderBench.Firefox, bench.Page.SelectedRoute);
    }

    /// <summary>And the source is not pulled off its own output a second time for it.</summary>
    [Fact]
    public void Pointing_it_back_does_not_take_the_source_aside_again()
    {
        var bench = new RecorderBench();

        bench.Page.SelectedRoute = RecorderBench.Firefox;

        int connected = bench.Wiring.Connected;
        int aside = bench.Wiring.Aside;

        bench.Wiring.Wired(RecorderBench.Microphone);

        Assert.True(Read(bench, () => bench.Wiring.Connected > connected));

        Assert.Equal(aside, bench.Wiring.Aside);
    }

    /// <summary>
    /// A graph that agrees with what was chosen is read and left alone, which is every reading.
    /// </summary>
    /// <remarks>
    /// The rate this has to be right about: the graph is read every couple of seconds for as long
    /// as a page carrying the picker is up, and a reading that rewired anything would be running
    /// the machine's own tools at that rate for the rest of the session.
    /// </remarks>
    [Fact]
    public void A_graph_that_agrees_is_left_alone()
    {
        var bench = new RecorderBench();

        bench.Page.SelectedRoute = RecorderBench.Firefox;

        int connected = bench.Wiring.Connected;
        int aside = bench.Wiring.Aside;

        Assert.True(Read(bench, () => bench.Page.SelectedRoute == RecorderBench.Firefox));

        Thread.Sleep(50);

        Assert.Equal(connected, bench.Wiring.Connected);
        Assert.Equal(aside, bench.Wiring.Aside);
    }
}
