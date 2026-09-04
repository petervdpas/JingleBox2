using System.Collections.Generic;
using JingleBox2.Help.Enums;

namespace JingleBox2.Help.Records;

/// <summary>
/// One paragraph, heading or list line of a help topic, with its words already read.
/// </summary>
/// <param name="Kind">What shape of block it is.</param>
/// <param name="Level">
/// How deep a heading is, from the number of hashes. One for anything that is not a heading,
/// which is a number nothing reads rather than a claim about a paragraph.
/// </param>
/// <param name="Spans">The words, split where the drawing of them changes.</param>
public sealed record MarkdownBlock(MarkdownKind Kind, int Level, IReadOnlyList<MarkdownSpan> Spans);
