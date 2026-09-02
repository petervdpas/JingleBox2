using System;
using System.Collections.Generic;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Tracker.Effects.Interfaces;

namespace JingleBox2.Tracker.Effects;

/// <inheritdoc/>
/// <remarks>
/// The table is empty, and that is the honest state of it: the folder rules, the registry and
/// the rack's Effects tab are built and there is not one engine yet, so nothing registers and
/// the tab shows nothing. An effect that could be had but makes no sound is exactly the box this
/// codebase refuses to put on a rack, so the first entry here arrives with the class that does
/// the work rather than before it.
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
    private static readonly Dictionary<string, Func<int, int, IAudioInsert>> Built =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public bool Has(string? id) => id is { Length: > 0 } && Built.ContainsKey(id);

    /// <inheritdoc/>
    public IAudioInsert? Make(string? id, int sampleRate, int maxFrames) =>
        id is { Length: > 0 } && Built.TryGetValue(id, out var make) ? make(sampleRate, maxFrames) : null;
}
