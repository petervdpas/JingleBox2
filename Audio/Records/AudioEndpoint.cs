namespace JingleBox2.Audio.Records;

/// <summary>One place this machine can play audio out of.</summary>
/// <remarks>
/// Not the same list as the output the engine plays through, and deliberately: that is chosen
/// through BASS and is about where this application's own sound goes. This is the system's own
/// list of endpoints, and it exists for the one thing that needs the system's own name for one,
/// which is telling Windows where another program should play.
/// </remarks>
/// <param name="Id">
/// What the system calls it, which is a string rather than a number and is stable across
/// restarts. It is an address rather than a description and is never shown.
/// </param>
/// <param name="Name">What it is called on the page.</param>
public readonly record struct AudioEndpoint(string Id, string Name);
