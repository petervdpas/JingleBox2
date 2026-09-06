using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Routing.Interfaces;

namespace JingleBox2.Audio.Routing;

/// <inheritdoc/>
public sealed class AudioRoutingFactory : IAudioRoutingFactory
{
    /// <inheritdoc/>
    public IAudioRouting Create(IRecordingService recording, ISilentOutput? silent = null)
    {
        var pipewire = new PipeWireRouting();
        if (pipewire.IsAvailable) return pipewire;

        var loopback = new WindowsLoopbackRouting(recording, silent);
        if (loopback.IsAvailable) return loopback;

        return new NoAudioRouting();
    }
}
