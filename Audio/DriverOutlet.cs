using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// An ASIO driver, which owns the card outright: the library is opened on its own silent device,
/// the mix is a decoding stream, and the driver takes blocks out of it at whatever length its own
/// panel is set to.
///
/// It holds the driver's number, which is what lets it be opened with the same two arguments as
/// every other outlet. Written out at the call site instead, that number was a third argument
/// threaded through two methods for the sake of one of the three ways out.
/// </remarks>
public sealed class DriverOutlet : IMixOutlet
{
    /// <summary>The drivers this machine has, and the one that is open.</summary>
    private readonly IAsioDevices _asio;

    /// <summary>Which driver, counting from nought inside its own world.</summary>
    private readonly int _index;

    /// <summary>Takes the drivers and which of them this is.</summary>
    /// <param name="asio">The drivers this machine has.</param>
    /// <param name="index">Which one.</param>
    public DriverOutlet(IAsioDevices asio, int index)
    {
        _asio = asio;
        _index = index;
    }

    /// <inheritdoc/>
    public bool Pulls => true;

    /// <inheritdoc/>
    public string Word => "the driver";

    /// <inheritdoc/>
    public bool Open(int stream, int rate) => _asio.Open(_index, stream, rate);

    /// <inheritdoc/>
    public string Why => _asio.Missing;

    /// <inheritdoc/>
    public void Close() => _asio.Close();
}
