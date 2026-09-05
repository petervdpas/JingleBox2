using System.Collections.Generic;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Faces.Records;

namespace JingleBox2.SoundDevices.Interfaces;

/// <summary>
/// What the designer edits: a face, its parameters, and the folder both live in.
/// </summary>
/// <remarks>
/// A machine's project and an effect's are separate types in separate worlds, and to the page
/// that lays a face out they are the same document. It holds a panel and a list of what the
/// panel's controls turn, it is named and coloured, it says what it is in one line, and it is
/// written into a folder that carries its pictures. Nothing on this interface is about notes or
/// about audio, which is the test of whether it belongs here: a question only one world can
/// answer is not a question the designer should be asking.
///
/// Settable where <see cref="IRackProject"/> is not, because the designer is where those are
/// typed. A project implements both, which costs nothing: the same properties answer.
///
/// What is deliberately absent is everything the two do differently, and there is a seam for
/// that as well: making a fresh one, reading a folder, carrying a folder somewhere else and
/// writing a zip are <see cref="IDesignWorld"/>, since a manifest is called one thing or the
/// other and an id says which world it is in.
/// </remarks>
public interface IDesignProject
{
    /// <summary>What it is called in files, forever, and what decides whether it has an engine.</summary>
    string Id { get; }

    /// <summary>What it is called, which is yours to change and means nothing in a file.</summary>
    string Name { get; set; }

    /// <summary>The one line under the name saying what sort of thing it is.</summary>
    string Summary { get; set; }

    /// <summary>Who made it, for one that is going to be handed to somebody else.</summary>
    string Author { get; set; }

    /// <summary>Bumped by whoever makes it, and by a save that was not given a new one.</summary>
    string Version { get; set; }

    /// <summary>Its colours, which are its own and not the application's.</summary>
    PanelTheme Theme { get; set; }

    /// <summary>The folder it lives in, or empty for one that has never been saved.</summary>
    string Folder { get; set; }

    /// <summary>
    /// The page it carries about itself, written in the designer and saved beside the manifest.
    /// </summary>
    /// <remarks>
    /// Settable here and read-only on <see cref="IRackProject"/>, the same as everything else on
    /// this interface: the designer is where a device's prose is typed and the rack is where it
    /// is read.
    /// </remarks>
    string Help { get; set; }

    /// <summary>How its face is put together.</summary>
    Panel Panel { get; set; }

    /// <summary>What it can be set to, which is what the panel's controls turn.</summary>
    List<Parameter> Parameters { get; }

    /// <summary>Whether this project has a folder yet.</summary>
    bool IsSaved { get; }

    /// <summary>
    /// Whether the picker on its face browses your recordings rather than its own presets.
    /// </summary>
    /// <remarks>
    /// Asked by the designer so a picker is laid out against the list it will really have, which
    /// is the difference between a control 258 wide and the same control with a category dropdown
    /// in front of it. Nothing but a machine can answer yes: an effect is sent no recordings.
    /// </remarks>
    bool? BrowsesTakes();

    /// <summary>Writes the manifest into its folder, making it if it is not there yet.</summary>
    /// <param name="folder">Where it goes, or nothing to write it back where it came from.</param>
    void Save(string? folder = null);

    /// <summary>Copies a picture into the folder under the next free number.</summary>
    /// <param name="path">The picture being added, wherever it is now.</param>
    string? AddImage(string path);

    /// <summary>Deletes every picture the face no longer names, and says how many went.</summary>
    /// <param name="kept">What the face still names, as the panel writes it.</param>
    int SweepImages(ISet<string> kept);

    /// <summary>Closes the gaps in the picture numbers, and says what became what.</summary>
    IReadOnlyDictionary<string, string> RenumberImages();

    /// <summary>Takes a picture out of the folder, file and all.</summary>
    /// <param name="named">What the panel calls it, relative to the folder.</param>
    bool RemoveImage(string named);
}
