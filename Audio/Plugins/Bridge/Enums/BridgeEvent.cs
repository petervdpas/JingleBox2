namespace JingleBox2.Audio.Plugins.Bridge.Enums;

/// <summary>What one queued event is. Written into the shared block before a block is asked for.</summary>
public enum BridgeEvent : int
{
    /// <summary>Not an event. A slot nobody has written reads as this.</summary>
    None = 0,

    /// <summary>A parameter moved: the id says which, the value says where to.</summary>
    ParameterValue = 1,

    /// <summary>A key pressed: the id is the note number, the value is how hard.</summary>
    NoteOn = 2,

    /// <summary>That key let go. The value is unused.</summary>
    NoteOff = 3,

    /// <summary>Everything sounding is to stop, which is the transport stopping or a track going away.</summary>
    AllNotesOff = 4
}
