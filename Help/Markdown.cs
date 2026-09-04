using System.Collections.Generic;
using System.Text;
using JingleBox2.Help.Enums;
using JingleBox2.Help.Interfaces;
using JingleBox2.Help.Records;

namespace JingleBox2.Help;

/// <inheritdoc/>
/// <remarks>
/// One pass down the lines for the blocks and one pass along each block for the marks, since
/// nothing here nests: a heading is a heading because of how its line begins, and a mark runs to
/// its closing mark or to the end of the block.
/// </remarks>
public sealed class Markdown : IMarkdown
{
    /// <summary>What a heading line begins with.</summary>
    private const char Hash = '#';

    /// <summary>What a list line begins with, after whatever it is indented by.</summary>
    private const string Dash = "- ";

    /// <summary>The marks, longest first, since two asterisks have to be tried before one.</summary>
    private const string Strong = "**";

    /// <inheritdoc cref="MarkdownSpan.Code"/>
    private const char Tick = '`';

    /// <inheritdoc/>
    public IReadOnlyList<MarkdownBlock> Read(string? text)
    {
        var blocks = new List<MarkdownBlock>();

        if (string.IsNullOrWhiteSpace(text)) return blocks;

        var paragraph = new List<string>();
        var bullet = new List<string>();

        foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.Trim();

            if (line.Length == 0)
            {
                Gather(blocks, paragraph, MarkdownKind.Paragraph);
                Gather(blocks, bullet, MarkdownKind.Bullet);
                continue;
            }

            if (line[0] == Hash)
            {
                Gather(blocks, paragraph, MarkdownKind.Paragraph);
                Gather(blocks, bullet, MarkdownKind.Bullet);
                blocks.Add(Heading(line));
                continue;
            }

            if (line.StartsWith(Dash))
            {
                Gather(blocks, paragraph, MarkdownKind.Paragraph);
                Gather(blocks, bullet, MarkdownKind.Bullet);
                bullet.Add(line[Dash.Length..]);
                continue;
            }

            if (bullet.Count > 0 && Indented(raw))
            {
                bullet.Add(line);
                continue;
            }

            Gather(blocks, bullet, MarkdownKind.Bullet);
            paragraph.Add(line);
        }

        Gather(blocks, paragraph, MarkdownKind.Paragraph);
        Gather(blocks, bullet, MarkdownKind.Bullet);

        return blocks;
    }

    /// <summary>
    /// Whether a line is written under the one before it rather than beside it.
    /// </summary>
    /// <remarks>
    /// This is the whole of how a list line goes on past the width of an editor. Without it the
    /// second line of a bullet is read as a paragraph, so the bullet stops in the middle of its
    /// own sentence and the rest of it stands underneath as prose, unmarked and out of line.
    /// That is exactly what it looked like the first time this was drawn.
    /// </remarks>
    /// <param name="raw">The line as it was written, with its indent still on it.</param>
    private bool Indented(string raw) => raw.Length > 0 && char.IsWhiteSpace(raw[0]);

    /// <summary>
    /// Turns whatever has piled up into one block and empties the pile.
    /// </summary>
    /// <remarks>
    /// The lines are joined with a space, which is the rule this whole thing is worth having
    /// for: where a line ends is where somebody's editor ran out, and where a block breaks is a
    /// blank line or the next thing that begins one. A join that kept the newlines would put a
    /// break in the middle of a sentence at every width but the one it was typed at.
    /// </remarks>
    /// <param name="blocks">What has been read so far.</param>
    /// <param name="lines">The words waiting to become a block.</param>
    /// <param name="kind">What they are waiting to become.</param>
    private void Gather(List<MarkdownBlock> blocks, List<string> lines, MarkdownKind kind)
    {
        if (lines.Count == 0) return;

        blocks.Add(new MarkdownBlock(kind, 1, Marks(string.Join(" ", lines))));

        lines.Clear();
    }

    /// <summary>
    /// One heading, as deep as it has hashes.
    /// </summary>
    /// <remarks>
    /// Held to six, which is as many as markdown has, so a line of hashes is a heading rather
    /// than a depth nothing can draw.
    /// </remarks>
    /// <param name="line">The line, beginning with at least one hash.</param>
    private MarkdownBlock Heading(string line)
    {
        int depth = 0;

        while (depth < line.Length && line[depth] == Hash) depth++;

        return new MarkdownBlock(MarkdownKind.Heading, depth > 6 ? 6 : depth, Marks(line[depth..].Trim()));
    }

    /// <summary>
    /// Splits one block's words where the way they are drawn changes.
    /// </summary>
    /// <remarks>
    /// A mark that is never closed is not a mark. It is put back into the words as the two
    /// characters it is made of and the reading carries on, which is what keeps an asterisk
    /// somebody meant as an asterisk from swallowing the rest of a paragraph.
    ///
    /// The two are read side by side rather than one inside the other, since a key name in bold
    /// is the only nesting anybody would want and it is spelled by putting the code marks inside
    /// the bold ones, which this reads as bold, then bold code, then bold again.
    /// </remarks>
    /// <param name="text">One block's words, with its own opening mark already taken off.</param>
    private IReadOnlyList<MarkdownSpan> Marks(string text)
    {
        var spans = new List<MarkdownSpan>();
        var plain = new StringBuilder();

        bool strong = false;
        int at = 0;

        while (at < text.Length)
        {
            if (text[at] == Tick && text.IndexOf(Tick, at + 1) is var close and > 0)
            {
                Flush(spans, plain, strong);
                spans.Add(new MarkdownSpan(text[(at + 1)..close], strong, Code: true));
                at = close + 1;
                continue;
            }

            if (Opens(text, at) && Closes(text, at, strong))
            {
                Flush(spans, plain, strong);
                strong = !strong;
                at += Strong.Length;
                continue;
            }

            plain.Append(text[at]);
            at++;
        }

        Flush(spans, plain, strong);

        return spans;
    }

    /// <summary>Whether the two characters here are the bold mark.</summary>
    /// <param name="text">The words being read.</param>
    /// <param name="at">Where the reading has got to.</param>
    private bool Opens(string text, int at) =>
        at + 1 < text.Length && text[at] == Strong[0] && text[at + 1] == Strong[1];

    /// <summary>
    /// Whether this bold mark has a partner, so that it is a mark rather than two asterisks.
    /// </summary>
    /// <remarks>
    /// A mark that is closing needs no partner: it is the partner. One that is opening is only a
    /// mark if the words go on to close it, which is what leaves an ordinary pair of asterisks
    /// in the middle of a sentence alone.
    /// </remarks>
    /// <param name="text">The words being read.</param>
    /// <param name="at">Where the reading has got to.</param>
    /// <param name="strong">Whether the reading is already inside a bold stretch.</param>
    private bool Closes(string text, int at, bool strong) =>
        strong || text.IndexOf(Strong, at + Strong.Length, System.StringComparison.Ordinal) >= 0;

    /// <summary>Puts whatever plain words have piled up down as a span, if there are any.</summary>
    /// <param name="spans">What has been read so far.</param>
    /// <param name="plain">The words waiting to become a span.</param>
    /// <param name="strong">Whether they were inside a bold stretch.</param>
    private void Flush(List<MarkdownSpan> spans, StringBuilder plain, bool strong)
    {
        if (plain.Length == 0) return;

        spans.Add(new MarkdownSpan(plain.ToString(), strong));

        plain.Clear();
    }
}
