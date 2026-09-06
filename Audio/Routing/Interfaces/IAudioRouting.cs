using System.Collections.Generic;
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

    /// <summary>
    /// Whether a source can be taken off everything else, so it reaches this application alone.
    /// </summary>
    /// <remarks>
    /// **Capturing a source and taking it aside are two different acts**, and only the first is
    /// what every program that records does. A browser captured is still playing out of the
    /// speakers, which is right for streaming and wrong on air: what is wanted here is the sound
    /// coming through the desk and nowhere else.
    ///
    /// False is an ordinary answer and says the machine cannot do it, not that this application
    /// will not: on a graph the links are moved, and on a machine with no graph a program can
    /// only be pointed at another output, so there has to be one to point it at.
    /// </remarks>
    bool CanTakeAside { get; }

    /// <summary>
    /// Takes a source off everything but this application, and remembers where it was.
    /// </summary>
    /// <remarks>
    /// One source at a time, since it is the one feeding the input: taking a second aside puts
    /// the first back first, or a machine would be left with two programs unplugged from their
    /// own outputs and nothing saying so.
    ///
    /// **What it changes is somebody else's machine**, so it is undone deliberately rather than
    /// left to a process ending: see <see cref="GiveBack"/>.
    /// </remarks>
    /// <param name="route">The source to take aside, as the picker offers it.</param>
    /// <returns>False where the machine cannot, or where nothing was there to move.</returns>
    bool TakeAside(AudioRoute route);

    /// <summary>
    /// Puts back whatever was taken aside, and does nothing where nothing was.
    /// </summary>
    /// <remarks>
    /// Called when another source is chosen and on the way out of the application. What was
    /// unplugged is somebody's own machine rather than ours, so leaving a browser silent after
    /// this program has closed is the worst thing this feature could do.
    /// </remarks>
    void GiveBack();
}
