using System;
using System.Collections.Generic;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;

namespace JingleBox2.SoundDevices.SoundEffects;

/// <inheritdoc/>
/// <remarks>
/// One line an engine, and the line arrives with the class that does the work rather than before
/// it: an effect that could be had and makes no sound is exactly the box this codebase refuses to
/// put on a rack.
///
/// Ids are compared without case, the way the machines' are, since an id is typed by hand into a
/// manifest and a capital letter is not a different effect.
/// </remarks>
public sealed class SoundEffectEngines : ISoundEffectEngines
{
    /// <summary>What each id builds, by id.</summary>
    /// <remarks>
    /// Written out one by one rather than found by reflection: the list of what this build can
    /// make is worth being able to read, and an engine that arrives by being discovered is one
    /// nobody can find by looking.
    /// </remarks>
    private static readonly Dictionary<string, Func<string, int, int, ISoundEffectEngine>> Built =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [Delayed] = (id, rate, _) => new Delay(rate, id),
            [Filtered] = (id, rate, _) => new Sweep(rate, id),
            [Driven] = (id, rate, _) => new Drive(rate, id),
        };

    /// <summary>
    /// EchoBox, which is a delay.
    /// </summary>
    /// <remarks>
    /// The id is what a chain writes down and what says which engine is behind the face, so it
    /// is written out here and in the effect's own manifest and can never change. What it is
    /// called on the rack is the manifest's business and is somebody's to edit; this is not.
    /// </remarks>
    public const string EchoBox = "effect.echobox";

    /// <summary>Sweeper, which is a resonant filter with a drive into it.</summary>
    /// <remarks><inheritdoc cref="EchoBox" path="/remarks"/></remarks>
    public const string Sweeper = "effect.sweeper";

    /// <summary>Roaster, which is a drive.</summary>
    /// <remarks><inheritdoc cref="EchoBox" path="/remarks"/></remarks>
    public const string Roaster = "effect.roaster";

    /// <summary>A delay line, which is what EchoBox is a face over.</summary>
    /// <remarks>
    /// An engine name and not an effect id. It is written into the application because the class
    /// behind it is, and a manifest naming it is asking for this arithmetic; what the effect
    /// wearing it is called, and what its id is, are the manifest's own business.
    /// </remarks>
    public const string Delayed = "delay";

    /// <summary>A resonant filter with a drive into it, which is what Sweeper is a face over.</summary>
    /// <remarks><inheritdoc cref="Delayed" path="/remarks"/></remarks>
    public const string Filtered = "filter";

    /// <summary>A drive, which is what Roaster is a face over.</summary>
    /// <remarks><inheritdoc cref="Delayed" path="/remarks"/></remarks>
    public const string Driven = "drive";

    /// <summary>What this run has registered, for turning an id into an engine.</summary>
    /// <remarks>
    /// Left out where there is nothing to look in, which is the registry itself: it is holding
    /// the manifest and asks by engine rather than by id. Everything downstream has only the id a
    /// chain wrote down, so it needs somewhere to look it up.
    /// </remarks>
    private readonly ISoundEffectProjects? _projects;

    /// <summary>Takes the list to resolve ids in, or none.</summary>
    /// <param name="projects">What this run has registered.</param>
    public SoundEffectEngines(ISoundEffectProjects? projects = null) => _projects = projects;

    /// <summary>Which engine each of the three original ids implied.</summary>
    /// <remarks>
    /// The effects that shipped before an effect could name its own engine. Their manifests say
    /// nothing, and every chain on anybody's disc names them, so the mapping cannot go.
    ///
    /// Compared without regard to case, like every other id here, since a folder name is what it
    /// came from and a capital letter is not a different effect.
    /// </remarks>
    private static readonly Dictionary<string, string> Was =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [EchoBox] = Delayed,
            [Sweeper] = Filtered,
            [Roaster] = Driven,
        };

    /// <summary>Which engine that id implied before an effect could name one, or nothing.</summary>
    /// <param name="id">An effect id.</param>
    private static string? Older(string? id) =>
        id is { Length: > 0 } && Was.TryGetValue(id, out var engine) ? engine : null;

    /// <inheritdoc/>
    public string? EngineOf(string? id, string? named)
    {
        if (named is { Length: > 0 }) return named;

        string? mine = _projects?.For(id)?.Engine;

        return mine is { Length: > 0 } ? mine : Older(id);
    }

    /// <inheritdoc/>
    public bool HasEngine(string? engine) => engine is { Length: > 0 } && Built.ContainsKey(engine);

    /// <inheritdoc/>
    public bool Has(string? id) => HasEngine(EngineOf(id, null));

    /// <inheritdoc/>
    public ISoundEffectEngine? Make(string? id, int sampleRate, int maxFrames) =>
        id is { Length: > 0 } && EngineOf(id, null) is { } engine && Built.TryGetValue(engine, out var make)
            ? make(id, sampleRate, maxFrames)
            : null;
}
