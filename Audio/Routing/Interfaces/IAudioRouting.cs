using System.Collections.Generic;
using JingleBox2.Audio.Routing;
using JingleBox2.Audio.Routing.Records;

namespace JingleBox2.Audio.Routing.Interfaces;

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
    /// <summary>
    /// Whether this machine can do it at all.
    /// </summary>
    /// <remarks>
    /// False is an ordinary answer rather than a fault, and it is asked of the machine rather
    /// than worked out from the platform: a Linux box with no PipeWire tools installed has
    /// nothing to patch. An implementation may also turn itself off here after the underlying
    /// tools have failed enough times to make the point.
    /// </remarks>
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
