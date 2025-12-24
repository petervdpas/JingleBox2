using System;
using System.Collections.Generic;

namespace JingleBox2.Midi;

public sealed class MidiService : IMidiService
{
    public event EventHandler<MidiMessage>? MessageReceived;

    public IReadOnlyList<string> GetInputDevices()
    {
        // TODO: enumerate real MIDI devices
        return Array.Empty<string>();
    }

    public void Open(string deviceName)
    {
        // TODO: open device + hook callbacks
    }

    public void Close()
    {
        // TODO: close device
    }

    public void Dispose()
    {
        Close();
    }

    // Call this from backend callback later
    private void Raise(MidiMessage msg)
        => MessageReceived?.Invoke(this, msg);
}
