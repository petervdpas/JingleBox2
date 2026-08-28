using System;
using System.Collections.Generic;
using System.Globalization;
using JingleBox2.Machines.Ui.Interfaces;

namespace JingleBox2.Machines.Ui;

/// <inheritdoc/>
internal sealed class TickList : ITickList
{
    /// <inheritdoc/>
    public double[] Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<double>();

        var marks = new List<double>();

        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                marks.Add(value);
        }

        return marks.ToArray();
    }
}
