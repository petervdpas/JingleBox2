using JingleBox2.Diagnostics;
using System;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <summary>
/// The transport buttons on a controller that speaks Mackie Control.
/// </summary>
/// <remarks>
/// The fourth router, and the same shape as the three before it: this one knows the protocol
/// and nothing about the application, and an adapter reaches the transport. See
/// <see cref="MidiRouter"/> for pads, <see cref="MidiNoteRouter"/> for notes and
/// <see cref="MidiControlRouter"/> for knobs.
///
/// Mackie Control is what a controller speaks when it has been told which DAW it is talking to.
/// It is a published protocol, and its transport buttons are notes at fixed numbers, pressed as
/// a note on at full velocity and released as a note off. Measured on a MiniLab 3 in DAW mode,
/// which sends exactly these:
/// <code>
/// 0x56  86  cycle
/// 0x5D  93  stop
/// 0x5E  94  play
/// 0x5F  95  record
/// </code>
///
/// A controller like that splits itself in two: the buttons go out one port speaking this, and
/// everything else stays ordinary MIDI on another. Which is why this is a role of its own in
/// SETTINGS rather than something the pads or the tracker would find: on the port these arrive
/// on, note 94 is the play button and not a note anybody wants to hear.
/// </remarks>
public sealed class MidiTransportRouter
{
    /// <summary>The notes Mackie Control puts its transport on.</summary>
    private const int Rewind = 0x5B;
    private const int Forward = 0x5C;
    private const int Stop = 0x5D;
    private const int Play = 0x5E;
    private const int Record = 0x5F;
    private const int Cycle = 0x56;

    /// <summary>
    /// The same four buttons as ordinary controllers, which is the other thing they do.
    /// </summary>
    /// <remarks>
    /// A MiniLab 3's transport is not a row of buttons at all. It is pads four to eight with
    /// shift held, and in the device's DAW program they arrive on the ordinary port as plain
    /// controllers, in the order Mackie Control puts them in too:
    /// <code>
    /// CC 105  loop     CC 106  stop     CC 107  play     CC 108  record     CC 109  tap tempo
    /// </code>
    ///
    /// Under Mackie Control the first four are notes 0x56, 0x5D, 0x5E and 0x5F, in the same
    /// order. Arturia's manual asks for one or the other and not both: a host using the DAW
    /// program is told to leave the MCU port switched off, because the two would answer the
    /// same press twice.
    ///
    /// So both are read, and reading both costs nothing. A device sends one or the other
    /// depending on which program it is in, and neither number means anything else on a port
    /// somebody has pointed at the transport.
    ///
    /// Tap tempo is named and left alone. It is the one button here with somewhere obvious to
    /// go that it has not been given yet: four taps is a tempo, and the tracker has one.
    /// </remarks>
    private const int StopCc = 106;
    private const int PlayCc = 107;
    private const int RecordCc = 108;
    private const int CycleCc = 105;

    /// <summary>Tap tempo, which is the fifth of them and sets nothing here yet.</summary>
    private const int TapCc = 109;

    private readonly ITransportKeys _transport;

    /// <param name="transport">Where a press comes out. Not a view model, so this can be tested.</param>
    public MidiTransportRouter(ITransportKeys transport) => _transport = transport;

    /// <summary>The three realtime bytes, which are the transport as MIDI has always had it.</summary>
    /// <remarks>
    /// Older than Mackie Control by a decade and understood by every sequencer ever built,
    /// because they are in the specification rather than in a manufacturer's protocol. One byte,
    /// no channel, no data. Continue is play: a device that stopped part way and is starting
    /// again is asking for the same thing here, since this transport has no memory of where it
    /// was.
    /// </remarks>
    private const int Started = 0xFA;
    private const int Continued = 0xFB;
    private const int RealtimeStop = 0xFC;

    /// <summary>
    /// What a MIDI Machine Control message asks for.
    /// </summary>
    /// <remarks>
    /// The other dialect, and the one a KeyStep Pro sends by default, which is why it exists
    /// here: on that device the three transport buttons are MMC unless somebody goes into the
    /// utility menu and changes it. The frame is
    /// <code>F0 7F &lt;device&gt; 06 &lt;command&gt; F7</code>
    /// where the device byte is 0x7F for everybody at once, and the command is the whole of the
    /// message.
    /// </remarks>
    private const int MmcStop = 0x01;
    private const int MmcPlay = 0x02;
    private const int MmcDeferredPlay = 0x03;
    private const int MmcForward = 0x04;
    private const int MmcRewind = 0x05;
    private const int MmcRecord = 0x06;
    private const int MmcRecordExit = 0x07;
    private const int MmcPause = 0x09;

