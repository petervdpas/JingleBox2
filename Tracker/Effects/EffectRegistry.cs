using JingleBox2.Files.Interfaces;
using JingleBox2.Tracker.Effects.Interfaces;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tracker.Effects;

/// <inheritdoc/>
/// <remarks>
/// What an effect adds to the rack's own rules is two answers: a folder is read as an
/// <see cref="EffectProject"/>, and an id is kept only when this build has an engine for it.
/// There is nothing to forget between readings, since the list of effects is whatever
/// <see cref="RackRegistry{T}.Load"/> hands back rather than a static one somewhere, and a
/// shipped effect is taken by the archive, which is the same staging folder and the same swap a
/// machine arrives through.
///
/// The engine gate is why the Effects tab is empty today rather than showing five boxes that
/// cannot sound. Every folder in the effects folder is read; each is offered to
/// <see cref="IEffectEngines"/>, and one it has no engine for is passed over without a word.
/// </remarks>
public sealed class EffectRegistry : RackRegistry<EffectProject>
{
    /// <summary>Which ids this build can actually make.</summary>
    private readonly IEffectEngines _engines;

    /// <summary>Who puts an effect's files where the effect goes.</summary>
    private readonly IRackArchive<EffectProject> _archive;

    /// <summary>
    /// Takes what this needs, or makes the ordinary ones.
    /// </summary>
    /// <remarks>
    /// The folder's name is written out here rather than asked for, so the one name the effects
    /// folder has on disc can be found by looking for it.
    /// </remarks>
    /// <param name="engines">
    /// Which effects this build has. Left out, the real list, which is empty until the first
    /// engine is written; given, whatever a test wants to hold it to.
    /// </param>
    /// <param name="paths">
    /// How this system decides two paths are the same. Left out, the rule this system really has.
    /// </param>
    /// <param name="folder">Where the application keeps its things, defaulted to the real one.</param>
    /// <param name="shipped">Where the effects that ship live, defaulted to beside the program.</param>
    /// <param name="archive">Who carries an effect's folder. Left out, the ordinary one, pointed here.</param>
    public EffectRegistry(
        IEffectEngines? engines = null,
        IFilePaths? paths = null,
        IAppFolder? folder = null,
        string? shipped = null,
        IRackArchive<EffectProject>? archive = null)
        : base("effects", "effect", paths, folder, shipped)
    {
        _engines = engines ?? new EffectEngines();
        _archive = archive ?? new EffectArchive(this);
    }

    /// <inheritdoc/>
    protected override EffectProject? Open(string folder) => EffectProject.Open(folder);

    /// <inheritdoc/>
    protected override bool Register(EffectProject project) => _engines.Has(project.Id);

    /// <inheritdoc/>
    protected override bool Take(EffectProject project) => _archive.Add(project) != null;
}
