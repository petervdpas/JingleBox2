namespace JingleBox2.Midi;

/// <summary>
/// What a controller's transport buttons ask for.
/// </summary>
/// <remarks>
/// Three words and nothing else, because three words is all the hardware has. What they do is
/// somebody else's question: the transport in this application is patched to the page in front
/// of you, so play plays a song on TRACKER and a take on RECORD and does nothing on the pages
/// that have nothing to play.
///
/// The seam matters more here than the size of it suggests. The three dialects a device can ask
/// in (Mackie Control notes, plain controllers, realtime bytes and machine control) all come out
/// as one of these, so <see cref="MidiTransportRouter"/> can be tested on raw bytes with nothing
/// behind it but a counter.
/// </remarks>
public interface ITransportKeys
{
    /// <summary>Start, or carry on. A device that says continue is asking for this.</summary>
    void Play();

    /// <summary>Stop. A device that says pause is asking for this, since there is nowhere to pause.</summary>
    void Stop();

    /// <summary>Record.</summary>
    void Record();
}
