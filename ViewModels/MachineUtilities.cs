using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Tracker.Machines;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace JingleBox2.ViewModels;

/// <summary>One recording, and how loud it is.</summary>
public sealed partial class WaveLevel : ObservableObject
{
    public WaveLevel(string named, string path, double peak)
    {
        Named = named;
        Path = path;
        Peak = peak;
    }

    /// <summary>What it is called where it was found, which for a preset is what travels.</summary>
    public string Named { get; }

    /// <summary>Just the file, for a column that already knows where it came from.</summary>
    public string Name => System.IO.Path.GetFileName(Named);

    /// <summary>Where it really is.</summary>
    public string Path { get; }

    /// <summary>Its loudest moment, 0 to 1.</summary>
    public double Peak { get; }

    public string PeakText =>
        Normalization.ToDecibels(Peak).ToString("0.0", CultureInfo.InvariantCulture) + " dB";

    /// <summary>
    /// What this file would move by, once a target has been worked out.
    /// </summary>
    /// <remarks>
    /// Said out loud rather than held quietly, so the column showing it follows the target as it
    /// is dragged. A row rebuilt on every step of a drag would be a list flickering under the
    /// hand for the sake of one number.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GainText))]
    private double gain = 1;

    public string GainText => Math.Abs(Gain - 1) < 0.001
        ? "stays"
        : Normalization.ToDecibels(Gain).ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + " dB";
}

/// <summary>One of the jobs the page offers.</summary>
/// <param name="Key">Which one it is. Declared, not made up, so the page can be read.</param>
/// <param name="Name">What it is called in the list.</param>
/// <param name="Blurb">One line under the name, for somebody choosing between them.</param>
public sealed record UtilityTool(string Key, string Name, string Blurb)
{
    public const string Rename = "rename";

    public const string Level = "level";

    public override string ToString() => Name;
}

/// <summary>Which recordings the level tool is looking at.</summary>
public enum WaveScope
{
    /// <summary>The ones the picked preset plays.</summary>
    Preset,

    /// <summary>Every recording this machine's presets play.</summary>
    Machine,

    /// <summary>Whatever is in a folder somewhere on the disc.</summary>
    Folder,
}

/// <summary>
/// The jobs on the open machine that are neither laying out a panel nor filling in a preset.
/// </summary>
/// <remarks>
/// Both of the tools here are things somebody would otherwise do in a file manager and an audio
/// editor: a preset is a file, a folder of recordings beside it, and a set of names inside it
/// pointing at that folder, so renaming one by hand means getting three things right at once and
/// a kit that is quietly broken when you get one of them wrong.
///
/// They work on the machine open in the designer and no other. A page acting on a machine other
/// than the one whose name is at the top of the window is a page you can level the wrong kit
/// from without noticing: to reach another machine's tools, open that machine.
/// </remarks>
public sealed partial class MachineUtilities : ObservableObject
{
    private readonly Func<MachineProject?> _open;

    public MachineUtilities(Func<MachineProject?> open)
    {
        _open = open;

        Reread();
    }

    // The machine, which is whichever one is open ----------------------------------------------

    public string MachineName => _open() is { Name.Length: > 0 } one ? one.Name : "";

    public bool HasMachine => _open() is { Folder.Length: > 0 };

    /// <summary>Where it keeps its presets.</summary>
    public string Home =>
        _open() is { Folder.Length: > 0 } one ? Path.Combine(one.Folder, MachineProject.PresetsFolder) : "";

    /// <summary>The presets in it.</summary>
    public ObservableCollection<MachinePresetSlot> Presets { get; } = new();

    /// <summary>The one the tools act on. A machine has many, so this one is picked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreset))]
    private MachinePresetSlot? preset;

    public bool HasPreset => Preset != null;

    partial void OnPresetChanged(MachinePresetSlot? value)
    {
        Wanted = value?.Name ?? "";

        Look();
    }

