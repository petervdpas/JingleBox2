using System.Collections.Generic;
using JingleBox2.Tracker;
using JingleBox2.SoundDevices.SoundMachines.Records;

namespace JingleBox2.SoundDevices.SoundMachines.Interfaces;

/// <summary>
/// What each machine comes with: a folder of files, one preset to a file.
/// </summary>
/// <remarks>
/// Files rather than code, so a preset can be added, edited or taken out without a build, and
/// so an instrument saved off the rack can be dropped straight in as one: a preset file is
/// an instrument file, the same shape, read by the same reader.
///
/// The folder is <c>presets</c> inside the machine's own folder, found by the machine's id, which
/// is what makes a preset travel with the machine in a zip. The number a filename starts with is
/// only there to hold the order they are offered in; the name on the panel is the one inside the
/// file.
///
/// Called a library rather than the machine's presets because
/// <c>JingleBox2.Rack.SoundDevices.Faces.Interfaces.IPanelPresets</c> is already that name, and
/// it is a different thing: that one is the picker a panel puts in front of you, this one is
/// where the files are read from.
///
/// What has been read is remembered, and it is remembered per library rather than per program.
/// A folder that a running application never changes is worth walking once, but as a static
/// cache it outlived the thing it was about: one test's read decided what the next test saw,
/// and a machine reinstalled under the same name went on offering the presets it used to have.
/// A library is cheap to make, so anybody wanting a fresh look makes one.
/// </remarks>
public interface IPresetLibrary
{
    /// <summary>
    /// What this machine offers: its own presets, then yours, each in filename order.
    /// </summary>
    /// <remarks>
    /// Read once and kept, and read again after a preset of yours is kept or taken off through
    /// this library. Somebody else writing into the folder is not seen until a library is made again.
    /// </remarks>
    /// <param name="machine">The machine to look up. Nothing offers nothing.</param>
    IReadOnlyList<SoundMachinePreset> For(SoundMachine? machine);

    /// <summary>
    /// Why that name cannot be used for a preset of yours on that machine, or nothing when it can.
    /// </summary>
    /// <remarks>
    /// **A preset of yours lives in the machine's own presets folder**, the installed one, beside
    /// the ones the machine ships with. That folder is kept file by file when the machine is
    /// updated and nothing in it is ever deleted, so a preset you keep there survives the next
    /// version arriving, and it travels in the machine's zip. Which is why the name has to be a
    /// file's name on every system, and why it may not be one of the machine's own: two presets
    /// called Init, one of them yours, is a picker nobody can trust.
    ///
    /// A name already used by a preset of yours is not refused. Keeping under it replaces it,
    /// which whoever is asking says out loud first.
    /// </remarks>
    /// <param name="machine">The machine the preset is for.</param>
    /// <param name="name">The name somebody typed.</param>
    string Refusal(SoundMachine? machine, string name);

    /// <summary>The preset of yours on that machine called that, or nothing.</summary>
    /// <param name="machine">The machine to look on.</param>
    /// <param name="name">The name, compared without regard to case.</param>
    SoundMachinePreset? Yours(SoundMachine? machine, string name);

    /// <summary>
    /// Keeps that sound as a preset of yours under that name, replacing one of yours called that.
    /// </summary>
    /// <remarks>
    /// Written in the shape the machine's own presets are, through the same writer, so a preset
    /// you keep reads back exactly as one that shipped does. The sound is copied and renamed, the
    /// recordings it names are copied into a folder beside it named after it (see
    /// <see cref="IPresetRecordings"/>), and the instrument it came from is left alone: whoever
    /// asked puts the kept preset on it afterwards if it should play the copies.
    /// </remarks>
    /// <param name="machine">The machine it is a preset of.</param>
    /// <param name="sound">What it sounds like now.</param>
    /// <param name="name">What to call it.</param>
    /// <returns>The preset as it is now listed, or nothing when the name was refused or the file could not be written.</returns>
    SoundMachinePreset? Keep(SoundMachine? machine, TrackerInstrument sound, string name);

    /// <summary>
    /// Takes a preset of yours off the machine, and never one the machine ships with.
    /// </summary>
    /// <param name="machine">The machine it is on.</param>
    /// <param name="preset">The preset, as this library listed it.</param>
    /// <returns>Whether it was taken off.</returns>
    bool Remove(SoundMachine? machine, SoundMachinePreset? preset);

    /// <summary>
    /// Whether that recording is kept in the folder of a preset of yours on that machine, and so is yours to edit.
    /// </summary>
    /// <remarks>
    /// A wave the machine ships would come back the next time the machine is brought up to date,
    /// and one on your recordings shelf is shared by every song and preset that names it, so only a
    /// wave inside the machine's presets folder that does not ship is edited in place.
    /// </remarks>
    /// <param name="machine">The machine the wave is played on.</param>
    /// <param name="path">The wave.</param>
    bool Owns(SoundMachine? machine, string path);
}
