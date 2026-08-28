using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using JingleBox2.Tracker.Machines.Interfaces;

namespace JingleBox2.Tracker.Machines;

/// <inheritdoc/>
public sealed class MachineWatch : IMachineWatch
{
    /// <inheritdoc/>
    public void Items<T>(
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
            foreach (var one in held())
            {
                one.PropertyChanged -= Moved;
                one.PropertyChanged += Moved;
            }

            told();
        };
    }
}
