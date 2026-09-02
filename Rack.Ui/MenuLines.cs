using System.Collections.Generic;
using Avalonia.Controls;
using JingleBox2.Rack.Faces.Records;
using JingleBox2.Rack.Ui.Interfaces;

namespace JingleBox2.Rack.Ui;

/// <inheritdoc/>
public sealed class MenuLines : IMenuLines
{
    /// <inheritdoc/>
    public IReadOnlyList<MenuItem> Listed(IEnumerable<PanelMenuItem> offers)
    {
        var made = new List<MenuItem>();

        foreach (var offer in offers)
        {
            var item = new MenuItem { Header = offer.Said, IsEnabled = offer.Live };

            if (offer.Chosen is { } chosen) item.Click += (_, _) => chosen();

            if (offer.Tip.Length > 0) ToolTip.SetTip(item, offer.Tip);

            made.Add(item);
        }

        return made;
    }
}
