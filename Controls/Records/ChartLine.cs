using System.Collections.Generic;
using Avalonia.Media;

namespace JingleBox2.Controls.Records;

/// <summary>
/// One line on a <see cref="HistoryChart"/>.
/// </summary>
/// <param name="Values">
/// A reading each, oldest first, on the chart's own scale. Not a number where there was no
/// reading yet, which leaves that stretch of the chart empty rather than drawn at nought.
/// </param>
/// <param name="Colour">What colour it is drawn in.</param>
/// <param name="Thickness">How thick, in pixels.</param>
public sealed record ChartLine(IReadOnlyList<double> Values, Color Colour, double Thickness = 1.5);
