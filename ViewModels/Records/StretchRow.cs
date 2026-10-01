using System.Globalization;
using JingleBox2.Tracker.Records;

namespace JingleBox2.ViewModels.Records;

/// <summary>One loop of lines on a slot, as a line of its own under the slot in the order list.</summary>
/// <param name="Slot">Which slot of the order it is on.</param>
/// <param name="Loop">The lines and how many times they go round.</param>
public sealed record StretchRow(int Slot, LineLoop Loop)
{
    /// <summary>What the line says: the lines, and how many times.</summary>
    public string Label =>
        "\u21ba" + Loop.From.ToString("00", CultureInfo.InvariantCulture)
        + "\u2013" + Loop.To.ToString("00", CultureInfo.InvariantCulture)
        + "\u00d7" + Loop.Times;
}
