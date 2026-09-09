using System;
using System.Diagnostics;
using System.Threading;
using JingleBox2.Midi;
using JingleBox2.Midi.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Following somebody else's clock: the counting, the two ways of being told to go, and holding.
/// </summary>
/// <remarks>
/// Two threads meet in this type, so the tests use two: one standing in for the port that ticks
/// and one for the transport that waits. That is the whole reason it is a seam of its own — the
/// decisions in it can be put a question to without a port, a device, or a clock thread.
///
/// **The one worth reading is <see cref="Silence_holds_the_transport_rather_than_stopping_it"/>.**
/// A master paused for a bar and a master unplugged look identical on the wire, and only one of
/// them wants the music to stop, so silence holds.
/// </remarks>
public class MidiClockFollowTests
{
    /// <summary>A follower already following, which is what most of these want.</summary>
    private static MidiClockFollow Following()
    {
        var follow = new MidiClockFollow();

        follow.Follow(true);

        return follow;
    }

    /// <summary>Nothing is counted until it is told to follow.</summary>
    /// <remarks>
    /// A machine on its own clock has ports that may be ticking away at it, and none of it is
    /// any of this object's business until somebody says so.
    /// </remarks>
    [Fact]
    public void Nothing_counts_until_it_is_following()
    {
        var follow = new MidiClockFollow();

        Assert.False(follow.IsFollowing);

        follow.Start();
        follow.Tick();
        follow.Tick();
        follow.Placed(16);

        Assert.Equal(0, follow.Ticks);
        Assert.Equal(0, follow.Pointer);
    }

    /// <summary>Ticks are counted once it is.</summary>
    [Fact]
    public void Ticks_are_counted()
    {
        var follow = Following();

        for (int at = 0; at < 24; at++) follow.Tick();

        Assert.Equal(24, follow.Ticks);
    }

    /// <summary>
    /// And counted whether or not the master has said go.
    /// </summary>
    /// <remarks>
    /// Plenty of gear ticks continuously and starts and stops on top of it. A tick dropped for
    /// want of a start would put the whole pass one tick behind for its length.
    /// </remarks>
    [Fact]
    public void Ticks_are_counted_before_a_start_arrives()
    {
        var follow = Following();

        follow.Tick();
        follow.Tick();

        Assert.Equal(2, follow.Ticks);
    }

    /// <summary>A start puts the count back to nought and clears the pointer.</summary>
    [Fact]
    public void A_start_is_from_the_top()
    {
        var follow = Following();

        follow.Placed(64);
        follow.Tick();
        follow.Tick();

        follow.Start();

        Assert.Equal(0, follow.Ticks);
        Assert.Equal(0, follow.Pointer);
    }

    /// <summary>
    /// A continue puts the count back and leaves the pointer, which is the difference.
    /// </summary>
    /// <remarks>
    /// A continue means from where the pointer said, so the ticks that follow are counted from
    /// there rather than from the top of the song. Clearing the pointer here would make every
    /// continue a start, which is the whole desk playing from bar one.
    /// </remarks>
    [Fact]
    public void A_continue_keeps_the_pointer()
    {
        var follow = Following();

        follow.Placed(64);
        follow.Tick();

        follow.Resume();

        Assert.Equal(0, follow.Ticks);
        Assert.Equal(64, follow.Pointer);
    }

    /// <summary>A pointer arriving before the go is kept until it is asked for.</summary>
    /// <remarks>
    /// The standard's order is the position and then the go, so the pointer has to survive until
    /// the go turns up.
    /// </remarks>
    [Fact]
    public void A_pointer_is_kept_until_the_go_arrives()
    {
        var follow = Following();

        follow.Placed(128);

        Assert.Equal(128, follow.Pointer);

        follow.Resume();

        Assert.Equal(128, follow.Pointer);
    }

    /// <summary>Nonsense in a pointer is held at the top rather than taken.</summary>
    [Fact]
    public void A_nonsense_pointer_is_the_top()
    {
        var follow = Following();

        follow.Placed(-40);

        Assert.Equal(0, follow.Pointer);
    }

    /// <summary>Both ways of being told to go say so, and say which.</summary>
    [Fact]
    public void Going_is_announced_and_says_which_kind()
    {
        var follow = Following();

        bool? fromTheTop = null;

        follow.Began += top => fromTheTop = top;

        follow.Start();

        Assert.True(fromTheTop);

        follow.Resume();

        Assert.False(fromTheTop);
    }

    /// <summary>And stopping says so.</summary>
    [Fact]
    public void Stopping_is_announced()
    {
        var follow = Following();

        bool ended = false;

        follow.Ended += () => ended = true;

        follow.Start();
        follow.Cease();

        Assert.True(ended);
    }

