using System;
using JingleBox2.Rack.SoundDevices.Faces.Records;
using JingleBox2.SoundDevices.Interfaces;

namespace JingleBox2.SoundDevices.SoundEffects;

/// <inheritdoc/>
/// <remarks>
/// An effect travels the way a machine does, since both are a folder with a manifest at the top:
/// the same zip, the same staging folder and the same swap, through <see cref="SoundEffectArchive"/>.
/// </remarks>
public sealed class SoundEffectWorld : IDesignWorld
{
    /// <summary>Who unpacks and copies effects, since an effect's folder travels.</summary>
    private readonly IRackArchive<SoundEffectProject> _crates;

    /// <summary>The effects folder on disc, which is where a browse starts.</summary>
    private readonly IRackRegistry<SoundEffectProject> _registry = new SoundEffectRegistry();

    /// <summary>Takes the archive, or the ordinary one.</summary>
    /// <param name="crates">Who carries and zips an effect's folder.</param>
    public SoundEffectWorld(IRackArchive<SoundEffectProject>? crates = null) => _crates = crates ?? new SoundEffectArchive();

    /// <inheritdoc/>
    public string Word => "effect";

    /// <inheritdoc/>
    public string ManifestName => SoundEffectProject.ManifestName;

    /// <inheritdoc/>
    public string Installed => _registry.Installed;

    /// <inheritdoc/>
    public IDesignProject New() => new SoundEffectProject
    {
        Id = "effect." + Guid.NewGuid().ToString("n")[..8],
        Name = "New effect",
        Version = "1.0",
        Theme = new PanelTheme("#7B838C")
    };

    /// <inheritdoc/>
    public IDesignProject? Open(string folder) => SoundEffectProject.Open(folder);

    /// <inheritdoc/>
    public bool CopyInto(IDesignProject project, string folder)
    {
        if (project is not SoundEffectProject effect) return false;

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
        if (project is SoundEffectProject effect) _crates.Export(effect, zipPath);
    }
}
