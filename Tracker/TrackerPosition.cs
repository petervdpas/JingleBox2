using System;

namespace JingleBox2.Tracker;

/// <summary>Where the player is: which entry in the order list, and which step inside it.</summary>
public readonly record struct TrackerPosition(int OrderIndex, int Line)
{
    public static readonly TrackerPosition Start = new(0, 0);

    public override string ToString() => $"{OrderIndex:00}:{Line:00}";
}

public enum TrackerEventKind
{
    /// <summary>Start a voice on this track.</summary>
    Trigger,

    /// <summary>Stop whatever this track is playing.</summary>
    Stop,

    /// <summary>Change the running voice without retriggering it.</summary>
    Adjust
}

/// <summary>One thing to do to one track on one step.</summary>
public readonly record struct TrackerEvent(
    int Track,
    TrackerEventKind Kind,
    Note Note,
    int Instrument,
    float? Gain,
    TrackerEffect Effect)
{
    public static TrackerEvent Stop(int track) =>
        new(track, TrackerEventKind.Stop, Note.Off, TrackerCell.NoInstrument, null, TrackerEffect.None);
}
