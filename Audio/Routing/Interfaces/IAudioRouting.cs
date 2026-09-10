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
    /// Why a source cannot be taken aside, in words fit for a page. Empty where it can.
    /// </summary>
    /// <remarks>
    /// **Greyed for two different reasons with no way to tell them apart is most of what made
    /// this baffling.** A machine that cannot do it at all and a machine that can but has not
    /// been told where to put the source are the same dead switch, and only one of them is
    /// something somebody can act on: the second is a picker two lines up that nobody has
    /// touched, and the first is not their fault at all.
    ///
    /// So the reason travels with the refusal rather than being worked out again by whatever is
    /// drawing. It is a sentence rather than a code, since the only thing anybody does with it
    /// is read it.
    /// </remarks>
    string AsideNote { get; }

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
    /// Takes off anything the source has got back onto since it was taken aside.
    /// </summary>
    /// <remarks>
    /// **An arrangement over somebody else's graph does not stay made.** A stream is remade
    /// whenever the program owning it opens a new one, reloads a page or is moved between
    /// outputs, and what remakes it is the system's own session manager, which knows nothing
    /// about this and wires it straight back to the speakers. The source is then playing in two
    /// places at once, out of its own output and a buffer later through here, which is what it
    /// sounds like rather than what it looks like: the same audio twice, slightly apart.
    ///
    /// So the arrangement is held on the clock that already keeps the capture standing. What is
    /// taken off is only what has come back, never the whole thing again: putting the links back
    /// and pulling them out each time would let the source out of the desk for a fraction of a
    /// second on every reading, which is audible.
    ///
    /// Where nothing is standing at all, this takes the source aside outright, since the picker
    /// chooses what is playing on somebody's behalf and a switch turned on before a source
    /// existed would otherwise wait for a choice that is never coming. A source that is not the
    /// one supposed to be aside is refused rather than unplugged.
    ///
    /// **Who holds it differs per machine and the caller does not have to know.** Where the
    /// arrangement is a wire it has to be held here; where it is a standing instruction about
    /// where a program plays, the system holds it and false is the honest answer.
    /// </remarks>
    /// <param name="route">The source that is supposed to be aside, as the picker offers it.</param>
    /// <returns>True where something had crept back and was taken off again.</returns>
    bool HoldAside(AudioRoute route);

    /// <summary>
    /// Puts back whatever was taken aside, and does nothing where nothing was.
    /// </summary>
    /// <remarks>
    /// Called when another source is chosen and on the way out of the application. What was
    /// unplugged is somebody's own machine rather than ours, so leaving a browser silent after
    /// this program has closed is the worst thing this feature could do.
    /// </remarks>
    void GiveBack();

    /// <summary>
    /// Whether that source is this application's own output coming back, so far as this
    /// subsystem can tell.
    /// </summary>
    /// <remarks>
    /// **Asked here because only the subsystem knows how its own names work.** What an output is
    /// playing can be captured, and hearing that through the same output sends it round again;
    /// hearing a *different* output's is the ordinary way anybody records another program and goes
    /// round nothing. Telling the two apart means matching a source against a device, and the two
    /// are named by whatever wired this machine up.
    ///
    /// On Windows both halves come out of the same endpoint naming, so they can be compared. On a
    /// graph they need not: a node carries the description its owner gave it and the output is
    /// whatever the audio library calls the device, and those are two different registries. So the
    /// question is put to each subsystem rather than answered once above them with a comparison
    /// that happens to work on the machine it was written on.
    ///
    /// **Nothing is a fourth answer.** Cannot tell is a real state and it is not the same as no:
    /// whoever asks is expected to read it as a loop, since being wrong the other way is a room
    /// full of feedback at whatever the master is set to. A subsystem that has not been taught to
    /// tell says nothing rather than guessing.
    /// </remarks>
    /// <param name="source">The source that has been chosen.</param>
    /// <param name="output">What this application plays out of, by name.</param>
    /// <returns>True where it is ours, false where it is another output's, nothing where it cannot be told.</returns>
    bool? IsOurOutput(AudioRoute source, string? output);
}
