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

/// <summary>One recording a preset plays, and how loud it is.</summary>
public sealed partial class WaveLevel : ObservableObject
{
    public WaveLevel(string named, string path, double peak)
    {
        Named = named;
        Path = path;
        Peak = peak;
    }

    /// <summary>What it is called in the machine, folder and all, which is what travels.</summary>
    public string Named { get; }

    /// <summary>Just the file, for a column that already knows which folder it is in.</summary>
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

/// <summary>
/// The jobs on a machine that are neither laying out a panel nor filling in a preset.
/// </summary>
/// <remarks>
/// Both of these are things somebody would otherwise do in a file manager and a text editor: a
/// preset is a file, a folder of recordings beside it, and a set of names inside it that point
/// at that folder, so renaming one by hand means getting three things right at once and a kit
/// that is quietly broken when you get one of them wrong.
///
/// It works on the preset that is picked on the presets page. Two pages about one thing is
/// worse than one page and a second set of tools for it: what you are working on is what you
/// were already working on.
/// </remarks>
public sealed partial class MachineUtilities : ObservableObject
{
    private readonly MachinePresetDesk _desk;

    public MachineUtilities(MachinePresetDesk desk)
    {
        _desk = desk;

        _desk.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MachinePresetDesk.Picked) or nameof(MachinePresetDesk.Presets))
                Reread();
        };
    }

    /// <summary>The preset both tools act on, which is the one open on the presets page.</summary>
    public MachinePresetSlot? Picked => _desk.Picked;

    public bool HasPreset => Picked != null;

    /// <summary>What it is called now, and what the rename box starts at.</summary>
    public string PresetName => Picked?.Name ?? "";

    /// <summary>The file it is in, so it is clear which of the two names is being changed.</summary>
    public string PresetFile => Picked?.FileName ?? "";

    /// <summary>Where its recordings live, or nothing when it has none of its own.</summary>
    public string WaveFolder => _folder.Length == 0 ? "" : Path.GetFileName(_folder);

    private string _folder = "";

    /// <summary>The recordings the preset plays, with what each one peaks at.</summary>
    public ObservableCollection<WaveLevel> Waves { get; } = new();

    public bool HasWaves => Waves.Count > 0;

    [ObservableProperty] private string said = "";

    [ObservableProperty] private string problem = "";

    public bool HasProblem => Problem.Length > 0;

    partial void OnProblemChanged(string value) => OnPropertyChanged(nameof(HasProblem));

    /// <summary>
    /// Reads the picked preset again: its name, its folder, and what its recordings peak at.
    /// </summary>
    /// <remarks>
    /// Off the file rather than off the form on the other page. A level is a fact about a file
    /// on the disc, and the form is what somebody has typed and not saved yet.
    /// </remarks>
    public void Reread()
    {
        Waves.Clear();

        _folder = "";

        Wanted = PresetName;

        Problem = "";

        if (Picked is { } one && File.Exists(one.Path))
        {
            foreach (string named in PresetWaves.Named(one.Path))
            {
                string full = MachinePaths.Outside(named, _desk.Folder);

                if (!File.Exists(full)) continue;

                if (_folder.Length == 0 && Path.GetDirectoryName(full) is { Length: > 0 } home &&
                    MachinePaths.Under(home, _desk.Folder))
                    _folder = home;

                Waves.Add(new WaveLevel(named, full, Peak(full)));
            }
        }

        Retell();
    }

    private void Retell()
    {
        Work();

        OnPropertyChanged(nameof(Picked));
        OnPropertyChanged(nameof(HasPreset));
        OnPropertyChanged(nameof(PresetName));
        OnPropertyChanged(nameof(PresetFile));
        OnPropertyChanged(nameof(WaveFolder));
        OnPropertyChanged(nameof(HasWaves));
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

    // Renaming ------------------------------------------------------------------------------

    /// <summary>What the preset is to be called.</summary>
    [ObservableProperty] private string wanted = "";

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
        if (Picked is not { } one) return;

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
            string home = _desk.Folder;
            string prefix = Numbered(one.FileName);
            string wantedFile = Path.Combine(home, prefix + name + ".json");

            if (File.Exists(wantedFile))
            {
                Problem = "There is already a preset in " + Path.GetFileName(wantedFile) + ".";

                return;
            }

            // The folder moves only when this preset is the only one playing out of it.
            string? folder = Shared() ? null : _folder;
            string? wantedFolder = folder is { Length: > 0 } ? Path.Combine(home, name) : null;

            if (wantedFolder is { Length: > 0 } && Directory.Exists(wantedFolder))
            {
                Problem = "There is already a folder called " + name + " beside the presets.";

                return;
            }

            var held = JsonNode.Parse(File.ReadAllText(one.Path)) as JsonObject;

            if (held == null)
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

            _desk.Reread();

            _desk.Picked = _desk.Presets.FirstOrDefault(slot =>
                Tracker.FilePaths.Same(slot.Path, wantedFile)) ?? _desk.Picked;

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

    /// <summary>True when another preset plays out of this preset's folder as well.</summary>
    private bool Shared() =>
        _folder.Length > 0 &&
        PresetWaves.Users(_folder, _desk.Folder, _desk.Presets
            .Where(slot => Picked is not { } mine || !Tracker.FilePaths.Same(slot.Path, mine.Path))
            .Select(slot => slot.Path)).Count > 0;

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

    // Levels --------------------------------------------------------------------------------

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
    /// In place, which is the whole of what levelling a set means: the files inside the machine
    /// are the machine's, they travel with it, and a level held anywhere else would be a level
    /// that arrives on somebody else's rack as a number nothing applies.
    ///
    /// A file two presets play is a file two presets play. It is rewritten once and both hear
    /// it, which is why the rename above will not split such a folder either.
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

        Said = moved == 0
            ? "Nothing needed moving."
            : moved == 1 ? "One recording levelled." : moved + " recordings levelled.";

        Reread();
    });
}