    /// <summary>
    /// Reads a transport press, in whichever of the three dialects the device chose.
    /// </summary>
    /// <remarks>
    /// A realtime byte and a machine control message are neither of them a button, so neither has
    /// a press to wait for. Both are read above the guard rather than being given a pressed-ness
    /// they do not have, which is also what keeps them out of the pads: every other router in the
    /// application begins by asking for a press, and <see cref="MidiMessage.IsOn"/> is false on
    /// both kinds.
    ///
    /// For the two dialects that are buttons it is the press and not the release. A button sends
    /// both halves, and acting on both would stop what the press had just started.
    ///
    /// Everything named and left alone is named on purpose, so the log says what was pressed
    /// rather than a number, and so that giving one of them a job later is a line here rather
    /// than a rediscovery of the protocol.
    /// </remarks>
    public void Handle(MidiMessage message)
    {
        if (message is null) return;

        if (message.Type == MidiMessageType.Realtime) { Realtime(message); return; }
        if (message.Type == MidiMessageType.SystemExclusive) { Exclusive(message); return; }

        if (!message.IsOn) return;

        if (message.Type == MidiMessageType.ControlChange)
        {
            switch (message.Value)
            {
                case PlayCc: Say(message, "play"); _transport.Play(); return;
                case StopCc: Say(message, "stop"); _transport.Stop(); return;
                case RecordCc: Say(message, "record"); _transport.Record(); return;
                case CycleCc: Say(message, "loop, which this does nothing with yet"); return;
                case TapCc: Say(message, "tap tempo, which this does nothing with yet"); return;
            }

            return;
        }

        if (message.Type != MidiMessageType.Note) return;

        switch (message.Value)
        {
            case Play: Say(message, "play"); _transport.Play(); return;
            case Stop: Say(message, "stop"); _transport.Stop(); return;
            case Record: Say(message, "record"); _transport.Record(); return;

            case Rewind: Say(message, "rewind, which this does nothing with yet"); return;
            case Forward: Say(message, "forward, which this does nothing with yet"); return;
            case Cycle: Say(message, "cycle, which this does nothing with yet"); return;
        }

        Log.Write(LogArea.Midi, () =>
            "transport: '" + message.Device + "' sent note " + message.Value
            + ", which is not one of Mackie Control's transport buttons");
    }

    /// <summary>Start, continue or stop, straight off the wire.</summary>
    private void Realtime(MidiMessage message)
    {
        switch (message.Value)
        {
            case Started: Told(message, "start"); _transport.Play(); return;
            case Continued: Told(message, "continue, which is play here"); _transport.Play(); return;
            case RealtimeStop: Told(message, "stop"); _transport.Stop(); return;
        }
    }

    /// <summary>
    /// MIDI Machine Control, which is a system exclusive message and nothing else.
    /// </summary>
    /// <remarks>
    /// Every other system exclusive message belongs to somebody, a device answering who it is or
    /// a manufacturer's own settings protocol, and is left alone in silence: it is not this
    /// router's business, and the wire has already written down what arrived.
    ///
    /// A pause is a stop, because this transport has nowhere to be paused at.
    /// </remarks>
    private void Exclusive(MidiMessage message)
    {
        int command = Command(message.Bytes);

        if (command < 0) return;

        switch (command)
        {
            case MmcPlay: Told(message, "play"); _transport.Play(); return;
            case MmcDeferredPlay: Told(message, "deferred play, which is play here"); _transport.Play(); return;
            case MmcStop: Told(message, "stop"); _transport.Stop(); return;

            case MmcPause: Told(message, "pause, which is stop here"); _transport.Stop(); return;

            case MmcRecord: Told(message, "record"); _transport.Record(); return;

            case MmcRecordExit: Told(message, "record exit, which this does nothing with yet"); return;
            case MmcForward: Told(message, "fast forward, which this does nothing with yet"); return;
            case MmcRewind: Told(message, "rewind, which this does nothing with yet"); return;
        }

        Log.Write(LogArea.Midi, () =>
            "transport: '" + message.Device + "' sent machine control command "
            + command.ToString("X2") + ", which is not one this reads");
    }

    /// <summary>
    /// The command out of a machine control message, or below nought when it is not one.
    /// </summary>
    /// <remarks>
    /// <c>F0 7F &lt;device&gt; 06 &lt;command&gt; F7</c>. The device byte is not checked: 0x7F means
    /// everybody and is what hardware sends, and a device addressed to a particular unit number
    /// is addressing a tape machine that has not existed for thirty years. Refusing it would
    /// mean a transport button that does nothing for a reason nobody could ever guess.
    /// </remarks>
    private static int Command(byte[]? sysex)
    {
        if (sysex is null || sysex.Length < 6) return -1;

        if (sysex[0] != 0xF0 || sysex[1] != 0x7F || sysex[3] != 0x06) return -1;
        if (sysex[^1] != 0xF7) return -1;

        return sysex[4];
    }

    /// <summary>The line a press earns, naming the device and what was pressed on it.</summary>
    private static void Say(MidiMessage message, string what) =>
        Log.Write(LogArea.Midi, () => "transport: '" + message.Device + "' pressed " + what);

    /// <summary>The same line, for the two dialects where nobody pressed anything.</summary>
    private static void Told(MidiMessage message, string what) =>
        Log.Write(LogArea.Midi, () => "transport: '" + message.Device + "' asked for " + what);
}
