namespace JingleBox2.Help.Enums;

/// <summary>
/// What one block of a help topic is: the closed list of shapes the help is written in.
/// </summary>
/// <remarks>
/// Deliberately short. This is the markdown this application understands rather than the
/// markdown there is, and every entry earns its place by being something a help topic actually
/// needs: a section, a paragraph, and a line in a list. Tables, quotes, links and images are
/// not here, and the day one is wanted it is a member and a case rather than a rewrite.
/// </remarks>
public enum MarkdownKind
{
    /// <summary>Ordinary prose. A run of lines with no blank line between them is one of these.</summary>
    Paragraph,

    /// <summary>A section title, written with hashes.</summary>
    Heading,

    /// <summary>One line of a list, written with a dash.</summary>
    Bullet
}
