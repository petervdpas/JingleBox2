using System.Collections.Generic;

namespace JingleBox2.Midi;

public sealed class MidiConfig
{
    /// <summary>
    /// The single device older versions stored. Read once at load so the setting migrates into
    /// <see cref="Devices"/>, then left null.
    /// </summary>
    public string? InputDevice { get; set; }

    /// <summary>Every controller the app knows about, with the job it was given.</summary>
    public List<MidiDeviceBinding> Devices { get; set; } = new();

    public bool ToggleMode { get; set; } = true;

    public List<MidiMapping> Pads { get; set; } = new();
}
