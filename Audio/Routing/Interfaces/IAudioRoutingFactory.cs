using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio.Routing.Interfaces;

/// <summary>
/// Picks the routing the machine can actually do.
/// </summary>
/// <remarks>
/// Everything above this holds <see cref="IAudioRouting"/>, so a platform without a patchable
/// graph is a quiet absence rather than a special case anybody has to write around.
/// </remarks>
public interface IAudioRoutingFactory
{
    /// <summary>
    /// The routing this machine can do, asked of each in turn rather than decided by what the
    /// operating system is called.
    /// </summary>
    /// <remarks>
    /// A Linux machine without the PipeWire tools installed has no graph to patch, and only the
    /// machine can answer that, which is why each candidate is built and asked rather than
    /// chosen by platform. PipeWire goes first because it is the only one here that really
    /// rewires anything; the Windows one only chooses what the recorder listens to. When
    /// neither will do the job the answer is a routing that does nothing rather than null, so
    /// every caller holds an object and none of them has to carry a special case.
    /// </remarks>
    /// <param name="recording">
    /// The recorder, which the Windows side points at a device or an output. The PipeWire side
    /// never touches it: it patches the graph underneath and the recorder knows nothing about it.
    /// </param>
    IAudioRouting Create(IRecordingService recording);
}
