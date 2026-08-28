using System.Collections.Generic;
using System.Linq;
using JingleBox2.Tracker.Machines.Interfaces;
using JingleBox2.Tracker.Machines.Records;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Machines;

/// <inheritdoc/>
/// <param name="registry">
/// Who says which machines ship and which are installed. Left out, the ordinary one, which
/// reads this installation's own folders.
/// </param>
public sealed class MissingMachines(IMachineRegistry? registry = null) : IMissingMachines
{
    /// <summary>Who says what ships, which is the only place a missing machine's name survives.</summary>
    private readonly IMachineRegistry _registry = registry ?? new MachineRegistry();

    /// <inheritdoc/>
    public IReadOnlyList<MissingMachine> For(Song song)
    {
        var wanted = new List<MissingMachine>();

        if (song?.Instruments is not { } instruments) return wanted;

        var offered = _registry.Available().ToDictionary(one => one.Id, one => one.Name);

        var said = new HashSet<string>();

        foreach (var sound in instruments)
        {
            if (sound is null || sound.IsPlugin) continue;

            string id = sound.Machine.SlotId;

            if (id.Length == 0 || !said.Add(id)) continue;

            if (Machine.Installed.Any(one => one.SlotId == id)) continue;

            bool ships = offered.TryGetValue(id, out string? called) && called.Length > 0;

            wanted.Add(new MissingMachine(id, ships ? called! : sound.Machine.Name, ships));
        }

        return wanted;
    }
}
