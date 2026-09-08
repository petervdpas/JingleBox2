using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Config.Interfaces;
using JingleBox2.ViewModels.Interfaces;

namespace JingleBox2.Config;

/// <inheritdoc/>
/// <remarks>
/// Over the settings file, which is where everything about this installation rather than about a
/// song already lives, beside the places the blocks were left. Written as the cable is drawn,
/// since that is once per gesture and the file is small.
/// </remarks>
public sealed class PatchedIn : IPatchedIn
{
    /// <summary>The settings, holding the cables among everything else.</summary>
    private readonly AppConfig _cfg;

    /// <summary>What writes them out.</summary>
    private readonly IConfigStore _store;

    /// <summary>Takes the settings to keep the cables in, and what writes them.</summary>
    /// <param name="cfg">The settings this installation is running on.</param>
    /// <param name="store">What puts them on the disc.</param>
    public PatchedIn(AppConfig cfg, IConfigStore store)
    {
        _cfg = cfg;
        _store = store;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Sources => _cfg.PatchedIntoInput;

    /// <inheritdoc/>
    public bool Holds(string node) =>
        !string.IsNullOrEmpty(node) && _cfg.PatchedIntoInput.Contains(node, StringComparer.Ordinal);

    /// <inheritdoc/>
    public void Add(string node)
    {
        if (string.IsNullOrEmpty(node) || Holds(node)) return;

        _cfg.PatchedIntoInput.Add(node);

        _store.Save(_cfg);
    }

    /// <inheritdoc/>
    public void Remove(string node)
    {
        if (string.IsNullOrEmpty(node)) return;
        if (_cfg.PatchedIntoInput.RemoveAll(one => string.Equals(one, node, StringComparison.Ordinal)) == 0) return;

        _store.Save(_cfg);
    }
}
