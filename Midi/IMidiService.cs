using System;
using System.Collections.Generic;

namespace JingleBox2.Midi;

public interface IMidiService : IDisposable
{
    IReadOnlyList<string> GetInputDevices();

    /// <summary>The devices currently open, by name.</summary>
    IReadOnlyList<string> OpenDevices { get; }

    /// <summary>Opens a device and leaves the others alone. True when it is open afterwards.</summary>
    bool Open(string deviceIdOrName);

    void Close(string deviceIdOrName);
    void CloseAll();

    event EventHandler<MidiMessage>? MessageReceived;
}
