using JingleBox2.Machines;
using JingleBox2.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// Where the track playing this instrument has got to, as a machine's panel sees it.
/// </summary>
/// <remarks>
/// The lamps and the pages are already worked out by <see cref="TrackLocationViewModel"/>,
/// which watches the tracker and decides which page is showing and which lamp is lit. All this
/// does is say the same thing in the words a described panel understands, so a machine can put
/// the row on its own face rather than the application drawing one under every panel.
///
/// The page buttons come across as the wording on their caps, not as objects. A panel is handed
/// what to write and which one is pressed, and pressing one comes back here by number: anything
/// more would be the machine's face holding the tracker's view models.
/// </remarks>
/// <param name="place">The lamps, already following whatever is playing.</param>
public sealed class TrackLocation(TrackLocationViewModel place) : IMachineLocation
{
    public bool Live => place.IsLive;

    public int Lamps => TrackLocationViewModel.PageLines;

    public int Lit => place.Lamp;

    public int FirstNumber => place.FirstNumber;

    /// <summary>What is written on each page's cap, rebuilt only when the pattern changes length.</summary>
    public IReadOnlyList<string> Pages => _pages ??= place.Pages.Select(one => one.Text).ToArray();

    private string[]? _pages;

    public int Page => place.Page;

    public void Show(int page)
    {
        if (page < 0 || page >= place.Pages.Count) return;

        place.Pages[page].PickCommand.Execute(null);
    }

    /// <summary>
    /// Told when the playhead moved, or the pattern changed length.
    /// </summary>
    /// <remarks>
    /// Subscribed on the first listener rather than in the constructor, the way the other
    /// adapters do it: a panel that never draws this row should not be holding the tracker's
    /// coat tails.
    /// </remarks>
    public event EventHandler? Changed
    {
        add
        {
            _changed += value;

            Listen();
        }
        remove => _changed -= value;
    }

    private EventHandler? _changed;

    private bool _listening;

    private void Listen()
    {
        if (_listening) return;

        _listening = true;

        place.PropertyChanged += Moved;

        if (place.Pages is INotifyCollectionChanged told)
            told.CollectionChanged += (_, _) =>
            {
                _pages = null;

                _changed?.Invoke(this, EventArgs.Empty);
            };
    }

    private void Moved(object? sender, PropertyChangedEventArgs e) =>
        _changed?.Invoke(this, EventArgs.Empty);
}
