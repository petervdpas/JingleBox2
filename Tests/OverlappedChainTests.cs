using System;
using System.Collections.Generic;
using JingleBox2.Audio.Plugins;
using JingleBox2.Audio.Plugins.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A chain driven a round at a time against the same chain driven straight through.
/// </summary>
/// <remarks>
/// The whole claim of the overlapped path is that **not one sample changes**: the same boxes are
/// handed the same audio in the same order, and what moves is when a plugin in its own process is
/// asked for its block. So what is checked here is the audio and the order, on a chain built out
/// of doubles rather than out of processes: a plugin cannot be started in a test, and the rule
/// being tested has nothing to do with plugins in it.
///
/// The interleaving is checked as well as the result, because a run that collected each device
/// before starting the next would give an identical buffer and save nothing whatever, which is a
/// change that passes every test about audio and does not work.
/// </remarks>
public class OverlappedChainTests
{
    /// <summary>An insert that can be left in flight, and says when each half of it ran.</summary>
    /// <remarks>
    /// Stands for a plugin in its own process: the work happens at the collection, so a caller
    /// that begins two of them before collecting either has both in flight at once.
    /// </remarks>
    private sealed class Deferred(string name, float by, List<string> said) : IAudioInsert, IOverlappable
    {
        private bool _flying;

        public void Process(float[] buffer, int frames)
        {
            said.Add(name + " straight");
            Scale(buffer, frames);
        }

        public bool Begin(float[] buffer, int frames)
        {
            said.Add(name + " begun");
            _flying = true;
            return true;
        }

        public bool Advance(float[] buffer, int frames)
        {
            if (!_flying) return false;

            _flying = false;
            said.Add(name + " collected");
            Scale(buffer, frames);

            return false;
        }

        private void Scale(float[] buffer, int frames)
        {
            for (int at = 0; at < frames * 2; at++) buffer[at] *= by;
        }
    }

    /// <summary>An insert that cannot be left in flight, which is every effect of ours.</summary>
    private sealed class Here(string name, float by, List<string> said) : IAudioInsert
    {
        public void Process(float[] buffer, int frames)
        {
            said.Add(name + " straight");
            for (int at = 0; at < frames * 2; at++) buffer[at] *= by;
        }
    }

    private static float[] Ones(int frames)
    {
        var buffer = new float[frames * 2];
        Array.Fill(buffer, 1f);
        return buffer;
    }

    private static void Drive(PluginChain chain, float[] buffer, int frames)
    {
        if (!chain.Begin(buffer, frames)) return;

        while (chain.Advance(buffer, frames)) { }
    }

    /// <summary>The two ways of driving one chain leave the same audio.</summary>
    [Fact]
    public void A_chain_in_rounds_is_the_same_chain()
    {
        var said = new List<string>();

        var straight = new PluginChain();
        straight.Add(new Deferred("one", 2f, said));
        straight.Add(new Here("two", 3f, said));
        straight.Add(new Deferred("three", 0.5f, said));

        var rounds = new PluginChain();
        rounds.Add(new Deferred("one", 2f, said));
        rounds.Add(new Here("two", 3f, said));
        rounds.Add(new Deferred("three", 0.5f, said));

        var first = Ones(8);
        var second = Ones(8);

        straight.Process(first, 8);
        Drive(rounds, second, 8);

        Assert.Equal(first, second);
        Assert.Equal(3f, second[0]);
    }

    /// <summary>Each box still runs in its own order, since the audio flows through them.</summary>
    /// <remarks>
    /// This is the half that may never be overlapped. A chain's second box works on what the
    /// first one made, so a run that started both would hand the second box the audio as it
    /// arrived and throw the first one's work away.
    /// </remarks>
    [Fact]
    public void A_chain_is_still_walked_in_order()
    {
        var said = new List<string>();

        var chain = new PluginChain();
        chain.Add(new Deferred("one", 2f, said));
        chain.Add(new Deferred("two", 2f, said));

        Drive(chain, Ones(8), 8);

        Assert.Equal(new[] { "one begun", "one collected", "two begun", "two collected" }, said);
    }

