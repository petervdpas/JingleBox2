using System;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class PlaybackEndpoints : IPlaybackEndpointsHere
{
    /// <inheritdoc/>
    /// <remarks>
    /// Windows alone, since the endpoint ids these produce exist to be handed back to a Windows
    /// call. Everywhere else gets the empty one and loses nothing by it.
    /// </remarks>
    public IPlaybackEndpoints Here() =>
        OperatingSystem.IsWindows() ? new WindowsPlaybackEndpoints() : new NoPlaybackEndpoints();
}
