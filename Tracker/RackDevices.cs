using System;
using System.Collections.Generic;
using JingleBox2.Rack.Faces;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
/// <remarks>
/// Ordinary state on an ordinary object, which is the whole of what it is: a list and a lookup
/// over it. A static one would be one list for the process however many racks are being read, and
/// whatever a test put in it would still be there for the next test.
///
/// Ids are compared without case, since an id is typed by hand into a manifest and a capital
/// letter is not a different device.
/// </remarks>
/// <typeparam name="T">The manifest a device of this kind is read into.</typeparam>
public abstract class RackDevices<T> : IRackDevices<T> where T : class, IRackProject
{
    /// <summary>What was read, in the order it was read, which is the order the rack shows.</summary>
    private readonly List<T> _found = new();

    /// <summary>And the same by id, since that is how everything downstream asks.</summary>
    private readonly Dictionary<string, T> _byId = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public void Keep(IEnumerable<T> found)
    {
        _found.Clear();
        _byId.Clear();

        if (found is null) return;

        foreach (var one in found)
        {
            if (one.Id.Length == 0) continue;

            _found.Add(one);
            _byId[one.Id] = one;
        }
    }

    /// <inheritdoc/>
    public T? For(string? id) =>
        id is { Length: > 0 } && _byId.TryGetValue(id, out var found) ? found : null;

    /// <inheritdoc/>
    public bool Has(string? id) => For(id) is not null;

    /// <inheritdoc/>
    public IReadOnlyList<T> All => _found;

    /// <inheritdoc/>
    public Panel? PanelFor(string? id)
    {
        if (For(id)?.Panel is not { Root: { } root } panel) return null;

        return root.Children.Count == 0 && root.Parameter.Length == 0 ? null : panel;
    }
}
