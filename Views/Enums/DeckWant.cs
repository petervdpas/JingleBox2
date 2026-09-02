namespace JingleBox2.Views.Enums;

/// <summary>What a keystroke is asking the transport for, or nothing at all.</summary>
/// <remarks>
/// A closed list rather than two bools, so a caller reading it knows what it has and a key that
/// asks for neither has a word of its own.
/// </remarks>
public enum DeckWant
{
    /// <summary>Nothing: the key is not the transport's, or it is meant for something else.</summary>
    None,

    /// <summary>Start it if it is stopped and stop it if it is running, which is the space bar.</summary>
    Toggle,

    /// <summary>Start recording, or stop it where it is already going, which is Ctrl+R.</summary>
    Record
}
