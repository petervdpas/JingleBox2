using System.Collections.Generic;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// What each machine comes with: a folder of files, one preset to a file.
/// </summary>
/// <remarks>
/// Files rather than code, so a preset can be added, edited or taken out without a build, and
/// so an instrument saved off the rack can be dropped straight in as one: a preset file is
/// an instrument file, the same shape, read by the same reader.
///
/// The folder is named after the machine, beside the program. The number a filename starts with
/// is only there to hold the order they are offered in; the name on the panel is the one inside
/// the file.
///
/// Called a library rather than the machine's presets because
/// <c>JingleBox2.Machines.Abstractions.IMachinePresets</c> is already that name, and it is a
/// different thing: that one is the picker a panel puts in front of you, this one is where the
/// files are read from.
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
    /// What this machine offers. Read once and kept, since the folder does not change under us.
    /// </summary>
    /// <param name="machine">The machine to look up. Nothing offers nothing.</param>
    IReadOnlyList<MachinePreset> For(Machine? machine);
}
