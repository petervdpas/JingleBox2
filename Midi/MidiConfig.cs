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
/// song leaves all of this exactly as it was. There is no exception, an instrument on a track
/// included: every learned link lands here, and a link an older song is still holding is read
/// and displaced rather than added to. See <see cref="ControlLink"/>.
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

    /// <summary>
    /// Whether a knob takes hold of a value on its first movement rather than waiting to be
    /// swept past it.
    /// </summary>
    /// <remarks>
    /// Off, which is what a hardware desk does and what this has always done: a control that says
    /// where it is takes hold only once your hand has passed where the parameter already sits, so
    /// a knob left at three o'clock does not snap a filter wide open the moment it is nudged.
    ///
    /// On, the value follows the knob at once. **That is what somebody wants who has just applied
    /// a template**, where otherwise every control on it has to be swept to the value it is
    /// already about to set before it does anything: from a chair, a knob that is picking up and
    /// a knob that is not wired at all look exactly alike, and hunting for eight values in turn is
    /// worse than the lurch picking up avoids.
    ///
    /// One setting for the whole desk rather than one per link, the shape <see cref="ToggleMode"/>
    /// already keeps: it is how somebody is working rather than a fact about one knob, and per
    /// link it would be a decision to make every time anything was pointed at anything.
    ///
    /// It reaches only the controls it is about, which are the ones saying where they are. An
    /// endless encoder sends how far it turned and has nothing to reconcile, so nothing here
    /// touches it; and what a control is is still worked out by watching it, since that is a fact
    /// about the hardware rather than a preference.
    /// </remarks>
    public bool InstantPickup { get; set; }

    /// <summary>Which button fires which pad.</summary>
    public List<MidiMapping> Pads { get; set; } = new();

    /// <summary>
    /// What a settings file written before the links had a file of their own is still holding.
    /// </summary>
    /// <remarks>
    /// **Read and never written.** Every knob and fader pointed at something lives in
    /// <c>remotecontrol-links.json</c> now, which is
    /// <see cref="Interfaces.IRemoteControlLinks"/>: they are written from the MIDI thread as a
    /// hand learns a knob, they were a fifth of a settings document that is serialised whole
    /// whenever anything on any page moves, and they are the one thing in it somebody carries
    /// between machines.
    ///
    /// The field stays so that a file already on somebody's disc can be read, exactly as
    /// <see cref="MidiMapping"/> stays for the pad table above it. It is carried across once and
    /// emptied, and an empty one is the flag that it has been: see <c>MainWindow.Carried</c>,
    /// which is the one place that holds both. The pad table's own carry over still lands here
    /// first, since that happens as the settings are read.
    ///
    /// In neither file is it in a song, and that has not changed: the controller is in the room
    /// and the song is in a file, and a link names a machine and a parameter, so it is true of
    /// every song you open rather than of the one it was made in.
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
