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

    /// <inheritdoc/>
    public InputAside Set(AudioRoute? source, bool heard, string? playingOut)
    {
        Source = source;
        Heard = heard;
        _moved = false;

        _routing.GiveBack();

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
    public void GiveBack() => _routing.GiveBack();
}
