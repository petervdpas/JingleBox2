using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Rack.Faces;
using JingleBox2.Tracker;
using JingleBox2.Devices.SoundMachines;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using JingleBox2.ViewModels.Records;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.Devices.SoundMachines.Interfaces;
using JingleBox2.Devices.SoundMachines.Records;

namespace JingleBox2.ViewModels;

/// <summary>
/// The presets a machine ships with, edited as a form.
/// </summary>
/// <remarks>
/// Lines with names and values, lists you can add to, and groups: what JSON is, said as
/// something to fill in. Nobody counts brackets and nobody can leave a comma out.
///
/// No panel and no controls of the machine's own. What a preset is worth is decided by playing
/// it on the rack, where the machine is; this is the page where its contents are set out, and a
/// front panel here would be the machine drawn twice in one program.
///
/// It edits the machine being built, not the one installed. The folder is the project's, so what
/// is written here goes into the zip when the machine is exported and arrives with it.
/// </remarks>
public sealed partial class MachinePresetDesk : ObservableObject
{
    /// <summary>The waves a preset names.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IPresetWaves WaveFiles = new PresetWaves();

    /// <summary>Whether a path is inside a machine, and what it is called in there.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly ISoundMachinePaths Inside = new SoundMachinePaths();

    /// <summary>How a preset file is read and written.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly ISoundMachinePresetFile PresetFiles = new SoundMachinePresetFile();

    /// <summary>Whether two paths are one file, by this machine's rules.</summary>
    private readonly IFilePaths _paths = new FilePaths();

    /// <summary>
    /// The machine being built, asked rather than held.
    /// </summary>
    /// <remarks>
    /// Asked every time, because the machine open in the designer changes underneath this page and
    /// a held one would have the presets written into the folder of a machine nobody is looking at.
    /// </remarks>
    private readonly Func<SoundMachineProject?> _project;

    /// <summary>Takes the desk on to whichever machine the designer has open.</summary>
    public MachinePresetDesk(Func<SoundMachineProject?> project) => _project = project;

    /// <summary>What the machine ships with, in the order its folder lists them.</summary>
    public ObservableCollection<MachinePresetSlot> Presets { get; } = new();

    /// <summary>The one being edited. Setting it reads its file.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreset))]
    private MachinePresetSlot? picked;

    /// <summary>True when a preset is open, so the page can show it rather than a blank.</summary>
    public bool HasPreset => Picked != null;

    /// <summary>
    /// The preset as lines to fill in, laid out the way the machine's own description says.
    /// </summary>
    /// <remarks>
    /// The relation the page turns on: what a machine looks like and what its presets hold are
    /// the same declaration read twice. The panel makes a knob of a parameter; this makes a line
    /// of it.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasForm))]
    private MachinePresetForm? form;

    /// <summary>True when there are lines to fill in, which the browse preset has not.</summary>
    public bool HasForm => Form != null;

    /// <summary>
    /// True when the preset open is the one saying the picker offers your own recordings.
    /// </summary>
    /// <remarks>
    /// Not a preset in the ordinary sense: it sets nothing on the machine and never will. It is
    /// how a machine whose whole sound is a recording of yours says which browser it has, in the
    /// one place a machine says what it ships with. So it has no form: there is nothing on it to
    /// fill in, and drawing the machine's settings against it would invite somebody to set them
    /// and then quietly throw them away.
    /// </remarks>
    [ObservableProperty] private bool browses;

    /// <summary>What is wrong with it, or nothing when it would save.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    private string problem = "";

    /// <summary>True when there is a refusal to show.</summary>
    public bool HasProblem => Problem.Length > 0;

    /// <summary>Said after something was written, so the page can show it happened.</summary>
    [ObservableProperty] private string said = "";

    /// <summary>True once the form has been changed and the file no longer says what it shows.</summary>
    [ObservableProperty] private bool moved;

