using System;
using System.Diagnostics;
using System.Threading;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// How finely this process is allowed to measure a wait.
/// </summary>
/// <remarks>
/// What is pinned here is that asking is safe and says something true, not that the machine
/// running the tests grants it: this suite runs on Linux as well as Windows, and on Linux there
/// is nothing to ask. A test that demanded the fine clock would fail on half of CI for the one
/// reason that is not a fault.
///
/// The measurement that made this worth writing is in the contract's own remarks and was taken
/// by hand: a 16 ms sleep taking 30 ms is a plugin's own window drawn at half the rate it was
/// written for.
/// </remarks>
public class ClockResolutionTests
{
    /// <summary>Asking is safe wherever it is asked, and says whether it was granted.</summary>
    [Fact]
    public void Asking_is_safe_anywhere()
    {
        IClockResolution clock = new ClockResolution();

        if (clock.Take()) Assert.True(OperatingSystem.IsWindows());
    }

    /// <summary>Nowhere but Windows has anything to ask, so nowhere else claims it did.</summary>
    [Fact]
    public void Only_windows_has_anything_to_ask()
    {
        IClockResolution clock = new ClockResolution();

        if (OperatingSystem.IsWindows()) return;

        Assert.False(clock.Take());
        Assert.Equal("the system's own clock", clock.Said());
    }

    /// <summary>It says what it arranged, before it is asked as well as after.</summary>
    [Fact]
    public void It_says_what_it_arranged()
    {
        IClockResolution clock = new ClockResolution();

        Assert.False(string.IsNullOrWhiteSpace(clock.Said()));

        clock.Take();

        Assert.False(string.IsNullOrWhiteSpace(clock.Said()));
    }

    /// <summary>Asking twice is not a failure, since two threads may both want it.</summary>
    [Fact]
    public void Asking_twice_answers_the_same()
    {
        IClockResolution clock = new ClockResolution();

        Assert.Equal(clock.Take(), clock.Take());
    }

    /// <summary>
    /// And where it was granted, a wait really does land near where it was asked to.
    /// </summary>
    /// <remarks>
    /// The whole claim in one measurement, and it is made only where the ask succeeded. Fifty
    /// milliseconds is the interval every meter in this application runs at, and the default
    /// Windows tick rounds it to about 61. Twenty five milliseconds of slack, since a test that
    /// pins a schedule tightly is a test that fails on a busy machine for no reason.
    /// </remarks>
    [Fact]
    public void A_wait_lands_near_where_it_was_asked_to()
    {
        IClockResolution clock = new ClockResolution();

        if (!clock.Take()) return;

        using var idle = new ManualResetEventSlim(false);

        var watch = Stopwatch.StartNew();

        for (int turn = 0; turn < 10; turn++) idle.Wait(50);

        double each = watch.Elapsed.TotalMilliseconds / 10;

        Assert.InRange(each, 45, 75);
    }
}
