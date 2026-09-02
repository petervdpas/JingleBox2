using System.Collections.Generic;

namespace JingleBox2.Midi;

/// <summary>
/// Everything about MIDI that survives the application being shut.
/// </summary>
/// <remarks>
/// The desk rather than the music. Which controllers there are and what each was given to do,
/// which button fires which pad, and every knob that has been pointed at something. None of it
/// belongs to a song: the hardware is in the room and the song is in a file, so opening another
/// song leaves all of this exactly as it was. The one exception is a link made against an
/// instrument on a track, which is about that piece of music and is kept in the <c>.jibx</c>;
/// see <see cref="ControlLink"/> for which of the two a link lands in and why.
/// </remarks>
public sealed class MidiConfig
{
    /// <summary>
    /// The single device older versions stored. Read once at load so the setting migrates into
    /// <see cref="Devices"/>, then left null.
    /// </summary>
    public string? InputDevice { get; set; }

    /// <summary>Every controller the app knows about, with the job it was given.</summary>
    public List<MidiPortBinding> Devices { get; set; } = new();

    /// <summary>
    /// Whether a mapped button toggles a pad or always starts it from the beginning.
    /// </summary>
    /// <remarks>
    /// One setting for every pad rather than one per mapping. A pad box is used one way or the
    /// other for a whole show, and per-pad it would be sixteen decisions nobody wants to make.
    /// </remarks>
    public bool ToggleMode { get; set; } = true;

    /// <summary>Which button fires which pad.</summary>
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
