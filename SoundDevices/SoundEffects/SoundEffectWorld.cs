using System.Collections.Generic;
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
    /// <summary>What a face may carry. Holds nothing, so one serves the world.</summary>
    private readonly IPanelParts _parts = new PanelParts();

    /// <summary>Who unpacks and copies effects, since an effect's folder travels.</summary>
    private readonly IRackArchive<SoundEffectProject> _crates;

    /// <summary>The effects folder on disc, which is where a browse starts.</summary>
    private readonly IRackRegistry<SoundEffectProject> _registry = new SoundEffectRegistry();

    /// <summary>Takes the archive, or the ordinary one.</summary>
    /// <param name="crates">Who carries and zips an effect's folder.</param>
    public SoundEffectWorld(IRackArchive<SoundEffectProject>? crates = null) => _crates = crates ?? new SoundEffectArchive();

    /// <inheritdoc/>
    /// <remarks>An effect is handed a whole track's audio and is sent no notes at all.</remarks>
    public bool Played => false;

    /// <inheritdoc/>
    /// <remarks>
    /// Everything a box that is not played can fill, which is the list without the eight that
    /// need notes or a kit behind them.
    /// </remarks>
    public IReadOnlyList<string> Parts => _parts.For(Played);

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
    /// <remarks>
    /// It said no for a while, on the reasoning that a machine's preset is an instrument file and
    /// an effect has no instrument. That was an argument about how presets happened to be stored
    /// here rather than about what an effect is: every delay ever built ships them, and an
    /// effect's preset is a handful of numbers, which is less to write down than a machine's
    /// rather than more.
    /// </remarks>
    public bool HasPresets => true;

    /// <inheritdoc/>
    public void Export(IDesignProject project, string zipPath)
    {
        if (project is SoundEffectProject effect) _crates.Export(effect, zipPath);
    }
}
