using System;
using JingleBox2.Rack.Faces.Records;
using JingleBox2.Devices.Interfaces;

namespace JingleBox2.Devices.SoundEffects;

/// <inheritdoc/>
/// <remarks>
/// An effect travels the way a machine does, since both are a folder with a manifest at the top:
/// the same zip, the same staging folder and the same swap, through <see cref="EffectArchive"/>.
/// </remarks>
public sealed class EffectWorld : IDesignWorld
{
    /// <summary>Who unpacks and copies effects, since an effect's folder travels.</summary>
    private readonly IRackArchive<EffectProject> _crates;

    /// <summary>The effects folder on disc, which is where a browse starts.</summary>
    private readonly IRackRegistry<EffectProject> _registry = new EffectRegistry();

    /// <summary>Takes the archive, or the ordinary one.</summary>
    /// <param name="crates">Who carries and zips an effect's folder.</param>
    public EffectWorld(IRackArchive<EffectProject>? crates = null) => _crates = crates ?? new EffectArchive();

    /// <inheritdoc/>
    public string Word => "effect";

    /// <inheritdoc/>
    public string ManifestName => EffectProject.ManifestName;

    /// <inheritdoc/>
    public string Installed => _registry.Installed;

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
        if (project is not EffectProject effect) return false;

        _crates.CopyInto(effect, folder);

        return true;
    }

    /// <inheritdoc/>
    public bool Exports => true;

    /// <inheritdoc/>
    public bool HasPresets => false;

    /// <inheritdoc/>
    public void Export(IDesignProject project, string zipPath)
    {
        if (project is EffectProject effect) _crates.Export(effect, zipPath);
    }
}
