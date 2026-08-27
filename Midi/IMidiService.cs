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

    /// <summary>
    /// Sends bytes to a controller, for the few that listen as well as speak.
    /// </summary>
    /// <remarks>
    /// The application has been input only until now, which is what a controller mostly is. A
    /// few of them have a screen, and a screen is written to rather than read: see
    /// <see cref="ArturiaDisplay"/>.
    ///
    /// The output is opened when it is first needed and kept, because a device with a screen is
    /// written to on every turn of a knob and opening a port per message would be absurd.
    /// </remarks>
    /// <returns>True when it went. False when there is no such output, which is not an error.</returns>
    bool Send(string deviceIdOrName, byte[] bytes);
}
