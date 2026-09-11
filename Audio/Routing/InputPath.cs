using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Audio.Routing.Records;

namespace JingleBox2.Audio.Routing;

/// <inheritdoc/>
/// <remarks>
/// One of these for the application, held where the recorder is. It keeps the two facts and
/// nothing else: what the machine is doing about them is asked of the route every time rather
/// than remembered here, since the graph moves under this and a memory of it would be a second
/// answer to go stale.
/// </remarks>
public sealed class InputPath : IInputPath
{
    /// <summary>The route that carries the act out, which is the half that differs per machine.</summary>
    private readonly IAudioRouting _routing;

    /// <summary>Takes the route this will be asking.</summary>
    /// <param name="routing">The machine's own, which is PipeWire's here and Windows's there.</param>
    public InputPath(IAudioRouting routing) => _routing = routing;

    /// <inheritdoc/>
    public AudioRoute? Source { get; private set; }

    /// <inheritdoc/>
    public bool Heard { get; private set; }

    /// <inheritdoc/>
    /// <remarks>
    /// Only a monitor can be ours coming back, since nothing else has been near an output at all:
    /// a microphone, a line in and a program are refused the question rather than answered no.
    /// Compared against <c>false</c> so that cannot tell falls in with ours, which is the cautious
    /// way round and is the one the contract names.
    /// </remarks>
    public bool CanHear(AudioRoute? source, string? playingOut)
    {
        if (source is not { Kind: AudioRouteKind.Monitor }) return true;

        return _routing.IsOurOutput(source, playingOut) == false;
    }

    /// <summary>The question this was last asked, so the same one is not answered twice.</summary>
    /// <remarks>
    /// **Being told the same thing again is the ordinary case rather than a corner.** The graph is
    /// read on a clock, and a reading ends by saying what the input is pointed at, which is almost
    /// always what it was pointed at a second ago. Acting on that means giving the source back and
    /// taking it off again on every reading: what that sounds like is the source out of its own
    /// speakers for a fraction of a second, once a second, for as long as a page carrying the
    /// picker is up, and what it does to the machine is a pair of tool runs against somebody's
    /// graph at that rate. A hardware device rewired that often is one that answers busy when it
    /// is asked for.
    ///
    /// So the arrangement is made where the question changes and held by <see cref="Hold"/>
    /// otherwise, which is the whole of why holding is a separate call: it takes off only what has
    /// crept back, where this moves things.
    ///
    /// The answer is kept beside the question, since a caller asking the same thing twice wants
    /// the same answer and not a claim that nothing happened.
    ///
    /// **Hear it is not part of the question, deliberately.** Where a source plays is settled by
    /// choosing it and the tick has nothing to say about that: counted in, every press would give
    /// the source back and take it off again, letting it out of the desk and back for a moment on
    /// each press. It is written down all the same, above the question, since
    /// <see cref="Heard"/> is something a caller can ask about and an answer from the first press
    /// of the session would be no answer at all.
    /// </remarks>
    private string _asked = "";

    /// <summary>What the last question came to.</summary>
    private InputAside _answered = InputAside.Nothing;

    /// <summary>Whether anything has been asked yet, since the first question is not a repeat.</summary>
    private bool _everAsked;

    /// <inheritdoc/>
    public InputAside Set(AudioRoute? source, bool heard, string? playingOut)
    {
        Heard = heard;

        string asking = (source?.Node ?? "") + "\n" + (playingOut ?? "");

        if (_everAsked && asking == _asked) return _answered;

        _everAsked = true;
        _asked = asking;

        Source = source;
        _moved = false;

        _routing.GiveBack();

        return _answered = Take(source, playingOut);
    }

    /// <summary>Takes the source off its own output, and says what came of it.</summary>
    /// <param name="source">What the input is now pointed at, or nothing.</param>
    /// <param name="playingOut">What this application plays out of, by name.</param>
    private InputAside Take(AudioRoute? source, string? playingOut)
    {
        if (source == null) return InputAside.Nothing;
        if (!CanHear(source, playingOut)) return InputAside.Nothing;
        if (!_routing.TakeAside(source)) return InputAside.Refused;

        _moved = true;

        return InputAside.Moved;
    }

    /// <summary>Whether the source that is held was really taken off its own output.</summary>
    /// <remarks>
    /// **Held rather than worked out again, because holding is not the same question as taking.**
    /// A source that could not be heard was never moved, and the route's own hold takes a source
    /// aside outright where it is holding nothing: asked about one it never touched, it would
    /// unplug it on the next reading, which is a source silenced by a rule that had already
    /// decided to leave it alone.
    /// </remarks>
    private bool _moved;

    /// <inheritdoc/>
    public bool Hold() => _moved && Source is { } source && _routing.HoldAside(source);

    /// <inheritdoc/>
    /// <remarks>
    /// What was asked is forgotten with it, or the next question would be read as the one already
    /// standing and answered without putting anything back where it belongs.
    /// </remarks>
    public void GiveBack()
    {
        _everAsked = false;
        _moved = false;

        _routing.GiveBack();
    }
}
