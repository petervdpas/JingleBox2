namespace JingleBox2.Tracker.Enums;

/// <summary>What the player is doing. Pause keeps the position; stop returns to the start.</summary>
public enum TrackerTransportState
{
    /// <summary>Not running, and the next start begins at the top of the song.</summary>
    Stopped,

    /// <summary>Running, with the clock advancing the pattern under the cursor.</summary>
    Playing,

    /// <summary>Not running, but the position is kept, so starting again carries on from it.</summary>
    Paused
}
