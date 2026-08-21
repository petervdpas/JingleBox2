namespace JingleBox2.Tracker;

/// <summary>What the player is doing. Pause keeps the position; stop returns to the start.</summary>
public enum TrackerTransportState
{
    Stopped,
    Playing,
    Paused
}
