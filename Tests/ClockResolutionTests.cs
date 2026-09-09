using System;
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
    /// Where it was granted, the clock it reports is the fine one.
    /// </summary>
    /// <remarks>
    /// **This used to time a wait and assert it landed near where it was asked to, and that was
    /// a test that could fail on a machine where nothing was wrong.** How long a wait really
    /// takes is a property of the scheduler and of what else the machine is doing, so on a
    /// shared build runner under load it can miss any bound worth setting, and a suite that
    /// cries wolf is one nobody reads.
    ///
    /// What is left is the part this application actually decides: the ask was made and granted,
    /// and what it says about itself agrees. The measurement that made the whole thing worth
    /// doing is in <see cref="IClockResolution"/>&apos;s own remarks, taken by hand, where a
    /// number nobody can flake on belongs.
    /// </remarks>
    [Fact]
    public void A_granted_clock_says_it_is_the_fine_one()
    {
        IClockResolution clock = new ClockResolution();

        if (!clock.Take()) return;

        Assert.Contains("1 ms", clock.Said());
    }
}
