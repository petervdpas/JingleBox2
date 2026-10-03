namespace JingleBox2.Config.Enums;

/// <summary>A part of the tracker or the mixer that can be switched off, screen and work together.</summary>
public enum Feature
{
    /// <summary>The automation under the pattern: its lanes are drawn, played and recorded.</summary>
    PatternAutomation,

    /// <summary>The Command... dialog on the pattern's right click menu.</summary>
    CommandEditor,

    /// <summary>The faded patterns before and after the one being worked on.</summary>
    NeighbourPatterns,

    /// <summary>The control readings printed on each block of a chain.</summary>
    ChainReadings,

    /// <summary>The song's automation on the mixer: its lanes are drawn, played and recorded.</summary>
    SongAutomation,

    /// <summary>A track's ducking under another, and the row on its strip that sets it.</summary>
    SideChain,

    /// <summary>The patchbay tab beside the desk, and the picture it keeps up to date.</summary>
    Patchbay
}
