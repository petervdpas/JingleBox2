using System.Collections.Generic;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Devices.SoundMachines.Interfaces;

/// <summary>
/// What you have: the machines and the plugins, one file each, kept outside any song.
/// </summary>
/// <remarks>
/// The rack is where a sound starts. Taking an instrument into a song copies it, and from then
/// on the copy is the song's: editing it there changes that song and nothing else, and editing
/// the one here changes what the next song will start from. Two songs can therefore use the same
/// kick sounding differently, which is what anybody who has built a kick for one track and not
/// for another expects.
///
/// A machine is a fixture on it, one apiece under its own name, always there. It cannot be
/// renamed, deleted or duplicated, the way a rack has the boxes it has; a plugin can be deleted
/// but takes its name from the VST3 or CLAP. Nothing else belongs here at all, and anything that
/// is neither is moved aside on the next open through <see cref="Retire"/>, because there is no
/// longer any way to make one.
///
/// What a new instrument starts from is its machine's presets, which belong to the machine and
/// are never written here: see <see cref="Records.SoundMachinePreset"/>. A preset seeded onto the shelf would
/// be one more thing to scroll past, to rename by accident, and to wonder whether you made.
///
/// One file per instrument, named by its id, so renaming one costs nothing and breaks no song.
/// A synth or a plugin travels inside a song that way, patch and all. A recording does not: the
/// instrument keeps the path it was made from and the audio stays where it is, so a song moved
/// to another machine finds a sample instrument pointing at nothing. Packing a song is what
/// answers that, and it is the song's business rather than the rack's.
/// </remarks>
public interface ISoundMachineRack : ISampleUsage
{
    /// <summary>Where the files are, which is a folder a person can be pointed at.</summary>
    string Folder { get; }

    /// <summary>Where one instrument's file is, by its id.</summary>
    string PathFor(string id);

    /// <summary>
    /// Everything on the rack, by name.
    /// </summary>
    /// <remarks>
    /// Read off disc every time rather than held, because the folder is somewhere a person can
    /// go: a file dropped in or taken out should show up without the application being
    /// restarted. Unreadable files are skipped rather than fatal, so one bad file is one
    /// instrument missing and not an empty rack.
    /// </remarks>
    IReadOnlyList<TrackerInstrument> List();

    /// <summary>One instrument by id, or null when there is no such file or it will not read.</summary>
    TrackerInstrument? Load(string id);

    /// <summary>
    /// Writes an instrument down, giving it an id first if it has none.
    /// </summary>
    /// <remarks>
    /// Through the safe writer, since this is somebody's work: a half written file is an
    /// instrument that silently will not read, and the moment it would happen is a crash or a
    /// power cut, which is the moment nobody has a copy.
    /// </remarks>
    void Save(TrackerInstrument instrument);

    /// <summary>Where instruments that are no longer on the rack are kept.</summary>
    string RetiredDirectory { get; }

    /// <summary>
    /// Moves an instrument off the rack without destroying it. False when there was no such
    /// file.
    /// </summary>
    /// <remarks>
    /// The rack holds the machines and the plugins and nothing else, so everything else has to
    /// come off it. Moved rather than deleted, because what comes off is the only copy of work
    /// somebody did, and a folder they can go and look in costs nothing.
    /// </remarks>
    bool Retire(string id);

    /// <summary>Removes an instrument for good. False when there was nothing to remove.</summary>
    bool Delete(string id);

    /// <summary>
    /// Which machines have ever been put on the rack, whether or not they are on it now.
    /// </summary>
    /// <remarks>
    /// What has been offered rather than what is present, which is the rule the registry keeps
    /// for the same reason. Putting a machine on the rack is a deliberate act and so is taking
    /// it off: deciding by absence would put a machine you threw out back on the next time the
    /// rack was read, and there would be no way to be without one.
    ///
    /// So a machine this rack has never been offered gets its box, and one it has been offered
    /// is left alone whether or not the box is still there. A machine registered after the rack
    /// was built still arrives, and one you took off stays off.
    /// </remarks>
    IReadOnlyCollection<string> Shelved { get; }

    /// <summary>Writes down that a machine has been put on the rack.</summary>
    /// <param name="id">The machine's slot id.</param>
    void Shelve(string id);
}
