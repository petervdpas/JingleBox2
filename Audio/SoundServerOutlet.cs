using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// The sound server, which owns the machine's graph. Arranged exactly as a driver is and for the
/// same reason: the library is opened on its own silent device, the mix is a decoding stream, and
/// this application is a node the server pulls from.
/// </remarks>
public sealed class SoundServerOutlet : IMixOutlet
{
    /// <summary>This application as a node on the server's graph.</summary>
    private readonly IPipeWireOutput _pipe;

    /// <summary>Takes the node it speaks through.</summary>
    /// <param name="pipe">This application on the graph.</param>
    public SoundServerOutlet(IPipeWireOutput pipe) => _pipe = pipe;

    /// <inheritdoc/>
    public bool Pulls => true;

    /// <inheritdoc/>
    public string Word => "the sound server";

    /// <inheritdoc/>
    public bool Open(int stream, int rate) => _pipe.Open(stream, rate);

    /// <inheritdoc/>
    public string Why => _pipe.Missing;

    /// <inheritdoc/>
    public void Close() => _pipe.Close();
}
