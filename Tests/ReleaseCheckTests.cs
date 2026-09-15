using System;
using System.Threading;
using System.Threading.Tasks;
using JingleBox2.Releases;
using JingleBox2.Releases.Interfaces;
using JingleBox2.Releases.Records;
using JingleBox2.UI.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Whether the release running is behind, and what is said about it, with no network in it.
/// </summary>
public sealed class ReleaseCheckTests
{
    /// <summary>A feed that answers what it was given, or throws where told to.</summary>
    private sealed class Feed(Release? answer, bool throws = false) : IReleaseFeed
    {
        /// <inheritdoc/>
        public Task<Release?> Latest(CancellationToken cancel = default) =>
            throws ? throw new InvalidOperationException("no network") : Task.FromResult(answer);
    }

    /// <summary>The page every release in these tests is on.</summary>
    private const string Page = "https://github.com/petervdpas/JingleBox2/releases/tag/v2.5.4";

    /// <summary>A release tagged as given.</summary>
    private static Release Latest(string tag) => new(tag, Page);

    /// <summary>A release after the one running is news, said with both versions.</summary>
    [Theory]
    [InlineData("2.5.3")]
    [InlineData("2.4.9")]
    [InlineData("1.0")]
    [InlineData("2.5.3+abc123")]
    public void A_newer_release_is_said_with_the_version_running_beside_it(string running)
    {
        var news = new ReleaseCheck(new Feed(null)).Compare(running, Latest("v2.5.4"));

        Assert.NotNull(news);
        Assert.Equal(StatusKind.Done, news!.Kind);
        Assert.StartsWith("JingleBox2 v2.5.4 is out.", news.Text);
        Assert.Equal(Page, news.Link);
    }

    /// <summary>Being up to date, or ahead, is the ordinary case and says nothing.</summary>
    [Theory]
    [InlineData("2.5.4")]
    [InlineData("v2.5.4")]
    [InlineData("2.5.4+deadbeef")]
    [InlineData("2.6.0")]
    [InlineData("3.0.0")]
    public void The_same_or_a_later_version_says_nothing(string running)
    {
        Assert.Null(new ReleaseCheck(new Feed(null)).Compare(running, Latest("v2.5.4")));
    }

    /// <summary>2.10 is after 2.9.</summary>
    [Fact]
    public void Versions_are_compared_as_numbers_and_not_as_words()
    {
        var check = new ReleaseCheck(new Feed(null));

        Assert.NotNull(check.Compare("2.9.0", Latest("v2.10.0")));
        Assert.Null(check.Compare("2.10.0", Latest("v2.9.0")));
    }

    /// <summary>2.5 and 2.5.0 are one release.</summary>
    [Fact]
    public void A_missing_third_number_is_nought_and_not_before_it()
    {
        Assert.Null(new ReleaseCheck(new Feed(null)).Compare("2.5.0", Latest("v2.5")));
    }

    /// <summary>A build from a checkout cannot be behind, and says which release is the latest.</summary>
    [Theory]
    [InlineData("0.0.0-local")]
    [InlineData("0.0.0-local+abc")]
    [InlineData("")]
    [InlineData("not a version")]
    public void A_build_with_no_release_version_says_which_release_is_the_latest(string running)
    {
        var news = new ReleaseCheck(new Feed(null)).Compare(running, Latest("v2.5.4"));

        Assert.NotNull(news);
        Assert.Equal(StatusKind.Plain, news!.Kind);
        Assert.Equal("This is a local build. The latest release is v2.5.4.", news.Text);
    }

    /// <summary>A tag that is not a version is nothing to compare against.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v")]
    [InlineData("vX.Y")]
    public void A_release_whose_tag_is_not_a_version_says_nothing(string tag)
    {
        Assert.Null(new ReleaseCheck(new Feed(null)).Compare("2.0.0", Latest(tag)));
    }

    /// <summary>No answer from the feed, which is a machine offline, says nothing.</summary>
    [Fact]
    public async Task Nothing_known_from_the_feed_says_nothing()
    {
        Assert.Null(await new ReleaseCheck(new Feed(null)).Check("2.0.0"));
    }

    /// <summary>The check compares what the feed answered.</summary>
    [Fact]
    public async Task What_the_feed_answers_is_what_is_compared()
    {
        var news = await new ReleaseCheck(new Feed(Latest("v2.5.4"))).Check("2.5.0");

        Assert.Equal("JingleBox2 v2.5.4 is out. This is v2.5.0.", news!.Text);
    }

    /// <summary>A feed that breaks its promise not to throw still leaves nothing said.</summary>
    [Fact]
    public async Task A_feed_that_throws_says_nothing_rather_than_taking_the_start_with_it()
    {
        Assert.Null(await new ReleaseCheck(new Feed(null, throws: true)).Check("2.0.0"));
    }
}
