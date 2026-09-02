namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// Which of the two things the designer is designing, and everything that differs between them.
/// </summary>
/// <remarks>
/// The page is one page. Laying out a face, dropping parts on it, naming parameters, sizing the
/// columns and keeping the undo are the same work whether the face belongs to a machine or to an
/// effect, so there is one editor and one view, told which world it is in.
///
/// What differs is at the edges and is all here: what a fresh one is, what its id begins with,
/// what the manifest is called, what the word is in a sentence on the status line, whether the
/// folder can be carried somewhere else, and whether it can be written out as a zip. An effect
/// cannot be exported yet, and says so rather than offering a button that would write half a
/// parcel.
/// </remarks>
public interface IDesignWorld
{
    /// <summary>What one of these is called in a sentence: "machine" or "effect".</summary>
    string Word { get; }

    /// <summary>What the file at the top of one of these folders is called.</summary>
    string ManifestName { get; }

    /// <summary>A fresh one, with an id of its own and a name saying it is new.</summary>
    /// <remarks>
    /// The id is made here and never typed, and it is not one this build has an engine for: a
    /// thing designed under an id of its own is read off disc and never reaches the rack, which
    /// is the gate that has to move before somebody else's can be had at all.
    /// </remarks>
    IDesignProject New();

    /// <summary>Reads the project in that folder, or nothing when there is none in it.</summary>
    /// <param name="folder">The folder to read.</param>
    IDesignProject? Open(string folder);

    /// <summary>
    /// Carries the folder's other files to where the project is about to be written.
    /// </summary>
    /// <remarks>
    /// The other half of Save as. A box is its folder: the manifest names pictures and presets by
    /// the names they have inside it, so a manifest written into an empty folder somewhere else
    /// is a face that draws nothing. The files go first and the manifest after them, since the
    /// one on disc is behind whatever is on screen.
    /// </remarks>
    /// <param name="project">The project being carried, as it stands on disc.</param>
    /// <param name="folder">Where it is going.</param>
    bool CopyInto(IDesignProject project, string folder);

    /// <summary>Whether one of these can be written out as a zip to hand to somebody.</summary>
    bool Exports { get; }

    /// <summary>
    /// Whether it ships presets that are edited here, on a page of their own.
    /// </summary>
    /// <remarks>
    /// A machine's preset is an instrument file, a whole thing with a name on it, because that is
    /// what a machine with settings is. An effect has no instrument and no name of its own on a
    /// chain, so its preset is the smaller thing, an id and the values its knobs were left at,
    /// and that file does not exist yet. Until it does, the page is not offered rather than
    /// offered and empty.
    /// </remarks>
    bool HasPresets { get; }

    /// <summary>Writes it out as a zip, where that is offered at all.</summary>
    /// <param name="project">The project being written out.</param>
    /// <param name="zipPath">Where the file goes.</param>
    void Export(IDesignProject project, string zipPath);
}
