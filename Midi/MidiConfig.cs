using System.Collections.Generic;

namespace JingleBox2.Midi;

public sealed class MidiConfig
{
    public string? InputDevice { get; set; }
    public bool ToggleMode { get; set; } = true;

    public List<MidiMapping> Pads { get; set; } = new();
}
