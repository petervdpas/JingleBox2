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

    /// <summary>The block the settings are a part of, told whenever they move.</summary>
    private readonly ISettingsBlock _settings;

    /// <summary>Takes the settings block to keep the cables in.</summary>
    /// <param name="settings">The settings block this installation is running on.</param>
    public PatchedIn(ISettingsBlock settings)
    {
        _settings = settings;
        _cfg = settings.Config;
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

        _settings.Moved();
    }

    /// <inheritdoc/>
    public void Remove(string node)
    {
        if (string.IsNullOrEmpty(node)) return;
        if (_cfg.PatchedIntoInput.RemoveAll(one => string.Equals(one, node, StringComparison.Ordinal)) == 0) return;

        _settings.Moved();
    }
}
