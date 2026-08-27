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

    private readonly ITransportKeys _transport;

    public MidiTransportRouter(ITransportKeys transport) => _transport = transport;

    public void Handle(MidiMessage message)
    {
        if (message is null || message.Type != MidiMessageType.Note) return;

        // The press, not the release. A button sends both, and doing it twice would stop what
        // the press had just started.
        if (!message.IsOn) return;

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
