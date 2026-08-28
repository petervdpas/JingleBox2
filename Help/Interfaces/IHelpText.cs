using System.Collections.Generic;
using JingleBox2.Help.Records;

namespace JingleBox2.Help.Interfaces;

/// <summary>
/// Everything the app explains about itself, in one place, looked up by id.
/// </summary>
/// <remarks>
/// The ids are declared as constants and the table is written out in full, rather than being
/// built from a prefix and a name at the point of use. That way every id that exists appears
/// as a literal, so it can be searched for, and a page asking for one that was never written
/// says so instead of showing an empty window.
///
/// Prose lives here rather than in the pages so the pages stay about their controls, and so an
/// explanation can be improved without touching a layout.
/// </remarks>
public interface IHelpText
{








    /// <summary>The topic with that id, or null when nothing has been written for it.</summary>
    HelpTopic? Find(string? id);

    /// <summary>Everything there is, for the help window's list.</summary>
    IReadOnlyList<HelpTopic> All { get; }
}
