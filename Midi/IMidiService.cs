using System;
using System.Collections.Generic;

namespace JingleBox2.Midi;

public interface IMidiService : IDisposable
{
    IReadOnlyList<string> GetInputDevices();

    void Open(string deviceIdOrName);
    void Close();

    event EventHandler<MidiMessage>? MessageReceived;
}
