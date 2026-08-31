using System;
using JingleBox2.Midi.Enums;

namespace JingleBox2.Midi;

/// <summary>
/// One hardware control pointed at one thing in the program.
/// </summary>
/// <remarks>
/// The question this has to answer is what a knob means when the machines are not the same.
/// Knob one on Zampler is a filter and knob one on BongaBong is not; a mapping that says
/// "controller 21 moves the thing on track three" is a mapping that means something different
/// every time you change what track three plays.
///
/// So a mapping about a machine names the machine, and the parameter by the key that machine
/// stores it under. Controller 21 is Zampler's cutoff, and it is Zampler's cutoff on every
/// track, in every song, for ever. Learn your controller once per machine and it is learned.
/// A track playing something else is not driven by it at all, which is right: that knob has
/// nothing to say to a drum machine.
///
/// Which track, then, is a separate question with a separate answer, and that is
/// <see cref="ControlScope"/>. Normally the one you are working on, so a bank of knobs drives
/// whatever is in front of you. Pinned to a track where that is the point, which is the mixer:
/// a fader bank is about all the tracks at once and none of them is the one you are looking at.
///
/// Kept in the settings rather than in the song, because the desk does not change when the song
/// does. The hardware is in the room, not in the file.
/// </remarks>
public sealed class ControlMapping
{
    /// <summary>
    /// The controller this was learned on, by name.
    /// </summary>
    /// <remarks>
    /// Kept because a controller number means nothing on its own: two devices both have a CC 22
    /// and they are not the same knob. Without the name, plugging in a second controller would
    /// have it quietly driving whatever the first one was pointed at.
    ///
    /// A mapping whose device is not plugged in is kept exactly as it is. Nothing prunes it and
    /// nothing warns about it: a controller left in the other room is not a decision to unwire
    /// it, and the layout has to be there when it comes back. It is only ever displaced by
    /// somebody pointing something else at the same control.
    ///
    /// Empty means any device, which is what a mapping made before this existed reads as.
    /// </remarks>
    public string Device { get; set; } = "";

    /// <summary>1 to 16, as the message says it.</summary>
    public int Channel { get; set; } = 1;

    /// <summary>Which continuous controller, 0 to 127.</summary>
    public int Cc { get; set; }

    /// <summary>What sort of thing it is pointed at, which decides which fields below are read.</summary>
    public ControlKind Kind { get; set; } = ControlKind.Instrument;

    /// <summary>Whether it follows the track you are working on or stays on one.</summary>
    public ControlScope Scope { get; set; } = ControlScope.Focused;

    /// <summary>Which track, counted from zero. Only read when the scope is fixed.</summary>
    public int Track { get; set; }

    /// <summary>
    /// The machine this knob is about, by its slot id. Empty means any of them, which is only
    /// sensible for a parameter every machine has.
    /// </summary>
    public string Machine { get; set; } = "";

    /// <summary>Which parameter, for a machine. The key it is stored under, never its name.</summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// Which parameter by place rather than by name: the third knob on whatever face is in
    /// front of you. Below nought for a link that names one, which is every link a person made.
    /// </summary>
    /// <remarks>
    /// Only ever set by the layout a controller falls back on when nothing has been pointed at
    /// anything. A link made by hand names its parameter, because you pointed at that parameter
    /// and no other; a link nobody made cannot, because the machine in front of you tomorrow is
    /// not the one in front of you now. The place is what the two have in common.
    ///
    /// Read through the order a panel reads in, so it means the third control your eye lands on
    /// and not the third line of a file. See <see cref="Machines.PanelOrder"/>.
    /// </remarks>
    public int Ordinal { get; set; } = -1;

    /// <summary>
    /// The plugin this knob is about, by the id the scanner gave it. A plugin's parameter
    /// numbers mean nothing without knowing whose they are.
    /// </summary>
    public string Plugin { get; set; } = "";

    /// <summary>Which insert, counted from zero. Only read when the scope is fixed.</summary>
    public int Slot { get; set; }

    /// <summary>Which parameter, as the plugin numbers them.</summary>
    public uint Parameter { get; set; }

    /// <summary>Which strip control, for <see cref="ControlKind.Mix"/>.</summary>
    public MixControl Mix { get; set; } = MixControl.Volume;

