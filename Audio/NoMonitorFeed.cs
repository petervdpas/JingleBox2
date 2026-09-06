using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// For anything holding an engine that makes no sound: a test, and the plugin host process,
/// which has a recorder in it the way it has everything else in it and captures nothing.
/// Answering nothing is the honest answer there, rather than a stream on a bus that is not open.
/// </remarks>
public sealed class NoMonitorFeed : IMonitorFeed
{
    /// <inheritdoc/>
    public bool IsOpen => false;

    /// <inheritdoc/>
    public Plugins.Interfaces.IAudioInsert? Insert { get; set; }

    /// <inheritdoc/>
    public bool Open(int rate, int channels) => false;

    /// <inheritdoc/>
    public void Push(byte[] data, int bytes)
    {
    }

    /// <inheritdoc/>
    public void Close()
    {
    }
}
