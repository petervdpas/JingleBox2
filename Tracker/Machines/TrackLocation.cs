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
    /// <inheritdoc/>
    public bool Live => place.IsLive;

    /// <inheritdoc/>
    public int Lamps => TrackLocationViewModel.PageLines;

    /// <inheritdoc/>
    public int Lit => place.Lamp;

    /// <inheritdoc/>
    public int FirstNumber => place.FirstNumber;

    /// <inheritdoc/>
    /// <remarks>
    /// Kept once it has been worked out and thrown away when the list of pages is replaced,
    /// which is the only thing that changes it. This is read while the panel draws, so building
    /// a fresh array of strings per frame is a cost nobody asked for.
    /// </remarks>
    public IReadOnlyList<string> Pages => _pages ??= place.Pages.Select(one => one.Text).ToArray();

    /// <summary>The caps as last worked out, or nothing when they need working out again.</summary>
    private string[]? _pages;

    /// <inheritdoc/>
    public int Page => place.Page;

    /// <inheritdoc/>
    /// <remarks>
    /// A page number outside the list is ignored rather than clamped. A panel is drawn from a
    /// description that can have more page buttons on it than the pattern has pages, and a press
    /// on one of those should do nothing rather than jump somewhere.
    /// </remarks>
    public void Show(int page)
    {
        if (page < 0 || page >= place.Pages.Count) return;

        place.Pages[page].PickCommand.Execute(null);
    }

    /// <inheritdoc/>
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

    /// <summary>Everyone told when the playhead moved or the pattern changed length.</summary>
    private EventHandler? _changed;

    /// <summary>Whether the lamps are being watched yet. A latch, never taken off again.</summary>
    private bool _listening;

    /// <summary>
    /// Puts the subscriptions on, once.
    /// </summary>
    /// <remarks>
    /// A pattern of a different length is a different set of pages, so the caps are dropped when
    /// the list is replaced and worked out again on the next draw.
    /// </remarks>
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

    /// <summary>Anything on the lamps moving is the whole panel worth drawing again.</summary>
    private void Moved(object? sender, PropertyChangedEventArgs e) =>
        _changed?.Invoke(this, EventArgs.Empty);
}
