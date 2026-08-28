using System;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Enums;

namespace JingleBox2.Midi;

/// <summary>
/// Sends each message where its device is pointed. Everything upstream of this is device
/// agnostic, and everything downstream never has to ask who sent what.
/// </summary>
/// <remarks>
/// The one place the settings' answer to "what does this controller drive" is applied. A role is
/// flags rather than a choice, so a message can go to two places at once, which is what one
/// controller doing two jobs looks like: the keys play the tracker and the knobs move the
/// machine.
///
/// The role is looked up per message rather than per device, because a device can be given a
/// different job while it is plugged in and the next message should already obey.
/// </remarks>
public sealed class MidiDispatcher
{
    private readonly MidiConfig _cfg;
    private readonly Action<MidiMessage>? _pads;
    private readonly Action<MidiMessage>? _tracker;
    private readonly Action<MidiMessage>? _controls;
    private readonly Action<MidiMessage>? _transport;

    /// <param name="cfg">The settings, read live, so a job given a moment ago is already in force.</param>
    /// <param name="pads">Where a message for the pads goes: buttons on their way to being fired.</param>
    /// <param name="tracker">Where the keys go, to be played and to be typed into a pattern.</param>
    /// <param name="controls">Where the knobs go, to whatever they have been pointed at.</param>
    /// <param name="transport">Where play, stop and the rest go, in whichever of the three dialects.</param>
    /// <remarks>
    /// Every one of the four is optional, because the pieces are wired at different points as the
    /// window is built and a half wired dispatcher is better than a null one.
    /// </remarks>
    public MidiDispatcher(MidiConfig cfg, Action<MidiMessage>? pads, Action<MidiMessage>? tracker,
                          Action<MidiMessage>? controls = null, Action<MidiMessage>? transport = null)
    {
        _cfg = cfg;
        _pads = pads;
        _tracker = tracker;
        _controls = controls;
        _transport = transport;
    }

    /// <summary>
    /// Hands the message to each half its device has been pointed at.
    /// </summary>
    /// <remarks>
    /// A line is written only when it goes nowhere. A message that is delivered says so further
    /// down in the words of whoever it reached; one dropped here has nobody left to speak for it,
    /// and a controller that does nothing because it was never given a job in SETTINGS is the
    /// single most common thing anybody is looking for in this log.
    /// </remarks>
    public void Handle(MidiMessage msg)
    {
        if (msg is null) return;

        var role = MidiDeviceBindings.RoleFor(_cfg.Devices, msg.Device);

        if (role == MidiDeviceRole.None)
            Log.Write(LogArea.Midi, () =>
                "dispatch " + msg.Type + " ch" + msg.Channel + " val=" + msg.Value
                + " from '" + msg.Device + "' DRIVES NOTHING: it has been given no job in SETTINGS");

        if ((role & MidiDeviceRole.Pads) != 0) _pads?.Invoke(msg);
        if ((role & MidiDeviceRole.Tracker) != 0) _tracker?.Invoke(msg);
        if ((role & MidiDeviceRole.Controls) != 0) _controls?.Invoke(msg);
        if ((role & MidiDeviceRole.Transport) != 0) _transport?.Invoke(msg);
    }
}
