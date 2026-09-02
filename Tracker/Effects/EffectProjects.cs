using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Tracker.Effects.Interfaces;

namespace JingleBox2.Tracker.Effects;

/// <inheritdoc/>
public sealed class EffectProjects : IEffectProjects
{
    /// <summary>The effects this installation has, by id, in the order they were read.</summary>
    /// <remarks>
    /// A list as well as a lookup because the rack shows them in an order and the order is the
    /// one they were read in. Ordinary state on an ordinary object: a static one would be one
    /// list for the process however many racks are being read.
    /// </remarks>
    private readonly List<EffectProject> _found = new();

    /// <inheritdoc/>
    public void Keep(IEnumerable<EffectProject> effects)
    {
        _found.Clear();

        foreach (var effect in effects)
        {
            if (effect.Id.Length > 0) _found.Add(effect);
        }
    }

    /// <inheritdoc/>
    public EffectProject? For(string? id) =>
        id is { Length: > 0 }
            ? _found.FirstOrDefault(one => string.Equals(one.Id, id, StringComparison.OrdinalIgnoreCase))
            : null;

    /// <inheritdoc/>
    public bool Has(string? id) => For(id) is not null;

    /// <inheritdoc/>
    public IReadOnlyList<EffectProject> All => _found;
}
