using JingleBox2.Midi.Enums;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// The transport's four keys, pressed from somewhere other than the screen.
/// </summary>
/// <remarks>
/// The seam a link goes through, and it exists because a link and a protocol are not the same
/// thing. <c>ITransportKeys</c> is what a controller speaking Mackie Control or machine control
/// asks for, and it carries three words because three words is all those protocols have. A
/// person pointing a button at the transport is looking at four keys and can mean any of them,
/// pause included.
///
/// Pressing rather than setting, because none of the four is a value. What each one does is
/// still somebody else's question: the transport is patched to the page in front of you, so
/// play plays a song on TRACKER and a take on RECORD.
/// </remarks>
public interface ITransportPresses
{
    /// <summary>That key, as though it had been clicked.</summary>
    /// <param name="key">Which of the four.</param>
    void Press(TransportKey key);
}