    /// <summary>
    /// Reads the open machine's presets again, keeping whatever was picked if it is still there.
    /// </summary>
    /// <remarks>
    /// Called when the page is opened and when another machine is, because a preset can be made,
    /// renamed or thrown out on the page beside this one.
    /// </remarks>
    public void Reread()
    {
        string was = Preset?.Path ?? "";

        var found = new List<MachinePresetSlot>();

        string home = Home;

        if (home.Length > 0 && Directory.Exists(home))
        {
            foreach (string path in Directory
                         .EnumerateFiles(home, "*.json")
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                found.Add(new MachinePresetSlot(Called(path), path));
            }
        }

        Sync(Presets, found);

        OnPropertyChanged(nameof(Home));
        OnPropertyChanged(nameof(MachineName));
        OnPropertyChanged(nameof(HasMachine));

        Preset = Presets.FirstOrDefault(one => Tracker.FilePaths.Same(one.Path, was))
                 ?? Presets.FirstOrDefault();

        Retool();

        Look();
    }

    /// <summary>
    /// Brings a list up to date without emptying it under whatever is showing it.
    /// </summary>
    /// <remarks>
    /// Clearing and refilling looks like the same thing and is not. A picker whose list is taken
    /// away lets go of what was picked and writes that back through the binding, so reading the
    /// presets again set the preset to nothing, which left the tools with nothing to apply to
    /// and closed whichever one was open. A slot is a record, so an unchanged read is no change.
    /// </remarks>
    private static void Sync<T>(ObservableCollection<T> list, IReadOnlyList<T> wanted)
    {
        for (int i = list.Count - 1; i >= 0; i--)
            if (!wanted.Contains(list[i])) list.RemoveAt(i);

        for (int i = 0; i < wanted.Count; i++)
            if (i >= list.Count || !Equals(list[i], wanted[i])) list.Insert(i, wanted[i]);
    }

