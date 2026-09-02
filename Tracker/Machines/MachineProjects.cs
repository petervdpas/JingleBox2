using System;
using System.Collections.Generic;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Machines.Interfaces;

namespace JingleBox2.Tracker.Machines;

/// <inheritdoc/>
public sealed class MachineProjects : RackDevices<MachineProject>, IMachineProjects
{
    /// <inheritdoc/>
    /// <remarks>
    /// Through the slot id, which is the one string a kind and a folder on disc have in common.
    /// A kind that has no slot has no machine to be missing, so it is never refused: that is the
    /// plugin, and everything else that is not one of the engines this build ships.
    /// </remarks>
    public bool Has(TrackerInstrumentKind kind)
    {
        string slot = JingleBox2.Tracker.Records.Machine.For(kind).SlotId;

        if (slot.Length == 0) return true;

        if (For(slot) is null) return false;

        return _rack is null || _rack.Contains(slot);
    }

    /// <summary>Which machines are on the rack, or nothing while nobody has said.</summary>
    private HashSet<string>? _rack;

    /// <inheritdoc/>
    public void OnRack(IEnumerable<string> slots) =>
        _rack = slots is null ? null : new HashSet<string>(slots, StringComparer.OrdinalIgnoreCase);
}
