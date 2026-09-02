using System.Collections.Generic;
using JingleBox2.Devices.SoundMachines;
using JingleBox2.Devices.SoundMachines.Interfaces;

namespace JingleBox2.ViewModels;

/// <inheritdoc/>
/// <remarks>
/// The machines' half of the shelf. What it adds to the rack's own page is where the list goes
/// once it has been read: <see cref="IMachineProjects"/> is what everything showing a machine
/// asks, and it is the one instance the whole run shares.
/// </remarks>
public sealed class MachineShelfViewModel : RackShelfViewModel<MachineProject>
{
    /// <summary>The machines this run has, the one instance everything shares.</summary>
    private readonly IMachineProjects _machines;

    /// <summary>Reads what is installed and what is on offer.</summary>
    /// <param name="machines">Where what was read is kept for the run.</param>
    public MachineShelfViewModel(IMachineProjects machines)
        : base(new MachineRegistry(), new MachineArchive(), "machine") => _machines = machines;

    /// <inheritdoc/>
    protected override void Kept(IReadOnlyList<MachineProject> found) => _machines.Keep(found);
}
