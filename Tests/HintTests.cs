using System;
using System.Threading;
using JingleBox2.Hints;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A hint: said as often as anybody likes, answered once the saying stops.
/// </summary>
/// <remarks>
/// **The shape six places in this application were each keeping a timer for.** A level dragged
/// across a strip is one thing a person did and a hundred messages, and what follows it is a file
/// or a round trip to every plugin on a chain. Written out per place it was six clocks, five
/// rates and six chances to forget the stop; what that really costs is that a fault in one of
/// them is a fault in one of them.
///
/// Nothing here waits on the clock. <see cref="Hint.Due"/> is the whole rule and it takes no
/// thread, which is why it can be asked a hundred times in a millisecond rather than once a
/// second in real time.
/// </remarks>
public sealed class HintTests
{
    /// <summary>A hint that has never been said is owed nothing.</summary>
    [Fact]
    public void A_hint_nobody_said_is_owed_nothing()
    {
        int answered = 0;

        var hint = new Hint("nothing", TimeSpan.Zero, TimeSpan.Zero, () => answered++);

        Assert.False(hint.Owed);
        Assert.False(hint.Due());

        hint.Now();

        Assert.Equal(0, answered);
    }

    /// <summary>Saying it moved owes an answer, and it is not due while the saying goes on.</summary>
    [Fact]
    public void Saying_it_moved_owes_an_answer_that_is_not_due_yet()
    {
        var hint = new Hint("something", TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), () => { });

        hint.Moved();

        Assert.True(hint.Owed);
        Assert.False(hint.Due());
    }

    /// <summary>And it comes due once the saying has stopped for long enough.</summary>
    [Fact]
    public void It_comes_due_once_the_saying_stops()
    {
        int answered = 0;

        var hint = new Hint("something", TimeSpan.Zero, TimeSpan.FromHours(1), () => answered++);

        hint.Moved();

        Assert.True(hint.Due());

        hint.Now();

        Assert.Equal(1, answered);
        Assert.False(hint.Owed);
    }

    /// <summary>
    /// **A hundred sayings are one answer**, which is the whole of what it is for.
    /// </summary>
    [Fact]
    public void A_flurry_of_sayings_is_one_answer()
    {
        int answered = 0;

        var hint = new Hint("a drag", TimeSpan.Zero, TimeSpan.FromHours(1), () => answered++);

        for (int said = 0; said < 100; said++) hint.Moved();

        hint.Now();

        Assert.Equal(1, answered);
    }

    /// <summary>
    /// **A saying that never stops is answered anyway**, which is what the second rule is for.
    /// </summary>
    /// <remarks>
    /// A hand resting on a fader for a minute would otherwise hold the work back for a minute,
    /// and the work is somebody's settings reaching the disc.
    /// </remarks>
    [Fact]
    public void A_saying_that_never_stops_is_answered_anyway()
    {
        var hint = new Hint("a hand that never lets go", TimeSpan.FromMinutes(1), TimeSpan.Zero, () => { });

        hint.Moved();

        Assert.True(hint.Due());
    }

    /// <summary>Answering twice over one saying answers once.</summary>
    [Fact]
    public void Answering_twice_over_one_saying_answers_once()
    {
        int answered = 0;

        var hint = new Hint("something", TimeSpan.Zero, TimeSpan.Zero, () => answered++);

        hint.Moved();

        hint.Now();
        hint.Now();

        Assert.Equal(1, answered);
    }

    /// <summary>
    /// A saying that arrives while the answer runs is owed again rather than swallowed.
    /// </summary>
    /// <remarks>
    /// The answer can take a moment, and what moved during it is a change nobody has written
    /// down. Forgetting what is owed before the work rather than after it is the whole of the
    /// difference.
    /// </remarks>
    [Fact]
    public void A_saying_during_the_answer_is_owed_again()
    {
        Hint? hint = null;

        hint = new Hint("something", TimeSpan.Zero, TimeSpan.Zero, () => hint!.Moved());

        hint.Moved();

        hint.Now();

        Assert.True(hint.Owed);
    }

    /// <summary>The clock answers what is due, and lets go by answering everything owed.</summary>
    /// <remarks>
    /// The way out is the one moment that cannot be waited through: whatever moved in the last
    /// fraction of a second is otherwise the thing somebody comes back to find missing.
    /// </remarks>
    [Fact]
    public void Letting_the_clock_go_answers_what_is_owed()
    {
        int answered = 0;

        var clock = new HintClock();

        var hint = clock.Gathered(
            "something", TimeSpan.FromHours(1), TimeSpan.FromHours(1), () => answered++);

        hint.Moved();

        clock.Dispose();

        Assert.Equal(1, answered);
        Assert.False(hint.Owed);
    }

    /// <summary>And an answer that throws costs that answer rather than the application.</summary>
    /// <remarks>
    /// It runs unattended with nobody watching, and a hint is never the thing an application
    /// should be lost over.
    /// </remarks>
    [Fact]
    public void An_answer_that_throws_costs_that_answer()
    {
        int answered = 0;

        var clock = new HintClock();

        clock.Gathered("a bad one", TimeSpan.Zero, TimeSpan.Zero,
            () => throw new InvalidOperationException("no")).Moved();

        clock.Gathered("a good one", TimeSpan.Zero, TimeSpan.Zero, () => answered++).Moved();

        clock.Dispose();

        Assert.Equal(1, answered);
    }

    /// <summary>Nothing is answered on the way out where nothing was owed.</summary>
    [Fact]
    public void Nothing_owed_is_nothing_answered_on_the_way_out()
    {
        int answered = 0;

        var clock = new HintClock();

        clock.Gathered("something", TimeSpan.Zero, TimeSpan.Zero, () => answered++);

        clock.Dispose();

        Assert.Equal(0, answered);
    }

    /// <summary>And the clock really does answer on its own, without anybody letting it go.</summary>
    /// <remarks>
    /// The one test here that waits on real time, since the clock is the one part that cannot be
    /// asked without it. Everything else about a hint is a comparison.
    /// </remarks>
    [Fact]
    public void The_clock_answers_on_its_own()
    {
        int answered = 0;

        using var clock = new HintClock();

        clock.Gathered("something", TimeSpan.Zero, TimeSpan.Zero, () => answered++).Moved();

        var waited = System.Diagnostics.Stopwatch.StartNew();

        while (answered == 0 && waited.Elapsed < TimeSpan.FromSeconds(5)) Thread.Sleep(5);

        Assert.Equal(1, answered);
    }
}
