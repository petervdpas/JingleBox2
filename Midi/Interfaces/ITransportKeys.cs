
namespace JingleBox2.Midi.Interfaces;

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

    /// <summary>
    /// Cycle: turns looping on or off on whichever page the transport is patched to.
    /// </summary>
    /// <remarks>
    /// The fourth word, and it was the one every dialect already carried and nothing read.
    /// Mackie Control has it as note 0x56, a MiniLab sends controller 105, a nanoKONTROL2's
    /// CYCLE is controller 46, and all three were named here and answered with a log line saying
    /// this does nothing with it yet. It has somewhere obvious to go: the Loop switch sits in the
    /// tracker's bar beside the Pattern or Song picker, because what the end is and what happens
    /// when you reach it are one question, and a control surface puts its cycle key in the
    /// transport row for the same reason.
    ///
    /// Nothing on a page with nothing to go round, which is where <c>ITransportDeck.Loop</c>
    /// being defaulted to nothing earns its keep.
    /// </remarks>
    void Loop();
}
