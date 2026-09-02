using JingleBox2.Midi.Enums;

namespace JingleBox2.Midi.Records;

/// <summary>One device as the settings list shows it: bound or not, plugged in or not.</summary>
/// <param name="Device">The port's name, which is the only identity that survives a replug.</param>
/// <param name="IsConnected">False for one that is bound but not in the room right now.</param>
/// <param name="Role">Everything it has been given to do, which can be more than one thing.</param>
public readonly record struct MidiDeviceEntry(string Device, bool IsConnected, MidiPortRole Role);
