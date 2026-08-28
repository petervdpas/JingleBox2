namespace JingleBox2.Tracker.Synth.Enums;

/// <summary>Where a modulation comes from.</summary>
public enum ModSource
{
    /// <summary>The note's own envelope, so the modulation has the shape of the note.</summary>
    Envelope = 0,

    /// <summary>The low frequency oscillator, so it keeps going as long as the note does.</summary>
    Lfo = 1
}
