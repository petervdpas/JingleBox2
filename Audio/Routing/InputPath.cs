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

    /// <summary>The question this was last asked, so the same source is not pulled about twice.</summary>
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
    /// So the take is made where the question changes and held by <see cref="Hold"/> otherwise,
    /// which is the whole of why holding is a separate call: it takes off only what has crept
    /// back, where a take moves things.
    ///
    /// **This is the aside half alone, and the capture is pointed every time.** Connecting is the
    /// half the machine keeps undoing, since a session manager re-points a capture whenever the
    /// stream is remade, and putting it back where it belongs costs a link rather than a source
    /// out of the desk and back. So a repeat still connects, which is what makes
    /// <see cref="Interfaces.IInputArrangement.Again"/> mean something.
    ///
    /// What the take came to is kept beside the question, since a caller asking the same thing
    /// twice wants the same answer and not a claim that nothing happened.
    ///
    /// **A refusal is not an arrangement, so it is not one of the questions this holds.** Asked
    /// the same thing again after a take that did not come off, this answers it as though it were
    /// the first time, which is what makes every way in a way back: picking the source again, or
    /// the tick, or the output moving. Held as an answer, the one lost take would have stood for
    /// the rest of the session and every one of those would have been told nothing happened.
    ///
    /// **Hear it is not part of the question, deliberately.** Where a source plays is settled by
    /// choosing it and the tick has nothing to say about that: counted in, every press would give
    /// the source back and take it off again, letting it out of the desk and back for a moment on
    /// each press. It is written down all the same, above the question, since
    /// <see cref="Heard"/> is something a caller can ask about and an answer from the first press
    /// of the session would be no answer at all.
    /// </remarks>
    private string _asked = "";

    /// <summary>What the last take came to.</summary>
    private InputAside _aside;

    /// <summary>Whether anything has been asked yet, since the first question is not a repeat.</summary>
    private bool _everAsked;

    /// <inheritdoc/>
    /// <remarks>
    /// **Both moves are here: the source is taken off its own output, and the capture is pointed
    /// at it.** They were in two places, the connecting on the page and the taking here, and the
    /// order between them was whatever the call sites happened to be written in. Together they
    /// are what "the machine matches the setting" means, and either can fail on its own, which is
    /// why what comes back says both.
    ///
    /// The two are asked at different rates on purpose, which is written up on
    /// <see cref="_asked"/>: the same question again takes nothing aside and still points the
    /// capture.
    ///
    /// Connecting runs the machine's own graph tools and takes a moment, so nothing here may be
    /// called from the drawing thread: <see cref="Interfaces.IInputArrangement"/> is what calls
    /// it, and it does that on a thread of its own.
    /// </remarks>
    public InputArranged Set(AudioRoute? source, bool heard, string? playingOut)
    {
        Heard = heard;
        Source = source;

        string asking = (source?.Node ?? "") + "\n" + (playingOut ?? "");

        bool afresh = !_everAsked || asking != _asked || _aside == InputAside.Refused;

        _everAsked = true;
        _asked = asking;

        if (afresh)
        {
            _meant = false;

            _routing.GiveBack();

            _aside = Take(source, playingOut);
        }

        return new InputArranged(_aside, source != null && _routing.Connect(source));
    }

    /// <summary>Takes the source off its own output, and says what came of it.</summary>
    /// <param name="source">What the input is now pointed at, or nothing.</param>
    /// <param name="playingOut">What this application plays out of, by name.</param>
    private InputAside Take(AudioRoute? source, string? playingOut)
    {
        if (source == null) return InputAside.Nothing;
        if (!CanHear(source, playingOut)) return InputAside.Nothing;

        _meant = true;

        return _routing.TakeAside(source) ? InputAside.Moved : InputAside.Refused;
    }

    /// <summary>Whether the source that is held is meant to be off its own output.</summary>
    /// <remarks>
    /// **Meant rather than done, and that difference is the whole of why holding works.** Every
    /// reason a take does not come off is a passing one: this application's own capture is not in
    /// the graph yet, so there is no link to break; the source is not playing at that instant, so
    /// it has none either; or a reading of the graph was still running and the door was shut.
    /// <see cref="Hold"/> is what answers for all three, on the clock that is already keeping the
    /// capture standing, and the route's own hold says so in as many words: what is supposed to
    /// be aside is remembered whether or not anything came off.
    ///
    /// Written down as done, that promise was never kept. One take that found nothing to pull
    /// left this believing it had never moved the source, so holding would not look at it again,
    /// and the source stayed on its own speakers for the rest of the session: Hear it made no
    /// difference in either position and nothing anywhere said why.
    ///
    /// What it still does not cover is a source this decided to leave alone. One that cannot be
    /// heard was never meant to be aside, and holding it would unplug it on the next reading,
    /// which is a source silenced by a rule that had already decided not to touch it.
    /// </remarks>
    private bool _meant;

    /// <inheritdoc/>
    public bool Aside => _meant && Source != null;

    /// <inheritdoc/>
    public bool Hold() => Aside && Source is { } source && _routing.HoldAside(source);

    /// <inheritdoc/>
    /// <remarks>
    /// What was asked is forgotten with it, or the next question would be read as the one already
    /// standing and answered without putting anything back where it belongs.
    /// </remarks>
    public void GiveBack()
    {
        _everAsked = false;
        _meant = false;
        _aside = InputAside.Nothing;

        _routing.GiveBack();
    }
}
