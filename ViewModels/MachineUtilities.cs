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
using JingleBox2.ViewModels.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels.Records;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.Tracker.Machines.Interfaces;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;

namespace JingleBox2.ViewModels;

/// <summary>One recording, and how loud it is.</summary>
public sealed partial class WaveLevel : ObservableObject
{
    /// <summary>The peak normalisation rules. Holds nothing, so one serves the whole object.</summary>
    private readonly INormalization _levels = new Normalization();

    /// <summary>One row of the level tool's list.</summary>
    /// <param name="named">What it is called where it was found.</param>
    /// <param name="path">Where the file really is.</param>
    /// <param name="peak">Its loudest moment, read off the file.</param>
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

    /// <summary>Its loudest moment in decibels, which is how anybody reads a level.</summary>
    public string PeakText =>
        _levels.ToDecibels(Peak).ToString("0.0", CultureInfo.InvariantCulture) + " dB";

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

    /// <summary>What it would move by, with a word rather than a nought for a file that stays.</summary>
    public string GainText => Math.Abs(Gain - 1) < MachineUtilities.SmallestMove
        ? "stays"
        : _levels.ToDecibels(Gain).ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + " dB";
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
    /// <summary>Reading and writing WAV files. Holds nothing, so one serves the whole object.</summary>
    private readonly IWavFile _wav = new WavFile();

    /// <summary>The peak normalisation rules. Holds nothing, so one serves the whole object.</summary>
    private readonly INormalization _levels = new Normalization();

    /// <summary>The waves a preset names.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IPresetWaves WaveFiles = new PresetWaves();

    /// <summary>Whether a path is inside a machine, and what it is called in there.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IMachinePaths Inside = new MachinePaths();

    /// <summary>Whether two paths are one file, by this machine's rules.</summary>
    private readonly IFilePaths _paths = new FilePaths();

    /// <summary>
    /// Whichever machine is open in the designer, asked rather than held.
    /// </summary>
    /// <remarks>
    /// Asked every time, because the machine changes underneath this page and a held one would
    /// have the tools acting on a machine whose name is no longer at the top of the window.
    /// </remarks>
    private readonly Func<MachineProject?> _open;

    /// <summary>Reads the open machine's presets and works out which tools apply.</summary>
    public MachineUtilities(Func<MachineProject?> open)
    {
        _open = open;

        Reread();
    }

    /// <summary>What the open machine is called, or nothing when none is.</summary>
    public string MachineName => _open() is { Name.Length: > 0 } one ? one.Name : "";

    /// <summary>True when there is a machine on disc to act on.</summary>
    /// <remarks>
    /// A folder rather than a name, since a machine being built and never saved has a name and
    /// nothing on the disc for a tool to touch.
    /// </remarks>
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

    /// <summary>True when a preset is picked, which is what the rename tool needs.</summary>
    public bool HasPreset => Preset != null;

    /// <summary>Fills the rename box with what it is called now, and reads the levels again.</summary>
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

