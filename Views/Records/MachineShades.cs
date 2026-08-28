using Avalonia.Media;

namespace JingleBox2.Views.Records;

/// <summary>
/// What a machine's theme comes to on the panel: the colours themselves, not the distances.
/// </summary>
/// <param name="Face">The chassis.</param>
/// <param name="Panel">The groups standing on it.</param>
/// <param name="Edge">The lines around them.</param>
/// <param name="Mark">The marks, curves and meters.</param>
/// <param name="Ink">The lettering, dark or pale by whichever the face can be read against.</param>
/// <param name="Muted">And the lettering that is only there if you look for it.</param>
public readonly record struct MachineShades(
    Color Face,
    Color Panel,
    Color Edge,
    Color Mark,
    Color Ink,
    Color Muted);
