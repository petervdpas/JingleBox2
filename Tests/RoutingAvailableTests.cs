using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JingleBox2.Audio.Records;
using JingleBox2.Audio.Routing;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Audio.Routing.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What asking whether the routing is available costs, which is a fact about the contract rather
/// than about either machine.
/// </summary>
/// <remarks>
/// **A property is read from a binding and from the head of a timer's tick**, which is to say on
/// the drawing thread, and nothing about reading one warns a caller that the machine's audio
/// endpoints are about to be walked through COM. So the rule is that it is asked of the machine
/// once and kept, and settled again where the routes are already being read, which is off that
/// thread.
///
/// The fault this is written from was visible and not audible: the transport kept perfect time
/// and the pattern arrived in clumps, because the thread that draws was gone for a third of a
/// second twice a second. **What it answers was never wrong**, which is why nothing about the
/// answers would have caught it, and why these count the walks instead.
///
/// Every test here runs on both machines and says something on both, since the walk only happens
/// on Windows at all: there the answer is one, elsewhere it is nought, and a version that asked
/// per read would fail on Windows and pass here for the rest of its life.
/// </remarks>
public class RoutingAvailableTests
{
    /// <summary>How many walks one honest look costs on this machine.</summary>
    private static int Once => OperatingSystem.IsWindows() ? 1 : 0;

    /// <summary>A recorder with something playing out of it, so there is something to offer.</summary>
    private static RecorderBench.Deaf Machine() =>
        new()
        {
            Outputs = new[] { new LoopbackDevice(0, "Speakers") },
            Playing = new[] { new AudioProgram(4242, "Firefox") },
        };

    /// <summary>
    /// **Asking it forty times asks the machine once.**
    /// </summary>
    /// <remarks>
    /// The whole of it. Forty rather than two because the fault was a tick every two seconds for
    /// as long as the page was up, and a guard that merely halved the walks would pass a test
    /// that asked twice.
    /// </remarks>
    [Fact]
    public void Asking_it_over_and_over_walks_the_machine_once()
    {
        var machine = Machine();
        var routing = new WindowsRouting(machine);

        for (int i = 0; i < 40; i++) _ = routing.IsAvailable;

        Assert.Equal(Once, machine.OutputWalks);
    }

    /// <summary>
    /// A reading walks each list once, where it used to walk both of them twice.
    /// </summary>
    /// <remarks>
    /// The second walk was the availability test in front of the reading, asking the same
    /// question of the same two lists the reading was about to take anyway.
    /// </remarks>
    [Fact]
    public void A_reading_walks_each_list_once()
    {
        var machine = Machine();
        var routing = new WindowsRouting(machine);

        routing.GetRoutes();

        Assert.Equal(Once, machine.OutputWalks);
        Assert.Equal(Once, machine.ProgramWalks);
    }

    /// <summary>
    /// **A machine that gains an output is noticed on the next reading**, so keeping the answer
    /// is not freezing it.
    /// </summary>
    /// <remarks>
    /// The trap under any kept answer, and the one worth a test of its own: a no that stuck would
    /// be a page that never came alive on a machine where the first thing asked happened to be
    /// asked before there was anything to find.
    /// </remarks>
    [Fact]
    public void Something_arriving_is_noticed_on_the_next_reading()
    {
        var machine = new RecorderBench.Deaf();
        var routing = new WindowsRouting(machine);

        Assert.False(routing.IsAvailable);

        machine.Outputs = new[] { new LoopbackDevice(0, "Speakers") };

        routing.GetRoutes();

        Assert.Equal(OperatingSystem.IsWindows(), routing.IsAvailable);
    }

    /// <summary>And one that loses everything stops offering, on the same reading.</summary>
    [Fact]
    public void Everything_going_away_is_noticed_too()
    {
        var machine = Machine();
        var routing = new WindowsRouting(machine);

        Assert.Equal(OperatingSystem.IsWindows(), routing.IsAvailable);

        machine.Outputs = Array.Empty<LoopbackDevice>();
        machine.Playing = Array.Empty<AudioProgram>();

        Assert.Empty(routing.GetRoutes());
        Assert.False(routing.IsAvailable);
    }

    /// <summary>
    /// A routing that only knows what is there once it has read, which is the real one's rule.
    /// </summary>
    /// <remarks>
    /// The whole of what <see cref="WindowsRouting"/> now does, with the walk taken out: it
    /// answers no until a reading has happened and whatever the last reading found afterwards.
    /// Written out here rather than using the real one because the real one answers no on
    /// anything but Windows whatever it is handed, so a page driven by it would be a test that
    /// says nothing on the machine most of these run on.
    /// </remarks>
    private sealed class Quiet : IAudioRouting
    {
        /// <summary>Whether this machine has anything to offer at the moment.</summary>
        public bool Anything { get; set; }

        /// <summary>What the last reading found.</summary>
        private bool _can;

        /// <inheritdoc/>
        public bool IsAvailable => _can;

        /// <inheritdoc/>
        public IReadOnlyList<AudioRoute> GetRoutes()
        {
            _can = Anything;

            return Anything ? new[] { RecorderBench.Firefox } : Array.Empty<AudioRoute>();
        }

        /// <inheritdoc/>
        public AudioRoute? GetCurrentRoute() => null;

        /// <inheritdoc/>
        public bool Connect(AudioRoute route) => Anything;

        /// <inheritdoc/>
        public bool CanTakeAside => false;

        /// <inheritdoc/>
        public string AsideNote => "";

        /// <inheritdoc/>
        public bool TakeAside(AudioRoute route) => false;

        /// <inheritdoc/>
        public bool HoldAside(AudioRoute route) => false;

        /// <inheritdoc/>
        public void GiveBack() { }

        /// <inheritdoc/>
        public bool? IsOurOutput(AudioRoute source, string? output) => null;
    }

    /// <summary>Long enough for a reading on the pool, short enough to fail rather than hang.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(3);

    /// <summary>Waits for a reading to have landed, or gives up.</summary>
    private static bool Within(Func<bool> done)
    {
        var clock = Stopwatch.StartNew();

        while (clock.Elapsed < Patience)
        {
            if (done()) return true;

            Thread.Sleep(10);
        }

        return done();
    }

    /// <summary>
    /// **A page that hears no reads again, or the answer can never change.**
    /// </summary>
    /// <remarks>
    /// The other half of keeping the answer, and the half that undoes it if it is forgotten. The
    /// reading is what settles whether there is anything here, the page is what does the reading,
    /// and the page used to refuse to read whenever the answer was no: so a machine that lost its
    /// last output answered no once and could never answer anything else, with the watch still
    /// ticking every two seconds and turning round at the door.
    ///
    /// **An answer that is kept may only be guarded on by somebody who is not the one who would
    /// refresh it.** Nothing about the routing itself can say this, which is why it is asked of
    /// the page: put the guard back in <c>RefreshRoutes</c> and the routing's own tests all still
    /// pass.
    /// </remarks>
    [Fact]
    public void A_page_that_heard_no_reads_again()
    {
        var routing = new Quiet();
        var bench = new RecorderBench(routing);

        bench.Page.RefreshRoutes();

        Assert.False(Within(() => bench.Page.Routes.Count > 0));
        Assert.False(routing.IsAvailable);

        routing.Anything = true;

        bench.Page.RefreshRoutes();

        Assert.True(
            Within(() => bench.Page.Routes.Count > 0),
            "a page that was told there was nothing here never asked again, so the picker is dead for the session");
    }
}