        Preset = Presets.FirstOrDefault(one => _paths.Same(one.Path, was))
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
    /// <remarks>
    /// A preset that will not read at all is still a file, and it is listed under its file name so
    /// that it can be picked and renamed out of the way rather than vanishing from the page.
    /// </remarks>
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
        }

        return Path.GetFileNameWithoutExtension(path);
    }

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

    /// <summary>
    /// Which of the three the page is showing, because that is what a set of panels binds to.
    /// </summary>
    /// <remarks>
    /// Written out rather than converted from the tool in the view, the same reasoning the level
    /// tool's three scopes are on: a converter that turns one value into three answers is a
    /// converter to read before the page can be understood.
    /// </remarks>
    public bool OnList => Tool == null;

    /// <inheritdoc cref="OnList"/>
    public bool OnRename => Tool?.Key == UtilityTool.Rename;

    /// <inheritdoc cref="OnList"/>
    public bool OnLevel => Tool?.Key == UtilityTool.Level;

    /// <summary>Opens one, which is what clicking a line in the list does.</summary>
    /// <remarks>
    /// Always enabled; the list only holds tools that apply. What a tool is looking at is read
    /// again on the way in, since it may have moved while another tool was open, or while the page
    /// was not being looked at at all.
    /// </remarks>
    public IRelayCommand<UtilityTool?> OpenCommand => new RelayCommand<UtilityTool?>(one =>
    {
        if (one == null) return;

        Problem = "";
        Said = "";

        Tool = one;

        Look();
    });

    /// <summary>And back to the list, clearing whatever the tool had to say.</summary>
    /// <remarks>Always enabled.</remarks>
    public IRelayCommand BackCommand => new RelayCommand(() =>
    {
        Tool = null;

        Problem = "";
        Said = "";
    });

    /// <summary>True while any tool applies, which is what the tab hangs on.</summary>
    public bool HasWork => Tools.Count > 0;

    /// <summary>Works out which tools apply now, keeping the one in hand where it still does.</summary>
    /// <remarks>
    /// The one in hand stays open if it still applies. Otherwise it goes back to the list, rather
    /// than quietly swapping the page for a different tool under the same heading.
    /// </remarks>
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

        Tool = was.Length == 0 ? null : Tools.FirstOrDefault(one => one.Key == was);
    }

    /// <summary>What the last thing done here did, in the words the page shows.</summary>
    [ObservableProperty] private string said = "";

    /// <summary>Why the last thing done here did not work, which is said apart from the rest.</summary>
    /// <remarks>
    /// Two lines rather than one, because a tool that has just refused to do something and a tool
    /// that has just done something are two states, and a single line would have the refusal
    /// wiped by the next success and read as though it had worked.
    /// </remarks>
    [ObservableProperty] private string problem = "";

    /// <summary>True when there is a refusal to show.</summary>
    public bool HasProblem => Problem.Length > 0;

    /// <summary>Keeps the flag in step with the text, since the page hangs on the flag.</summary>
    partial void OnProblemChanged(string value) => OnPropertyChanged(nameof(HasProblem));

    /// <summary>What the preset is to be called.</summary>
    [ObservableProperty] private string wanted = "";

    /// <summary>Where the picked preset's recordings live, or nothing when it has none.</summary>
    public string WaveFolder
    {
        get
        {
            if (Preset is not { } one || Home.Length == 0) return "";

            return WaveFiles.Folder(one.Path, Home) is { Length: > 0 } folder
                ? Path.GetFileName(folder)
                : "none inside the machine";
        }
    }

    /// <summary>The picked preset's file name, so the page shows what is really being renamed.</summary>
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
    ///
    /// Always enabled: every refusal here has a reason worth reading, and a greyed button says
    /// none of them.
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

            string? folder = WaveFiles.Folder(one.Path, home);

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

            Preset = Presets.FirstOrDefault(slot => _paths.Same(slot.Path, wantedFile)) ?? Preset;

            Said = folder is { Length: > 0 }
                ? "Renamed to '" + name + "', recordings and all."
                : "Renamed to '" + name + "'.";
        }
        catch (Exception ex)
        {
            Problem = "It could not be renamed: " + ex.Message;
        }
    });

    /// <summary>What a preset calls the field holding its own name.</summary>
    private const string NameKey = "Name";

    /// <summary>How a preset is written back.</summary>
    /// <remarks>
    /// Indented, because a preset is a file somebody may open and read, unlike an undo step, and
    /// a rename that reflowed the whole file would make every change to it unreadable afterwards.
    /// </remarks>
    private static readonly System.Text.Json.JsonSerializerOptions Pretty = new() { WriteIndented = true };

    /// <summary>The number a preset file starts with, kept through a rename.</summary>
    /// <remarks>
    /// The space after the digits belongs to the prefix, so the new name is not run into it:
    /// "03 Live Drums" renamed to "Studio Kit" comes out as "03 Studio Kit" and stays third.
    /// </remarks>
    private static string Numbered(string fileName)
    {
        int at = 0;

        while (at < fileName.Length && char.IsDigit(fileName[at])) at++;

        if (at == 0) return "";

        while (at < fileName.Length && fileName[at] == ' ') at++;

        return fileName[..at];
    }

    /// <summary>The other presets playing out of that folder.</summary>
    private IReadOnlyList<string> Others(string folder, MachinePresetSlot mine) =>
        WaveFiles.Users(folder, Home, Presets
            .Where(slot => !_paths.Same(slot.Path, mine.Path))
            .Select(slot => slot.Path));

    /// <summary>Points every recording in the preset at the folder's new name.</summary>
    private static void Repoint(JsonNode? node, string from, string to)
    {
        switch (node)
        {
            case JsonObject held:
                foreach (string key in held.Select(pair => pair.Key).ToList())
                {
                    if (held[key] is JsonValue value && value.TryGetValue(out string? said) && WaveFiles.IsWave(said))
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
                    if (list[i] is JsonValue value && value.TryGetValue(out string? said) && WaveFiles.IsWave(said))
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

    /// <summary>Which recordings the level tool is looking at.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OnPresetScope))]
    [NotifyPropertyChangedFor(nameof(OnMachineScope))]
    [NotifyPropertyChangedFor(nameof(OnFolderScope))]
    private WaveScope scope = WaveScope.Preset;

    /// <summary>Reads whatever the new scope holds, off the disc.</summary>
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

    /// <inheritdoc cref="OnPresetScope"/>
    public bool OnMachineScope
    {
        get => Scope == WaveScope.Machine;
        set { if (value) Scope = WaveScope.Machine; }
    }

    /// <inheritdoc cref="OnPresetScope"/>
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

    /// <summary>True when there is anything to level, so the page can say <see cref="Nothing"/>.</summary>
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

    /// <summary>
    /// The recordings the current scope holds, each one once.
    /// </summary>
    /// <remarks>
    /// The seen set runs across the whole scope rather than per preset, which is what stops a file
    /// two presets share being listed and then levelled twice.
    /// </remarks>
    private IEnumerable<WaveLevel> Found()
    {
        var seen = new HashSet<string>(_paths.Comparer);

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
                    if (!seen.Add(_paths.Full(path))) continue;

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

        foreach (string named in WaveFiles.Named(preset))
        {
            string full = Inside.Outside(named, home);

            if (!File.Exists(full)) continue;

            if (!seen.Add(_paths.Full(full))) continue;

            yield return new WaveLevel(named, full, Peak(full));
        }
    }

    /// <summary>What a file in a picked folder is called, said from that folder.</summary>
    private string Named(string path)
    {
        try
        {
            string root = _paths.Full(Folder) + Path.DirectorySeparatorChar;
            string full = _paths.Full(path);

            return full.StartsWith(root, _paths.Comparison)
                ? full[root.Length..].Replace(Path.DirectorySeparatorChar, '/')
                : Path.GetFileName(path);
        }
        catch (Exception)
        {
            return Path.GetFileName(path);
        }
    }

    /// <summary>The loudest moment in a file, or nought for one that will not read.</summary>
    /// <remarks>
    /// Its own reader and its own rules rather than the object's, because it is asked before
    /// there is an object: the list is built from what is already on disc. Both hold nothing, so
    /// a second one of each costs nothing.
    /// </remarks>
    private static double Peak(string path)
    {
        try
        {
            var (samples, _) = new WavFile().Read(path);

            return new Normalization().PeakOf(samples);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>Where the loudest moment is to end up, in dBFS.</summary>
    [ObservableProperty] private double target = Normalization.Target;

    /// <summary>Works out what every file would move by, without touching any of them.</summary>
    partial void OnTargetChanged(double value) => Work();

    /// <summary>The ends of the target slider, which are the levelling code's own limits.</summary>
    public double LeastTarget => Normalization.Quietest;

    /// <inheritdoc cref="LeastTarget"/>
    public double MostTarget => Normalization.Loudest;

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

    /// <inheritdoc cref="OnTargetChanged(double)"/>
    partial void OnKeepsBalanceChanged(bool value) => Work();

    /// <summary>What the loudest of them peaks at now.</summary>
    public string LoudestText => Waves.Count == 0
        ? ""
        : _levels.ToDecibels(Waves.Max(one => one.Peak)).ToString("0.0", CultureInfo.InvariantCulture) + " dB";

    /// <summary>What each file would move by, without moving anything.</summary>
    private void Work()
    {
        if (Waves.Count > 0)
        {
            double whole = _levels.GainFor(Waves.Max(one => one.Peak), Target);

            foreach (var wave in Waves)
                wave.Gain = KeepsBalance ? whole : _levels.GainFor(wave.Peak, Target);
        }

        OnPropertyChanged(nameof(LoudestText));
        OnPropertyChanged(nameof(Moves));
        OnPropertyChanged(nameof(CanLevel));
    }

    /// <summary>
    /// The smallest change worth rewriting a file for, as a gain either side of unity.
    /// </summary>
    /// <remarks>
    /// A hundredth of a decibel is under anybody's hearing and a rewrite is a whole file read,
    /// scaled and written back, so a set that is already where it should be is left alone rather
    /// than churned. The same figure decides whether a row says it stays put, which keeps the
    /// column and the button telling the same story.
    /// </remarks>
    internal const double SmallestMove = 0.001;

    /// <summary>How many files a level would actually rewrite.</summary>
    public int Moves => Waves.Count(one => Math.Abs(one.Gain - 1) >= SmallestMove);

    /// <summary>True when there is anything to move, which is what greys the button.</summary>
    public bool CanLevel => Moves > 0;

    /// <summary>
    /// Rewrites the recordings at their new level.
    /// </summary>
    /// <remarks>
    /// In place, which is the whole of what levelling a set means: the files inside a machine are
    /// the machine's, they travel with it, and a level held anywhere else would be a level that
    /// arrives on somebody else's rack as a number nothing applies.
    ///
    /// Always enabled; <see cref="CanLevel"/> is what the button's own look is bound to, and a
    /// file that would not move is skipped rather than rewritten with the same samples.
    /// </remarks>
    public IRelayCommand LevelCommand => new RelayCommand(() =>
    {
        Problem = "";

        int moved = 0;

        foreach (var wave in Waves.ToList())
        {
            if (Math.Abs(wave.Gain - 1) < SmallestMove) continue;

            try
            {
                var (samples, info) = _wav.Read(wave.Path);

                _levels.Apply(samples, wave.Gain);

                _wav.Write(wave.Path, samples, info.SampleRate, info.Channels);

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
