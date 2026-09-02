using JingleBox2.Rack.Faces;
using System;
using System.Collections.Generic;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Machines.Interfaces;

namespace JingleBox2.Tracker.Machines;

/// <inheritdoc/>
public sealed class MachineProjects : IMachineProjects
{
    /// <summary>The machines this installation has, by id.</summary>
    /// <remarks>
    /// Ordinary state on an ordinary object, which is the whole of the change: it used to be a
    /// static dictionary, and a static dictionary is one dictionary for the process however many
    /// racks are being read.
    /// </remarks>
    private readonly Dictionary<string, MachineProject> _found =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public void Keep(IEnumerable<MachineProject> machines)
    {
        _found.Clear();

        foreach (var machine in machines)
        {
            if (machine.Id.Length > 0) _found[machine.Id] = machine;
        }
    }

    /// <inheritdoc/>
    public MachineProject? For(string? id) =>
        id is { Length: > 0 } && _found.TryGetValue(id, out var machine) ? machine : null;

    /// <inheritdoc/>
    public Panel? PanelFor(string? id)
    {
        var machine = For(id);

        if (machine?.Panel.Root is not { } root) return null;

        return root.Children.Count == 0 && root.Parameter.Length == 0 ? null : machine.Panel;
    }

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
