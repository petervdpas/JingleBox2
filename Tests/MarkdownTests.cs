using System.Linq;
using JingleBox2.Help;
using JingleBox2.Help.Enums;
using JingleBox2.Help.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The markdown the help is written in, read into the blocks a page is drawn from.
/// </summary>
/// <remarks>
/// The unhappy half is most of this, because the input is prose written by hand and the way it
/// goes wrong is silent: an asterisk somebody meant as an asterisk swallowing the rest of a
/// paragraph, or a topic coming back empty because one line of it was odd. There is no such
/// thing here as a topic that will not open.
/// </remarks>
public class MarkdownTests
{
    /// <summary>The reader under test.</summary>
    private readonly IMarkdown _markdown = new Markdown();

    /// <summary>
    /// A run of lines is one paragraph, which is the rule the whole thing is worth having for.
    /// </summary>
    /// <remarks>
    /// Where a line ends is where somebody's editor ran out. Kept as written, prose breaks twice,
    /// once there and again where the window ran out, and comes out ragged at every width but
    /// the one it was typed at. That was in the help window for as long as it took to drag the
    /// splitter across.
    /// </remarks>
    [Fact]
    public void Lines_with_nothing_between_them_are_one_paragraph()
    {
        var blocks = _markdown.Read("one line\nand another\n");

        Assert.Single(blocks);
        Assert.Equal("one line and another", Words(blocks[0].Spans));
    }

