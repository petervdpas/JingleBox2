using System.Collections.Generic;
using Avalonia.Input;
using JingleBox2.Shortcuts.Enums;

namespace JingleBox2.Shortcuts.Interfaces;

/// <summary>
/// Which key does what. One object, so there is one place to change it from.
/// </summary>
/// <remarks>
/// Apart from the dispatcher on purpose. What a key means is a setting, and settings are edited,
/// stored and shown; what happens when it is pressed is plumbing. Keeping them apart is what
/// makes a page that edits shortcuts a list bound to this and nothing else, rather than a page
/// that has to know how keys are delivered.
///
/// Only ever holds what somebody changed. A shortcut left alone is not written down, so the
/// defaults can be improved later and will reach anybody who never had an opinion about them.
///
/// One key does one job: putting an action on a keystroke takes that keystroke off whatever else
/// had it. Two actions on one key is a state a settings page should never be able to leave
/// somebody in, since only one of them could ever happen and which would be an accident of the
/// order they are stored in.
/// </remarks>
public interface IShortcutMap
{
    /// <summary>
    /// Something moved: a key was put on an action, taken off one, or the lot were put back.
    /// </summary>
    /// <remarks>
    /// The map is what knows, so the map is what says so. It exists because a page shortcut is
    /// drawn twice: on the settings page that set it, which is looking at it already, and as an
    /// underline in the tab strip along the top, which is not. Without this the strip would go on
    /// marking the letter of a key somebody had just moved, and would be right again only after
    /// whatever redrew it next.
    ///
    /// Raised only when something really changed, so a page that writes the same key back over
    /// itself does not have every tab redraw for nothing.
    /// </remarks>
    event System.EventHandler? Changed;

    /// <summary>Puts every one back to what it ships as.</summary>
    void Reset();

    /// <summary>What that action is on, or nothing when somebody has taken it off.</summary>
    KeyGesture? For(ShortcutAction action);

    /// <summary>What that action is on, as a person would write it.</summary>
    string Said(ShortcutAction action);

    /// <summary>Which action a keystroke asks for, or nothing.</summary>
    /// <remarks>
    /// The modifiers have to agree exactly. Ctrl+Shift+Z is not Ctrl+Z with something else held
    /// down; it is the redo key, and reading it as undo would be reading past the thing that
    /// tells them apart.
    /// </remarks>
    ShortcutAction? Match(Key key, KeyModifiers modifiers);

    /// <summary>
    /// Puts an action on a key, or takes it off keys altogether with null.
    /// </summary>
    /// <remarks>
    /// Whatever else was on that keystroke loses it, which is the rule that keeps one key doing
    /// one job.
    ///
    /// A system shortcut is refused. What Save does is a fact about this application rather than
    /// a preference, and the guard is here rather than on the page that draws the list because
    /// the page is not the only way in: the same call reads a settings file that may have been
    /// edited by hand.
    /// </remarks>
    void Set(ShortcutAction action, KeyGesture? gesture);

    /// <summary>
    /// Reads what was stored, leaving anything it does not recognise alone.
    /// </summary>
    /// <remarks>
    /// A settings file outlives the build that wrote it, so an action this version has never
    /// heard of and a keystroke it cannot parse are both passed over rather than refused: a
    /// line in a settings file is not worth a start that fails.
    ///
    /// An action stored with no keystroke at all is one somebody deliberately took off, and it
    /// stays off rather than coming back as its default.
    /// </remarks>
    void Take(IEnumerable<ShortcutBinding>? saved);

    /// <summary>
    /// Only what differs from the defaults, for storing.
    /// </summary>
    /// <remarks>
    /// Writing all of them down would freeze the defaults as of the day somebody first opened
    /// SETTINGS, and a shortcut improved afterwards would reach nobody who had ever saved.
    /// </remarks>
    List<ShortcutBinding> Given();
}
