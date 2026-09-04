namespace JingleBox2.Help.Interfaces;

/// <summary>
/// The shortcuts somebody can change, as they stand at this moment, ready to go into the
/// keyboard page.
/// </summary>
/// <remarks>
/// Every other line of every help topic is prose in a file. These four are not: Save, Delete,
/// Undo and Redo are a setting, edited in SETTINGS, so a file that spelled them out would go on
/// saying <c>Ctrl+Z</c> after somebody had moved undo to F2. That is the fault this codebase has
/// already paid for twice, two spellings of one fact drifting apart, and the answer is the same
/// both times: ask the one thing that knows.
///
/// So the keyboard page carries a hole where these go and the prose around it is written where
/// all the other prose is. A reader cannot tell which lines came from the file and which from
/// the map, which is right: from a chair they are all simply the keys.
///
/// Worked out when it is asked for rather than when one of these is made, since the map is read
/// out of the settings file after the first help window exists, and edited after that.
/// </remarks>
public interface IShortcutSheet
{
    /// <summary>The system's four, as markdown, one line to a shortcut.</summary>
    string System { get; }

    /// <summary>The pages along the top and the key each is on, or that it is on none.</summary>
    /// <remarks>
    /// A page with no key says so rather than being left out. Left out, this would be empty on a
    /// fresh installation and there would be nothing on the page saying a key can be put on a
    /// page at all.
    /// </remarks>
    string Menu { get; }
}
