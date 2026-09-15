using System;
using System.Collections.ObjectModel;
using JingleBox2.UI.Records;

namespace JingleBox2.UI.Interfaces;

/// <summary>
/// The toasts showing in the corner of the window, and when each one goes.
/// </summary>
/// <remarks>
/// Only what the bus marked as a toast is taken; everything else is the bar's alone. A toast
/// goes once it has stood for as long as SETTINGS, Looks says, or when it is clicked. A fault
/// stands until it is clicked, which is the bar's own rule and for the bar's own reason: a fault
/// you had to catch inside a few seconds is a fault you missed.
///
/// There is a limit to how many stand at once, and the oldest goes to make room, since a corner
/// that fills with toasts faster than they fall away covers the page they are meant to be
/// beside. The same words said again while they are still showing are one toast, moved to the
/// newest place with their time started again, the way the bus treats a message said twice.
///
/// Told the time rather than reading a clock, so how long something stands can be asked
/// without waiting for it.
/// </remarks>
public interface IToastShelf
{
    /// <summary>What is showing, oldest first.</summary>
    ReadOnlyObservableCollection<StatusMessage> Showing { get; }

    /// <summary>Puts a message up if it is a toast.</summary>
    /// <param name="message">Anything the bus said.</param>
    /// <returns>True where it was a toast and is now showing.</returns>
    bool Take(StatusMessage message);

    /// <summary>Takes down every toast that has stood long enough.</summary>
    /// <param name="now">The moment being asked about.</param>
    /// <param name="lasts">How long a toast stands.</param>
    /// <returns>True while something left showing will still go on its own.</returns>
    bool Settle(DateTime now, TimeSpan lasts);

    /// <summary>Takes one toast down, because it was clicked.</summary>
    /// <param name="message">The toast. One that is not showing is ignored.</param>
    void Dismiss(StatusMessage message);
}