    /// <summary>
    /// Reads the picked preset's file and builds the form from it.
    /// </summary>
    /// <remarks>
    /// A preset written as a whole instrument, which is how the older ones on the disc are written,
    /// is turned into one written the way the machine is drawn on the way in, so the page has one
    /// shape to show and not two. Its recordings are made absolute at that point, since an
    /// instrument names them from wherever it happened to be read.
    ///
    /// Everything the page was saying is cleared first: the problem, the note and the moved flag
    /// all belong to the preset that was open and none of them is true of the next one.
    /// </remarks>
    partial void OnPickedChanged(MachinePresetSlot? value)
    {
        Problem = "";
        Said = "";
        Moved = false;
        Form = null;
        Browses = false;
        _held = null;

        if (value == null) return;

        try
        {
            var read = JsonNode.Parse(File.ReadAllText(value.Path)) as JsonObject;

            if (read == null)
            {
                Problem = "There is nothing in it.";

                return;
            }

            if (read.ContainsKey(MachinePresetWords.Browse))
            {
                Browses = true;

                return;
            }

            if (!read.ContainsKey(MachinePresetWords.Machine) && _project() is { } older)
            {
                var sound = JsonSerializer.Deserialize<TrackerInstrument>(File.ReadAllText(value.Path));

                if (sound != null)
                {
                    if (SoundMachine.SlotFor(older.Id) is { } was) sound.Kind = was.Kind;

                    sound.Kit?.Clamp();

                    foreach (var pad in sound.Kit?.Pads ?? Enumerable.Empty<DrumPad>())
                        if (pad.HasSound && !Path.IsPathRooted(pad.FilePath))
                            pad.FilePath = Path.GetFullPath(Path.Combine(Folder, pad.FilePath));

                    read = JsonNode.Parse(PresetFiles.Write(sound, older)) as JsonObject ?? read;
                }
            }

            _held = read;

            var project = _project();

            Form = new MachinePresetForm(
                read,
                new MachineProjectShape(
                    project?.Panel,
                    (IReadOnlyList<Parameter>?)project?.Parameters ?? Array.Empty<Parameter>()),
                () => Moved = true,
                Folder);
        }
        catch (Exception ex)
        {
            Problem = "It could not be read: " + ex.Message;
        }
    }

    /// <summary>The preset itself, which every line on the page reads and writes.</summary>
    private JsonObject? _held;

    /// <summary>Where this machine keeps them, or nothing when the machine has not been saved.</summary>
    public string Folder =>
        _project() is { Folder.Length: > 0 } project
            ? Path.Combine(project.Folder, SoundMachineProject.PresetsFolder)
            : "";

    /// <summary>True once there is a folder to keep presets in, which means a saved machine.</summary>
    public bool Ready => Folder.Length > 0;

    /// <summary>
    /// Where this preset keeps the recordings it ships with.
    /// </summary>
    /// <remarks>
    /// A folder of its own beside the preset file, named after it, which is how the presets
    /// already on the disc are arranged. One folder per preset rather than one for the machine,
    /// because two kits that both have a file called kick.wav are two different kicks, and a
    /// shared folder would make somebody choose which one keeps the name.
    ///
    /// Wherever this preset already keeps its recordings wins, if it keeps any. A preset made
    /// before the file was renamed, or one whose folder is named after the preset rather than the
    /// file, has its drums somewhere with a name of its own, and bringing the next one in beside a
    /// different name would split one kit across two folders.
    /// </remarks>
    public string Waves
    {
        get
        {
            if (Picked is not { } one || Folder.Length == 0) return "";

            if (Beside() is { Length: > 0 } already) return already;

            return Path.Combine(Folder, Path.GetFileNameWithoutExtension(one.Path));
        }
    }

    /// <summary>
    /// What to write down for a file that is now the machine's: its name from the presets folder.
    /// </summary>
    /// <remarks>
    /// Worked out from where it actually landed rather than from what the preset file is called.
    /// Those are two different names whenever a preset's recordings sit in a folder named after
    /// the preset instead of the file, and writing the second one down names a file that is not
    /// there.
    ///
    /// A path that will not read at all falls through to the full path, which is at least somewhere
    /// the file can be found today, rather than to nothing.
    /// </remarks>
    private string Relative(string full)
    {
        try
        {
            if (Inside.Named(full, Folder) is { } named) return named;
        }
        catch (Exception)
        {
        }

        return full;
    }

    /// <summary>
    /// The folder this preset's recordings are already in, or nothing when it has none.
    /// </summary>
    /// <remarks>
    /// Read off the preset itself, which is the JSON on the page. It used to be read off an
    /// instrument the desk held beside it, and once the page became the file that instrument
    /// stopped being set: the question was still asked and the answer was always nothing, so
    /// every recording brought in landed in the presets folder rather than beside the ones this
    /// preset was already using.
    ///
    /// A name that will not read is a name this cannot follow, and the next one may, so the walk
    /// carries on rather than giving up on the preset.
    /// </remarks>
    private string? Beside()
    {
        if (_held is not { } held) return null;

        string root;

        try
        {
            root = Path.GetFullPath(Folder);
        }
        catch (Exception)
        {
            return null;
        }

        foreach (string named in Named(held))
        {
            try
            {
                string full = Path.GetFullPath(Path.Combine(root, named));

                if (!Inside.Under(full, root)) continue;

                if (Path.GetDirectoryName(full) is { Length: > 0 } home) return home;
            }
            catch (Exception)
            {
            }
        }

        return null;
    }

