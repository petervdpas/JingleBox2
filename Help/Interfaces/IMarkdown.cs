using System.Collections.Generic;
using JingleBox2.Help.Records;

namespace JingleBox2.Help.Interfaces;

/// <summary>
/// The markdown this application understands, read into the blocks a page is drawn from.
/// </summary>
/// <remarks>
/// A subset, and a small one on purpose: sections, paragraphs, list lines, bold and the code
/// marks a key name is written in. That is what the help is written in and there is no second
/// author to surprise us, so the whole of the grammar fits on a page and can be read here rather
/// than in somebody else's specification.
///
/// Ours rather than a package, and it was a package for about ten minutes. The only build of the
/// obvious one that works with this toolkit is an alpha, and this is an application whose release
/// is the one build nobody gets to take back. What it would have bought is the half of markdown
/// the help does not use.
///
/// The rule that earns the whole thing on its own is the plain one every markdown has: a run of
/// lines with nothing blank between them is one paragraph, and where the line ends is not where
/// the paragraph breaks. Written out as a string with newlines in it and shown in a control that
/// wraps, prose breaks twice, once where somebody typed and again where the window ran out, and
/// comes out ragged at every width but the one it was written for. That was in this help window
/// for exactly as long as it took to drag the splitter.
///
/// It reads rather than refuses. A line nobody meant as markdown is a paragraph, an unclosed
/// mark is the text it is made of, and there is no such thing as a topic that will not open:
/// this is prose about an audio program, and the worst a mistake in it may cost is a word in the
/// wrong weight.
/// </remarks>
public interface IMarkdown
{
    /// <summary>The blocks that text is made of, in the order they are read.</summary>
    /// <param name="text">The topic's body, as it is written down.</param>
    IReadOnlyList<MarkdownBlock> Read(string? text);
}
