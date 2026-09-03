
using Avalonia.Input;
using JingleBox2.Rack.Controls.Interfaces;

namespace JingleBox2.Rack.Controls;

/// <inheritdoc/>
/// <remarks>
/// Three ways of saying it, and each is there for somebody. The middle button is what a picture
/// that scrolls has always taken and is the one that needs no hand on the keyboard. Ctrl is for
/// the mouse with two buttons, which is most laptops, and it is the one that was asked for.
/// Shift is what the chop editor already answered and is kept, since taking a working gesture
/// away to tidy the list up would be the change nobody asked for.
/// </remarks>
public sealed class WaveformPress : IWaveformPress
{
    /// <inheritdoc/>
    public bool MeansPan(bool middleButton, KeyModifiers held) =>
        middleButton ||
        held.HasFlag(KeyModifiers.Control) ||
        held.HasFlag(KeyModifiers.Shift);
}
