using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace JingleBox2.Tracker.Machines;

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
public static class MachineWatch
{
    /// <summary>
    /// Says <paramref name="told"/> whenever the owner moves, one of its things moves, or the
    /// list of things is replaced.
    /// </summary>
    /// <param name="owner">The thing holding the list, which says when the selection moves.</param>
    /// <param name="items">The list, watched so a refill re-hooks what is in it.</param>
    /// <param name="told">What to say when any of it happens.</param>
    public static void Items<T>(
        INotifyPropertyChanged owner,
        INotifyCollectionChanged items,
        Func<IEnumerable<T>> held,
        Action told)
        where T : INotifyPropertyChanged
    {
        void Moved(object? sender, PropertyChangedEventArgs e) => told();

        owner.PropertyChanged += Moved;

        foreach (var one in held()) one.PropertyChanged += Moved;

        items.CollectionChanged += (_, _) =>
        {
            // Taken off first and put back on, rather than kept track of: a list that has been
            // refilled shares most of its contents with the one before it as often as not, and
            // the ones that have gone are unreachable and stop mattering along with the objects
            // they were.
            foreach (var one in held())
            {
                one.PropertyChanged -= Moved;
                one.PropertyChanged += Moved;
            }

            told();
        };
    }
}

/// <summary>
/// The one question every adapter asks before writing a setting.
/// </summary>
/// <remarks>
/// A knob reports the value it already has on every mouse move that did not cross a step, and a
/// song marked dirty by that is a song that can never be closed without being asked about.
/// </remarks>
public static class MachineSetting
{
    /// <summary>Writes it if it really is different, and says whether it was.</summary>
    public static bool Moved(double was, double now, Action write)
    {
        if (Math.Abs(was - now) < 1e-9) return false;

        write();

        return true;
    }
}
