namespace JingleBox2.Help.Records;

/// <summary>
/// A stretch of one block that is drawn the same way throughout.
/// </summary>
/// <remarks>
/// Two flags rather than a kind, because they are two independent questions and a key name in a
/// heading is both: <c>Ctrl+H</c> written in code marks inside a bold sentence is one span that
/// is drawn in the monospaced face and in bold, and an enum would need a member for the pair.
/// </remarks>
/// <param name="Text">The words themselves, with the marks taken off.</param>
/// <param name="Strong">Whether it was written between double asterisks.</param>
/// <param name="Code">Whether it was written between backticks, which is how a key is named.</param>
public sealed record MarkdownSpan(string Text, bool Strong = false, bool Code = false);
