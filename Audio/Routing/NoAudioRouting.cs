using System.Collections.Generic;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Audio.Routing.Records;

namespace JingleBox2.Audio.Routing;

/// <summary>What every platform without a patchable graph gets.</summary>
/// <remarks>
/// Everything above holds an <see cref="IAudioRouting"/> whatever the machine turns out to be,
/// so the absence of routing is an object that politely says no rather than a null every caller
/// has to remember to check. It talks to nothing and can fail at nothing.
/// </remarks>
public sealed class NoAudioRouting : IAudioRouting
{
    /// <inheritdoc/>
    /// <remarks>Always false. There is nothing here to be available.</remarks>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    /// <remarks>Nothing, always, which the picker shows as a page with no sources on it.</remarks>
    public IReadOnlyList<AudioRoute> GetRoutes() => System.Array.Empty<AudioRoute>();

    /// <inheritdoc/>
    /// <remarks>Nothing is being routed, so there is nothing to name.</remarks>
    public AudioRoute? GetCurrentRoute() => null;

    /// <inheritdoc/>
    /// <remarks>Refuses everything, since there is no graph in which to make the connection.</remarks>
    public bool Connect(AudioRoute route) => false;
}
