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

    /// <summary>
    /// Where the presets a machine ships with go, one instrument file to a preset.
    /// </summary>
    /// <remarks>
    /// Inside the machine and not beside the program, which is where they used to be. A preset
    /// is content the machine came with, the same as a picture on its face and the recordings a
    /// kit is built out of, so it belongs in the folder that gets zipped and handed to somebody.
    /// Kept outside, a machine exported here arrived there with an empty picker.
    /// </remarks>
    public const string PresetsFolder = "presets";

    /// <summary>Where the pictures on a machine's face go.</summary>
    /// <remarks>
    /// Beside the sounds and for the same reason. A machine is a folder and the zip is that
    /// folder, so a logo put in here travels with the machine without anything being arranged:
    /// the panel names the file, the folder carries it, and the two find each other again on
    /// whatever disc they land on.
    /// </remarks>
    public const string ImagesFolder = "images";

    /// <summary>What every picture in a machine is called, before its number.</summary>
    /// <remarks>
    /// Declared rather than written into the naming, so the one word every picture in every
    /// machine is named after can be found by looking for it.
    /// </remarks>
    private const string ImageStem = "image";

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
    /// Where the picker at the top of its panel gets its list. See <see cref="MachineStarts"/>.
    /// </summary>
    /// <remarks>
    /// Empty when the machine does not say, which is not the same as saying presets: a machine
    /// written before this field existed is already installed on people's discs, and reading its
    /// silence as "presets" would take the takes off the Recording machine's picker on every one
    /// of them. Silence means whatever the app decided before, and only a machine that says
    /// something overrules that.
    /// </remarks>
    public string StartsFrom { get; set; } = "";

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
        Directory.CreateDirectory(Path.Combine(Folder, ImagesFolder));

        File.WriteAllText(Path.Combine(Folder, ManifestName), JsonSerializer.Serialize(this, Layout));
    }

    /// <summary>What the picker on a panel calls the thing it browses.</summary>
    /// <remarks>
    /// Written out rather than built, so the one word that decides which browser a machine has
    /// can be found by looking for it, here and in every machine.json that names it.
    /// </remarks>
    public const string SourceProperty = "source";

    /// <summary>The picker on this machine's panel, if it draws one.</summary>
    private static MachineElement? Picker(MachineElement element)
    {
        if (element.Element == MachineElementKinds.Preset) return element;

        foreach (var child in element.Children)
            if (Picker(child) is { } found) return found;

        return null;
    }

    /// <summary>What a preset writes to say the picker offers your own recordings.</summary>
    /// <remarks>
    /// Written out rather than built, so the one word that decides which browser a machine has
    /// can be found by looking for it.
    /// </remarks>
    private const string BrowseKey = "Browse";

    /// <summary>
    /// True when this machine's presets say the picker should offer your recordings.
    /// </summary>
    /// <remarks>
    /// The machine says it in its own presets folder rather than in a flag, because that is
    /// where it can be read and changed: a machine whose whole sound is a recording of yours
    /// ships one preset saying so, and that preset is the thing you can open and see.
    ///
    /// Falls back to <see cref="StartsFrom"/> for a machine installed before the preset existed,
    /// which is every copy already on somebody's disc.
    /// </remarks>
    public bool? BrowsesTakes()
    {
        // The picker itself, every time and never remembered. Which of the two browsers a machine
        // has is a fact about the control that does the browsing, so the panel is where it is
        // said, and while somebody is laying the machine out that answer changes under us.
        if (Picker(Panel.Root) is { } picker
            && picker.Properties.TryGetValue(SourceProperty, out string? said)
            && said.Trim().Length > 0)
            return string.Equals(said.Trim(), MachineStarts.Takes, StringComparison.OrdinalIgnoreCase);

        // The older ways of saying it, which cost a folder read, so they are read once.
        if (_asked) return _browses;

        _browses = Asked();
        _asked = true;

        return _browses;
    }

    private bool? _browses;

    private bool _asked;

    /// <summary>
    /// What this machine says about its browser, or nothing when it says nothing at all.
    /// </summary>
    /// <remarks>
    /// Nothing is a real answer and not a no. Every copy of a machine installed before the browse
    /// preset existed says nothing, and reading that as "its own presets" takes the take shelf off
    /// the Recording machine on every one of them.
    /// </remarks>
    private bool? Asked()
    {
        string folder = Path.Combine(Folder, PresetsFolder);

        try
        {
            if (Directory.Exists(folder))
            {
                foreach (string path in Directory.EnumerateFiles(folder, "*.json"))
                {
                    using var read = JsonDocument.Parse(File.ReadAllText(path));

                    if (read.RootElement.TryGetProperty(BrowseKey, out var browse) &&
                        string.Equals(browse.GetString(), MachineStarts.Takes, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.LogArea.App, "The presets could not be read from " + folder, ex);
        }

        if (StartsFrom.Length == 0) return null;

        return string.Equals(StartsFrom, MachineStarts.Takes, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Copies a picture into the machine's own folder, and hands back what the panel should
    /// call it.
    /// </summary>
    /// <returns>The name to put on the panel, or null when the machine has nowhere to keep it.</returns>
    /// <remarks>
    /// A copy and not a reference, because a machine pointing at a picture somewhere else on
    /// this disc would arrive on somebody else's with a hole in its face. Once it is in here it
    /// is part of the machine, and the zip is the folder.
    ///
    /// It is renamed on the way in, and what it was called outside is forgotten at the door. The
    /// pictures in a machine are image1, image2, image3, in the order they were brought in: a
    /// file that arrived as "Screenshot from 2026-04-11 14-22-07.png" has a name that is about
    /// somebody's desktop rather than about this machine, and a folder of them is unreadable.
    /// The extension is kept, so anything that looks in the folder can still open what it finds.
    ///
    /// The number is the first one nothing in the folder is using, so a picture is never written
    /// over. The same file brought in twice is two pictures under two numbers: whether a machine
    /// wants the same artwork in two places is its business, and comparing files to save a few
    /// kilobytes would be a surprise nobody asked for.
    ///
    /// Taking one out again is <see cref="RemoveImage"/>, which the designer calls when the last
    /// element showing a picture goes.
    /// </remarks>
    public string? AddImage(string path)
    {
        if (Folder.Length == 0) return null;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        string images = Path.Combine(Folder, ImagesFolder);

        Directory.CreateDirectory(images);

        string suffix = Path.GetExtension(path);

        for (int at = 1; ; at++)
        {
            string stem = ImageStem + at;

            // Whatever it is called, not just whatever it is called with this extension. A png
            // and a jpg both landing on image3 would be two pictures nobody could tell apart in
            // the folder, and one of them would be a surprise the next time either was replaced.
            if (Directory.GetFiles(images, stem + ".*").Length > 0) continue;

            File.Copy(path, Path.Combine(images, stem + suffix));

            return ImagesFolder + "/" + stem + suffix;
        }
    }

    /// <summary>
    /// Deletes every picture the machine no longer names, and says how many went.
    /// </summary>
    /// <remarks>
    /// The other half of keeping the folder honest. A picture can stop being used without any
    /// element being removed: point one at a different file and the old one is nobody's. This is
    /// asked at the moment the machine is written down, so what is saved and what is in the
    /// folder are the same machine.
    ///
    /// Only files of ours are considered. Anything else somebody put in the folder is theirs.
    /// </remarks>
    public int SweepImages(ISet<string> kept)
    {
        if (Folder.Length == 0) return 0;

        string images = Path.Combine(Folder, ImagesFolder);

        if (!Directory.Exists(images)) return 0;

        int gone = 0;

        try
        {
            foreach (string file in Directory.GetFiles(images))
            {
                string stem = Path.GetFileNameWithoutExtension(file);

                if (!stem.StartsWith(ImageStem, StringComparison.OrdinalIgnoreCase)) continue;

                if (!int.TryParse(stem[ImageStem.Length..], out _)) continue;

                if (kept.Contains(ImagesFolder + "/" + Path.GetFileName(file))) continue;

                File.Delete(file);

                gone++;
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.LogArea.App, "The pictures could not be swept in " + Folder, ex);
        }

        return gone;
    }

    /// <summary>
    /// Closes the gaps in the picture numbers, and says what became what.
    /// </summary>
    /// <remarks>
    /// A machine holding image2 and no image1 is a machine that has plainly lost something, and
    /// the folder is the first place anybody looks when a picture does not draw. So after one
    /// goes, the rest shuffle down: what is left is image1, image2 and so on with nothing
    /// missing, and the panel is told what everything is called now.
    ///
    /// The order is the order the numbers were in, so the pictures keep their sequence and
    /// nobody's second logo becomes their first. Renaming downwards can never land on a file
    /// that has not been dealt with yet, since every new number is at or below the old one.
    ///
    /// Anything in the folder that is not one of ours is left alone. A machine is a folder
    /// somebody can put things in, and renumbering is not a licence to rearrange it.
    /// </remarks>
    public IReadOnlyDictionary<string, string> RenumberImages()
    {
        var moved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (Folder.Length == 0) return moved;

        string images = Path.Combine(Folder, ImagesFolder);

        if (!Directory.Exists(images)) return moved;

        try
        {
            var ours = new List<(int At, string Path)>();

            foreach (string file in Directory.GetFiles(images))
            {
                string stem = Path.GetFileNameWithoutExtension(file);

                if (!stem.StartsWith(ImageStem, StringComparison.OrdinalIgnoreCase)) continue;

                if (!int.TryParse(stem[ImageStem.Length..], out int at)) continue;

                ours.Add((at, file));
            }

            ours.Sort((one, other) => one.At.CompareTo(other.At));

            for (int i = 0; i < ours.Count; i++)
            {
                string was = ours[i].Path;
                string suffix = Path.GetExtension(was);
                string now = Path.Combine(images, ImageStem + (i + 1) + suffix);

                if (string.Equals(was, now, StringComparison.Ordinal)) continue;

                File.Move(was, now);

                moved[ImagesFolder + "/" + Path.GetFileName(was)] = ImagesFolder + "/" + Path.GetFileName(now);
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.LogArea.App, "The pictures could not be renumbered in " + Folder, ex);
        }

        return moved;
    }

    /// <summary>
    /// Takes a picture out of the machine, file and all.
    /// </summary>
    /// <remarks>
    /// Called when the last element naming it has gone. A machine is a folder that gets zipped
    /// and handed to somebody, so a picture nothing shows is weight in the parcel and a puzzle
    /// for whoever opens it. The original is still wherever it was picked from: what is deleted
    /// here is this machine's copy of it.
    ///
    /// The name is checked before anything is deleted, the same way the importer checks a zip's:
    /// it has to land inside this machine's own pictures folder. A name is a claim, and this one
    /// arrives out of a file somebody else may have written.
    /// </remarks>
    public bool RemoveImage(string named)
    {
        if (Folder.Length == 0 || string.IsNullOrWhiteSpace(named)) return false;

        try
        {
            string images = Path.GetFullPath(Path.Combine(Folder, ImagesFolder));
            string wanted = Path.GetFullPath(Path.Combine(Folder, named));

            if (!wanted.StartsWith(images + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return false;

            if (!File.Exists(wanted)) return false;

            File.Delete(wanted);

            Diagnostics.Log.Write(Diagnostics.LogArea.App, () => "machine picture removed: " + wanted);

            return true;
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.LogArea.App, "A picture could not be removed from " + Folder, ex);

            return false;
        }
    }
}
