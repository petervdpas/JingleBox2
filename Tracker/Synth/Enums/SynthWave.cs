namespace JingleBox2.Tracker.Synth.Enums;

/// <summary>
/// The shape a synth voice's oscillator makes.
/// </summary>
/// <remarks>
/// The numbers are written down in songs and in presets, so they are fixed rather than
/// implied by the order: a shape added in the middle later must not silently turn every saw
/// in every song into something else.
/// </remarks>
public enum SynthWave
{
    /// <summary>The plain tone, and the only one here with nothing above its own pitch in it.</summary>
    Sine = 0,

    /// <summary>Half the cycle up and half down: hollow, and the loudest of them for its pitch.</summary>
    Square = 1,

    /// <summary>A ramp: everything the harmonic series has, which is what a filter wants to work on.</summary>
    Saw = 2,

    /// <summary>Softer than a square and brighter than a sine.</summary>
    Triangle = 3,

    /// <summary>A square whose two halves are uneven, set by the duty control.</summary>
    Pulse = 4,

    /// <summary>No pitch at all, for percussion and for wind.</summary>
    Noise = 5
}
