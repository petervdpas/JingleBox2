using System;
using System.Collections.Generic;
using JingleBox2.Tracker.Effects.Interfaces;

namespace JingleBox2.Tracker.Effects;

/// <inheritdoc/>
/// <remarks>
/// One line an engine, and the line arrives with the class that does the work rather than before
/// it: an effect that could be had and makes no sound is exactly the box this codebase refuses to
/// put on a rack.
///
/// Ids are compared without case, the way the machines' are, since an id is typed by hand into a
/// manifest and a capital letter is not a different effect.
/// </remarks>
public sealed class EffectEngines : IEffectEngines
{
    /// <summary>What each id builds, by id.</summary>
    /// <remarks>
    /// Written out one by one rather than found by reflection: the list of what this build can
    /// make is worth being able to read, and an engine that arrives by being discovered is one
    /// nobody can find by looking.
    /// </remarks>
    private static readonly Dictionary<string, Func<string, int, int, IEffectEngine>> Built =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [EchoBox] = (id, rate, _) => new Delay(rate, id),
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

    /// <inheritdoc/>
    public bool Has(string? id) => id is { Length: > 0 } && Built.ContainsKey(id);

    /// <inheritdoc/>
    public IEffectEngine? Make(string? id, int sampleRate, int maxFrames) =>
        id is { Length: > 0 } && Built.TryGetValue(id, out var make) ? make(id, sampleRate, maxFrames) : null;
}
