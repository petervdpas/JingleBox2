using System.Collections.Generic;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// The output a source is sent to so that nobody hears it, on a machine with no graph.
/// </summary>
/// <remarks>
/// **Somebody has to choose it, and nothing here can choose it for them.** On PipeWire a source
/// is unplugged and that is the whole of it; Windows has no such move, so taking a source aside
/// means sending it somewhere else, and where that is depends on what is plugged into the
/// machine. A virtual cable is the usual answer and a spare output is as good; picking one at
/// random would be sending somebody's programme out of a socket in the wall.
///
/// Kept in the settings, since it is a fact about this installation and this machine: the same
/// cable is the right answer tomorrow.
/// </remarks>
public interface ISilentOutput
{
    /// <summary>Every output that could be used, by the system's own names for them.</summary>
    IReadOnlyList<AudioEndpoint> Outputs { get; }

    /// <summary>
    /// Which of them is the one nobody is listening to, or nothing while none is chosen.
    /// </summary>
    /// <remarks>
    /// Nothing chosen is the ordinary state and means a source cannot be taken aside here, which
    /// the switch says by being grey rather than by failing when it is pressed.
    /// </remarks>
    string? Chosen { get; set; }
}
