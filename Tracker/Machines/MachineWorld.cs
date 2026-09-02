using System;
using JingleBox2.Rack.Faces.Records;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Machines.Interfaces;

namespace JingleBox2.Tracker.Machines;

/// <inheritdoc/>
/// <remarks>
/// A machine's folder is carried and zipped by the archive, which does more than copy files: a
/// machine arrives as a zip as well as from a folder, and either way it has to be named around
/// whatever is already in the machines folder.
/// </remarks>
public sealed class MachineWorld : IDesignWorld
{
    /// <summary>Who unpacks and copies machines, since a machine's folder travels.</summary>
    private readonly IMachineArchive _crates;

    /// <summary>Takes the archive, or the ordinary one.</summary>
    /// <param name="crates">Who carries and zips a machine's folder.</param>
    public MachineWorld(IMachineArchive? crates = null) => _crates = crates ?? new MachineArchive();

    /// <inheritdoc/>
    public string Word => "machine";

    /// <inheritdoc/>
    public string ManifestName => MachineProject.ManifestName;

    /// <inheritdoc/>
    public IDesignProject New() => new MachineProject
    {
        Id = "machine." + Guid.NewGuid().ToString("n")[..8],
        Name = "New machine",
        Version = "1.0",
        Theme = new PanelTheme("#7B838C")
    };

    /// <inheritdoc/>
    public IDesignProject? Open(string folder) => MachineProject.Open(folder);

    /// <inheritdoc/>
    public bool CopyInto(IDesignProject project, string folder)
    {
        if (project is not MachineProject machine) return false;

        _crates.CopyInto(machine, folder);

        return true;
    }

    /// <inheritdoc/>
    public bool Exports => true;

    /// <inheritdoc/>
    public bool HasPresets => true;

    /// <inheritdoc/>
    public void Export(IDesignProject project, string zipPath)
    {
        if (project is MachineProject machine) _crates.Export(machine, zipPath);
    }
}
