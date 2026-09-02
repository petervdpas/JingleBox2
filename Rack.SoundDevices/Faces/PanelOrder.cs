using System;
using System.Collections.Generic;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;

namespace JingleBox2.Rack.SoundDevices.Faces;

/// <inheritdoc/>
public sealed class PanelOrder : IPanelOrder
{
    /// <inheritdoc/>
    public IReadOnlyList<string> Of(Panel? panel)
    {
        var found = new List<string>();

        if (panel?.Root is { } root) Walk(root, found, new HashSet<string>(StringComparer.Ordinal));

        return found;
    }

    /// <inheritdoc/>
    public string At(Panel? panel, int ordinal)
    {
        if (ordinal < 0) return "";

        var order = Of(panel);

        return ordinal < order.Count ? order[ordinal] : "";
    }

    /// <summary>
    /// Adds this element's parameter and then everything under it, depth first, which is the
    /// order somebody's eye takes a panel in.
    /// </summary>
    /// <remarks>
    /// The set of what has already been seen is what makes a parameter named twice count once.
    /// A value shown beside the knob that turns it is two elements naming one parameter, and a
    /// controller pointed at "the third control" must not find the same one twice.
    /// </remarks>
    private void Walk(PanelElement element, List<string> found, HashSet<string> already)
    {
        if (element.Parameter is { Length: > 0 } key && already.Add(key)) found.Add(key);

        foreach (var child in element.Children) Walk(child, found, already);
    }
}
