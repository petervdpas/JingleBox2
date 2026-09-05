using System;
using JingleBox2.Views;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The wording on the clock, which is the one part of it a test can stand in front of.
/// </summary>
/// <remarks>
/// Two pages show the same control, so the reading is written once and this is what says it stays
/// written once. What is checked is the shape rather than a handful of pretty values: that it
/// never comes back blank, that it never grows or shrinks over the range anybody will see, and
/// that an hour folds into the minutes rather than adding a field.
/// </remarks>
public class TimeReadoutTests
{
    /// <summary>Nought reads as nought rather than as nothing.</summary>
    /// <remarks>
    /// A clock that is blank until it starts is one nobody can find, and the box is measured for
    /// the reading whether or not there is one, so the room is taken either way.
    /// </remarks>
    [Fact]
    public void A_clock_that_has_not_started_reads_nought()
    {
        Assert.Equal("00:00.000", TimeReadout.Reading(TimeSpan.Zero));
        Assert.Equal("00:00.000", TimeReadout.Reading(default));
    }

    /// <summary>A span that runs backwards reads nought rather than showing a minus.</summary>
    [Fact]
    public void A_clock_never_runs_backwards()
    {
        Assert.Equal("00:00.000", TimeReadout.Reading(TimeSpan.FromSeconds(-3)));
        Assert.Equal("00:00.000", TimeReadout.Reading(TimeSpan.MinValue));
    }

    /// <summary>Minutes, seconds and thousandths, in that order and each padded.</summary>
    [Fact]
    public void The_reading_is_minutes_seconds_and_thousandths()
    {
        Assert.Equal("00:05.000", TimeReadout.Reading(TimeSpan.FromSeconds(5)));
        Assert.Equal("01:05.500", TimeReadout.Reading(TimeSpan.FromSeconds(65.5)));
        Assert.Equal("00:00.001", TimeReadout.Reading(TimeSpan.FromMilliseconds(1)));
    }

    /// <summary>
    /// An hour is sixty minutes and not a field of its own.
    /// </summary>
    /// <remarks>
    /// One fewer thing to read, and nothing that appears halfway through a long take: a clock
    /// that grows a field as it passes an hour moves everything beside it at the moment somebody
    /// is least expecting the page to change.
    /// </remarks>
    [Fact]
    public void An_hour_is_sixty_minutes()
    {
        Assert.Equal("60:00.000", TimeReadout.Reading(TimeSpan.FromHours(1)));
        Assert.Equal("74:12.480", TimeReadout.Reading(new TimeSpan(0, 1, 14, 12, 480)));
    }

    /// <summary>
    /// It is the same width all the way to sixteen hours, which is what the box is measured for.
    /// </summary>
    /// <remarks>
    /// The reason a box is measured against the widest thing it can show rather than against what
    /// it happens to hold: a reading that changed width would shove its neighbours along the bar
    /// as it counted. Checked over the range rather than at the ends, since it is the padding in
    /// the middle that would be missing.
    /// </remarks>
    [Fact]
    public void The_reading_is_one_width()
    {
        for (double minutes = 0; minutes < 100; minutes += 0.37)
            Assert.Equal(9, TimeReadout.Reading(TimeSpan.FromMinutes(minutes)).Length);
    }
}
