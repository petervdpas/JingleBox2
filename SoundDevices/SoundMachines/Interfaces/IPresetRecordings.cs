using JingleBox2.Tracker;

namespace JingleBox2.SoundDevices.SoundMachines.Interfaces;

/// <summary>
/// The recordings a preset of yours keeps beside it, in a folder named after the preset.
/// </summary>
/// <remarks>
/// **A preset of yours owns its waves.** A kit you chopped plays pieces written to your
/// recordings shelf, and the full recording they were cut from is on the shelf as well, so a
/// preset that only named them would change the day one of them is edited or tidied away, and
/// could not be handed on. So keeping a preset copies every recording its sound names, the
/// chopped pieces and the original a kit was chopped from, into
/// <c>presets/&lt;preset name&gt;/</c>, which is exactly how a preset that ships keeps its
/// sounds, and points the sound at the copies.
///
/// Nothing is moved and nothing is deleted: the shelf keeps its own files, and an older copy in
/// the folder that nothing names any more is left where it is, since a song may still play it.
/// A recording already in the preset's own folder stays where it is, which is what lets a wave
/// edited there survive the preset being kept again. A name already taken by a different file is
/// numbered rather than written over. A path naming nothing on this disc is left as it was.
/// </remarks>
public interface IPresetRecordings
{
    /// <summary>Copies what the sound names into that folder and points the sound at the copies.</summary>
    /// <param name="sound">The sound about to be kept, already a copy of the instrument's.</param>
    /// <param name="beside">The preset's own folder, made if it is needed.</param>
    void Gather(TrackerInstrument sound, string beside);
}