    /// <summary>Two chains driven together really are in flight together.</summary>
    /// <remarks>
    /// The point of the whole exercise, and the one thing a test on the audio alone cannot see:
    /// both are begun before either is collected, so the two processes wake at the same time.
    /// </remarks>
    [Fact]
    public void Two_chains_are_in_flight_at_once()
    {
        var said = new List<string>();

        var left = new PluginChain();
        var right = new PluginChain();

        left.Add(new Deferred("left", 2f, said));
        right.Add(new Deferred("right", 2f, said));

        var one = Ones(8);
        var two = Ones(8);

        bool flyingLeft = left.Begin(one, 8);
        bool flyingRight = right.Begin(two, 8);

        Assert.True(flyingLeft);
        Assert.True(flyingRight);

        while (flyingLeft || flyingRight)
        {
            if (flyingLeft) flyingLeft = left.Advance(one, 8);
            if (flyingRight) flyingRight = right.Advance(two, 8);
        }

        Assert.Equal(new[] { "left begun", "right begun", "left collected", "right collected" }, said);
        Assert.Equal(2f, one[0]);
        Assert.Equal(2f, two[0]);
    }

    /// <summary>A chain of our own effects reports nothing in flight and still does the work.</summary>
    /// <remarks>
    /// Which is what makes the switch free where there are no plugins: the first pass does the
    /// whole chain where it stands and no round ever runs.
    /// </remarks>
    [Fact]
    public void A_chain_with_nothing_to_wait_for_is_done_at_once()
    {
        var said = new List<string>();

        var chain = new PluginChain();
        chain.Add(new Here("one", 2f, said));
        chain.Add(new Here("two", 3f, said));

        var buffer = Ones(8);

        Assert.False(chain.Begin(buffer, 8));
        Assert.Equal(6f, buffer[0]);
        Assert.Equal(new[] { "one straight", "two straight" }, said);
    }

    /// <summary>A bypassed box is stepped over either way.</summary>
    [Fact]
    public void A_bypassed_box_is_stepped_over()
    {
        var said = new List<string>();

        var chain = new PluginChain();
        var skipped = chain.Add(new Deferred("skipped", 100f, said));
        chain.Add(new Deferred("kept", 2f, said));

        skipped.Bypassed = true;

        var buffer = Ones(8);

        Drive(chain, buffer, 8);

        Assert.Equal(2f, buffer[0]);
        Assert.Equal(new[] { "kept begun", "kept collected" }, said);
    }

    /// <summary>
    /// A run begun and abandoned is finished before the next one starts.
    /// </summary>
    /// <remarks>
    /// It cannot happen while the mixer drives every run to its end, which it does, and the cost
    /// of being certain is one comparison. What it protects against is the worst shape of fault
    /// this path has: a box left holding an answer nobody collected refuses every block after it,
    /// for the rest of the session, and from a chair that is one plugin going silent for no
    /// reason anybody can see.
    ///
    /// **The abandoned answer lands on the next block rather than being thrown away**, which is
    /// the deliberate half of this and is what the doubling below is. There is nowhere else to
    /// put it without keeping a spare buffer per chain for a case that never happens, and one
    /// block carrying a moment of a plugin's own output is a far smaller thing than that plugin
    /// being dead until the application is restarted.
    /// </remarks>
    [Fact]
    public void A_run_left_half_finished_does_not_poison_the_next_one()
    {
        var said = new List<string>();

        var chain = new PluginChain();
        chain.Add(new Deferred("one", 2f, said));
        chain.Add(new Deferred("two", 2f, said));

        var buffer = Ones(8);

        Assert.True(chain.Begin(buffer, 8));

        said.Clear();

        var fresh = Ones(8);

        Drive(chain, fresh, 8);

        Assert.Equal(8f, fresh[0]);
        Assert.Equal(new[] { "one collected", "one begun", "one collected", "two begun", "two collected" }, said);
    }
}
