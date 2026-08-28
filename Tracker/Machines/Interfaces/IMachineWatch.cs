using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace JingleBox2.Tracker.Machines.Interfaces;

/// <summary>
/// What every adapter that hands a list to a panel has to arrange, in one place.
/// </summary>
/// <remarks>
/// A panel is told "something moved" and reads everything again, which is right, because the
/// things that move here move on every note and on every movement of the pointer. What is
/// awkward is where the telling comes from: the list itself says nothing when one of the things
/// in it changes, so each of them has to be watched too, and a list that is refilled leaves the
/// old ones being watched and the new ones not.
///
/// The pads of a kit and the zones of a map are different things and are described in their own
/// words. This is not those words: it is the wiring underneath both of them, and there is no
/// version of it that is right for one and wrong for the other.
/// </remarks>
public interface IMachineWatch
{
    /// <summary>
    /// Says <paramref name="told"/> whenever the owner moves, one of its things moves, or the
    /// list of things is replaced.
    /// </summary>
    /// <remarks>
    /// Nothing is ever unhooked. What is watched here lives exactly as long as the adapter that
    /// asked for it, and an adapter lives as long as the panel, so an unsubscribe would only
    /// ever run at the moment both are being let go of anyway.
    ///
    /// On a refill the handler is taken off every item and put straight back on, rather than a
    /// record being kept of which ones already have it. A refilled list shares most of its
    /// contents with the one before it as often as not, and the ones that have gone are
    /// unreachable and stop mattering along with the objects they were.
    /// </remarks>
    /// <typeparam name="T">What is in the list, each of which says when it moves.</typeparam>
    /// <param name="owner">The thing holding the list, which says when the selection moves.</param>
    /// <param name="items">The list, watched so a refill re-hooks what is in it.</param>
    /// <param name="held">The list again, read afresh each time, since a refill replaces it.</param>
    /// <param name="told">What to say when any of it happens.</param>
    void Items<T>(
        INotifyPropertyChanged owner,
        INotifyCollectionChanged items,
        Func<IEnumerable<T>> held,
        Action told)
        where T : INotifyPropertyChanged;
}