    /// <summary>And a blank line is what breaks one.</summary>
    [Fact]
    public void A_blank_line_starts_a_new_paragraph()
    {
        var blocks = _markdown.Read("first\n\nsecond");

        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, block => Assert.Equal(MarkdownKind.Paragraph, block.Kind));
    }

    /// <summary>Hashes make a heading, and how many of them says how deep.</summary>
    [Theory]
    [InlineData("# Top", 1, "Top")]
    [InlineData("## Section", 2, "Section")]
    [InlineData("### Under it", 3, "Under it")]
    public void Hashes_make_a_heading(string line, int level, string said)
    {
        var block = Assert.Single(_markdown.Read(line));

        Assert.Equal(MarkdownKind.Heading, block.Kind);
        Assert.Equal(level, block.Level);
        Assert.Equal(said, Words(block.Spans));
    }

    /// <summary>A heading ends the paragraph above it without a blank line being needed.</summary>
    [Fact]
    public void A_heading_ends_what_was_above_it()
    {
        var blocks = _markdown.Read("some prose\n## Section\nmore prose");

        Assert.Equal(3, blocks.Count);
        Assert.Equal(MarkdownKind.Heading, blocks[1].Kind);
    }

    /// <summary>A dash makes a list line, and the dash itself is not part of it.</summary>
    [Fact]
    public void A_dash_makes_a_bullet()
    {
        var block = Assert.Single(_markdown.Read("- a thing"));

        Assert.Equal(MarkdownKind.Bullet, block.Kind);
        Assert.Equal("a thing", Words(block.Spans));
    }

    /// <summary>
    /// A list line goes on past the width of an editor, if the rest of it is indented.
    /// </summary>
    /// <remarks>
    /// Without this a bullet stops in the middle of its own sentence and the rest stands
    /// underneath as prose, unmarked and out of line with the list. It looked exactly like that
    /// the first time the help was drawn.
    /// </remarks>
    [Fact]
    public void A_bullet_goes_on_where_the_next_line_is_indented()
    {
        var block = Assert.Single(_markdown.Read("- the first half\n  and the second"));

        Assert.Equal(MarkdownKind.Bullet, block.Kind);
        Assert.Equal("the first half and the second", Words(block.Spans));
    }

    /// <summary>And a line that is not indented is the next thing rather than more of it.</summary>
    [Fact]
    public void A_line_hard_against_the_margin_ends_the_bullet()
    {
        var blocks = _markdown.Read("- a bullet\nand some prose");

        Assert.Equal(2, blocks.Count);
        Assert.Equal(MarkdownKind.Bullet, blocks[0].Kind);
        Assert.Equal(MarkdownKind.Paragraph, blocks[1].Kind);
    }

    /// <summary>Two bullets in a row are two bullets, not one long one.</summary>
    [Fact]
    public void Two_dashes_are_two_bullets()
    {
        var blocks = _markdown.Read("- one\n- two");

        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, block => Assert.Equal(MarkdownKind.Bullet, block.Kind));
    }

    /// <summary>Backticks name a key, which is what has it drawn in the pattern's face.</summary>
    [Fact]
    public void Backticks_name_a_key()
    {
        var block = Assert.Single(_markdown.Read("press `Ctrl+H` for help"));

        var key = Assert.Single(block.Spans, span => span.Code);

        Assert.Equal("Ctrl+H", key.Text);
        Assert.Equal("press Ctrl+H for help", Words(block.Spans));
    }

    /// <summary>Two asterisks either side make a stretch bold.</summary>
    [Fact]
    public void Asterisks_make_it_bold()
    {
        var block = Assert.Single(_markdown.Read("**mind this** and not that"));

        Assert.Equal("mind this", Assert.Single(block.Spans, span => span.Strong).Text);
        Assert.Equal("mind this and not that", Words(block.Spans));
    }

    /// <summary>
    /// A key name inside a bold sentence is both, which is why a span carries two flags.
    /// </summary>
    [Fact]
    public void A_key_inside_a_bold_sentence_is_both()
    {
        var block = Assert.Single(_markdown.Read("**press `Space` now**"));

        var key = Assert.Single(block.Spans, span => span.Code);

        Assert.True(key.Strong, "a key in a bold sentence is drawn bold too");
    }

    /// <summary>
    /// An asterisk somebody meant as an asterisk is left where it is.
    /// </summary>
    /// <remarks>
    /// This is the one that would be found by somebody's help text going bold halfway down and
    /// staying that way. An opening mark is only a mark if the words go on to close it.
    /// </remarks>
    [Fact]
    public void An_unclosed_mark_is_just_text()
    {
        var block = Assert.Single(_markdown.Read("two stars **and no more"));

        Assert.Equal("two stars **and no more", Words(block.Spans));
        Assert.DoesNotContain(block.Spans, span => span.Strong);
    }

    /// <summary>And so is a backtick with no partner.</summary>
    [Fact]
    public void An_unclosed_backtick_is_just_text()
    {
        var block = Assert.Single(_markdown.Read("a ` on its own"));

        Assert.Equal("a ` on its own", Words(block.Spans));
        Assert.DoesNotContain(block.Spans, span => span.Code);
    }

    /// <summary>Nothing at all is no blocks rather than one empty one.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n\n")]
    public void Nothing_reads_as_nothing(string? text)
    {
        Assert.Empty(_markdown.Read(text));
    }

    /// <summary>A line of hashes is a heading rather than a depth nothing can draw.</summary>
    [Fact]
    public void A_wall_of_hashes_is_still_a_heading()
    {
        var block = Assert.Single(_markdown.Read("########## deep"));

        Assert.Equal(MarkdownKind.Heading, block.Kind);
        Assert.Equal(6, block.Level);
    }

    /// <summary>A heading with nothing after the hashes is a heading that says nothing.</summary>
    [Fact]
    public void An_empty_heading_does_not_throw()
    {
        var block = Assert.Single(_markdown.Read("##"));

        Assert.Equal(MarkdownKind.Heading, block.Kind);
        Assert.Empty(block.Spans);
    }

    /// <summary>Windows line endings read the same as the other kind.</summary>
    /// <remarks>
    /// Help text is written in this repository and edited on both platforms, and a stray
    /// carriage return at the end of every line would be a character in every paragraph.
    /// </remarks>
    [Fact]
    public void Carriage_returns_are_not_words()
    {
        var blocks = _markdown.Read("## Section\r\n\r\nsome prose\r\n");

        Assert.Equal(2, blocks.Count);
        Assert.Equal("Section", Words(blocks[0].Spans));
        Assert.Equal("some prose", Words(blocks[1].Spans));
    }

    /// <summary>
    /// Every topic that ships reads as something.
    /// </summary>
    /// <remarks>
    /// The reader is only half of this: the other half is the prose, which is content this
    /// repository is answerable for the way a shipped preset is. A topic that came back with no
    /// blocks would open an empty page, and nothing anywhere would say why.
    /// </remarks>
    [Fact]
    public void Every_shipped_topic_reads()
    {
        foreach (var topic in new HelpText().All)
        {
            Assert.NotEmpty(_markdown.Read(topic.Body));
            Assert.False(string.IsNullOrWhiteSpace(topic.Title), topic.Id + " has no title");
        }
    }

    /// <summary>
    /// And no topic that ships is left holding a mark that was never closed.
    /// </summary>
    /// <remarks>
    /// Which is how the prose already in this application turned out to be written: one topic
    /// says something in double asterisks, and until the help was drawn rather than shown it
    /// rendered as the asterisks themselves.
    /// </remarks>
    [Fact]
    public void No_shipped_topic_has_a_stray_mark()
    {
        foreach (var topic in new HelpText().All)
        foreach (var block in _markdown.Read(topic.Body))
        foreach (var span in block.Spans)
        {
            Assert.DoesNotContain("**", span.Text);
            Assert.DoesNotContain("`", span.Text);
        }
    }

    /// <summary>What a block says, with the marks gone, for comparing against the plain words.</summary>
    /// <param name="spans">The stretches the block was read into.</param>
    private static string Words(System.Collections.Generic.IReadOnlyList<Help.Records.MarkdownSpan> spans) =>
        string.Concat(spans.Select(span => span.Text));
}
