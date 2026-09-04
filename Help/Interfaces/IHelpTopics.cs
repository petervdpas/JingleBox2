using System.Collections.Generic;
using JingleBox2.Help.Records;

namespace JingleBox2.Help.Interfaces;

/// <summary>
/// A folder of markdown files, read as the topics the help window shows.
/// </summary>
/// <remarks>
/// One file to a topic, named after its id, which is how a badge in a layout and a file on disc
/// find each other: <c>Topic="settings.engine"</c> beside the engine card is
/// <c>settings.engine.md</c> and nothing has to be registered anywhere. A topic added is a file
/// added.
///
/// It was ten string literals in a C# file before this, and prose in source is prose nobody
/// edits: it cannot be read without the braces around it, a paragraph rewritten is a rebuild,
/// and the one place the writing wants to be is the place a compiler is standing. Markdown in
/// files is what the rest of what ships already does, the same as a machine's manifest and a
/// controller's profile.
///
/// The shape of a file is the shape of a page and carries no header block of its own. The first
/// heading is the title, the paragraph under it is the summary the list shows and the tooltip
/// says, and everything after that is the page. So a file read by somebody who has never seen
/// this code is a page that reads correctly on its own, which is the whole reason for choosing
/// markdown over a format with fields in it.
/// </remarks>
public interface IHelpTopics
{
    /// <summary>
    /// Every topic in that folder, or none at all when the folder is not there.
    /// </summary>
    /// <remarks>
    /// A folder that is missing is an empty list rather than a throw, since the answer to a help
    /// folder that did not ship is a help window with nothing in it, and an application that
    /// will not start is a great deal worse than one that cannot explain itself.
    /// </remarks>
    /// <param name="folder">Where the files are.</param>
    IReadOnlyList<HelpTopic> In(string folder);
}
