using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Rack.Faces.Interfaces;

namespace JingleBox2.Rack.Faces;

/// <inheritdoc/>
public sealed class MenuOptions : IMenuOptions
{
    /// <inheritdoc/>
    public IReadOnlyList<string> Named(string? said)
    {
        if (said is null) return MenuOptionWords.All;

        return said
            .Split(MenuOptionWords.Between, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc/>
    public bool Carries(IReadOnlyList<string> named, string? option)
    {
        if (option is not { Length: > 0 }) return true;

        return named.Any(one => string.Equals(one, option, StringComparison.OrdinalIgnoreCase));
    }
}
