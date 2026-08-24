using JingleBox2.Machines;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// A machine being built: a folder on disc with everything the machine is in it.
/// </summary>
/// <remarks>
/// A project is one machine. Not one instrument: a machine only becomes an instrument inside a
/// song, where it is given a name and settings of its own. What is here is the box itself, what
/// it is called, what colour it is, what engine it plays with, and later the panel it shows and
/// the sounds it ships with.
///
/// A folder rather than a file, because a machine that ships samples is a machine with a folder
/// of samples, and because a folder is the thing that gets zipped and sold. The manifest at the
/// top of it, machine.json, is what the registry reads when the machine is installed.
///
/// It lives wherever you keep your work. Installing copies it in beside the app's own; the
/// project stays yours.
/// </remarks>
public sealed class MachineProject
{
    public const string ManifestName = "machine.json";

    /// <summary>Where the samples a machine ships with go.</summary>
    public const string SoundsFolder = "sounds";

    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    /// <summary>
    /// The folder this project is kept in, or empty for one that has never been saved.
    /// </summary>
    /// <remarks>
    /// Not written into the manifest: where a machine happens to sit is the business of
    /// whoever has it, and a path baked into the file would be wrong the moment the folder was
    /// copied, zipped or sold.
    /// </remarks>
    [JsonIgnore]
    public string Folder { get; set; } = "";

    /// <summary>
    /// What this machine is called in files, forever.
    /// </summary>
    /// <remarks>
    /// Written into every song that uses it, so it is set once when the project is made and
    /// never again. Ours are "machine.zampler" and the like; one of yours takes your own
    /// prefix, so two people naming a machine "Piano" do not collide.
    /// </remarks>
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Summary { get; set; } = "";

    /// <summary>Who made it, for a machine that is going to be handed to somebody else.</summary>
    public string Author { get; set; } = "";

    /// <summary>Bumped by whoever makes it, and shown beside the name on the rack.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// The engine it plays with, by the id of the machine whose sound it borrows.
    /// </summary>
    /// <remarks>
    /// A machine built on one of ours is the ordinary case and needs no code at all: your own
    /// samples, your own name and colour, over an engine that already exists. Empty means the
    /// machine brings its own engine, which is the version that arrives as a DLL.
    /// </remarks>
    public string Engine { get; set; } = "";

    /// <summary>Its colours, which are its own and not the application's.</summary>
    public MachineTheme Theme { get; set; } = new("#7B838C");

    /// <summary>
    /// What this machine can be set to.
    /// </summary>
    /// <remarks>
    /// The machine itself, more than anything else here: the panel is these drawn, the patch is
    /// these stored, and a song's instrument is these with values in them.
    /// </remarks>
    public List<MachineParameter> Parameters { get; set; } = new();

    /// <summary>
    /// How those parameters are arranged on the machine's face.
    /// </summary>
    /// <remarks>
    /// Kept beside the parameters and not instead of them: the list is what the machine can be
    /// set to, this is only how it is shown. A project that has not been arranged yet has an
    /// empty panel, which is the state a machine is in the moment its parameters are made.
    /// </remarks>
    public MachinePanel Panel { get; set; } = new();

    [JsonIgnore]
    public bool IsSaved => Folder.Length > 0;

    /// <summary>Reads the project in that folder, or null when there is no machine in it.</summary>
    public static MachineProject? Open(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return null;

        string manifest = Path.Combine(folder, ManifestName);

        if (!File.Exists(manifest)) return null;

        try
        {
            var read = JsonSerializer.Deserialize<MachineProject>(File.ReadAllText(manifest), Layout);

            if (read == null) return null;

            read.Folder = folder;

            return read;
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.LogArea.App, "Machine project could not be read", ex);

            return null;
        }
    }

    /// <summary>Writes the project into its folder, making it if it is not there yet.</summary>
    public void Save(string? folder = null)
    {
        if (!string.IsNullOrWhiteSpace(folder)) Folder = folder!;

        if (Folder.Length == 0) throw new InvalidOperationException("A project needs a folder before it can be saved.");

        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory(Path.Combine(Folder, SoundsFolder));

        File.WriteAllText(Path.Combine(Folder, ManifestName), JsonSerializer.Serialize(this, Layout));
    }
}
