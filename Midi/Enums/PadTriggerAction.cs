namespace JingleBox2.Midi.Enums;

/// <summary>What a pad is being asked to do.</summary>
public enum PadTriggerAction
{
    /// <summary>Play it if it is stopped, stop it if it is playing.</summary>
    /// <remarks>
    /// What a pad box wants, and the default: one button per pad and one press does both jobs,
    /// because there is no second button to stop with.
    /// </remarks>
    Toggle,

    /// <summary>Play it from the beginning, whatever it was doing.</summary>
    /// <remarks>
    /// What a jingle wants: hit it again and it starts again, which is the whole point of a
    /// stinger. Toggle would stop it instead, half a second into a two second sting.
    /// </remarks>
    Start,

    /// <summary>Stop it, and do nothing if it was already stopped.</summary>
    Stop
}
