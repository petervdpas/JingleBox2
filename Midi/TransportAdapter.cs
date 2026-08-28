using JingleBox2.Diagnostics;
using JingleBox2.ViewModels;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Interfaces;

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

    /// <param name="transport">The caps at the top of the window, patched to the page in front.</param>
    public TransportAdapter(TransportSwitch transport) => _transport = transport;

    /// <inheritdoc/>
    public void Play()
    {
        Said("play", _transport.CanPlay);
        _transport.PlayCommand.Execute(null);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The one of the three that always has something behind it, borrowed or not, so it is said
    /// plainly rather than through <see cref="Said"/>: there is no refusal to report.
    /// </remarks>
    public void Stop()
    {
        Log.Write(LogArea.Midi, () => "transport: stop, on the page in front of you");
        _transport.StopCommand.Execute(null);
    }

    /// <inheritdoc/>
    public void Record()
    {
        Said("record", _transport.CanRecord);
        _transport.RecordCommand.Execute(null);
    }

    /// <summary>
    /// Says whether that press had anywhere to go, since the command itself will not.
    /// </summary>
    /// <remarks>
    /// The last silent drop on this path, and the one that costs the most to find. A button on
    /// the desk is recognised, named in the log, handed to the transport, and then quietly
    /// refused, because the transport is patched to the page you are on and SETTINGS has
    /// nothing to play. From outside that is identical to a button that sent nothing at all,
    /// which is where an evening goes.
    /// </remarks>
    private void Said(string what, bool can) =>
        Log.Write(LogArea.Midi, () =>
            can
                ? "transport: " + what + ", on the page in front of you"
                : "transport: " + what + " ARRIVED AND WAS REFUSED. The transport belongs to the"
                  + " page you are on, and that page cannot " + what
                  + (_transport.Borrowed
                      ? ". Something is running on another page, so only stop is live until it is stopped"
                      : ". TRACKER plays a song and RECORD plays a take; the others have nothing to play"));
}