    /// <summary>A waiting thread is let go the moment the count is reached.</summary>
    [Fact]
    public void Waiting_ends_when_the_count_arrives()
    {
        var follow = Following();

        follow.Start();

        using var cancel = new CancellationTokenSource();

        bool got = false;

        var waiting = new Thread(() => got = follow.WaitFor(6, cancel.Token)) { IsBackground = true };

        waiting.Start();

        for (int at = 0; at < 6; at++)
        {
            Thread.Sleep(2);
            follow.Tick();
        }

        Assert.True(waiting.Join(TimeSpan.FromSeconds(5)), "the wait never ended");
        Assert.True(got);
    }

    /// <summary>
    /// **Silence holds the transport where it is rather than stopping it.**
    /// </summary>
    /// <remarks>
    /// The decision this whole type exists to encode. A master paused for a bar and a master
    /// unplugged look identical on the wire: no stop message, just no more ticks. Deciding after
    /// some length of silence that it has gone would mean choosing that length, and any length is
    /// wrong for one of the two cases.
    ///
    /// So the wait simply does not end, which is what holding looks like from the inside. Checked
    /// by waiting a good deal longer than the twenty milliseconds the waiter wakes on, so a
    /// version that gave up on a timeout would be caught.
    /// </remarks>
    [Fact]
    public void Silence_holds_the_transport_rather_than_stopping_it()
    {
        var follow = Following();

        follow.Start();

        using var cancel = new CancellationTokenSource();

        bool ended = false;

        var waiting = new Thread(() => { follow.WaitFor(24, cancel.Token); ended = true; })
        {
            IsBackground = true
        };

        waiting.Start();

        follow.Tick();
        follow.Tick();

        Thread.Sleep(400);

        Assert.False(ended, "the wait gave up on silence instead of holding");
        Assert.Equal(2, follow.Ticks);

        cancel.Cancel();

        Assert.True(waiting.Join(TimeSpan.FromSeconds(5)), "cancelling did not let it go");
    }

    /// <summary>A stop from the master lets a held thread go, which is how a pass ends.</summary>
    [Fact]
    public void A_stop_lets_a_held_thread_go()
    {
        var follow = Following();

        follow.Start();

        using var cancel = new CancellationTokenSource();

        bool got = true;

        var waiting = new Thread(() => got = follow.WaitFor(96, cancel.Token)) { IsBackground = true };

        waiting.Start();

        Thread.Sleep(30);

        follow.Cease();

        Assert.True(waiting.Join(TimeSpan.FromSeconds(5)), "the stop did not let it go");
        Assert.False(got, "a stop should read as the pass being over rather than reached");
    }

    /// <summary>And so does being told to stop following at all.</summary>
    /// <remarks>
    /// Turning the setting off while a pass is held would otherwise leave a thread waiting on a
    /// clock nobody is listening to for the rest of the session.
    /// </remarks>
    [Fact]
    public void Turning_following_off_lets_a_held_thread_go()
    {
        var follow = Following();

        follow.Start();

        using var cancel = new CancellationTokenSource();

        var waiting = new Thread(() => follow.WaitFor(96, cancel.Token)) { IsBackground = true };

        waiting.Start();

        Thread.Sleep(30);

        follow.Follow(false);

        Assert.True(waiting.Join(TimeSpan.FromSeconds(5)), "it was left holding");
    }

    /// <summary>Waiting before any go has arrived does not wait: there is no pass yet.</summary>
    [Fact]
    public void Waiting_before_a_go_is_not_a_pass()
    {
        var follow = Following();

        using var cancel = new CancellationTokenSource();

        var clock = Stopwatch.StartNew();

        Assert.False(follow.WaitFor(1, cancel.Token));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), "it waited when there was nothing to wait for");
    }

    /// <summary>
    /// A hundred ticks from one thread are a hundred ticks, which is what a lock is for here.
    /// </summary>
    /// <remarks>
    /// The count is the one thing both threads touch. Run from several at once, because a race
    /// that fires one time in twenty passes a test that runs it once.
    /// </remarks>
    [Fact]
    public void Every_tick_is_counted_however_many_threads_send_them()
    {
        for (int again = 0; again < 20; again++)
        {
            var follow = Following();

            follow.Start();

            var threads = new Thread[4];

            for (int at = 0; at < threads.Length; at++)
            {
                threads[at] = new Thread(() =>
                {
                    for (int each = 0; each < 250; each++) follow.Tick();
                });

                threads[at].Start();
            }

            foreach (var one in threads) one.Join();

            Assert.Equal(1000, follow.Ticks);
        }
    }
}
