using System.Collections.Generic;
using Avalonia.Input;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Records;

namespace JingleBox2.Shortcuts.Interfaces;

/// <summary>
/// The keys the pattern answers for itself, said once.
/// </summary>
/// <remarks>
/// **A key was being written down in three places and had to be written down in one.** The view's
/// own key handling decided which key did what, a hand-written list filled the card in SETTINGS,
/// and the help page said it again in prose, so a key added in the first place appeared nowhere
/// and a key changed there quietly disagreed with the other two. That is the same fault this
/// codebase already paid for with the shortcut map, and the answer written down then was that
/// there has to be **one list of what the application answers**.
///
/// So the table is here and the view is left with only which method each action calls, which is
/// not a second spelling of anything: the key is in one place, the words are in one place, and a
/// key added is a row added.
///
/// It is not the shortcut map and must not become one. The map is what somebody may change and
/// stores only what differs from the defaults; these cannot be changed at all, in the same way
/// the transport's own keys cannot, which is why they arrive on the same card.
/// </remarks>
public interface IPatternKeys
{
    /// <summary>Every key, in the order a reader meets them.</summary>
    IReadOnlyList<PatternKey> All { get; }

    /// <summary>
    /// What a key press asks for, or nothing when it is not one of these.
    /// </summary>
    /// <remarks>
    /// **The most particular row wins**, so <c>Ctrl+Shift+V</c> is itself and not a paste with a
    /// shift held. Written as an order of cases in a switch statement instead, that is a trap
    /// that has already been fallen into once here: <c>Ctrl+V</c> is matched by asking whether
    /// control is held, which holding shift as well does not stop being true, so the narrower key
    /// written underneath the wider one never fires.
    ///
    /// Shift is otherwise ignored, since on the cursor keys it says how far a block reaches
    /// rather than which key was pressed.
    /// </remarks>
    /// <param name="key">The key that went down.</param>
    /// <param name="held">What was held with it.</param>
    PatternAction Find(Key key, KeyModifiers held);

    /// <summary>
    /// One row per action for a page that lists what the application answers.
    /// </summary>
    /// <remarks>
    /// Per action rather than per key, so an action on two keys reads as one line naming both,
    /// which is what the octave is.
    /// </remarks>
    IReadOnlyList<SystemKey> Listed { get; }
}
