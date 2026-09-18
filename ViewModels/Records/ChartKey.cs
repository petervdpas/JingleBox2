using Avalonia.Media;

namespace JingleBox2.ViewModels.Records;

/// <summary>
/// One entry in a chart's key on SETTINGS, System: a swatch of a line's colour, its name, and
/// what it reads now.
/// </summary>
/// <param name="Name">What the line is, such as CPU3.</param>
/// <param name="Reading">What it reads now, such as 12.5%.</param>
/// <param name="Swatch">The line's colour.</param>
public sealed record ChartKey(string Name, string Reading, IBrush Swatch);
