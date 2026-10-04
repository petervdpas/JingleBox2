using System.Threading;
using JingleBox2.Sync;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The real Ableton Link library, reached through the session class, without ever joining the
/// network.
/// </summary>
/// <remarks>
/// **Nothing here calls <c>Use(true)</c>.** A test that joined would become a peer in whatever
/// session is on the network it runs on, and a tempo it proposed would move somebody's Live in
/// the next room. <c>Ready</c> makes the native instance and leaves it off the network, which is
/// enough to read and write its timeline.
///
/// That the library is present is asserted rather than assumed, and that is a fact about this
/// repository rather than about the machine: all three builds are checked in under
/// <c>native/</c> and the test project copies its platform's one beside the tests. On Windows
/// this is the only thing that loads the cross compiled DLL before a release does.
/// </remarks>
public class AbletonLinkLibraryTests
{
    /// <summary>The library is carried, and loads.</summary>
    [Fact]
    public void The_library_is_here()
    {
        using var link = new AbletonLink();

        Assert.True(link.Present, link.Missing);
        Assert.Null(link.Missing);
    }

    /// <summary>Before anything is made, every read answers nothing and every write does nothing.</summary>
    [Fact]
    public void Before_it_is_made_nothing_answers_and_nothing_throws()
    {
        using var link = new AbletonLink();

        Assert.False(link.IsOn);
        Assert.Equal(0, link.Now);
        Assert.True(double.IsNaN(link.BeatAt(123, 4)));
        Assert.Equal(0, link.TimeAt(4, 4));
        Assert.False(link.IsPlaying);
        Assert.Equal(0, link.Peers);

        link.Propose(130);
        link.Play(true);
        link.Use(false);

        Assert.False(link.IsOn);
    }

    /// <summary>Made and off the network, it is a timeline of its own and not a peer.</summary>
    [Fact]
    public void Made_it_is_not_on_the_network()
    {
        using var link = new AbletonLink();

        link.Ready();

        Assert.False(link.IsOn);
        Assert.NotEqual(0, link.Now);
        Assert.Equal(0, link.Peers);
    }

    /// <summary>A beat and its moment are each other's inverse, which is what the clock thread leans on.</summary>
    [Fact]
    public void A_beat_and_its_moment_go_round()
    {
        using var link = new AbletonLink();

        link.Ready();

        long now = link.Now;
        double beat = link.BeatAt(now, 4);
        long back = link.TimeAt(beat, 4);

        Assert.InRange(back - now, -2, 2);
    }

    /// <summary>The clock moves forward and in microseconds.</summary>
    [Fact]
    public void The_clock_is_in_microseconds()
    {
        using var link = new AbletonLink();

        link.Ready();

        long before = link.Now;
        Thread.Sleep(50);
        long after = link.Now;

        Assert.InRange(after - before, 40_000, 500_000);
    }

    /// <summary>A tempo put to the session is the one the timeline runs at.</summary>
    [Fact]
    public void A_proposed_tempo_is_the_timeline_tempo()
    {
        using var link = new AbletonLink();

        link.Ready();
        link.Propose(97);

        Assert.Equal(97, link.Tempo, 3);

        long now = link.Now;
        double beats = link.BeatAt(now + 60_000_000, 4) - link.BeatAt(now, 4);

        Assert.Equal(97, beats, 3);
    }

    /// <summary>Its own tempo coming back is not announced as a peer's, or a lane would edit the song.</summary>
    [Fact]
    public void Its_own_tempo_is_not_heard_as_a_peers()
    {
        using var link = new AbletonLink();

        link.Ready();

        int heard = 0;
        link.TempoHeard += _ => Interlocked.Increment(ref heard);

        link.Propose(111);
        link.Propose(111.004);
        Thread.Sleep(200);

        Assert.Equal(0, Volatile.Read(ref heard));
    }

    /// <summary>Nonsense tempos are not proposed.</summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0)]
    [InlineData(-120)]
    public void A_tempo_that_is_not_one_is_refused(double bpm)
    {
        using var link = new AbletonLink();

        link.Ready();

        double before = link.Tempo;
        link.Propose(bpm);

        Assert.Equal(before, link.Tempo);
    }

    /// <summary>Start and stop reach the session's state while they are shared, and not while they are not.</summary>
    [Fact]
    public void Start_and_stop_reach_the_session_only_while_shared()
    {
        using var link = new AbletonLink();

        link.Ready();

        link.SharesStartStop = false;
        link.Play(true);
        Assert.False(link.IsPlaying);

        link.SharesStartStop = true;
        link.Play(true);
        Assert.True(link.IsPlaying);

        link.Play(false);
        Assert.False(link.IsPlaying);
    }

    /// <summary>Taken down twice is taken down once.</summary>
    [Fact]
    public void Disposing_twice_is_harmless()
    {
        var link = new AbletonLink();

        link.Ready();
        link.Dispose();
        link.Dispose();

        Assert.Equal(0, link.Now);
    }
}
