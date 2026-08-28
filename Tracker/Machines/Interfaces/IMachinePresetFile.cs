using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace JingleBox2.Tracker.Machines.Interfaces;

/// <summary>
/// A preset written the way the machine is drawn: one small piece of JSON per control.
/// </summary>
/// <remarks>
/// Every knob on a machine names a parameter, and every pad button names itself. So a preset is
/// those names with values against them, and nothing else. What the machine already says is not
/// said again: which key a pad answers to is on the button, so it is not in the preset, and
/// reordering the grid cannot silently move every drum along one the way a list of sixteen could.
///
/// It replaces a preset written as a whole instrument. That shape carried a plugin path, a plugin
/// id and a base note into a drum kit, none of which a kit has, and it keyed its pads by their
/// place in a list.
///
/// The machine's id at the top is what says which shape a file is in. A file without one is the
/// older kind and is read as an instrument, which is what every preset on the machines that have
/// not been converted still is.
///
/// Three shapes of machine, and one shape of block. A machine may hold a grid of pads, a map of
/// zones, or nothing but itself, and those are three different machines and are read and written
/// as three. What they share is what a block of JSON is: a set of names the machine declared,
/// with the values under them, written through the one adapter that knows what each name means.
/// So one block writer and one line reader are the whole of the format, and everything else is a
/// question of which things there are.
///
/// A preset that will not read is written to <see cref="Diagnostics.Enums.LogArea.Machines"/> rather
/// than to the application's own area, as everything under this folder is, and comes back as
/// nothing rather than throwing: a machine with one bad preset in its folder should still open.
///
/// A seam rather than a static class because both halves of it reach the disc: one reads a file
/// and the other writes the names inside it relative to the machine's own folder, by a path rule
/// that differs between Windows and Linux. Neither could be put a question to while this was
/// static, which is exactly the pair where a fault is silent and lands on somebody else's
/// machine.
/// </remarks>
public interface IMachinePresetFile
{
    /// <summary>What the preset is called on the picker.</summary>
    string NameKey { get; }

    /// <summary>Which machine it is for, and the mark that says it is written this way.</summary>
    string MachineKey { get; }

    /// <summary>The word that says the picker offers your own recordings instead of presets.</summary>
    string BrowseKey { get; }

    /// <summary>
    /// What the element holding a machine's things calls the settings that belong to one of them.
    /// </summary>
    /// <remarks>
    /// A kit has nothing else: every knob on BongaBong is about the pad in hand, so it says
    /// nothing and all of them are the pad's. A sampler has both halves at once, one filter and
    /// as many zones as it turned out to need, and no reader could tell which key is which by
    /// looking. So the machine says.
    /// </remarks>
    string SettingsProperty { get; }

    /// <summary>What a pad button calls the key it answers to.</summary>
    /// <remarks>
    /// Written out rather than built, so the one property a kit's keyboard depends on can be
    /// found by looking for it, here and in every machine.json that names it.
    /// </remarks>
    string KeyProperty { get; }

    /// <summary>True when that file is written the new way.</summary>
    /// <param name="read">The document as it was parsed, or nothing.</param>
    bool Keyed(JsonNode? read);

    /// <summary>
    /// Reads one, applying it to a fresh instrument of that machine's kind.
    /// </summary>
    /// <remarks>
    /// The instrument is what the engine plays, so a preset has to become one before it can be
    /// heard. What this knows that nothing else does is which name goes where: a machine-wide
    /// key is a setting on the instrument, and a name that stands for one of the machine's things
    /// is that thing's block.
    ///
    /// Which adapter answers a key outside a block and which answers the keys inside one is
    /// worked out first, and it is the only thing about a preset that depends on what machine it
    /// is for. A machine with buttons is a kit, one with a map is a sampler, and one with
    /// neither is whichever of the three plain machines its slot says it is.
    ///
    /// How many things a machine holds is not the same question on the two of them. A kit has as
    /// many pads as the machine declares buttons, which is the only place that number is said,
    /// and the key each pad answers to is on the button rather than in the preset, so a preset
    /// saying it too would be a second place for it to be wrong. A sampler has one zone per
    /// block in the order the file writes them: nothing declares how many zones a sampler has,
    /// since a piano sampled every fourth key is thirteen of them and the same piano sampled
    /// once is one, so the preset is where that number comes from.
    /// </remarks>
    /// <param name="path">The preset file.</param>
    /// <param name="machine">The machine it is for, which is what says how it is read.</param>
    TrackerInstrument? Read(string path, MachineProject machine);

    /// <summary>
    /// Writes one out from an instrument, keyed the way the machine is drawn.
    /// </summary>
    /// <remarks>
    /// Only what the machine declares. A setting the machine has no control for is not written,
    /// because it is not a thing this machine can be set to, and carrying it would put a base
    /// note into a drum kit again.
    ///
    /// A pad's block is named after the key the pad answers to. That is the one fact about a pad
    /// that is true outside the machine as well: it is the note that fires it in a pattern, so a
    /// preset can be read against a keyboard rather than against a list of names somebody
    /// invented. A zone's block is named after the zone, for the same reason a pad's is not
    /// numbered: a preset that says what is on "Squeal" can be read, and one that says what is
    /// on the fourth thing in a list cannot.
    ///
    /// A sampler writes the machine's own half first, so the file opens on the filter rather
    /// than on the eleventh piece of a chop.
    /// </remarks>
    /// <param name="sound">The instrument being written down.</param>
    /// <param name="machine">The machine it came off, which is what says how it is written.</param>
    string Write(TrackerInstrument sound, MachineProject machine);

    /// <summary>
    /// The pad buttons the machine declares, with the key each answers to.
    /// </summary>
    /// <remarks>
    /// The count is how many pads a kit built on this machine has, and it is the only place that
    /// number is said. A machine that draws no grid has none, which is how a kit is told apart
    /// from every other machine here.
    /// </remarks>
    /// <param name="machine">The machine whose face is being read.</param>
    List<(string Name, string Key, int Semitone)> Buttons(MachineProject machine);
}
