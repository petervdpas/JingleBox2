using System.Collections.Generic;

namespace JingleBox2.Audio.Routing;

/// <summary>
/// Points the recorder's input at something the system is already producing: a device, an
/// output's monitor, or one running program.
/// </summary>
/// <remarks>
/// This is a property of the sound server, not of the audio engine, so it exists only where
/// the system has a graph to patch. Everywhere else it reports itself unavailable and the
/// recorder falls back to picking a capture device, which is all Windows offers anyway.
/// </remarks>
public interface IAudioRouting
{
    bool IsAvailable { get; }

    /// <summary>Everything with audio to give right now. A program only appears while it plays.</summary>
    IReadOnlyList<AudioRoute> GetRoutes();

    /// <summary>What the recorder is currently taking its audio from, or null when nothing is.</summary>
    AudioRoute? GetCurrentRoute();

    /// <summary>
    /// Sends one source into the recorder, replacing whatever was feeding it. False when the
    /// recorder is not listening, since there is nothing to connect to until it is.
    /// </summary>
    bool Connect(AudioRoute route);
}

/// <summary>What every platform without a patchable graph gets.</summary>
public sealed class NoAudioRouting : IAudioRouting
{
    public bool IsAvailable => false;

    public IReadOnlyList<AudioRoute> GetRoutes() => System.Array.Empty<AudioRoute>();

    public AudioRoute? GetCurrentRoute() => null;

    public bool Connect(AudioRoute route) => false;
}
