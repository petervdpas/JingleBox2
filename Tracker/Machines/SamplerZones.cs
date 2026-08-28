using JingleBox2.Machines;
using JingleBox2.ViewModels;
using System;
using System.Linq;
using JingleBox2.Machines.Interfaces;
using JingleBox2.Tracker.Machines;
using JingleBox2.Tracker.Machines.Interfaces;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// A zone map, shown to a described panel as the picture on Zampler's face.
/// </summary>
/// <remarks>
/// What <see cref="KitPads"/> is for the machine with sixteen pads, this is for the machine with
/// as many zones as it turned out to need. The map already exists and is already being edited:
/// <see cref="ZoneMapViewModel"/> is what the strip has always been bound to. This is the same
/// map answering the fewer, simpler questions a panel drawn from a description asks.
///
/// Nothing is copied. Two views of one map that each held their own list would disagree the
/// first time an edge was dragged, and an edge is dragged on every movement of the pointer.
/// </remarks>
/// <param name="map">The map on the other side, which is the one the editor is already on.</param>
public sealed class SamplerZones(ZoneMapViewModel map) : IMachineZones
{
    /// <summary>Following a list of things and what each of them says.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IMachineWatch Watching = new MachineWatch();

    /// <summary>
    /// Whether the map is being watched yet.
    /// </summary>
    /// <remarks>
    /// A latch and not a count: the subscription goes on with the first listener and is never
    /// taken off, so a panel opened twice would otherwise hang two sets of handlers on one map.
    /// </remarks>
    private bool _listening;

    /// <inheritdoc/>
    public int Count => map.Zones.Count;

    /// <inheritdoc/>
    public string Cap(int at) => Zone(at)?.Title ?? "";

    /// <inheritdoc/>
    public int Low(int at) => Zone(at)?.Zone.Low ?? 0;

    /// <inheritdoc/>
    public int High(int at) => Zone(at)?.Zone.High ?? 0;

    /// <inheritdoc/>
    public int Root(int at) => Zone(at)?.Zone.Root ?? 0;

    /// <inheritdoc/>
    public bool Filled(int at) => Zone(at)?.HasSound ?? false;

    /// <inheritdoc/>
    /// <remarks>Minus one when nothing is picked, which is what a fresh map looks like.</remarks>
    public int Picked
    {
        get => map.Selected is { } one ? map.Zones.IndexOf(one) : -1;
        set => map.SelectAt(value);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// In the order that keeps them from crossing on the way. Each of these clamps itself
    /// against the other two as it is written, so setting a low edge above the current high
    /// edge would be turned round and then turned back, and the zone would arrive somewhere
    /// nobody dragged it to. Moving whichever edge is travelling outwards first leaves room for
    /// the one following it.
    /// </remarks>
    public void Move(int at, int low, int high, int root)
    {
        if (Zone(at) is not { } zone) return;

        if (low > zone.Zone.Low)
        {
            zone.High = high;
            zone.Low = low;
        }
        else
        {
            zone.Low = low;
            zone.High = high;
        }

        zone.Root = root;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Wired on the first listener rather than in the constructor, so a map nothing is watching
    /// costs nothing. The zones are watched as well as the map: an edge dragged is a change to
    /// the zone, and the map says nothing when one moves.
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

    /// <summary>Everyone told when a zone moves, is picked, or is given a different recording.</summary>
    private EventHandler? _changed;

    /// <summary>
    /// Puts the subscription on, once.
    /// </summary>
    /// <remarks>
    /// A map refilled from a chop or from a folder of samples is a new set of zones, which is
    /// why the list is watched as well as the zones in it: see
    /// <see cref="MachineWatch"/>.
    /// </remarks>
    private void Listen()
    {
        if (_listening) return;

        _listening = true;

        Watching.Items<SampleZoneViewModel>(
            map, map.Zones, () => map.Zones, () => _changed?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>That zone, or nothing when the number is outside the map.</summary>
    /// <remarks>
    /// A described panel can name more zones than the map has, and a number that was right one
    /// frame ago is ordinary rather than a fault while a map is being refilled, so every reader
    /// above holds against nothing.
    /// </remarks>
    private SampleZoneViewModel? Zone(int at) => map.Zones.ElementAtOrDefault(at);
}
