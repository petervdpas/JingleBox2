using JingleBox2.Tracker;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The one thing that knows whether the song has anything off disc, and what put it there.
/// </summary>
/// <remarks>
/// **Thirty edits, one answer.** A flag can only ever say that something happened, and told by
/// thirty callers it is thirty callers all saying the same nothing. A knob on a machine on the
/// rack marked a song holding no instruments at all as unsaved, and the one word that would have
/// ended it in a minute is <c>the mix</c>: a mixer link was answering from a page the mixer was
/// not on, and what the log could say at the time is true of every edit there is.
/// </remarks>
public sealed class SongWatchTests
{
    /// <summary>A fresh song has nothing in it and nothing to say about why.</summary>
    [Fact]
    public void A_fresh_one_is_clean_and_says_nothing()
    {
        var watch = new SongWatch();

        Assert.False(watch.Unsaved);
        Assert.Equal("", watch.Because);
    }

    /// <summary>An edit says what it was, and the answer keeps it.</summary>
    [Fact]
    public void An_edit_says_what_it_was()
    {
        var watch = new SongWatch();

        watch.Changed("the mix");

        Assert.True(watch.Unsaved);
        Assert.Equal("the mix", watch.Because);
    }

    /// <summary>
    /// **It moves once**, however many edits follow, which is what the buttons are drawn from.
    /// </summary>
    /// <remarks>
    /// A fader dragged across its range is a hundred edits and one change of this answer. Raised
    /// per edit, the two buttons that light on it would be rebuilt a hundred times for a gesture
    /// that changed one thing.
    /// </remarks>
    [Fact]
    public void It_moves_once_however_many_edits_follow()
    {
        var watch = new SongWatch();
        int moved = 0;

        watch.Moved += () => moved++;

        watch.Changed("the mix");
        watch.Changed("the mix");
        watch.Changed("the tempo");

        Assert.Equal(1, moved);
    }

    /// <summary>But the reason follows the last edit, so the second cause is not lost.</summary>
    /// <remarks>
    /// Which is the case this exists for: a song made unsaved by something you did, and then
    /// changed again by something you did not ask for, would otherwise read as the first all the
    /// way through.
    /// </remarks>
    [Fact]
    public void The_reason_follows_the_last_edit()
    {
        var watch = new SongWatch();

        watch.Changed("typing a note");
        watch.Changed("the mix");

        Assert.Equal("the mix", watch.Because);
    }

    /// <summary>Saving leaves it clean with nothing to explain, and says so once.</summary>
    [Fact]
    public void Saving_leaves_it_clean()
    {
        var watch = new SongWatch();
        int moved = 0;

        watch.Changed("the mix");

        watch.Moved += () => moved++;

        watch.Saved();

        Assert.False(watch.Unsaved);
        Assert.Equal("", watch.Because);
        Assert.Equal(1, moved);
    }

    /// <summary>And saving a song that was already clean moves nothing.</summary>
    [Fact]
    public void Saving_a_clean_one_moves_nothing()
    {
        var watch = new SongWatch();
        int moved = 0;

        watch.Moved += () => moved++;

        watch.Saved();

        Assert.Equal(0, moved);
    }

    /// <summary>An edit after a save says so again, since it is a change all over.</summary>
    [Fact]
    public void An_edit_after_a_save_moves_it_again()
    {
        var watch = new SongWatch();
        int moved = 0;

        watch.Moved += () => moved++;

        watch.Changed("the mix");
        watch.Saved();
        watch.Changed("the tempo");

        Assert.Equal(3, moved);
        Assert.Equal("the tempo", watch.Because);
    }
}
