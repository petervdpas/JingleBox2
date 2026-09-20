using System;
using JingleBox2.Music.Interfaces;

namespace JingleBox2.Music;

/// <inheritdoc/>
public sealed class MidiWheelInput : IMidiWheelInput
{
    /// <inheritdoc cref="IMidiWheelInput.ModulationController"/>
    public const int ModulationController = 1;

    /// <inheritdoc cref="IMidiWheelInput.BendCentre"/>
    public const int BendCentre = 8192;

    /// <inheritdoc cref="IMidiWheelInput.BendMost"/>
    public const int BendMost = 16383;

    /// <summary>The hardest a controller can be pushed, which is seven bits.</summary>
    public const int MostValue = 127;

    /// <inheritdoc/>
    int IMidiWheelInput.ModulationController => ModulationController;

    /// <inheritdoc/>
    int IMidiWheelInput.BendCentre => BendCentre;

    /// <inheritdoc/>
    int IMidiWheelInput.BendMost => BendMost;

    /// <inheritdoc/>
    public double LeanFor(int bend)
    {
        int held = Math.Clamp(bend, 0, BendMost);

        if (held == BendCentre) return 0;

        return held < BendCentre
            ? (held - BendCentre) / (double)BendCentre
            : (held - BendCentre) / (double)(BendMost - BendCentre);
    }

    /// <inheritdoc/>
    public double AmountFor(int value) => Math.Clamp(value, 0, MostValue) / (double)MostValue;
}
