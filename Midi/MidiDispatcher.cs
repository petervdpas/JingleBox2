using System;
using JingleBox2.Diagnostics;

namespace JingleBox2.Midi;

/// <summary>
/// Sends each message where its device is pointed. Everything upstream of this is device
/// agnostic, and everything downstream never has to ask who sent what.
/// </summary>
public sealed class MidiDispatcher
{
    private readonly MidiConfig _cfg;
    private readonly Action<MidiMessage>? _pads;
    private readonly Action<MidiMessage>? _tracker;
    private readonly Action<MidiMessage>? _controls;

    public MidiDispatcher(MidiConfig cfg, Action<MidiMessage>? pads, Action<MidiMessage>? tracker,
                          Action<MidiMessage>? controls = null)
    {
        _cfg = cfg;
        _pads = pads;
        _tracker = tracker;
        _controls = controls;
    }

    public void Handle(MidiMessage msg)
    {
        if (msg is null) return;

        var role = MidiDeviceBindings.RoleFor(_cfg.Devices, msg.Device);

        // Only when it goes nowhere. A message that is delivered says so further down, in the
        // words of whoever it reached; one that is dropped here has nobody left to speak for it,
        // and a controller that does nothing because it was never given a job is the single
        // most common thing to be looking for.
        if (role == MidiDeviceRole.None)
            Log.Write(LogArea.Midi, () =>
                "dispatch " + msg.Type + " ch" + msg.Channel + " val=" + msg.Value
                + " from '" + msg.Device + "' DRIVES NOTHING: it has been given no job in SETTINGS");

        if ((role & MidiDeviceRole.Pads) != 0) _pads?.Invoke(msg);
        if ((role & MidiDeviceRole.Tracker) != 0) _tracker?.Invoke(msg);
        if ((role & MidiDeviceRole.Controls) != 0) _controls?.Invoke(msg);
    }
}
