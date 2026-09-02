using System.Collections.Generic;
using JingleBox2.Devices.SoundEffects;
using JingleBox2.Devices.SoundEffects.Interfaces;

namespace JingleBox2.ViewModels;

/// <inheritdoc/>
/// <remarks>
/// The effects' half of the shelf, and the same page: an effect is imported, added and thrown out
/// exactly as a machine is, since both are a folder with a manifest at the top. What differs is
/// where the list goes once it has been read.
/// </remarks>
public sealed class EffectShelfViewModel : RackShelfViewModel<EffectProject>
{
    /// <summary>The effects this run has, the one instance everything shares.</summary>
    private readonly IEffectProjects _effects;

    /// <summary>Reads what is installed and what is on offer.</summary>
    /// <param name="effects">Where what was read is kept for the run.</param>
    public EffectShelfViewModel(IEffectProjects effects)
        : base(new EffectRegistry(), new EffectArchive(), "effect") => _effects = effects;

    /// <inheritdoc/>
    protected override void Kept(IReadOnlyList<EffectProject> found) => _effects.Keep(found);
}
