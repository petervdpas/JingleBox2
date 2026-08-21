namespace JingleBox2.Audio.Routing;

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
