using System.Collections.Generic;
using Avalonia.Controls;
using JingleBox2.Rack.SoundDevices.Faces.Records;
using JingleBox2.Rack.Controls.Interfaces;

namespace JingleBox2.Rack.Controls;

/// <inheritdoc/>
public sealed class MenuLines : IMenuLines
{
    /// <inheritdoc/>
    public IReadOnlyList<Control> Listed(IEnumerable<PanelMenuItem> offers)
    {
        var made = new List<Control>();
        PanelMenuItem? above = null;

        foreach (var offer in offers)
        {
            if (Divides(above, offer)) made.Add(new Separator());

            above = offer;

            var item = new MenuItem { Header = offer.Said, IsEnabled = offer.Live };

            if (offer.Chosen is { } chosen) item.Click += (_, _) => chosen();

            if (offer.Tip.Length > 0) ToolTip.SetTip(item, offer.Tip);

            made.Add(item);
        }

        return made;
    }

    /// <inheritdoc/>
    public bool Divides(PanelMenuItem? above, PanelMenuItem line) =>
        above is not null && line is not null
        && !string.Equals(above.Section, line.Section, System.StringComparison.Ordinal);
}
