using JingleBox2.Machines;
using JingleBox2.ViewModels;
using System;
using System.Linq;

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
public sealed class SamplerZones(ZoneMapViewModel map) : IMachineZones
{
    private bool _listening;

    public int Count => map.Zones.Count;

    public string Cap(int at) => Zone(at)?.Title ?? "";

    public int Low(int at) => Zone(at)?.Zone.Low ?? 0;

    public int High(int at) => Zone(at)?.Zone.High ?? 0;

    public int Root(int at) => Zone(at)?.Zone.Root ?? 0;

    public bool Filled(int at) => Zone(at)?.HasSound ?? false;

    /// <summary>Which zone the settings beside the map are about.</summary>
    public int Picked
    {
        get => map.Selected is { } one ? map.Zones.IndexOf(one) : -1;
        set => map.SelectAt(value);
    }

    /// <summary>
    /// Puts a zone where a drag has left it, all three numbers at once.
    /// </summary>
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

    /// <summary>
    /// Told when a zone moves, is picked, or is given a different recording.
    /// </summary>
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

    private EventHandler? _changed;

    private void Listen()
    {
        if (_listening) return;

        _listening = true;

        // A map refilled from a chop or from a folder of samples is a new set of zones, which is
        // why the list is watched as well as the zones in it. See <see cref="MachineWatch"/>.
        MachineWatch.Items<SampleZoneViewModel>(
            map, map.Zones, () => map.Zones, () => _changed?.Invoke(this, EventArgs.Empty));
    }

    private SampleZoneViewModel? Zone(int at) => map.Zones.ElementAtOrDefault(at);
}
