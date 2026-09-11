using JingleBox2.Audio.Routing;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The one sentence the input channel says about itself, for every way a gesture can end.
/// </summary>
/// <remarks>
/// **These could not be written before, and that is the argument for the module.** The wording
/// lived in five places around the page (<c>Listening</c>, <c>ApplyRoute</c> twice, <c>Agree</c>
/// and the switch's own setter), each writing the status line, last one winning by whenever a
/// thread came back. Two of those sentences were provably never seen by anybody: the loop was
/// overwritten a millisecond after it was written, and a machine refusing to move a source was
/// discarded on the following line.
///
/// Pulled out, every outcome is a value in and a string out, so the ones that only happen when a
/// machine refuses something can be read here rather than hoped for.
/// </remarks>
public class InputWordsTests
{
    /// <summary>The words, which hold nothing.</summary>
    private static InputWords Words() => new();

    /// <summary>A program playing somewhere, which is the kind that can be taken aside.</summary>
    private static readonly AudioRoute Firefox = new("app:1", "Firefox", AudioRouteKind.Application);

    /// <summary>What an output is playing.</summary>
    private static readonly AudioRoute Speakers = new("mon:1", "Speakers", AudioRouteKind.Monitor);

    /// <summary>Nothing chosen is nothing to say.</summary>
    /// <remarks>
    /// Empty rather than a sentence about having no source, since the page leaves the line alone
    /// where this is empty: whatever was last said is more use than being told nothing is chosen.
    /// </remarks>
    [Fact]
    public void No_source_says_nothing()
    {
        Assert.Equal("", Words().Line(null, true, true, InputAside.Nothing, true));
    }

    /// <summary>
    /// A source giving nothing is said first, before anything about hearing it.
    /// </summary>
    /// <remarks>
    /// Nothing else about a source matters while it is silent, and this is the one that tells
    /// somebody the picker is not the thing to go back to: it will be picked up on its own.
    /// </remarks>
    [Fact]
    public void A_silent_source_is_said_before_anything_else()
    {
        string said = Words().Line(Firefox, true, true, InputAside.Moved, connected: false);

        Assert.Contains("not giving anything", said);
    }

    /// <summary>
    /// **The loop is said, which it never was before.**
    /// </summary>
    /// <remarks>
    /// The case that was written and thrown away a millisecond later on every route change. It is
    /// the one state where the switch is on, everything is working, and there is deliberately no
    /// sound, so it is the sentence somebody most needs and the one nobody ever saw.
    /// </remarks>
    [Fact]
    public void The_loop_is_said()
    {
        string said = Words().Line(Speakers, true, canHear: false, InputAside.Nothing, true);

        Assert.Contains("loop", said);
    }

    /// <summary>
    /// **And a machine that could not move a source says so, which it also never did.**
    /// </summary>
    /// <remarks>
    /// Discarded on the very next line by the switch's own setter. It says the recording is
    /// happening as well, since it is: what failed is the taking aside and not the capture, and a
    /// sentence that only named the failure would read as nothing working.
    /// </remarks>
    [Fact]
    public void A_refusal_says_what_still_works()
    {
        string said = Words().Line(Firefox, true, true, InputAside.Refused, true);

        Assert.Contains("could not take it off its own output", said);
        Assert.Contains("being recorded", said);
    }

    /// <summary>A source taken aside says where it is coming out, which turns on the switch.</summary>
    [Fact]
    public void A_source_taken_aside_says_where_it_comes_out()
    {
        var words = Words();

        Assert.Contains("nowhere else", words.Line(Firefox, true, true, InputAside.Moved, true));
        Assert.Contains("is not being heard", words.Line(Firefox, false, true, InputAside.Moved, true));
    }

    /// <summary>And one left where it was still says whether it is being heard.</summary>
    [Fact]
    public void A_source_left_alone_still_says_whether_it_is_heard()
    {
        var words = Words();

        Assert.Contains("hearing it", words.Line(Speakers, true, true, InputAside.Nothing, true));
        Assert.DoesNotContain("hearing it", words.Line(Speakers, false, true, InputAside.Nothing, true));
    }

    /// <summary>
    /// A silent source wins over a loop, and a loop wins over a refusal.
    /// </summary>
    /// <remarks>
    /// The order is what somebody needs to know rather than the order things happened in, and it
    /// is worth pinning: a version that reported them the other way round would answer every
    /// question except the one being asked.
    /// </remarks>
    [Fact]
    public void The_order_is_by_what_matters_most()
    {
        var words = Words();

        Assert.Contains("not giving anything",
            words.Line(Speakers, true, canHear: false, InputAside.Refused, connected: false));

        Assert.Contains("loop",
            words.Line(Speakers, true, canHear: false, InputAside.Refused, connected: true));
    }
}
