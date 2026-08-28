using JingleBox2.Midi.Enums;

namespace JingleBox2.Midi;

/// <summary>
/// A device and its role, stored by name: device indexes shift when hardware is plugged in
/// or out, and a name survives that.
/// </summary>
public sealed class MidiDeviceBinding
{
    /// <summary>The port's own name, trimmed, since ALSA pads them to a fixed width.</summary>
    public string Device { get; set; } = "";

    /// <summary>What it drives. Never None in a stored list: a binding for nothing is dropped.</summary>
    public MidiDeviceRole Role { get; set; } = MidiDeviceRole.None;
}
