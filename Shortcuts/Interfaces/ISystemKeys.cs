using System.Collections.Generic;
using JingleBox2.Shortcuts.Records;

namespace JingleBox2.Shortcuts.Interfaces;

/// <summary>
/// Every key the application answers that is not yours to change.
/// </summary>
/// <remarks>
/// They come from two places and that is the whole reason this exists. Four of them are actions
/// delivered through the map, so what they are on is read off it. The rest are keys written into
/// a door of their own, one class apiece: the transport's two, the pointing mode, and the help.
/// Nothing delivers those through the map and nothing ever should, since a door answers before
/// the map is consulted at all.
///
/// Listed as one thing because from a chair they are one thing. The settings page showed only
/// the map's four for about an hour, and the answer to "there are shortcuts missing" is not to
/// add them to the map, which would be two ways of delivering one keystroke, but to have one
/// list of what the application answers.
///
/// The keys that are settings say what they are on now rather than what they shipped on, since
/// that is the only reason to read a list like this at all.
/// </remarks>
public interface ISystemKeys
{
    /// <summary>All of them, in the order somebody reads them.</summary>
    IReadOnlyList<SystemKey> All { get; }
}
