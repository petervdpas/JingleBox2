using System;
using System.Collections.ObjectModel;
using System.Linq;
using JingleBox2.UI.Enums;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class ToastShelf : IToastShelf
{
    /// <summary>How many toasts stand at once.</summary>
    public const int MostShown = 4;

    /// <summary>What is showing, oldest first, which the read-only view is over.</summary>
    private readonly ObservableCollection<StatusMessage> _showing = new();

    /// <summary>Makes an empty shelf.</summary>
    public ToastShelf() => Showing = new ReadOnlyObservableCollection<StatusMessage>(_showing);

    /// <inheritdoc/>
    public ReadOnlyObservableCollection<StatusMessage> Showing { get; }

    /// <inheritdoc/>
    public bool Take(StatusMessage message)
    {
        if (message is not { Toast: true }) return false;

        foreach (var same in _showing.Where(m => m.Text == message.Text && m.Kind == message.Kind).ToList())
            _showing.Remove(same);

        _showing.Add(message);

        while (_showing.Count > MostShown) _showing.RemoveAt(0);

        return true;
    }

    /// <inheritdoc/>
    public bool Settle(DateTime now, TimeSpan lasts)
    {
        foreach (var gone in _showing.Where(m => m.Kind != StatusKind.Fault && now - m.At >= lasts).ToList())
            _showing.Remove(gone);

        return _showing.Any(m => m.Kind != StatusKind.Fault);
    }

    /// <inheritdoc/>
    public void Dismiss(StatusMessage message) => _showing.Remove(message);
}
