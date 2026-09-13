namespace JingleBox2.Tracker.Records;

/// <summary>
/// One direction of a track's MIDI: which port and which channel.
/// </summary>
/// <remarks>
/// The same shape both ways, because a track listens to a port and a channel and sends to a port
/// and a channel. Saved with the song on the track's strip, the way Renoise keeps a device and a
/// channel on its instruments and a DAW keeps them on its tracks: which ports are open at all is
/// SETTINGS' business, and which of them a part of this song plays through is the song's.
///
/// A channel of nought is off, which is what a strip written before this reads back as. A port
/// left empty means any open port when listening, and nowhere when sending, since a note has to
/// go somewhere named.
/// </remarks>
public sealed record TrackMidiRoute
{
    /// <summary>The lowest channel, counted from one the way every device's screen counts them.</summary>
    public const int FirstChannel = 1;

    /// <summary>And the highest, since a channel is four bits.</summary>
    public const int LastChannel = 16;

    /// <summary>The port's name as the system gives it, or empty.</summary>
    public string Port { get; init; } = "";

    /// <summary>1 to 16, or nought for off.</summary>
    public int Channel { get; init; }

    /// <summary>True when a channel has been picked.</summary>
    public bool IsOn => Channel is >= FirstChannel and <= LastChannel;

    /// <summary>True when a port has been named.</summary>
    public bool HasPort => !string.IsNullOrWhiteSpace(Port);
}
