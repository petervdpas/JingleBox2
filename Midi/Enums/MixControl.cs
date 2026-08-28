namespace JingleBox2.Midi.Enums;

/// <summary>The handful of things a mixer strip has, named rather than counted.</summary>
/// <remarks>
/// An enum and not a string key, so the set of them is visible to anything reading this and
/// there is no name to spell wrong. A strip is a fixed thing with a fixed set of controls,
/// unlike a machine, whose parameters are its own business.
/// </remarks>
public enum MixControl
{
    /// <summary>The fader.</summary>
    Volume,

    /// <summary>Where it sits between the two sides.</summary>
    Pan,

    /// <summary>Off. A knob writes it as anything at or above the middle of its range.</summary>
    Mute,

    /// <summary>And on its own, which the same rule applies to.</summary>
    Solo,

    /// <summary>How far another track's ducker pulls this one down.</summary>
    Duck,

    /// <summary>
    /// How long the ducking takes to come back up.
    /// </summary>
    /// <remarks>
    /// Last, so a mapping saved before this existed still reads as the control it was given. It
    /// was missing rather than left out: every other value on a strip could be pointed at and
    /// this one had no name for a link to use, so the knob beside Duck was the one thing on the
    /// mixer a controller could not reach.
    /// </remarks>
    Release
}
