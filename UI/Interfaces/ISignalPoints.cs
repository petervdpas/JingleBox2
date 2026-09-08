using System.Collections.Generic;
using JingleBox2.UI.Records;

namespace JingleBox2.UI.Interfaces;

/// <summary>
/// Which points the routing has, and what each one is carrying.
/// </summary>
/// <remarks>
/// **The patchbay and the routing table are one model, and this is where they meet.** A block on
/// the picture is a node, a connection point on it is a port, and a <see cref="SignalPoint"/> is
/// that pair; the meters on the desk are fed from the same pairs. The two halves shared that
/// vocabulary and said so nowhere, which is a coupling that holds until it does not: a port
/// renamed on the picture leaves the meter behind it reading nought, with nothing to say why and
/// nothing that would fail.
///
/// So the points are a list rather than a switch buried in whoever happened to be asked. What the
/// picture draws can be walked against what the table answers for, which is
/// <c>Tests/OneRoutingModelTests.cs</c>, and that is the whole of what makes them one thing
/// rather than two that agree by luck.
///
/// Handed the readings rather than reaching for them, so the list can be put a question to
/// without an engine, a sound card or a window.
/// </remarks>
public interface ISignalPoints
{
    /// <summary>
    /// Every point this application can say something about.
    /// </summary>
    /// <remarks>
    /// Ours alone. A block on the machine is somebody else's program and we measure nothing about
    /// it, which is why a meter there is absent rather than empty.
    /// </remarks>
    IReadOnlyList<SignalPoint> Ours { get; }

    /// <summary>What that point is carrying now.</summary>
    /// <remarks>
    /// Nothing rather than nought for a point that is not ours, since those are two different
    /// answers: nought is silence and nothing is nobody can say.
    /// </remarks>
    /// <param name="point">The place on the routing.</param>
    PatchLevel At(SignalPoint point);
}
