using System.Collections.Generic;
using Avalonia.Controls;
using JingleBox2.Machines.Records;
using JingleBox2.Machines.Ui.Interfaces;

namespace JingleBox2.Machines.Ui;

/// <inheritdoc/>
public sealed class MenuLines : IMenuLines
{
    /// <inheritdoc/>
    public IReadOnlyList<MenuItem> Listed(IEnumerable<MachineMenuItem> offers)
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
