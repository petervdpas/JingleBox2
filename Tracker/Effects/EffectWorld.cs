using System;
using System.IO;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.Rack.Faces.Records;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tracker.Effects;

/// <inheritdoc/>
/// <remarks>
/// An effect's folder is only files, so carrying it is a plain copy. There is no zip yet, and
/// saying so is better than offering a button that would write half a parcel: importing one is
/// the other half of that, and neither exists until an effect is worth handing to somebody.
/// </remarks>
public sealed class EffectWorld : IDesignWorld
{
    /// <summary>How a folder is carried whole.</summary>
    private readonly IFolderCopy _copy;

    /// <summary>Takes how a folder is copied, or the ordinary way.</summary>
    /// <param name="copy">Who carries a folder and everything under it.</param>
    public EffectWorld(IFolderCopy? copy = null) => _copy = copy ?? new FolderCopy();

    /// <inheritdoc/>
    public string Word => "effect";

    /// <inheritdoc/>
    public string ManifestName => EffectProject.ManifestName;

    /// <inheritdoc/>
    public IDesignProject New() => new EffectProject
    {
        Id = "effect." + Guid.NewGuid().ToString("n")[..8],
        Name = "New effect",
        Version = "1.0",
        Theme = new PanelTheme("#7B838C")
    };

    /// <inheritdoc/>
    public IDesignProject? Open(string folder) => EffectProject.Open(folder);

    /// <inheritdoc/>
    public bool CopyInto(IDesignProject project, string folder)
    {
        if (!project.IsSaved || string.IsNullOrWhiteSpace(folder)) return false;

        if (!Directory.Exists(project.Folder)) return false;

        try
        {
            _copy.Into(project.Folder, folder);

            return true;
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(
                Diagnostics.Enums.LogArea.Machines, "An effect could not be carried to " + folder, ex);

            return false;
        }
    }

    /// <inheritdoc/>
    public bool Exports => false;

    /// <inheritdoc/>
    public bool HasPresets => false;

    /// <inheritdoc/>
    /// <remarks>Nothing: there is no effect zip yet, and <see cref="Exports"/> says so.</remarks>
    public void Export(IDesignProject project, string zipPath) { }
}
