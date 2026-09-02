using System.Collections.Generic;

namespace JingleBox2.Devices.SoundMachines.Interfaces;

/// <summary>
/// What a preset file plays, and who else plays it.
/// </summary>
/// <remarks>
/// Read out of the JSON as text, without knowing what machine it is for. A preset holds its
/// recordings under whatever key the machine calls them, so looking for the key would mean
/// knowing every machine; looking for a name ending in wav means knowing none of them, and a
/// machine somebody else writes tomorrow is covered by that on the day it arrives.
///
/// One place, because three things ask it and they have to agree: renaming a preset moves the
/// folder, deleting one takes the folder with it, and levelling one rewrites what is in it. Two
/// answers to "what does this preset play" would be one preset renamed and one folder left.
///
/// A seam rather than a static class because every question here is answered by reading a file
/// off the disc and comparing paths by the rule this system happens to have. Those are the two
/// things a test has to be able to stand in front of, and while this was static it could do
/// neither.
/// </remarks>
public interface IPresetWaves
{
    /// <summary>Every recording a preset names, as written down, each one once.</summary>
    /// <remarks>
    /// A preset that will not read names nothing, which is the safe answer everywhere this is
    /// asked: nothing gets renamed, nothing gets deleted, nothing gets rewritten. Reported as an
    /// empty list rather than thrown, because a folder of presets with one bad file in it should
    /// still be usable.
    /// </remarks>
    /// <param name="presetPath">The preset file being read.</param>
    IReadOnlyList<string> Named(string presetPath);

    /// <summary>True when that name is one of a preset's recordings.</summary>
    /// <remarks>
    /// Every recording this program writes is a wav, so the test is the extension and nothing
    /// else. A machine that one day carries an mp3 would need this widened; nothing here reads
    /// the file, so widening it is one string.
    /// </remarks>
    /// <param name="said">The name as the preset wrote it, or nothing.</param>
    bool IsWave(string? said);

    /// <summary>
    /// The folder inside the machine that a preset plays out of, or nothing when it has none.
    /// </summary>
    /// <remarks>
    /// The first one it names, since a preset written by this program keeps all of its
    /// recordings together. One that names two folders has whichever it named first, and
    /// <see cref="Users"/> is what stops that being acted on.
    /// </remarks>
    /// <param name="presetPath">The preset file being read.</param>
    /// <param name="home">The machine's folder, which the preset's names are said from.</param>
    string? Folder(string presetPath, string home);

    /// <summary>
    /// Which of those presets play out of that folder.
    /// </summary>
    /// <remarks>
    /// The question asked before a folder is renamed or removed. Two presets can share one, and
    /// a folder moved out from under the second is a kit that opens with empty pads.
    /// </remarks>
    /// <param name="folder">The folder about to be renamed or removed.</param>
    /// <param name="home">The machine's folder, which the presets' names are said from.</param>
    /// <param name="presets">The preset files to ask, which is usually the machine's whole shelf.</param>
    IReadOnlyList<string> Users(string folder, string home, IEnumerable<string> presets);
}
