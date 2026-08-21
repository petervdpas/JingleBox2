namespace JingleBox2.Audio.Routing;

/// <summary>
/// Picks the routing the machine can actually do. Everything above this holds the interface,
/// so a platform without a patchable graph is a quiet absence rather than a special case.
/// </summary>
public static class AudioRouting
{
    public static IAudioRouting Create(IRecordingService recording)
    {
        var pipewire = new PipeWireRouting();
        if (pipewire.IsAvailable) return pipewire;

        var loopback = new WindowsLoopbackRouting(recording);
        if (loopback.IsAvailable) return loopback;

        return new NoAudioRouting();
    }
}