    /// <summary>Which of the transport's four keys, for a mapping that names the transport.</summary>
    /// <remarks>
    /// Meaningless for every other kind, in the way <see cref="Mix"/> is, and left at its first
    /// value there rather than made nullable: a mapping is read by kind and nothing looks at
    /// this unless the kind says to.
    /// </remarks>
    public TransportKey Transport { get; set; } = TransportKey.Play;

    /// <summary>
    /// How the hardware and the software are reconciled when they disagree.
    /// </summary>
    /// <remarks>
    /// Starts as <see cref="ControlPickup.Sensed"/> and is written here once it has been worked
    /// out, so a session after this one does not have to watch three messages again. A
    /// controller's own file beats what was worked out by watching, and corrects this rather than
    /// migrating it: the number stored was a guess made before anything knew the device.
    /// </remarks>
    public ControlPickup Pickup { get; set; } = ControlPickup.Sensed;

    /// <summary>Which way an encoder counts, once that has been worked out.</summary>
    public ControlTurn Turn { get; set; } = ControlTurn.Offset;

    /// <summary>What to call it in a list of mappings. Filled in when it is learned.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// What it is pointed at, by the name a person reads: a machine, an effect, a mixer strip
    /// or the transport.
    /// </summary>
    /// <remarks>
    /// The ids above say which thing this is about and are the only ones that decide anything;
    /// this is the same fact in the words on the front of it, and it is here because a list of
    /// links is read rather than resolved. Grouping by <see cref="Machine"/> would head a card
    /// "oddskilla", which is a folder name, and a plugin's id is a hash.
    ///
    /// <see cref="Name"/> is this and the control's own words run together, which is how it was
    /// written at every place a link is made and is why the two are not one field: a card
    /// headed with the machine wants the rest of the sentence on the row under it, and there is
    /// no way back to the two halves once they are one string.
    ///
    /// Empty on a link made before this existed. Such a link is grouped by its ids exactly as
    /// any other, and only its heading is plainer.
    /// </remarks>
    public string Owner { get; set; } = "";

    /// <summary>One of its own, for a song keeping a copy of what it was handed.</summary>
    public static ControlMapping Copy(ControlMapping one) => new()
    {
        Device = one.Device,
        Channel = one.Channel,
        Cc = one.Cc,
        Kind = one.Kind,
        Scope = one.Scope,
        Track = one.Track,
        Machine = one.Machine,
        Key = one.Key,
        Ordinal = one.Ordinal,
        Plugin = one.Plugin,
        Slot = one.Slot,
        Parameter = one.Parameter,
        Mix = one.Mix,
        Transport = one.Transport,
        Pickup = one.Pickup,
        Turn = one.Turn,
        Name = one.Name,
        Owner = one.Owner
    };

    /// <summary>True when this mapping and that message are about each other.</summary>
    /// <remarks>
    /// The device as well as the number, unless the mapping does not name one. Two controllers
    /// on the desk both have a CC 22, and a mapping made on one has nothing to say about the
    /// other.
    /// </remarks>
    public bool Answers(MidiMessage message) =>
        message != null
        && message.Type == MidiMessageType.ControlChange
        && message.Channel == Channel
        && message.Value == Cc
        && (Device.Length == 0 || MidiService.SameName(Device, message.Device));

    /// <summary>
    /// True when both point at the same thing in the program, whatever they were learned on.
    /// </summary>
    /// <remarks>
    /// What makes a link the last one laid down on a control rather than one of a growing pile.
    /// Pointing a second knob at a filter is saying you want that knob on it, not that you want
    /// two, and the one you meant is the one you just turned.
    /// </remarks>
    public bool SameTarget(ControlMapping other)
    {
        if (other is null || other.Kind != Kind) return false;

        return Kind switch
        {
            ControlKind.Instrument or ControlKind.Action =>
                string.Equals(other.Machine, Machine, StringComparison.Ordinal)
                && string.Equals(other.Key, Key, StringComparison.Ordinal),

            ControlKind.Insert =>
                string.Equals(other.Plugin, Plugin, StringComparison.Ordinal)
                && other.Parameter == Parameter,

            ControlKind.Mix => other.Mix == Mix && other.Scope == Scope
                               && (Scope != ControlScope.Fixed || other.Track == Track),

            ControlKind.Transport => other.Transport == Transport,

            _ => false
        };
    }

    /// <summary>True when both are the same physical control on the same controller.</summary>
    public bool SameControl(ControlMapping other) =>
        other != null
        && other.Channel == Channel
        && other.Cc == Cc
        && MidiService.SameName(other.Device, Device);
}
