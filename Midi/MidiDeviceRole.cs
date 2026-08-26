namespace JingleBox2.Midi;

/// <summary>
/// What a controller is allowed to drive. Flags rather than a single choice, so one device
/// can do both jobs when there is only one plugged in.
/// </summary>
[System.Flags]
public enum MidiDeviceRole
{
    None = 0,
    Pads = 1,
    Tracker = 2,

    /// <summary>
    /// Knobs and faders, driving parameters. Its own flag because a controller is often two
    /// things at once: the keys play the tracker and the knobs move the machine, and one of
    /// those is a device somebody may want off without the other.
    /// </summary>
    Controls = 4
}

/// <summary>
/// A device and its role, stored by name: device indexes shift when hardware is plugged in
/// or out, and a name survives that.
/// </summary>
public sealed class MidiDeviceBinding
{
    public string Device { get; set; } = "";
    public MidiDeviceRole Role { get; set; } = MidiDeviceRole.None;
}
