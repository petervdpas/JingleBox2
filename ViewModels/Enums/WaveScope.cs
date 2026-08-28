using JingleBox2.Tracker.Records;

namespace JingleBox2.ViewModels.Enums;

/// <summary>Which recordings the level tool is looking at.</summary>
public enum WaveScope
{
    /// <summary>The ones the picked preset plays.</summary>
    Preset,

    /// <summary>Every recording this machine's presets play.</summary>
    Machine,

    /// <summary>Whatever is in a folder somewhere on the disc.</summary>
    Folder,
}
