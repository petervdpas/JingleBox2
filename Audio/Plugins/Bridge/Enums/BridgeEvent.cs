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
    AllNotesOff = 4,

    /// <summary>
    /// The pitch wheel moved. The value is where it leans, minus one to one; the id is unused.
    /// </summary>
    /// <remarks>
    /// Its own kind rather than a parameter change worked out on this side, because which
    /// parameter a wheel turns is the plugin's own answer and only the process holding the
    /// plugin can ask it. See <c>Vst3Abi.MidiMappingId</c>.
    /// </remarks>
    Bend = 5,

    /// <summary>The modulation wheel moved, nought to one. The id is unused.</summary>
    /// <inheritdoc cref="Bend"/>
    Modulate = 6
}
