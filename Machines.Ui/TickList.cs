using System;
using System.Collections.Generic;
using System.Globalization;

namespace JingleBox2.UI;

/// <summary>
/// The scale marks beside a fader, written the way they read: "6,0,-6,-12". Junk is skipped
/// rather than throwing, since these come from markup and a typo should cost a mark, not a page.
/// </summary>
public static class TickList
{
    public static double[] Parse(string? text)
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
