using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.UI;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;
using System;
using System.Collections.ObjectModel;

namespace JingleBox2.ViewModels;

/// <summary>
/// The toasts in the corner of the main window, fed by the same bus the bar along the bottom is.
/// </summary>
/// <remarks>
/// What goes up and what comes down is <see cref="IToastShelf"/>'s; this is the wiring around it,
/// the thread a message arrives on and the clock that takes toasts away. The clock runs only
/// while a toast is standing that will go on its own, since nothing showing most of the time is
/// the ordinary case.
/// </remarks>
public sealed partial class ToastsViewModel : ObservableObject
{
    /// <summary>Often enough that a toast goes when it says it will.</summary>
    private const int TickMs = 250;

    /// <summary>What is showing and when it goes.</summary>
    private readonly IToastShelf _shelf;

    /// <summary>How long a toast stands, asked afresh so a change in SETTINGS lands at once.</summary>
    private readonly Func<TimeSpan> _lasts;

    /// <summary>Takes toasts down, while there is one standing that will go on its own.</summary>
    private readonly DispatcherTimer _clock;

    /// <summary>Wires the toasts to the bus.</summary>
    /// <param name="bus">Where everything in the app says what it has to say.</param>
    /// <param name="lasts">How long a toast stands.</param>
    /// <param name="shelf">What is showing, or a fresh shelf.</param>
    public ToastsViewModel(StatusBus bus, Func<TimeSpan> lasts, IToastShelf? shelf = null)
    {
        _shelf = shelf ?? new ToastShelf();
        _lasts = lasts;

        _clock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickMs) };
        _clock.Tick += (_, _) => Settle();

        bus.Posted += (_, message) =>
        {
            if (message.Toast) Dispatcher.UIThread.Post(() => Arrived(message));
        };
    }

    /// <summary>What is showing, oldest first.</summary>
    public ReadOnlyObservableCollection<StatusMessage> Showing => _shelf.Showing;

    /// <summary>A toast arrived: put it up and start the clock that will take it down.</summary>
    private void Arrived(StatusMessage message)
    {
        if (_shelf.Take(message) && !_clock.IsEnabled) _clock.Start();
    }

    /// <summary>Takes down what has stood long enough, and stops the clock once nothing will go on its own.</summary>
    private void Settle()
    {
        if (!_shelf.Settle(DateTime.Now, _lasts())) _clock.Stop();
    }

    /// <summary>Takes one toast down, because it was clicked.</summary>
    [RelayCommand]
    private void Dismiss(StatusMessage? message)
    {
        if (message != null) _shelf.Dismiss(message);
    }
}
