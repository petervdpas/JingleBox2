using JingleBox2.Files.Interfaces;
using JingleBox2.SoundDevices.Interfaces;
using JingleBox2.SoundDevices.SoundMachines.Records;

namespace JingleBox2.SoundDevices.SoundMachines;

/// <inheritdoc/>
/// <remarks>
/// What a machine adds to the rack's own rules is four answers: a folder is read as a
/// <see cref="SoundMachineProject"/>, an id is offered to <see cref="Records.SoundMachine.Register"/>,
/// which refuses one it has no engine for, the list read last time is forgotten before a new one
/// is read, and a shipped machine is taken by the archive rather than by a plain copy, since a
/// machine also arrives as a zip and has to be named around a folder that is already there.
/// </remarks>
public sealed class SoundMachineRegistry : RackRegistry<SoundMachineProject>
{
    /// <summary>Who puts a machine's files where the machine goes.</summary>
    private readonly IRackArchive<SoundMachineProject> _archive;

    /// <summary>
    /// Takes the two things this needs, or makes the ordinary ones.
    /// </summary>
    /// <remarks>
    /// The registry and the archive each need the other: an archive installs into the folder the
    /// registry names, and the registry hands the archive every shipped machine it has not yet
    /// offered. Made without one, each builds the other and hands itself over, so the pair is
    /// built once and there is no third instance to go looking for.
    ///
    /// The folder's name is written out here rather than asked for, so the one name the machines
    /// folder has on disc can be found by looking for it.
    /// </remarks>
    /// <param name="archive">
    /// Who unpacks and copies machines into the installed folder. Left out, the ordinary one,
    /// pointed back at this registry.
    /// </param>
    /// <param name="paths">
    /// How this system decides two paths are the same. Left out, the rule this system really
    /// has; given, whatever a test wants to hold it to.
    /// </param>
    /// <param name="folder">Where the application keeps its things, defaulted to the real one.</param>
    /// <param name="shipped">Where the machines that ship live, defaulted to beside the program.</param>
    public SoundMachineRegistry(
        IRackArchive<SoundMachineProject>? archive = null,
        IFilePaths? paths = null,
        IAppFolder? folder = null,
        string? shipped = null)
        : base("machines", "machine", paths, folder, shipped)
    {
        _archive = archive ?? new SoundMachineArchive(this, new SoundMachinePaths(Paths));
    }

    /// <inheritdoc/>
    protected override SoundMachineProject? Open(string folder) => SoundMachineProject.Open(folder);

    /// <inheritdoc/>
    /// <remarks>
    /// Everything the app shows about a machine comes through here: what it is called, what it
    /// says it is, and what colour it wears are the machine's own and are read from its folder.
    /// </remarks>
    protected override bool Register(SoundMachineProject project) =>
        SoundMachine.Register(project.Id, project.Name, project.Summary, project.Theme);

    /// <inheritdoc/>
    /// <remarks>
    /// A machine thrown out in SETTINGS has to be gone from the list the moment it is rebuilt,
    /// and the list of machines is a static one on <see cref="Records.SoundMachine"/> rather than
    /// whatever the caller does with what it is handed.
    /// </remarks>
    protected override void Forget() => SoundMachine.Forget();

    /// <inheritdoc/>
    protected override bool Take(SoundMachineProject project) => _archive.Add(project) != null;
}
