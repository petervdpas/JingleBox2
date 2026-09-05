using System.Collections.Generic;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;

namespace JingleBox2.ViewModels;

/// <inheritdoc/>
/// <remarks>
/// The effects' half of the shelf, and the same page: an effect is imported, added and thrown out
/// exactly as a machine is, since both are a folder with a manifest at the top. What differs is
/// where the list goes once it has been read.
/// </remarks>
public sealed class SoundEffectShelfViewModel : RackShelfViewModel<SoundEffectProject>
{
    /// <summary>The effects this run has, the one instance everything shares.</summary>
    private readonly ISoundEffectProjects _effects;

    /// <summary>Reads what is installed and what is on offer.</summary>
    /// <param name="effects">Where what was read is kept for the run.</param>
    public SoundEffectShelfViewModel(ISoundEffectProjects effects)
        : this(effects, new SoundEffectRegistry())
    {
    }

    /// <summary>The one registry, made once and handed to the archive that reads the same folders.</summary>
    /// <remarks>
    /// The machines' half keeps the same rule, and for the same reason: two registries over one
    /// pair of folders are two answers to what this installation has.
    /// </remarks>
    /// <param name="effects">Where what was read is kept for the run.</param>
    /// <param name="registry">Which folders are read, and what the archive is told about them.</param>
    private SoundEffectShelfViewModel(ISoundEffectProjects effects, SoundEffectRegistry registry)
        : base(registry, new SoundEffectArchive(registry), "effect") => _effects = effects;

    /// <inheritdoc/>
    protected override void Kept(IReadOnlyList<SoundEffectProject> found) => _effects.Keep(found);
}
