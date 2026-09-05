using System;
using System.Collections.Generic;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.SoundDevices.Interfaces;

namespace JingleBox2.SoundDevices;

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
public abstract class RackSoundDevices<T> : IRackSoundDevices<T> where T : class, IRackProject
{
    /// <summary>What was read, by name, which is the order the rack shows.</summary>
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

        _found.Sort(ByName);
    }

    /// <summary>
    /// Puts two devices in the order somebody would look for them in.
    /// </summary>
    /// <remarks>
    /// Alphabetical, and sorted once here rather than at each of the places that draw a list, so
    /// the rack, the pickers and the shelf in SETTINGS cannot come out in three different orders.
    ///
    /// It used to be the order the folders happened to be read in, which is the disc's and is not
    /// an order at all. That was survivable while a world held a handful of devices whose names
    /// were known here; a device is made in the designer and named by whoever made it, so there
    /// is no curated list to fall back on and the only useful order is by name.
    ///
    /// By the id after the name, so that two devices sharing a name sit in a settled order
    /// instead of swapping places between runs, which would read as the list flickering.
    /// </remarks>
    /// <param name="left">One device.</param>
    /// <param name="right">And the one it is being placed against.</param>
    private static int ByName(T left, T right)
    {
        int byName = string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);

        return byName != 0 ? byName : string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
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
