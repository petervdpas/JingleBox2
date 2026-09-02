using JingleBox2.SoundDevices.SoundMachines.Interfaces;
using JingleBox2.SoundDevices.Interfaces;

namespace JingleBox2.SoundDevices.SoundEffects;

/// <inheritdoc/>
/// <remarks>
/// An effect's own two answers: its manifest is <c>effect.json</c> and a folder is read by
/// <see cref="SoundEffectProject.Open"/>. A zip of an effect is a zip of a folder, the same as a
/// machine's, so it is carried, unpacked, checked and swapped by exactly the same code: what
/// differs between the two worlds is the name of one file.
/// </remarks>
public sealed class SoundEffectArchive : RackArchive<SoundEffectProject>
{
    /// <summary>
    /// Takes the two things this needs, or makes the ordinary ones.
    /// </summary>
    /// <remarks>
    /// The registry and the archive each need the other, so one made without a registry builds
    /// one and hands itself over, the same arrangement the machines' pair keeps.
    /// </remarks>
    /// <param name="registry">Who names the installed folder. Left out, the ordinary one.</param>
    /// <param name="paths">How a path is tested for being inside a folder.</param>
    public SoundEffectArchive(IRackRegistry<SoundEffectProject>? registry = null, ISoundMachinePaths? paths = null)
        : base(registry ?? new SoundEffectRegistry(), paths)
    {
    }

    /// <inheritdoc/>
    protected override string ManifestName => SoundEffectProject.ManifestName;

    /// <inheritdoc/>
    protected override SoundEffectProject? Open(string folder) => SoundEffectProject.Open(folder);
}
