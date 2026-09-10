using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// Whose clock the transport runs on: its own, or one named port's.
    /// </summary>
    /// <remarks>
    /// Nought is its own, so every settings file written before this reads back as the tracker
    /// keeping its own time, which is what it was doing.
    /// </remarks>
    public Enums.MidiClockSource ClockSource { get; set; } = Enums.MidiClockSource.Own;

    /// <summary>
    /// Which port's clock is followed, when one is.
    /// </summary>
    /// <remarks>
    /// A name rather than an index, the same as everything else here, since a port's number moves
    /// when something else is plugged in and its name does not. Meaningless while
    /// <see cref="ClockSource"/> is its own, and kept anyway: unticking follow and ticking it
    /// again should not make somebody find the port a second time.
    /// </remarks>
    public string? ClockPort { get; set; }

    /// <summary>
    /// Which outputs are sent clock, by name.
    /// </summary>
    /// <remarks>
    /// **A list, and independent of <see cref="ClockSource"/>.** Any number of devices can be
    /// driven at once, and whether this machine is keeping its own time has nothing to do with
    /// whether it is passing time on: a transport following an external clock may still be the
    /// only thing a drum machine is plugged into.
    ///
    /// Empty means nothing is sent, which is what a fresh installation does. Nothing here is
    /// switched on for somebody: clock arriving at a device that was not expecting it is a device
    /// that starts running when its owner did not ask.
    /// </remarks>
    public List<string> ClockOutputs { get; set; } = new();

    /// <summary>
    /// The outputs that are really driven, which is <see cref="ClockOutputs"/> without the port
    /// being followed.
    /// </summary>
    /// <remarks>
    /// **The one rule that keeps a clock from being echoed at the machine that sent it.** A device
    /// with a MIDI in and a MIDI out is one port name in the input list and one in the output
    /// list, and where the two read the same the obvious way to set this up is to follow it and
    /// tick it. Every tick would then go straight back, and a device that recognises clock as well
    /// as sending it is in a loop neither end can see. Nothing is named here and nothing is
    /// looked up: the port that is followed is whatever somebody chose, and it is that one that is
    /// left out.
    ///
    /// Read here rather than guarded at the wire because this is the one place both answers are
    /// already in hand, and because it is a decision about the setting rather than about a
    /// message: what the settings page shows ticked is what somebody asked for, and what is driven
    /// is what that can honestly mean. Left out silently would be a tick doing nothing with no
    /// explanation, so whoever calls this says which was dropped.
    ///
    /// Compared without regard to case, like every other port name here. With nothing followed it
    /// is <see cref="ClockOutputs"/> exactly, which is every machine on its own clock.
    /// </remarks>
    [JsonIgnore]
    public IReadOnlyList<string> ClockDriven
    {
        get
        {
            if (ClockSource != Enums.MidiClockSource.Followed) return ClockOutputs;
            if (string.IsNullOrWhiteSpace(ClockPort)) return ClockOutputs;

            var kept = new List<string>(ClockOutputs.Count);

            foreach (string one in ClockOutputs)
                if (!string.Equals(one, ClockPort, StringComparison.OrdinalIgnoreCase))
                    kept.Add(one);

            return kept;
        }
    }
}
