namespace JingleBox2.Tracker.Synth.Enums;

/// <summary>What the oscillator's modulation lands on.</summary>
public enum VcoModTarget
{
    /// <summary>The pitch, which is vibrato at a low rate and something else entirely at a high one.</summary>
    Frequency = 0,

    /// <summary>How wide the pulse is, which moves the tone without moving the note.</summary>
    PulseWidth = 1
}
