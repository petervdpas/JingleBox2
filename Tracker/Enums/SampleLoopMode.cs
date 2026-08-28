namespace JingleBox2.Tracker.Enums;

/// <summary>How a sample repeats, if it does.</summary>
public enum SampleLoopMode
{
    /// <summary>Plays once and stops at the end of the window.</summary>
    None = 0,

    /// <summary>Jumps back to the loop start every time it reaches the loop end.</summary>
    Forward = 1,

    /// <summary>Turns round at each end of the loop instead of jumping.</summary>
    PingPong = 2
}