    /// <summary>Every recording the preset names, in the order it names them.</summary>
    /// <remarks>
    /// A preset's recordings live in its blocks, one to a thing on the machine, and the whole
    /// point of the name is that it is said from the presets folder so the preset travels. So
    /// anything with a slash in it is one, and nothing else is.
    /// </remarks>
    private static IEnumerable<string> Named(JsonObject held)
    {
        foreach (var (_, node) in held)
        {
            if (node is JsonObject block)
            {
                foreach (var (_, line) in block)
                    if (line is JsonValue said && said.TryGetValue(out string? words)
                        && words.Length > 0 && words.Contains('/'))
                        yield return words;

                continue;
            }

            if (node is JsonValue value && value.TryGetValue(out string? one)
                && one.Length > 0 && one.Contains('/'))
                yield return one;
        }
    }

    /// <summary>Reads the folder again, keeping whichever preset was open if it is still there.</summary>
    public void Reread()
    {
        string was = Picked?.Path ?? "";

        Presets.Clear();

        OnPropertyChanged(nameof(Ready));
        OnPropertyChanged(nameof(Folder));

        string folder = Folder;

        if (folder.Length > 0 && Directory.Exists(folder))
        {
            foreach (string path in Directory
                         .EnumerateFiles(folder, "*" + SoundMachineRack.Extension)
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                Presets.Add(new MachinePresetSlot(Named(path), path));
            }
        }

        Picked = Presets.FirstOrDefault(one => _paths.Same(one.Path, was)) ?? Presets.FirstOrDefault();
    }

    /// <summary>
    /// A preset with nothing in it but the shape of one.
    /// </summary>
    /// <remarks>
    /// The shape rather than an empty file, because an empty file teaches nobody what goes in
    /// one. What is written is the least a preset can be: a name, and a machine that plays your
    /// own recordings rather than any it ships with, which is what a preset is unless it says
    /// otherwise.
    ///
    /// Always enabled; with no folder to write into it does nothing, which is the state the page
    /// is in before the machine has ever been saved.
    /// </remarks>
    public IRelayCommand NewCommand => new RelayCommand(() =>
    {
        string folder = Folder;

        if (folder.Length == 0) return;

        try
        {
            Directory.CreateDirectory(folder);

            string path = Free(folder);

            File.WriteAllText(path, Blank(Path.GetFileNameWithoutExtension(path)));

            Reread();

            Picked = Presets.FirstOrDefault(one => _paths.Same(one.Path, path)) ?? Picked;

            Said = "Made " + Path.GetFileName(path);
        }
        catch (Exception ex)
        {
            Problem = "It could not be made: " + ex.Message;
        }
    });

