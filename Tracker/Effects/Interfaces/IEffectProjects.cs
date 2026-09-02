using System.Collections.Generic;

namespace JingleBox2.Tracker.Effects.Interfaces;

/// <summary>
/// The effects this installation has, held for the run.
/// </summary>
/// <remarks>
/// The registry reads the folders off the disc once, at startup and again whenever the list is
/// rebuilt, and hands what it found here. Everything that shows an effect, draws one or asks
/// whether a chain can have one comes through this rather than reading the disc again.
///
/// The parallel of <see cref="Machines.Interfaces.IMachineProjects"/> and deliberately thinner.
/// A machine has to answer for an engine kind as well, because a song writes down which engine an
/// instrument is on; a chain writes down an effect's id and nothing else, so an id is the only
/// question there is.
/// </remarks>
public interface IEffectProjects
{
    /// <summary>Takes what the registry found, forgetting whatever was held before.</summary>
    /// <param name="effects">What was read and had an engine behind it.</param>
    void Keep(IEnumerable<EffectProject> effects);

    /// <summary>The effect with that id, or nothing when this installation has not got it.</summary>
    /// <param name="id">The id a chain wrote down.</param>
    EffectProject? For(string? id);

    /// <summary>
    /// True when this installation has that effect.
    /// </summary>
    /// <remarks>
    /// Asked before a chain sounds one. A slot naming an effect that is not here is silent and
    /// says so, the way an instrument on a machine that is not registered is: what it must not do
    /// is quietly pass the audio through as though nothing were missing.
    /// </remarks>
    /// <param name="id">The id a chain wrote down.</param>
    bool Has(string? id);

    /// <summary>Every effect this installation has, in the order they were read.</summary>
    IReadOnlyList<EffectProject> All { get; }
}
