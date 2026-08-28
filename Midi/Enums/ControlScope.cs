namespace JingleBox2.Midi.Enums;

/// <summary>Which track a mapping is about.</summary>
public enum ControlScope
{
    /// <summary>
    /// Whichever track you are working on. One knob, every track, no thought at the desk.
    /// </summary>
    Focused,

    /// <summary>
    /// One track and only that one. What a mixer wants: fader three is track three whether or
    /// not you are looking at it.
    /// </summary>
    Fixed
}
