namespace JingleBox2.Audio.Routing;

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

/// <summary>Something the recorder can be pointed at.</summary>
/// <param name="Node">
/// What the routing calls it underneath: a PipeWire node name, or one of this application's own
/// prefixed ids on Windows. It is an address rather than a description and is never shown.
/// </param>
/// <param name="Name">
/// What it is called on the page. A device describes itself and a program usually does not, in
/// which case this is the node name over again.
/// </param>
/// <param name="Kind">Which of the three sorts of source it is.</param>
public sealed record AudioRoute(string Node, string Name, AudioRouteKind Kind)
{
    /// <summary>What the picker shows. The kind matters as much as the name here.</summary>
    public string Display => Kind switch
    {
        AudioRouteKind.Monitor => $"{Name} (what is playing)",
        AudioRouteKind.Application => $"{Name} (application)",
        _ => Name
    };
}