    /// <summary>Writes the preset back to the file it came from.</summary>
    /// <remarks>
    /// A recording sitting inside the machine is written as its name in there, so the preset
    /// arrives whole on somebody else's disc; one from anywhere else is brought in first. A
    /// preset that could not be made to travel is a preset that ships broken.
    ///
    /// The browse preset is refused rather than written: it holds no settings, so saving it can
    /// only take something away.
    ///
    /// The folder is read again afterwards, because the name on the picker lives inside the file
    /// and may have just changed.
    ///
    /// Always enabled; with nothing open it does nothing.
    /// </remarks>
    public IRelayCommand SaveCommand => new RelayCommand(() =>
    {
        if (Picked is not { } one) return;

        if (Browses) return;

        if (_held is not { } held) return;

        try
        {
            if (_project() is { } project) held[MachinePresetWords.Machine] = project.Id;

            File.WriteAllText(one.Path, held.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            Said = "Saved " + one.FileName;
            Problem = "";
            Moved = false;

            Reread();
        }
        catch (Exception ex)
        {
            Problem = "It could not be saved: " + ex.Message;
        }
    });

    /// <summary>
    /// Where that recording is once it is the machine's, bringing it in if it is not.
    /// </summary>
    /// <remarks>
    /// Anywhere inside the presets folder counts as already in, not just this preset's own
    /// corner of it. Two presets sharing a folder of drums is an ordinary thing and asking the
    /// narrower question copies the whole kit again under a second name every time the preset is
    /// saved.
    /// </remarks>
    private string Home(string path)
    {
        if (path.Length == 0) return path;

        try
        {
            if (Inside.Under(path, Folder)) return path;
        }
        catch (Exception)
        {
            return path;
        }

        string brought = Bring(path);

        return brought.Length > 0 ? Path.Combine(Folder, brought.Replace('/', Path.DirectorySeparatorChar)) : path;
    }

    /// <summary>
    /// Takes it out of the machine, recordings and all.
    /// </summary>
    /// <remarks>
    /// The recordings as well, because they are the preset's: they were copied inside the
    /// machine so that the preset would travel, and a preset thrown away that leaves its audio
    /// behind is audio that goes on being handed to whoever the machine is sent to, in a zip,
    /// for ever, with nothing left pointing at it to say what it was.
    ///
    /// Only the files this preset names, and only then the folder, and only when it is empty
    /// afterwards. A folder another preset also plays out of is left where it is, and so is
    /// anything in there that no preset named: a note somebody dropped in beside the drums is
    /// theirs, and this is not the place to decide otherwise.
    ///
    /// What was removed is said after the list is rebuilt and not before: picking a different
    /// preset is what happens when this one goes, and picking clears whatever the page was saying.
    ///
    /// Always enabled; with nothing open it does nothing.
    /// </remarks>
    public IRelayCommand DeleteCommand => new RelayCommand(() =>
    {
        if (Picked is not { } one) return;

        try
        {
            int gone = Sweep(one);

            File.Delete(one.Path);

            Reread();

            Said = gone == 0
                ? "Removed " + one.FileName
                : "Removed " + one.FileName + " and " + (gone == 1 ? "its recording" : gone + " recordings");
        }
        catch (Exception ex)
        {
            Problem = "It could not be removed: " + ex.Message;
        }
    });

    /// <summary>
    /// Removes the recordings a preset about to be deleted was the only one playing.
    /// </summary>
    /// <remarks>
    /// Anybody else playing out of the folder and it is not this preset's to take, so nothing goes
    /// at all. The folder itself goes only when nothing of anybody else's is left in it: a note
    /// somebody dropped in beside the drums is theirs.
    /// </remarks>
    /// <returns>How many files went.</returns>
    private int Sweep(MachinePresetSlot one)
    {
        string home = Folder;

        if (home.Length == 0) return 0;

        if (WaveFiles.Folder(one.Path, home) is not { Length: > 0 } folder) return 0;

        var others = WaveFiles.Users(folder, home, Presets
            .Where(slot => !_paths.Same(slot.Path, one.Path))
            .Select(slot => slot.Path));

        if (others.Count > 0) return 0;

        int gone = 0;

        foreach (string named in WaveFiles.Named(one.Path))
        {
            string full = Inside.Outside(named, home);

            if (!Inside.Under(full, home) || !File.Exists(full)) continue;

            File.Delete(full);

            gone++;
        }

        if (Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length == 0)
            Directory.Delete(folder);

        return gone;
    }

    /// <summary>
    /// Copies a recording into the preset's own folder, and hands back what to write down.
    /// </summary>
    /// <remarks>
    /// The whole of what putting a wave into a machine means. A preset naming a recording
    /// somewhere else on this disc arrives on somebody else's with a hole in it, so anything a
    /// preset plays is brought inside the machine and the machine is what travels.
    ///
    /// The name is kept, since "live-snare-shot-up" is about the sound and is the only thing
    /// anybody looking in the folder has to go on. A name already taken by a different file gets
    /// a number; the same file brought in twice is brought in once, recognised by its length under
    /// the name it already has here.
    ///
    /// The folder is made as the copy happens rather than on the way in, so an import that goes
    /// wrong leaves nothing behind: an empty folder named after a preset is a folder somebody has
    /// to work out the meaning of later.
    /// </remarks>
    public string Bring(string path)
    {
        if (path.Length == 0 || Waves.Length == 0) return "";

        try
        {
            if (!File.Exists(path)) return "";

            string home = Path.GetFullPath(Waves);

            string named = Path.GetFileNameWithoutExtension(path);
            string suffix = Path.GetExtension(path);

            for (int at = 1; ; at++)
            {
                string wanted = at == 1 ? named + suffix : named + "-" + at + suffix;
                string full = Path.Combine(home, wanted);

                if (File.Exists(full))
                {
                    if (new FileInfo(full).Length == new FileInfo(path).Length) return Relative(full);

                    continue;
                }

                Directory.CreateDirectory(home);

                File.Copy(path, full);

                return Relative(full);
            }
        }
        catch (Exception ex)
        {
            Problem = "It could not be brought into the machine: " + ex.Message;

            return "";
        }
    }

    /// <summary>
    /// Brings recordings into the machine and gives them to the pads, in order.
    /// </summary>
    /// <remarks>
    /// The one thing about a preset that cannot be typed: a wave has to be copied in before it
    /// can be named, and where it lands is the machine's business rather than yours.
    ///
    /// Onto the pads the preset already speaks about first, then onto ones it does not, which it
    /// then starts speaking about. A preset is what somebody assigned, so filling it assigns.
    ///
    /// Each recording is written under whatever the machine calls the setting that holds one,
    /// which is the same key its panel's take button names. There is no fixed word for it: a
    /// machine says what its own settings are called and this reads that rather than assuming.
    /// </remarks>
    public void Fill(IReadOnlyList<string> paths)
    {
        if (_held is not { } held || Form is not { } form || paths.Count == 0) return;

        var brought = paths.Select(Bring).Where(name => name.Length > 0).ToList();

        if (brought.Count == 0) return;

        var machine = _project();
        var buttons = machine != null
            ? PresetFiles.Buttons(machine)
            : new List<(string Name, string Key, int Semitone)>();

        string? take = form.Sections
            .SelectMany(section => section.Lines)
            .FirstOrDefault(line => line.IsWave)?.Name;

        for (int at = 0; at < brought.Count && at < buttons.Count; at++)
        {
            string key = buttons[at].Key.Length > 0 ? buttons[at].Key : buttons[at].Name;

            if (held[key] is not JsonObject block)
            {
                block = new JsonObject();

                held[key] = block;
            }

            foreach (string word in Words(machine)) block[word] = block[word] ?? "";

            if (Words(machine).FirstOrDefault(IsTake) is { Length: > 0 } named) block[named] = brought[at];
        }

        Moved = true;

        form.Rebuild();

        Said = brought.Count == 1
            ? "Brought in " + Path.GetFileName(brought[0])
            : "Brought in " + brought.Count + " recordings";
    }

    /// <summary>What the machine calls the settings one of its things can hold.</summary>
    private IReadOnlyList<string> Words(Devices.SoundMachines.SoundMachineProject? machine) =>
        machine == null
            ? Array.Empty<string>()
            : new MachineProjectShape(machine.Panel, machine.Parameters).ThingWords;

    /// <summary>True when that setting is the one holding a recording.</summary>
    private bool IsTake(string key) =>
        _project() is { } machine && new MachineProjectShape(machine.Panel, machine.Parameters).IsTake(key);

    /// <summary>What the picker will call that file, which is what is written inside it.</summary>
    /// <remarks>
    /// A preset that will not read is still a file in the folder and is listed under its file
    /// name: hiding it would leave somebody hunting for the one that broke the picker.
    /// </remarks>
    private static string Named(string path)
    {
        try
        {
            using var read = JsonDocument.Parse(File.ReadAllText(path));

            if (read.RootElement.TryGetProperty("Name", out var name) &&
                name.GetString() is { Length: > 0 } said)
                return said;
        }
        catch (Exception)
        {
        }

        return Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>
    /// A filename nothing is using, numbered past everything already there.
    /// </summary>
    /// <remarks>
    /// Past the highest number in the folder rather than into the first gap. The number is what
    /// decides the order they are offered in, so a new preset belongs at the end.
    /// </remarks>
    private static string Free(string folder)
    {
        int highest = 0;

        foreach (string path in Directory.EnumerateFiles(folder, "*" + SoundMachineRack.Extension))
        {
            string stem = Path.GetFileNameWithoutExtension(path);

            int digits = 0;

            while (digits < stem.Length && char.IsDigit(stem[digits])) digits++;

            if (digits > 0 && int.TryParse(stem[..digits], out int at) && at > highest) highest = at;
        }

        for (int at = highest + 1; ; at++)
        {
            string wanted = Path.Combine(folder, at.ToString("00") + " Preset" + SoundMachineRack.Extension);

            if (!File.Exists(wanted)) return wanted;
        }
    }

    /// <summary>
    /// The least a preset can be: its name, and which machine it is for.
    /// </summary>
    /// <remarks>
    /// The machine's id is what says the file is written the way the machine is drawn rather than
    /// as a whole instrument. Everything else is filled in on the page, one line per control.
    /// </remarks>
    private string Blank(string name) =>
        "{\n"
        + "  \"" + SoundMachinePresetFile.NameKey + "\": \"" + name + "\",\n"
        + "  \"" + SoundMachinePresetFile.MachineKey + "\": \"" + (_project()?.Id ?? "") + "\"\n"
        + "}\n";
}
