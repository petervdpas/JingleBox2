using System;

namespace JingleBox2.Tracker;

/// <summary>
/// What was done in the tracker, so it can be undone.
/// </summary>
/// <remarks>
/// Whole copies rather than a description of each change, which is the right trade here and not
/// everywhere. A pattern is one array of value types with no allocation per cell, so a step is
/// a memory copy of a few kilobytes for an ordinary pattern; describing an edit instead would
/// mean a type per operation, an inverse for each, and the certainty that the nineteenth one
/// written would forget its inverse and undo would quietly corrupt a song. A copy cannot be
/// wrong about what it holds.
///
/// The unit is one call to <see cref="PatternEdit"/>, which is the whole reason that class is
/// the only door: one edit, one step, and a page of typing is a page of undos rather than one.
/// An edit that changed nothing leaves no step, worked out by noticing that the pattern still
/// holds what the last step kept, so a key that did nothing does not have to be undone.
///
/// Every step remembers which pattern it belongs to. Undo after switching patterns puts the
/// right one back and says which, rather than silently editing the one you are looking at.
///
/// It holds the song's patterns, so it is emptied when the song changes. A history that
/// outlived its song would hand somebody another song's notes.
///
/// Two kinds of step, one history, because Ctrl+Z means the last thing you did and not the last
/// thing you did of a particular kind. Typing a note is a pattern, and a step is its cells.
/// Taking an instrument out of a song is not: it renumbers every pattern that referred to it,
/// which is an edit across the whole document, so a step there is the song as its own file would
/// hold it. Keeping those apart in two histories would give one keystroke two meanings and a
/// person no way of knowing which they were about to get.
///
/// The kinds cost very different amounts, which is why they are kept apart at all. A pattern's
/// cells are a memory copy of a few kilobytes; a song is twelve to eighty. Serialising the whole
/// song for every keystroke would work and would be wasteful in exactly the place that must not
/// be.
/// </remarks>
public interface ITrackerHistory
{
    /// <summary>Raised when there is something different to say about what can be undone.</summary>
    event Action? Changed;

    /// <summary>True when there is something to take back.</summary>
    bool CanUndo { get; }

    /// <summary>True when something taken back can be put again.</summary>
    bool CanRedo { get; }

    /// <summary>What undo would take back, for a menu or a tooltip.</summary>
    string NextUndo { get; }

    /// <summary>And what redo would put back.</summary>
    string NextRedo { get; }

    /// <summary>
    /// Something is about to be done to a pattern. Called before it happens, not after.
    /// </summary>
    /// <remarks>
    /// Before, because what has to be kept is the state being left rather than the one being
    /// arrived at, and afterwards the first is gone.
    ///
    /// A step is not pushed when the edit before this one changed nothing: that step is worth no
    /// more than this one's and would cost somebody a keystroke to walk past, so it is reused
    /// and renamed instead.
    /// </remarks>
    void Taking(Pattern? pattern, string what);

    /// <summary>
    /// The song itself is about to change: an instrument, the order, how many tracks.
    /// </summary>
    /// <remarks>
    /// Called before, like the other one, and for the same reason. What separates these from a
    /// pattern edit is not how big they are but what they reach: taking an instrument out of a
    /// song renumbers every pattern that referred to it, and no snapshot of one pattern would
    /// put that back.
    ///
    /// Gathered by what it is and when, the same rule the instrument panel's knobs use and for
    /// the same reason: a fader dragged across its range says the mix changed a hundred times
    /// and is one thing a person did. The description is what identifies the gesture, which is
    /// enough, since two different edits do not share one.
    /// </remarks>
    /// <param name="song">
    /// The song as it stands before the edit, which is what the step keeps. Null when there is
    /// none open, and then nothing is taken.
    /// </param>
    /// <param name="what">
    /// What is about to be done, in the words a menu would show. Also what the gathering is by,
    /// so two goes at the same thing inside the window are one step.
    /// </param>
    /// <param name="onto">
    /// How to put a read-back song onto the one that is open. Everything in the tracker holds
    /// the live song, so a step cannot hand back a different object; the tracker knows how to
    /// pour one into the other and this does not.
    /// </param>
    void Taking(Song? song, string what, Func<Song, Song, bool> onto);

    /// <summary>Takes the last edit back. False when there is nothing to take back.</summary>
    /// <remarks>
    /// A step that will not go back is dropped rather than left at the top for somebody to press
    /// again and again. Everything under it is still good.
    /// </remarks>
    bool Undo();

    /// <summary>Puts back the last thing undone.</summary>
    bool Redo();

    /// <summary>
    /// Which pattern the next undo is about, so the view can go there first.
    /// </summary>
    /// <remarks>
    /// Nothing for a step about the song itself, which is not about one pattern and needs the
    /// view to stay where it is.
    /// </remarks>
    Pattern? UndoIsAbout { get; }

    /// <summary>And the next redo.</summary>
    Pattern? RedoIsAbout { get; }

    /// <summary>Empties it. For a song being closed, or opened.</summary>
    void Forget();
}
