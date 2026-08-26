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

    /// <summary>
    /// Every knob and fader that has been pointed at something.
    /// </summary>
    /// <remarks>
    /// In the settings rather than in a song, because the controller is in the room and the
    /// song is in a file. A mapping names a machine and a parameter, so it is true of every
    /// song you open rather than of the one it was made in: see <see cref="ControlMapping"/>.
    /// </remarks>
    public List<ControlMapping> Controls { get; set; } = new();
}
