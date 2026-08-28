using JingleBox2.Machines;
using System;
using System.Collections.Generic;
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
    public MachinePanel? PanelFor(string? id)
    {
        var machine = For(id);

        if (machine?.Panel.Root is not { } root) return null;

        return root.Children.Count == 0 && root.Parameter.Length == 0 ? null : machine.Panel;
    }
}