    /// <summary>What a preset calls itself, or its file name when it does not say.</summary>
    private static string Called(string path)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is JsonObject held &&
                held[NameKey]?.GetValue<string>() is { Length: > 0 } said)
                return said;
        }
        catch (Exception)
        {
            // A preset that will not read is still a file, and it is listed under its own name
            // so that it can be picked and renamed out of the way.
        }

        return Path.GetFileNameWithoutExtension(path);
    }

    // The tools --------------------------------------------------------------------------------

    /// <summary>
    /// The tools that apply to what is open, which is what the page offers.
    /// </summary>
    /// <remarks>
    /// A list rather than a fixed row of cards, because the number of them is not fixed. What
    /// belongs here is anything that is a job on a machine's files rather than a part of drawing
    /// a panel or filling in a preset, and there will be more of those than there are today.
    ///
    /// It is also the answer to a tool that does not apply. Rename wants a preset; a machine
    /// with none of them simply does not offer it, which is a shorter thing to read than a card
    /// explaining what it would have done.
    /// </remarks>
    public ObservableCollection<UtilityTool> Tools { get; } = new();

    /// <summary>
    /// The one open, or nothing while the list is showing.
    /// </summary>
    /// <remarks>
    /// Nothing to begin with, and nothing again on the way back. The page is a list of jobs
    /// until you pick one, and then it is that job: two tools laid out side by side already
    /// filled the width, and the ones still to come would not fit at all.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OnList))]
    [NotifyPropertyChangedFor(nameof(OnRename))]
    [NotifyPropertyChangedFor(nameof(OnLevel))]
    private UtilityTool? tool;

    public bool OnList => Tool == null;

    public bool OnRename => Tool?.Key == UtilityTool.Rename;

    public bool OnLevel => Tool?.Key == UtilityTool.Level;

    /// <summary>Opens one, which is what clicking a line in the list does.</summary>
    public IRelayCommand<UtilityTool?> OpenCommand => new RelayCommand<UtilityTool?>(one =>
    {
        if (one == null) return;

        Problem = "";
        Said = "";

        Tool = one;

        // What it is looking at may have moved while another tool was open, or while the page
        // was not being looked at at all.
        Look();
    });

    /// <summary>And back to the list.</summary>
    public IRelayCommand BackCommand => new RelayCommand(() =>
    {
        Tool = null;

        Problem = "";
        Said = "";
    });

    /// <summary>True while any tool applies, which is what the tab hangs on.</summary>
    public bool HasWork => Tools.Count > 0;

    /// <summary>Works out which tools apply now, keeping the one in hand where it still does.</summary>
    private void Retool()
    {
        string was = Tool?.Key ?? "";

        Tools.Clear();

        if (HasPreset)
            Tools.Add(new UtilityTool(UtilityTool.Rename, "Rename a preset",
                "The file, the name inside it, and the folder of recordings named after it."));

        if (HasMachine)
            Tools.Add(new UtilityTool(UtilityTool.Level, "Level recordings",
                "Put a set of wav files on one level, in a preset, this machine, or any folder."));

        OnPropertyChanged(nameof(HasWork));

        // The one in hand stays open if it still applies. Otherwise back to the list, rather
        // than quietly swapping the page for a different tool under the same heading.
        Tool = was.Length == 0 ? null : Tools.FirstOrDefault(one => one.Key == was);
    }

    [ObservableProperty] private string said = "";

    [ObservableProperty] private string problem = "";

    public bool HasProblem => Problem.Length > 0;

    partial void OnProblemChanged(string value) => OnPropertyChanged(nameof(HasProblem));

    // Renaming ---------------------------------------------------------------------------------

    /// <summary>What the preset is to be called.</summary>
    [ObservableProperty] private string wanted = "";

    /// <summary>Where the picked preset's recordings live, or nothing when it has none.</summary>
    public string WaveFolder
    {
        get
        {
            if (Preset is not { } one || Home.Length == 0) return "";

            return PresetWaves.Folder(one.Path, Home) is { Length: > 0 } folder
                ? Path.GetFileName(folder)
                : "none inside the machine";
        }
    }

    public string PresetFile => Preset?.FileName ?? "";

    /// <summary>
    /// Gives the preset a new name, and moves everything that was named after the old one.
    /// </summary>
    /// <remarks>
    /// Three things at once, which is why it is a button rather than a note in a manual: the
    /// file, the name written inside it, and the folder its recordings sit in along with every
    /// path in the preset that points into that folder. Any one of them left behind is a preset
    /// that opens with empty pads on somebody else's machine.
    ///
    /// The number the file starts with is kept. It is what puts the presets in order and it is
    /// not part of the name: "03 Live Drums" renamed to "Studio Kit" stays third.
    ///
    /// A folder more than one preset plays from is left alone, since renaming it would move the
    /// recordings out from under the others.
    /// </remarks>
    public IRelayCommand RenameCommand => new RelayCommand(() =>
    {
        Problem = "";

        if (Preset is not { } one) return;

        string name = (Wanted ?? "").Trim();

        if (name.Length == 0)
        {
            Problem = "A preset needs a name.";

            return;
        }

        if (name == one.Name)
        {
            Said = "That is what it is called already.";

            return;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            Problem = "A name cannot have a slash or a colon in it: it is a file name.";

            return;
        }

        try
        {
            string home = Home;
            string wantedFile = Path.Combine(home, Numbered(one.FileName) + name + ".json");

            if (File.Exists(wantedFile))
            {
                Problem = "There is already a preset in " + Path.GetFileName(wantedFile) + ".";

                return;
            }

            // The folder moves only when this preset is the only one playing out of it.
            string? folder = PresetWaves.Folder(one.Path, home);

            if (folder is { Length: > 0 } && Others(folder, one).Count > 0) folder = null;

            string? wantedFolder = folder is { Length: > 0 } ? Path.Combine(home, name) : null;

            if (wantedFolder is { Length: > 0 } && Directory.Exists(wantedFolder))
            {
                Problem = "There is already a folder called " + name + " beside the presets.";

                return;
            }

            if (JsonNode.Parse(File.ReadAllText(one.Path)) is not JsonObject held)
            {
                Problem = "That preset will not read, so it was not renamed.";

                return;
            }

            held[NameKey] = name;

            if (folder is { Length: > 0 })
            {
                Directory.Move(folder, wantedFolder!);

                Repoint(held, Path.GetFileName(folder), name);
            }

            File.WriteAllText(wantedFile, held.ToJsonString(Pretty));

            File.Delete(one.Path);

            Reread();

            Preset = Presets.FirstOrDefault(slot => Tracker.FilePaths.Same(slot.Path, wantedFile)) ?? Preset;

            Said = folder is { Length: > 0 }
                ? "Renamed to '" + name + "', recordings and all."
                : "Renamed to '" + name + "'.";
        }
        catch (Exception ex)
        {
            Problem = "It could not be renamed: " + ex.Message;
        }
    });

    private const string NameKey = "Name";

    private static readonly System.Text.Json.JsonSerializerOptions Pretty = new() { WriteIndented = true };

    /// <summary>The number a preset file starts with, kept through a rename.</summary>
    private static string Numbered(string fileName)
    {
        int at = 0;

        while (at < fileName.Length && char.IsDigit(fileName[at])) at++;

        if (at == 0) return "";

        // The space after the digits belongs to the prefix, so the new name is not run into it.
        while (at < fileName.Length && fileName[at] == ' ') at++;

        return fileName[..at];
    }

    /// <summary>The other presets playing out of that folder.</summary>
    private IReadOnlyList<string> Others(string folder, MachinePresetSlot mine) =>
        PresetWaves.Users(folder, Home, Presets
            .Where(slot => !Tracker.FilePaths.Same(slot.Path, mine.Path))
            .Select(slot => slot.Path));

    /// <summary>Points every recording in the preset at the folder's new name.</summary>
    private static void Repoint(JsonNode? node, string from, string to)
    {
        switch (node)
        {
            case JsonObject held:
                foreach (string key in held.Select(pair => pair.Key).ToList())
                {
                    if (held[key] is JsonValue value && value.TryGetValue(out string? said) && PresetWaves.IsWave(said))
                    {
                        held[key] = Moved(said!, from, to);

                        continue;
                    }

                    Repoint(held[key], from, to);
                }

                break;

            case JsonArray list:
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is JsonValue value && value.TryGetValue(out string? said) && PresetWaves.IsWave(said))
                    {
                        list[i] = Moved(said!, from, to);

                        continue;
                    }

                    Repoint(list[i], from, to);
                }

                break;
        }
    }

    /// <summary>The same name with its first folder changed, and nothing else touched.</summary>
    private static string Moved(string named, string from, string to)
    {
        int slash = named.IndexOf('/');

        if (slash <= 0) return named;

        return string.Equals(named[..slash], from, StringComparison.Ordinal)
            ? to + named[slash..]
            : named;
    }

    // Levels -----------------------------------------------------------------------------------

    /// <summary>Which recordings the level tool is looking at.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OnPresetScope))]
    [NotifyPropertyChangedFor(nameof(OnMachineScope))]
    [NotifyPropertyChangedFor(nameof(OnFolderScope))]
    private WaveScope scope = WaveScope.Preset;

    partial void OnScopeChanged(WaveScope value) => Look();

    /// <summary>
    /// The three of them as flags, because that is what a row of buttons is.
    /// </summary>
    /// <remarks>
    /// Written out rather than converted from the enum in the view. A converter that turns one
    /// value into three answers is a converter to read before the page can be understood, and
    /// there are three of them and there will never be many.
    /// </remarks>
    public bool OnPresetScope
    {
        get => Scope == WaveScope.Preset;
        set { if (value) Scope = WaveScope.Preset; }
    }

    public bool OnMachineScope
    {
        get => Scope == WaveScope.Machine;
        set { if (value) Scope = WaveScope.Machine; }
    }

    public bool OnFolderScope
    {
        get => Scope == WaveScope.Folder;
        set { if (value) Scope = WaveScope.Folder; }
    }

    /// <summary>The folder picked on disc, when that is what is being looked at.</summary>
    [ObservableProperty] private string folder = "";

    /// <summary>Points the tool at a folder anywhere, which is how a pack is levelled before use.</summary>
    public void Pick(string path)
    {
        Folder = path;

        Scope = WaveScope.Folder;

        Look();
    }

    /// <summary>What is being looked at, said in one line.</summary>
    public string Looking => Scope switch
    {
        WaveScope.Preset => Preset is { } one ? one.Name : "no preset",
        WaveScope.Machine => HasMachine ? "every preset of " + MachineName : "no machine open",
        _ => Folder.Length > 0 ? Folder : "no folder picked yet",
    };

    /// <summary>The recordings in scope, with what each one peaks at.</summary>
    public ObservableCollection<WaveLevel> Waves { get; } = new();

    public bool HasWaves => Waves.Count > 0;

    /// <summary>
    /// Why the list is empty, which is never the same reason twice.
    /// </summary>
    /// <remarks>
    /// An empty box reads as a broken tool. Most machines carry no recordings at all: a synth
    /// patch is numbers, and the Recording machine plays what is on your shelf rather than
    /// anything of its own, so a page finding nothing in either has found exactly what is there.
    /// The difference between that and a folder nobody has picked yet is the whole of what
    /// somebody looking at the empty box wants to know.
    /// </remarks>
    public string Nothing
    {
        get
        {
            if (Waves.Count > 0) return "";

            return Scope switch
            {
                WaveScope.Preset when Preset is not { } => "Pick a preset.",
                WaveScope.Preset => "'" + Preset!.Name + "' has no recordings inside the machine to level.",
                WaveScope.Machine when !HasMachine => "No machine is open.",
                WaveScope.Machine when Presets.Count == 0 => MachineName + " has no presets.",
                WaveScope.Machine => "No preset of " + MachineName + " has recordings inside the machine.",
                _ when Folder.Length == 0 => "No folder picked yet.",
                _ when !Directory.Exists(Folder) => "There is no folder at " + Folder + ".",
                _ => "No wav files in there.",
            };
        }
    }

    /// <summary>
    /// Reads what is in scope again, off the disc.
    /// </summary>
    /// <remarks>
    /// Off the files rather than off any form: a level is a fact about a file, and a form is
    /// what somebody has typed and not saved yet.
    /// </remarks>
    public void Look()
    {
        Waves.Clear();

        Problem = "";

        foreach (var wave in Found()) Waves.Add(wave);

        Work();

        OnPropertyChanged(nameof(HasWaves));
        OnPropertyChanged(nameof(Nothing));
        OnPropertyChanged(nameof(Looking));
        OnPropertyChanged(nameof(WaveFolder));
        OnPropertyChanged(nameof(PresetFile));
    }

    private IEnumerable<WaveLevel> Found()
    {
        var seen = new HashSet<string>(Tracker.FilePaths.Comparer);

        switch (Scope)
        {
            case WaveScope.Preset when Preset is { } one:
                foreach (var wave in Of(one.Path, seen)) yield return wave;

                break;

            case WaveScope.Machine:
                foreach (var slot in Presets)
                    foreach (var wave in Of(slot.Path, seen))
                        yield return wave;

                break;

            case WaveScope.Folder when Folder.Length > 0 && Directory.Exists(Folder):
                foreach (string path in Directory
                             .EnumerateFiles(Folder, "*.wav", SearchOption.AllDirectories)
                             .OrderBy(path => path, StringComparer.Ordinal))
                {
                    if (!seen.Add(Tracker.FilePaths.Full(path))) continue;

                    yield return new WaveLevel(Named(path), path, Peak(path));
                }

                break;
        }
    }

    /// <summary>
    /// The recordings one preset plays, each one once across the whole scope.
    /// </summary>
    /// <remarks>
    /// Once, because a file two presets play is one file. Levelling it twice would move it twice,
    /// and a machine levelled whole would come out with its shared drums quietly ahead of the
    /// rest of it.
    /// </remarks>
    private IEnumerable<WaveLevel> Of(string preset, HashSet<string> seen)
    {
        string home = Home;

        if (home.Length == 0) yield break;

        foreach (string named in PresetWaves.Named(preset))
        {
            string full = MachinePaths.Outside(named, home);

            if (!File.Exists(full)) continue;

            if (!seen.Add(Tracker.FilePaths.Full(full))) continue;

            yield return new WaveLevel(named, full, Peak(full));
        }
    }

    /// <summary>What a file in a picked folder is called, said from that folder.</summary>
    private string Named(string path)
    {
        try
        {
            string root = Tracker.FilePaths.Full(Folder) + Path.DirectorySeparatorChar;
            string full = Tracker.FilePaths.Full(path);

            return full.StartsWith(root, Tracker.FilePaths.Comparison)
                ? full[root.Length..].Replace(Path.DirectorySeparatorChar, '/')
                : Path.GetFileName(path);
        }
        catch (Exception)
        {
            return Path.GetFileName(path);
        }
    }

    /// <summary>The loudest moment in a file, or nought for one that will not read.</summary>
    private static double Peak(string path)
    {
        try
        {
            var (samples, _) = WavFile.Read(path);

            return Normalization.PeakOf(samples);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>Where the loudest moment is to end up, in dBFS.</summary>
    [ObservableProperty] private double target = Normalization.DefaultTargetDecibels;

    partial void OnTargetChanged(double value) => Work();

    public double LeastTarget => Normalization.MinTargetDecibels;

    public double MostTarget => Normalization.MaxTargetDecibels;

    /// <summary>
    /// True to move the whole set by one gain, false to put every file on the target itself.
    /// </summary>
    /// <remarks>
    /// The first is what a kit wants and is why it is the default. A kick and a hat that both
    /// peak at the same number are not a kit in balance: a kick carries far more of itself under
    /// its peak, and a set levelled file by file comes out with the quiet things shouting. One
    /// gain for all of them fixes a set that was simply recorded quiet and leaves the balance
    /// whoever made it chose.
    ///
    /// The other is still worth having, for a folder of odds and ends that were never a set.
    /// </remarks>
    [ObservableProperty] private bool keepsBalance = true;

    partial void OnKeepsBalanceChanged(bool value) => Work();

    /// <summary>What the loudest of them peaks at now.</summary>
    public string LoudestText => Waves.Count == 0
        ? ""
        : Normalization.ToDecibels(Waves.Max(one => one.Peak)).ToString("0.0", CultureInfo.InvariantCulture) + " dB";

    /// <summary>What each file would move by, without moving anything.</summary>
    private void Work()
    {
        if (Waves.Count > 0)
        {
            double whole = Normalization.GainFor(Waves.Max(one => one.Peak), Target);

            foreach (var wave in Waves)
                wave.Gain = KeepsBalance ? whole : Normalization.GainFor(wave.Peak, Target);
        }

        OnPropertyChanged(nameof(LoudestText));
        OnPropertyChanged(nameof(Moves));
        OnPropertyChanged(nameof(CanLevel));
    }

    /// <summary>How many files a level would actually rewrite.</summary>
    public int Moves => Waves.Count(one => Math.Abs(one.Gain - 1) >= 0.001);

    public bool CanLevel => Moves > 0;

    /// <summary>
    /// Rewrites the recordings at their new level.
    /// </summary>
    /// <remarks>
    /// In place, which is the whole of what levelling a set means: the files inside a machine are
    /// the machine's, they travel with it, and a level held anywhere else would be a level that
    /// arrives on somebody else's rack as a number nothing applies.
    /// </remarks>
    public IRelayCommand LevelCommand => new RelayCommand(() =>
    {
        Problem = "";

        int moved = 0;

        foreach (var wave in Waves.ToList())
        {
            if (Math.Abs(wave.Gain - 1) < 0.001) continue;

            try
            {
                var (samples, info) = WavFile.Read(wave.Path);

                Normalization.Apply(samples, wave.Gain);

                WavFile.Write(wave.Path, samples, info.SampleRate, info.Channels);

                moved++;
            }
            catch (Exception ex)
            {
                Problem = "'" + Path.GetFileName(wave.Path) + "' could not be written: " + ex.Message;
            }
        }

        Look();

        Said = moved == 0
            ? "Nothing needed moving."
            : moved == 1 ? "One recording levelled." : moved + " recordings levelled.";
    });
}
