using System;

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

    public MidiDispatcher(MidiConfig cfg, Action<MidiMessage>? pads, Action<MidiMessage>? tracker)
    {
        _cfg = cfg;
        _pads = pads;
        _tracker = tracker;
    }

    public void Handle(MidiMessage msg)
    {
        if (msg is null) return;

        var role = MidiDeviceBindings.RoleFor(_cfg.Devices, msg.Device);

        if ((role & MidiDeviceRole.Pads) != 0) _pads?.Invoke(msg);
        if ((role & MidiDeviceRole.Tracker) != 0) _tracker?.Invoke(msg);
    }
}
