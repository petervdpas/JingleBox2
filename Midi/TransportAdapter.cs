using JingleBox2.ViewModels;

namespace JingleBox2.Midi;

/// <summary>
/// The transport buttons, wired to the caps at the top of the window.
/// </summary>
/// <remarks>
/// Which means they are page sensitive without knowing it: the transport is patched to the deck
/// of whatever page you are on, so play on the keyboard plays the song on TRACKER and a take on
/// RECORD, and does nothing on the pages that cannot. The same thing the space bar and Ctrl+R
/// already work, pressed from somewhere else.
/// </remarks>
public sealed class TransportAdapter : ITransportKeys
{
    private readonly TransportSwitch _transport;

    public TransportAdapter(TransportSwitch transport) => _transport = transport;

    public void Play() => _transport.PlayCommand.Execute(null);

    public void Stop() => _transport.StopCommand.Execute(null);

    public void Record() => _transport.RecordCommand.Execute(null);
}
