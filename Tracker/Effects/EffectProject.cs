using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using JingleBox2.Rack.Effects.Interfaces;
using JingleBox2.Rack.Faces;
using JingleBox2.Rack.Faces.Records;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tracker.Effects;

/// <summary>
/// An effect being built: a folder on disc with everything the effect is in it.
/// </summary>
/// <remarks>
/// A folder rather than a file, the same as a machine and for the same reason: an effect that
/// ships a picture on its face is an effect with a folder of pictures, and a folder is the thing
/// that gets zipped and handed to somebody. The manifest at the top of it, effect.json, is what
/// the registry reads.
///
/// It is not a machine and this is not <see cref="Machines.MachineProject"/> with a flag on it.
/// What a machine carries that an effect has no use for is most of it: which engine it borrows,
/// where its picker gets its list, and a folder of sounds, all of which are about a thing that is
/// sent notes and plays them back. What is left is what any box on the rack is, which is
/// <see cref="IRackProject"/>, plus the face.
///
/// An effect in use is a slot on a track's chain, and it takes no name of its own: two of the
/// same effect on one track read as that effect twice, which is what two of the same plugin
/// already do and what a pedal board looks like. So there is no instrument here, and nothing
/// like one: what a chain writes down is this effect's id and the values its knobs were left at.
///
/// What goes wrong here is written to <see cref="Diagnostics.Enums.LogArea.Machines"/>, which is
/// the rack's area rather than the machine world's alone.
/// </remarks>
public sealed class EffectProject : IRackProject, IEffect
{
    /// <summary>What the file at the top of an effect's folder is called.</summary>
    /// <remarks>
    /// Written out rather than built, so the one file an effect is recognised by can be found by
    /// looking for it. It is deliberately not machine.json: a folder is one thing or the other,
    /// and a reader that had to open the file to find out which would be a reader that can be
    /// wrong.
    /// </remarks>
    public const string ManifestName = "effect.json";

    /// <summary>Where the presets an effect ships with go.</summary>
    public const string PresetsFolder = "presets";

    /// <summary>Where the pictures on an effect's face go.</summary>
    public const string ImagesFolder = "images";

    /// <summary>How the manifest is written, which is laid out for reading.</summary>
    /// <remarks>
    /// Indented on purpose. An effect.json is a file somebody builds, hands over and argues with,
    /// so it has to be readable in an editor.
    /// </remarks>
    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    /// <summary>
    /// The folder this project is kept in, or empty for one that has never been saved.
    /// </summary>
    /// <remarks>
    /// Not written into the manifest: where an effect happens to sit is the business of whoever
    /// has it, and a path baked into the file would be wrong the moment the folder was copied,
    /// zipped or sold.
    /// </remarks>
    [JsonIgnore]
    public string Folder { get; set; } = "";

    /// <summary>
    /// What this effect is called in files, forever.
    /// </summary>
    /// <remarks>
    /// Written into every song that puts one on a chain, so it is set once when the project is
    /// made and never again. Ours take the <c>effect.</c> prefix; one of yours takes your own, so
    /// two people naming an effect "Echo" do not collide.
    /// </remarks>
    public string Id { get; set; } = "";

    /// <summary>What it is called on the rack, which is yours to change and means nothing in a file.</summary>
    public string Name { get; set; } = "";

    /// <summary>The one line under the name saying what it does.</summary>
    public string Summary { get; set; } = "";

    /// <summary>Who made it, for an effect that is going to be handed to somebody else.</summary>
    public string Author { get; set; } = "";

    /// <summary>Bumped by whoever makes it, and shown beside the name on the rack.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Its colours, which are its own and not the application's.</summary>
    public PanelTheme Theme { get; set; } = new("#7B838C");

    /// <summary>
    /// What this effect can be set to.
    /// </summary>
    /// <remarks>
    /// The effect itself, more than anything else here: the panel is these drawn, a preset is
    /// these stored, and a slot on a chain is these with values in them.
    /// </remarks>
    public List<Parameter> Parameters { get; set; } = new();

    /// <summary>
    /// How those parameters are arranged on the effect's face.
    /// </summary>
    /// <remarks>
    /// The same <see cref="Panel"/> a machine's face is described with, because a face is a face:
    /// the same knobs, the same faders, the same Menu, laid out by whoever built the thing and
    /// drawn by the same library. What an effect adds is the footswitch, which is bypass and is
    /// a fact about the slot rather than a parameter.
    /// </remarks>
    public Panel Panel { get; set; } = new();

    /// <inheritdoc/>
    public string Colour => Theme.Accent;

    /// <summary>Whether this project has a folder yet.</summary>
    [JsonIgnore]
    public bool IsSaved => Folder.Length > 0;

    /// <summary>Reads the project in that folder, or null when there is no effect in it.</summary>
    /// <remarks>
    /// The folder is put on afterwards rather than read out of the file, so an effect that has
    /// been copied, zipped or moved knows where it actually is rather than where it once was. A
    /// manifest that will not parse is nothing rather than a fault: this is called for every
    /// folder in the effects folder, and one bad manifest should not take the rack with it.
    /// </remarks>
    /// <param name="folder">The folder to read.</param>
    public static EffectProject? Open(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return null;

        string manifest = Path.Combine(folder, ManifestName);

        if (!File.Exists(manifest)) return null;

        try
        {
            var read = JsonSerializer.Deserialize<EffectProject>(File.ReadAllText(manifest), Layout);

            if (read == null) return null;

            read.Folder = folder;

            return read;
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Machines, "Effect project could not be read", ex);

            return null;
        }
    }

    /// <summary>
    /// Writes the project into its folder, making it if it is not there yet.
    /// </summary>
    /// <remarks>
    /// The presets and pictures folders are made whether or not the effect has any, so that an
    /// effect's folder always has the same shape and there is somewhere to drop a file into.
    ///
    /// Throws rather than reporting: this is a save somebody asked for and is waiting on.
    /// </remarks>
    /// <param name="folder">Where it goes, or nothing to write it back where it came from.</param>
    public void Save(string? folder = null)
    {
        if (!string.IsNullOrWhiteSpace(folder)) Folder = folder!;

        if (Folder.Length == 0) throw new InvalidOperationException("A project needs a folder before it can be saved.");

        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory(Path.Combine(Folder, PresetsFolder));
        Directory.CreateDirectory(Path.Combine(Folder, ImagesFolder));

        File.WriteAllText(Path.Combine(Folder, ManifestName), JsonSerializer.Serialize(this, Layout));
    }
}
