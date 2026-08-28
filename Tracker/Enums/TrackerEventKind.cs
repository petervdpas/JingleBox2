namespace JingleBox2.Tracker.Enums;

/// <summary>What a step asks of a track. Three, because a cell's columns are independently blank.</summary>
public enum TrackerEventKind
{
    /// <summary>Start a voice on this track.</summary>
    Trigger,

    /// <summary>Stop whatever this track is playing.</summary>
    Stop,

    /// <summary>Change the running voice without retriggering it.</summary>
    Adjust
}
