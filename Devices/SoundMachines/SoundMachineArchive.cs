using JingleBox2.Devices.SoundMachines.Interfaces;
using JingleBox2.Devices.Interfaces;

namespace JingleBox2.Devices.SoundMachines;

/// <inheritdoc/>
/// <remarks>
/// A machine's own two answers: its manifest is <c>machine.json</c> and a folder is read by
/// <see cref="SoundMachineProject.Open"/>. Everything else about carrying a folder about is the
/// rack's and is written once.
/// </remarks>
public sealed class SoundMachineArchive : RackArchive<SoundMachineProject>
{
    /// <summary>
    /// Takes the two things this needs, or makes the ordinary ones.
    /// </summary>
    /// <remarks>
    /// The registry and the archive each need the other, so one made without a registry builds
    /// one and hands itself over, which is what stops the two defaults building each other for
    /// ever. Anything wiring these up on purpose makes the registry and lets it make the archive.
    /// </remarks>
    /// <param name="registry">
    /// Who names the installed folder. Left out, the ordinary one, pointed back at this archive.
    /// </param>
    /// <param name="paths">
    /// How a path is tested for being inside a folder. Left out, the ordinary one, which reads
    /// the rule off this system.
    /// </param>
    public SoundMachineArchive(IRackRegistry<SoundMachineProject>? registry = null, ISoundMachinePaths? paths = null)
        : base(registry!, paths)
    {
    }

    /// <inheritdoc/>
    protected override string ManifestName => SoundMachineProject.ManifestName;

    /// <inheritdoc/>
    protected override SoundMachineProject? Open(string folder) => SoundMachineProject.Open(folder);
}
