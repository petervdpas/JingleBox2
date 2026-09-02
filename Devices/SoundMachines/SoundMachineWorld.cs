using System;
using JingleBox2.Rack.Faces.Records;
using JingleBox2.Devices.Interfaces;

namespace JingleBox2.Devices.SoundMachines;

/// <inheritdoc/>
/// <remarks>
/// A machine's folder is carried and zipped by the archive, which does more than copy files: a
/// machine arrives as a zip as well as from a folder, and either way it has to be named around
/// whatever is already in the machines folder.
/// </remarks>
public sealed class SoundMachineWorld : IDesignWorld
{
    /// <summary>Who unpacks and copies machines, since a machine's folder travels.</summary>
    private readonly IRackArchive<SoundMachineProject> _crates;

    /// <summary>The machines folder on disc, which is where a browse starts.</summary>
    private readonly IRackRegistry<SoundMachineProject> _registry = new SoundMachineRegistry();

    /// <summary>Takes the archive, or the ordinary one.</summary>
    /// <param name="crates">Who carries and zips a machine's folder.</param>
    public SoundMachineWorld(IRackArchive<SoundMachineProject>? crates = null) => _crates = crates ?? new SoundMachineArchive();

    /// <inheritdoc/>
    public string Word => "machine";

    /// <inheritdoc/>
    public string ManifestName => SoundMachineProject.ManifestName;

    /// <inheritdoc/>
    public string Installed => _registry.Installed;

    /// <inheritdoc/>
    public IDesignProject New() => new SoundMachineProject
    {
        Id = "machine." + Guid.NewGuid().ToString("n")[..8],
        Name = "New machine",
        Version = "1.0",
        Theme = new PanelTheme("#7B838C")
    };

    /// <inheritdoc/>
    public IDesignProject? Open(string folder) => SoundMachineProject.Open(folder);

    /// <inheritdoc/>
    public bool CopyInto(IDesignProject project, string folder)
    {
        if (project is not SoundMachineProject machine) return false;

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
        if (project is SoundMachineProject machine) _crates.Export(machine, zipPath);
    }
}
