using System.Collections.Generic;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.SoundDevices.SoundMachines.Interfaces;

namespace JingleBox2.ViewModels;

/// <inheritdoc/>
/// <remarks>
/// The machines' half of the shelf. What it adds to the rack's own page is where the list goes
/// once it has been read: <see cref="ISoundMachineProjects"/> is what everything showing a machine
/// asks, and it is the one instance the whole run shares.
/// </remarks>
public sealed class SoundMachineShelfViewModel : RackShelfViewModel<SoundMachineProject>
{
    /// <summary>The machines this run has, the one instance everything shares.</summary>
    private readonly ISoundMachineProjects _machines;

    /// <summary>Reads what is installed and what is on offer.</summary>
    /// <param name="machines">Where what was read is kept for the run.</param>
    public SoundMachineShelfViewModel(ISoundMachineProjects machines)
        : this(machines, new SoundMachineRegistry())
    {
    }

    /// <summary>The one registry, made once and handed to the archive that reads the same folders.</summary>
    /// <remarks>
    /// Two of them would be two answers to what this installation has, which is the fault this
    /// codebase keeps naming. The archive is given the registry rather than left to make its own.
    /// </remarks>
    /// <param name="machines">Where what was read is kept for the run.</param>
    /// <param name="registry">Which folders are read, and what the archive is told about them.</param>
    private SoundMachineShelfViewModel(ISoundMachineProjects machines, SoundMachineRegistry registry)
        : base(registry, new SoundMachineArchive(registry), "machine") => _machines = machines;

    /// <inheritdoc/>
    protected override void Kept(IReadOnlyList<SoundMachineProject> found) => _machines.Keep(found);
}
