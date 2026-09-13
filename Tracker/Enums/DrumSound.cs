namespace JingleBox2.Tracker.Enums;

/// <summary>
/// What a hit found in a recording sounds like, as far as listening to it can tell.
/// </summary>
/// <remarks>
/// In the order a kit is laid out in, which is the order drummers and drum machines have always
/// used: the kick first, then the snare, then the hats and what rings, then everything else. A pad
/// grid filled in this order puts the kick under the first finger whatever the recording was.
/// </remarks>
public enum DrumSound
{
    /// <summary>Most of its weight under a hundred and fifty cycles, and gone quickly.</summary>
    Kick,

    /// <summary>A body in the middle with a rattle of noise over it.</summary>
    Snare,

    /// <summary>Almost all top, and short.</summary>
    ClosedHat,

    /// <summary>Almost all top, and left to ring.</summary>
    OpenHat,

    /// <summary>A body in the middle with little noise on it: a tone that falls away.</summary>
    Tom,

    /// <summary>Top that rings for a long time.</summary>
    Cymbal,

    /// <summary>Anything the rules cannot place.</summary>
    Percussion
}
