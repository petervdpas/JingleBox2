namespace JingleBox2.Midi;

/// <summary>
/// What a controller is allowed to drive. Flags rather than a single choice, so one device
/// can do both jobs when there is only one plugged in.
/// </summary>
[System.Flags]
public enum MidiDeviceRole
{
    /// <summary>Nothing at all, which is what a controller is until somebody says otherwise.</summary>
    None = 0,

    /// <summary>The pads on FIRE and PADS, through <see cref="MidiRouter"/>.</summary>
    Pads = 1,

    /// <summary>Notes into the tracker, which is a keyboard playing an instrument.</summary>
    Tracker = 2,

    /// <summary>
    /// Knobs and faders, driving parameters. Its own flag because a controller is often two
    /// things at once: the keys play the tracker and the knobs move the machine, and one of
    /// those is a device somebody may want off without the other.
    /// </summary>
    Controls = 4,

    /// <summary>
    /// The transport, and on a port that speaks Mackie Control, the whole surface with it.
    /// </summary>
    /// <remarks>
    /// Its own flag because such a controller is two devices as far as the settings are
    /// concerned: the buttons come out one port and everything else out another. On the port
    /// they arrive on, note 94 is the play button and not a note anybody wants to hear, so the
    /// pads and the tracker must not be pointed at it.
    ///
    /// It carries more than its name says, and deliberately. A surface speaking Mackie Control
    /// is one device sending one stream: its transport buttons, its faders, its knobs and its
    /// mute and solo buttons come out together and there is no way to have the first without
    /// the rest. So ticking this gives a real surface its mixer as well, which is what somebody
    /// plugging one in wants and what every other host does. The name is now narrower than the
    /// thing; it is kept because it is what is stored and what people have already ticked.
    /// </remarks>
    Transport = 8
}

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
