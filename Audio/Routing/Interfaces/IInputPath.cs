using JingleBox2.Audio.Routing.Records;

namespace JingleBox2.Audio.Routing.Interfaces;

/// <summary>
/// What the desk's input channel does to the machine: the source it is pointed at, and whether
/// that source is released into the mix.
/// </summary>
/// <remarks>
/// **The IN strip is two facts and one arrangement, and it was neither.** Which source the input
/// takes and whether Hear it is on were read in four places in the page, each asking a slightly
/// different question, and the arrangement they were supposed to produce was spread between
/// them: what came out was a browser chosen as the input that went on playing out of the
/// speakers, and a tick that changed a level and nothing else. There is one answer to make, so
/// there is one thing that makes it.
///
/// The two facts, said once:
///
/// - **A source pointed at the input belongs to the desk.** Choosing it is what takes it off its
///   own output, so what somebody hears the moment they pick a browser is nothing. Copying it
///   instead, which is what every recorder does and what this did, leaves the same audio playing
///   in two places a buffer apart the moment anybody listens.
/// - **Hear it releases it into the mix, and gates it otherwise.** With it off the source is off
///   its own output and not on the desk either, which is silence and is the point; with it on it
///   comes out of the master and nowhere else.
///
/// **What the taking is differs per machine and none of it is above this.** On a graph the links
/// are moved and on a machine without one the program is pointed at another output, which is
/// <c>PipeWireRouting</c> and <c>WindowsRouting</c> behind <see cref="IAudioRouting"/>. That is
/// where the operating systems part company and it is the only place they do: this holds the rule
/// and asks the route to carry it out, so a page above it says the same two words on both.
///
/// **Nothing here draws or reads a setting**, so the whole of what the IN strip does to somebody
/// else's machine can be put a question to without a window, a graph or a sound card.
/// </remarks>
public interface IInputPath
{
    /// <summary>The source the input is taking, or nothing while none is chosen.</summary>
    AudioRoute? Source { get; }

    /// <summary>Whether that source is being released into the mix.</summary>
    bool Heard { get; }

    /// <summary>
    /// Whether that source could be heard through the desk at all.
    /// </summary>
    /// <remarks>
    /// False for what this application itself plays out of, since hearing that through the same
    /// output sends it round again. Another output's is the ordinary way anybody records a second
    /// program and goes round nothing, so only ours is refused: see
    /// <see cref="IAudioRouting.IsOurOutput"/>, whose third answer of cannot tell is read as ours,
    /// because being wrong that way is a switch that does nothing and being wrong the other way is
    /// a room full of feedback.
    ///
    /// **Asked about a source rather than about the one this is holding**, deliberately. A page
    /// greys its switch while somebody is in the middle of choosing, which is before this has been
    /// told anything, and an answer about the source from a moment ago is the wrong answer at
    /// exactly the moment it matters.
    /// </remarks>
    /// <param name="source">The source in question, or nothing.</param>
    /// <param name="playingOut">What this application plays out of, by name.</param>
    bool CanHear(AudioRoute? source, string? playingOut);

    /// <summary>
    /// Makes the machine agree with the two facts, and says what became of the taking.
    /// </summary>
    /// <remarks>
    /// Told both at once rather than one at a time, because they are one arrangement: a source
    /// set without the tick and a tick set without the source are two half-answers that can
    /// disagree.
    ///
    /// **The same question twice moves nothing.** The page reads the graph on a clock and ends
    /// every reading here, so all but the first of those are the arrangement that is already
    /// standing; acted on, each one would give the source back and take it off again, which is
    /// the source out of the desk and back for a fraction of a second, once a second. The
    /// question is the source and what this application plays out of, and a repeat is answered
    /// from what the first one came to. Hear it is not in it, since where a source plays is
    /// settled by choosing it and the tick has nothing to say about that; keeping the
    /// arrangement standing against a session manager that rewires it is
    /// <see cref="Hold"/>'s job.
    ///
    /// Whatever was taken aside is put back before anything new is taken, since a source that is
    /// no longer the input has no business staying unplugged from its own output.
    ///
    /// What it answers is what happened rather than a sentence to show: the words are
    /// <see cref="IInputWords"/>'s.
    /// </remarks>
    /// <param name="source">What the input is now pointed at, or nothing.</param>
    /// <param name="heard">Whether Hear it is on.</param>
    /// <param name="playingOut">What this application plays out of, by name, for the loop test.</param>
    /// <returns>What became of taking the source off its own output.</returns>
    Enums.InputAside Set(AudioRoute? source, bool heard, string? playingOut);

    /// <summary>
    /// Puts back anything that has crept onto its own output since, and says whether any had.
    /// </summary>
    /// <remarks>
    /// **Taking a source aside is not a thing that stays done.** The graph belongs to the machine
    /// rather than to this application, and its session manager wires a stream back to the
    /// speakers whenever the stream is remade: a new tab, a page reloaded, a program moved between
    /// outputs. What that sounds like is the same audio twice with a buffer between the two.
    ///
    /// Only what has come back is taken off, never the whole arrangement again, since putting the
    /// links back and pulling them out on every reading would let the source out of the desk for a
    /// fraction of a second each time, which is audible.
    /// </remarks>
    /// <returns>True where something really had come back and was taken off again.</returns>
    bool Hold();

    /// <summary>Puts everything back where it was, and does nothing where nothing was moved.</summary>
    /// <remarks>
    /// **The one call in here that has to happen.** What was unplugged is somebody's own machine,
    /// so a browser left silent after this program has closed is the worst thing this could do,
    /// and there is nothing on the screen by then to say what happened.
    /// </remarks>
    void GiveBack();
}
