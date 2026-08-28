using JingleBox2.Audio.Routing.Enums;

namespace JingleBox2.Audio.Routing;

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
