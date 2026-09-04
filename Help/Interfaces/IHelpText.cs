using System.Collections.Generic;
using JingleBox2.Help.Records;

namespace JingleBox2.Help.Interfaces;

/// <summary>
/// Everything the app explains about itself, in one place, looked up by id.
/// </summary>
/// <remarks>
/// Prose lives here rather than in the pages, so the pages stay about their controls and an
/// explanation can be improved without touching a layout. It is one markdown file to a topic in
/// <c>help/</c> beside the program, named after the id a badge asks for, so adding a topic is
/// adding a file and nothing has to be registered.
///
/// The ids are also declared as constants, and both halves of that are the point. A file is
/// what somebody writes and edits; a constant is what a search finds, so every id that exists
/// appears somewhere a compiler and a grep can both see it, and a page asking for one that was
/// never written says so instead of showing an empty window. That the two agree is not left to
/// anybody's memory: <c>Tests/HelpTopicTests.cs</c> reads the folder and the constants and says
/// they are the same set, in both directions, since a constant with no file is a badge that
/// opens nothing and a file with no constant is a page somebody wrote and quietly lost.
/// </remarks>
public interface IHelpText
{
    /// <summary>The topic with that id, or null when nothing has been written for it.</summary>
    HelpTopic? Find(string? id);

    /// <summary>Everything there is, for the help window's list.</summary>
    IReadOnlyList<HelpTopic> All { get; }
}
