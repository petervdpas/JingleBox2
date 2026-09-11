using JingleBox2.Audio.Enums;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Three answers and they are asked in order, because two of them can be true of one machine: a
/// Windows box with a driver installed still has a system default, and the driver was picked on
/// purpose. So a driver named in the settings wins, the sound server is next, and the library is
/// what is left.
///
/// **The server is asked about a description, so the description has to be of a real device.**
/// A number below nought is a request for whatever the machine plays through rather than a row
/// in the library's list, and asked to describe one of those the library says no: the name comes
/// back empty and the default flag comes back false, which is this rule being asked about nothing
/// and answering that there is no server. Resolving that number to the row the library marks as
/// the default is the caller's, since only the caller has the library open.
/// </remarks>
public sealed class MixOutlets : IMixOutlets
{
    /// <summary>The drivers this machine has.</summary>
    private readonly IAsioDevices _asio;

    /// <summary>This application as a node on the sound server's graph.</summary>
    private readonly IPipeWireOutput _pipe;

    /// <summary>Which description means the server.</summary>
    private readonly ISoundServerOutput _server;

    /// <summary>Takes the three things an answer can be made of.</summary>
    /// <param name="asio">The drivers, or the machine's own.</param>
    /// <param name="pipe">The graph node, or the machine's own.</param>
    /// <param name="server">Which description means the server, or the ordinary rule.</param>
    public MixOutlets(
        IAsioDevices? asio = null,
        IPipeWireOutput? pipe = null,
        ISoundServerOutput? server = null)
    {
        _asio = asio ?? new AsioDevices();
        _pipe = pipe ?? new PipeWireOutput();
        _server = server ?? new SoundServerOutput();
    }

    /// <inheritdoc/>
    public IMixOutlet For(AudioOutputKind kind, int index, string named, bool standard) =>
        kind == AudioOutputKind.Asio ? new DriverOutlet(_asio, index)
        : _pipe.Present && _server.Is(named, standard) ? new SoundServerOutlet(_pipe)
        : new LibraryOutlet();
}
