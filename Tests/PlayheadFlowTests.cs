using System;
using JingleBox2.UI;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// How evenly the picture stepped, measured where it was drawn rather than where it was sent.
/// </summary>
/// <remarks>
/// **The number that matters is the spread and not the mean.** A transport that is keeping time
/// perfectly and a picture that limps read the same mean, since the limp is one step early and the
/// next one late: what tells them apart is how far the worst of them fell from that mean. So the
/// test that earns this file is the one where the mean is exactly right and the answer is still
/// sixteen milliseconds out.
///
/// The moment is handed in throughout, so five seconds of transport is a handful of arithmetic.
/// </remarks>
public sealed class PlayheadFlowTests
{
    /// <summary>A window short enough to close inside a test.</summary>
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(1);

    /// <summary>A moment, counted in milliseconds from when the page came up.</summary>
    private static TimeSpan At(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    /// <summary>Nothing is said while the window is still open.</summary>
    [Fact]
    public void Nothing_is_said_before_the_window_closes()
    {
        var flow = new PlayheadFlow(Window);

        for (int step = 0; step < 8; step++) Assert.Null(flow.Stepped(At(step * 100)));
    }

    /// <summary>Steps that come evenly are nought out, which is what a page nobody complains about does.</summary>
    [Fact]
    public void Even_steps_are_nothing_out()
    {
        var flow = new PlayheadFlow(Window);

        string? said = null;

        for (int step = 0; step <= 8; step++) said ??= flow.Stepped(At(step * 125));

        Assert.Equal(
            "tracker: the picture stepped 8 time(s), mean 125.0 ms, 125.0 to 125.0, worst 0.0 ms out",
            said);
    }

    /// <summary>
    /// **And a limp is the same mean with a spread on it**, which is the whole reason for the line.
    /// </summary>
    /// <remarks>
    /// A line of 125 milliseconds is seven and a half frames of a screen at sixty, so a picture
    /// that can only move on a frame shows one step after seven of them and the next after eight,
    /// for ever. The transport is not late by a microsecond and the page still limps.
    /// </remarks>
    [Fact]
    public void A_limp_is_the_right_mean_with_a_spread_on_it()
    {
        var flow = new PlayheadFlow(Window);

        double[] gaps = { 109, 141, 109, 141, 109, 141, 109, 141 };

        double at = 0;
        string? said = flow.Stepped(At(at));

        foreach (double gap in gaps)
        {
            at += gap;
            said ??= flow.Stepped(At(at));
        }

        Assert.Equal(
            "tracker: the picture stepped 8 time(s), mean 125.0 ms, 109.0 to 141.0, worst 16.0 ms out",
            said);
    }

    /// <summary>A run shorter than a window still says what it saw, since somebody pressed stop.</summary>
    [Fact]
    public void A_short_run_is_said_when_it_stops()
    {
        var flow = new PlayheadFlow(Window);

        flow.Stepped(At(0));
        flow.Stepped(At(125));

        Assert.Equal(
            "tracker: the picture stepped 1 time(s), mean 125.0 ms, 125.0 to 125.0, worst 0.0 ms out",
            flow.Stopped());
    }

    /// <summary>One step is not a gap, so a run of one says nothing.</summary>
    [Fact]
    public void One_step_is_not_a_gap()
    {
        var flow = new PlayheadFlow(Window);

        flow.Stepped(At(0));

        Assert.Null(flow.Stopped());
    }

    /// <summary>And a stop with nothing behind it says nothing either.</summary>
    [Fact]
    public void Nothing_that_never_moved_says_nothing() => Assert.Null(new PlayheadFlow(Window).Stopped());

    /// <summary>
    /// **The quiet between two runs is not a step that was late.**
    /// </summary>
    /// <remarks>
    /// Counted, it would be the worst gap in every session by a factor of a hundred and the line
    /// would say the picture limps on a machine where it does not.
    /// </remarks>
    [Fact]
    public void The_gap_across_a_stop_is_not_a_step()
    {
        var flow = new PlayheadFlow(Window);

        flow.Stepped(At(0));
        flow.Stepped(At(125));
        flow.Stopped();

        string? said = null;

        for (int step = 0; step <= 8; step++) said ??= flow.Stepped(At(10000 + step * 125));

        Assert.Equal(
            "tracker: the picture stepped 8 time(s), mean 125.0 ms, 125.0 to 125.0, worst 0.0 ms out",
            said);
    }

    /// <summary>A window that has closed starts again rather than reporting on the last one.</summary>
    [Fact]
    public void A_window_that_closed_starts_again()
    {
        var flow = new PlayheadFlow(Window);

        for (int step = 0; step <= 8; step++) flow.Stepped(At(step * 125));

        string? second = null;

        for (int step = 9; step <= 16; step++) second ??= flow.Stepped(At(step * 125));

        Assert.Equal(
            "tracker: the picture stepped 8 time(s), mean 125.0 ms, 125.0 to 125.0, worst 0.0 ms out",
            second);
    }
}
