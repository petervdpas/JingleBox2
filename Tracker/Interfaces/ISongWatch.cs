using System;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// The one thing that knows whether the song has anything in it that is not on disc, and what
/// put it there.
/// </summary>
/// <remarks>
/// **Every edit says what it was, and they all say it here.** There is no watcher over the song
/// noticing that it moved: a song is a few thousand cells, an order, a mix and a list of
/// instruments held by reference by half the application, and something comparing all of that
/// against the file on a clock would be work done constantly to answer a question the edits
/// already know the answer to.
///
/// So the edits tell it, and the point of there being one of these is that they tell **one**
/// thing. Told in thirty places and kept in thirty places, the only answer anybody could get is
/// that something happened; told in thirty places and kept here, the song can say what.
///
/// That is not a nicety. A knob on a machine on the rack marked a song holding no instruments at
/// all as unsaved, and the one word that would have ended it in a minute is <c>the mix</c>: a
/// mixer link was answering from a page the mixer was not on. What the log could say at the time
/// was "the song has something unsaved in it now", which is true of every edit there is.
///
/// It is the song's own fact rather than a page's, which is why it is here rather than on the
/// view model that used to hold the flag: closing the window, opening another song and cancelling
/// back to the file are all the same question asked of the same thing.
/// </remarks>
public interface ISongWatch
{
    /// <summary>True while there is something in the song that is not on disc.</summary>
    bool Unsaved { get; }

    /// <summary>
    /// What last put something in it, in the words the edit used, or nothing while it is clean.
    /// </summary>
    /// <remarks>
    /// The same words <c>Changing</c> gives the history, so one edit reads the same way whether
    /// it is being undone or being explained.
    /// </remarks>
    string Because { get; }

    /// <summary>Says the song changed, and why.</summary>
    /// <param name="what">
    /// What the edit was, in the words a person reads: the mix, the tempo, adding a pattern.
    /// </param>
    void Changed(string what);

    /// <summary>Says everything in it is on disc, which is what a save and an open both leave.</summary>
    void Saved();

    /// <summary>Raised when the answer moves, so whatever draws it can say so.</summary>
    /// <remarks>
    /// Only when it really moves. A fader dragged across its range is a hundred edits and one
    /// change of this answer, and a page rebuilt a hundred times for it would be a page nobody
    /// could work on.
    /// </remarks>
    event Action? Moved;
}
