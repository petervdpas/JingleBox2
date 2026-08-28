namespace JingleBox2.Midi.Enums;

/// <summary>What kind of thing a hardware control is pointed at.</summary>
public enum ControlKind
{
    /// <summary>A parameter on the machine a track plays: a knob on its own panel.</summary>
    Instrument,

    /// <summary>A parameter on a plugin in a track's insert chain.</summary>
    Insert,

    /// <summary>Something on a track's mixer strip.</summary>
    Mix,

    /// <summary>
    /// A button on a machine's panel: something to be done rather than a value to be moved.
    /// </summary>
    /// <remarks>
    /// Last, so a mapping saved before this existed still reads as the kind it was given.
    /// </remarks>
    Action
}
