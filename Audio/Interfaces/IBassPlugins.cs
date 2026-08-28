using System.Collections.Generic;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// The BASS add-ons sitting beside the program, and what they let it read.
/// </summary>
/// <remarks>
/// BASS reads a handful of formats on its own and everything else through a library per format,
/// dropped in beside it. That used to be one named library loaded in one place, which meant the
/// program could only ever read one more format than it was born with: adding FLAC would have
/// been a code change to read a file.
///
/// So nothing here is named. Whatever add-on is in the folder is loaded, and each one is asked
/// what it reads, which is how the import picker knows what to offer. Drop a library in and the
/// format appears; take it out and the format stops being offered, rather than being offered
/// and then failing.
///
/// The loading itself happens once for the whole process, however many of these there are: a
/// library loaded into BASS twice is a library loaded into BASS twice, and BASS is one library
/// in one process.
/// </remarks>
public interface IBassPlugins
{
    /// <summary>Loads every add-on in the program's folder, once for this process.</summary>
    void Load();

    /// <summary>Every file kind BASS can read here, built in and added together.</summary>
    IReadOnlyList<string> Kinds { get; }
}
