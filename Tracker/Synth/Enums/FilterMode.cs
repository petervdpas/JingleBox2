namespace JingleBox2.Tracker.Synth.Enums;

/// <summary>Which end of the filter is open.</summary>
public enum FilterMode
{
    /// <summary>Everything below the cutoff, which is what a filter usually means.</summary>
    LowPass = 0,

    /// <summary>Everything above it, for taking the body out of something.</summary>
    HighPass = 1
}
