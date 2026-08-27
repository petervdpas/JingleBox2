using JingleBox2.Diagnostics;
using System;

namespace JingleBox2.Midi;

/// <summary>What a controller's transport buttons ask for.</summary>
public interface ITransportKeys
{
    void Play();
    void Stop();
    void Record();
}

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

    public MidiTransportRouter(ITransportKeys transport) => _transport = transport;

    public void Handle(MidiMessage message)
    {
        if (message is null) return;

        // The press, not the release. A button sends both, and doing it twice would stop what
        // the press had just started.
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

            // Every other controller on that port is somebody's knob, and this is not the
            // place that reads those.
            return;
        }

        if (message.Type != MidiMessageType.Note) return;

        switch (message.Value)
        {
            case Play: Say(message, "play"); _transport.Play(); return;
            case Stop: Say(message, "stop"); _transport.Stop(); return;
            case Record: Say(message, "record"); _transport.Record(); return;

            // Named so the log says what was pressed rather than a number, and so that adding
            // one later is a line here instead of a rediscovery of the protocol.
            case Rewind: Say(message, "rewind, which this does nothing with yet"); return;
            case Forward: Say(message, "forward, which this does nothing with yet"); return;
            case Cycle: Say(message, "cycle, which this does nothing with yet"); return;
        }

        Log.Write(LogArea.Midi, () =>
            "transport: '" + message.Device + "' sent note " + message.Value
            + ", which is not one of Mackie Control's transport buttons");
    }

    private static void Say(MidiMessage message, string what) =>
        Log.Write(LogArea.Midi, () => "transport: '" + message.Device + "' pressed " + what);
}
