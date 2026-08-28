namespace JingleBox2.Tracker.Synth.Enums;

/// <summary>Which segment of an ADSR a voice is in.</summary>
public enum EnvelopeStage
{
    /// <summary>Rising to full, from wherever the note started.</summary>
    Attack,

    /// <summary>Falling from full towards the sustain level.</summary>
    Decay,

    /// <summary>Holding, until the note is let go of. Skipped when the sustain level is nought.</summary>
    Sustain,

    /// <summary>Falling away after a note off, from wherever the level happened to be.</summary>
    Release,

    /// <summary>Silent and done, which is what the mixer reaps a voice on.</summary>
    Finished
}
