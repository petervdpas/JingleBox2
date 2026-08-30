using System;
using System.Globalization;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// How a saved song says when it was last written, in the list you pick one from.
/// </summary>
/// <remarks>
/// A list of songs is read to find the one you were working on, so the date is what is scanned
/// rather than what is read. Said three ways for that reason: the time alone today, the day and
/// the time this week, the date for anything older. A full date and time on every row makes
/// every row the same width of digits and none of them worth looking at.
///
/// The wording is here rather than in the view because it is a rule about what to show and not
/// about how to draw it, and because a rule in a template cannot be put a question to.
/// </remarks>
public class SongListTests
{
    /// <summary>One saved at that moment, with everything else about it beside the point.</summary>
    private static SongFile Saved(DateTime when) => new("song", "/songs/song.jibx", "", when);

    /// <summary>Saved today, so the time alone: the date would say nothing you did not know.</summary>
    [Fact]
    public void Today_is_the_time_alone()
    {
        var when = DateTime.Now.Date.AddHours(14).AddMinutes(32);

        Assert.Equal(when.ToString("t", CultureInfo.CurrentCulture), Saved(when).SavedText);
    }

    /// <summary>This week, the day and the time, which is how anybody says it out loud.</summary>
    [Fact]
    public void This_week_is_the_day_and_the_time()
    {
        var when = DateTime.Now.AddDays(-2);

        Assert.Equal(when.ToString("ddd HH:mm", CultureInfo.CurrentCulture), Saved(when).SavedText);
    }

    /// <summary>And older than that, the date, since the day of the week has stopped helping.</summary>
    [Fact]
    public void Older_than_a_week_is_the_date()
    {
        var when = DateTime.Now.AddDays(-40);

        Assert.Equal(when.ToString("d", CultureInfo.CurrentCulture), Saved(when).SavedText);
    }

    /// <summary>
    /// A file that would not say when it was written has nothing to show rather than 1601 or
    /// today. The row simply leaves the column empty.
    /// </summary>
    [Fact]
    public void A_file_with_no_date_says_nothing()
    {
        var none = new SongFile("song", "/songs/song.jibx");

        Assert.False(none.HasSaved);
        Assert.Equal("", none.SavedText);

        Assert.False(Saved(DateTime.MinValue).HasSaved);
        Assert.Equal("", Saved(DateTime.MinValue).SavedText);
    }

    /// <summary>And a song that has a date says so, which is what the row asks before drawing.</summary>
    [Fact]
    public void A_file_with_a_date_says_so()
    {
        Assert.True(Saved(DateTime.Now).HasSaved);
        Assert.NotEqual("", Saved(DateTime.Now).SavedText);
    }

    /// <summary>The description is still its own question, and an empty one still reads as none.</summary>
    [Fact]
    public void The_date_did_not_disturb_the_description()
    {
        Assert.False(new SongFile("song", "/p").HasDescription);
        Assert.True(new SongFile("song", "/p", "about it").HasDescription);
        Assert.Equal("song", new SongFile("song", "/p", "about it", DateTime.Now).ToString());
    }
}
