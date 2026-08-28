namespace JingleBox2.Audio.Routing.Enums;

/// <summary>
/// What sort of source a route is, which decides how it is worded and where it sits in the list.
/// </summary>
/// <remarks>
/// The numbers are the reading order of the picker, not an arbitrary set: the list is sorted by
/// this, so devices come first, then what an output is playing, then the programs. Reordering
/// them reorders the page.
/// </remarks>
public enum AudioRouteKind
{
    /// <summary>A capture device: a microphone or a line in.</summary>
    Input = 0,

    /// <summary>What an output is playing, which is how you record the desktop.</summary>
    Monitor = 1,

    /// <summary>A running program's own audio, which is how you record one of them.</summary>
    Application = 2
}
