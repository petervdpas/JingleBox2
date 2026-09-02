using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Rack.Faces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using JingleBox2.ViewModels.Records;
using JingleBox2.Devices.SoundMachines;
using JingleBox2.Devices.SoundMachines.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// One value in a preset, assigned to one thing on the machine.
/// </summary>
/// <remarks>
/// What a knob is on the panel, said as a line. The machine declares a control once and it is
/// drawn twice: as the control where the machine is played, and as a line where its presets are
/// written. Neither knows about the other, and both come off the same declaration.
///
/// It reads and writes the preset itself. There is no copy in between, so what the line says and
/// what the file holds cannot drift apart.
/// </remarks>
public sealed partial class PresetLine : ObservableObject
{
    /// <summary>A number, typed.</summary>
    public const string NumberKind = "number";

    /// <summary>Words, typed.</summary>
    public const string WordsKind = "words";

    /// <summary>A recording, which has to be brought into the machine rather than typed.</summary>
    public const string WaveKind = "wave";

    /// <summary>The block this line lives in, written straight through.</summary>
    private readonly JsonObject _held;

    /// <summary>Which entry of it, which is what the machine calls this setting.</summary>
    private readonly string _key;

    /// <summary>Told after every write, so the desk knows the preset no longer matches its file.</summary>
    private readonly Action _changed;

    /// <summary>One line over one entry of a preset.</summary>
    /// <param name="held">The block the entry is in.</param>
    /// <param name="key">Which entry.</param>
    /// <param name="name">What the machine writes beside it.</param>
    /// <param name="kind">One of the three above.</param>
    /// <param name="unit">What it is measured in, if anything.</param>
    /// <param name="changed">Told after every write.</param>
    public PresetLine(JsonObject held, string key, string name, string kind, string unit, Action changed)
    {
        _held = held;
        _key = key;
        _changed = changed;

        Name = name;
        Kind = kind;
        Unit = unit;

        _reading = true;
        Text = Said();
        _reading = false;
    }

    /// <summary>What the machine calls it.</summary>
    public string Name { get; }

    /// <summary>Which of the three it is.</summary>
    public string Kind { get; }

    /// <summary>What it is measured in, if anything.</summary>
    public string Unit { get; }

    /// <summary>True for the one kind that is picked rather than typed.</summary>
    public bool IsWave => Kind == WaveKind;

    /// <summary>What it is set to. Writing it writes the preset.</summary>
    [ObservableProperty] private string text = "";

    /// <summary>True while the line is reading itself off the file, so reading is not writing.</summary>
    private bool _reading;

    /// <summary>
    /// Writes what was typed into the preset, as a number where the machine says it is one.
    /// </summary>
    /// <remarks>
    /// A number that will not parse is written as words rather than refused, so half a number typed
    /// on the way to a whole one is not thrown away under the hand.
    /// </remarks>
    partial void OnTextChanged(string value)
    {
        if (_reading) return;

        _held[_key] = Kind == NumberKind
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                ? JsonValue.Create(number)
                : JsonValue.Create(value);

        _changed();
    }

    /// <summary>Reads it again, for when something else wrote it.</summary>
    public void Reread()
    {
        string said = Said();

        if (said == Text) return;

        _reading = true;

        try
        {
            Text = said;
        }
        finally
        {
            _reading = false;
        }
    }

    /// <summary>
    /// What the file holds for this entry, as text.
    /// </summary>
    /// <remarks>
    /// Every kind JSON can hold, since a preset can have been written by hand or by an older
    /// version: words, a number, a flag, and anything else as it is written down.
    /// </remarks>
    private string Said()
    {
        if (!_held.TryGetPropertyValue(_key, out var node) || node is not JsonValue value) return "";

        if (value.TryGetValue(out string? words)) return words;

        if (value.TryGetValue(out double number)) return number.ToString("0.######", CultureInfo.InvariantCulture);

        if (value.TryGetValue(out bool flag)) return flag ? "1" : "0";

        return value.ToJsonString();
    }
}

/// <summary>
/// One thing on the machine that a preset has been given values for.
/// </summary>
/// <remarks>
/// A pad, or a setting the machine has one of. A preset holds only the ones somebody assigned:
/// an empty preset is empty, and one that sets a single drum is three lines of JSON rather than
/// sixteen blocks of nothing.
/// </remarks>
public sealed partial class PresetSection : ObservableObject
{
    /// <summary>The preset itself, since a block is renamed and removed from the whole file.</summary>
    private readonly JsonObject _held;

    /// <summary>Told after every change, so the desk knows the preset has moved.</summary>
    private readonly Action _changed;

    /// <summary>One block of a preset, with the lines the machine says it has.</summary>
    /// <param name="held">The preset the block is in.</param>
    /// <param name="key">What the block is called in the file.</param>
    /// <param name="heading">What the page writes over it.</param>
    /// <param name="lines">Its lines, already built.</param>
    /// <param name="changed">Told after every change.</param>
    /// <param name="canRename">True where the name is the builder's to choose.</param>
    public PresetSection(
        JsonObject held, string key, string heading, IReadOnlyList<PresetLine> lines, Action changed,
        bool canRename = false)
    {
        _held = held;
        _changed = changed;

        Key = key;
        CanRename = canRename;

        _heading = heading;

        Lines = new ObservableCollection<PresetLine>(lines);
    }

    /// <summary>What it is called in the file: a pad's key, or a setting's own key.</summary>
    public string Key { get; private set; }

    /// <summary>
    /// True where the name is the builder's to choose rather than the machine's.
    /// </summary>
    /// <remarks>
    /// A pad's block is headed by the key the machine gave that button, which is not a name
    /// anybody here gets to change: change it and the block stops being about that pad. A zone's
    /// name is nowhere else, so it is typed here or it is nothing.
    /// </remarks>
    public bool CanRename { get; }

    /// <summary>What the page writes over it, and what the file calls it.</summary>
    /// <remarks>
    /// A name already taken is refused rather than numbered: somebody typing a name that is already
    /// there meant that name, and a silently different one is worse than none. The rename would
    /// otherwise swallow the other block.
    /// </remarks>
    public string Heading
    {
        get => _heading;
        set
        {
            string wanted = (value ?? "").Trim();

            if (!CanRename || wanted.Length == 0 || wanted == _heading) return;

            if (_held.ContainsKey(wanted)) return;

            Rename(wanted);

            _heading = wanted;
            Key = wanted;

            OnPropertyChanged();

            _changed();
        }
    }

    /// <inheritdoc cref="Heading"/>
    private string _heading;

    /// <summary>
    /// Renames the block without moving it, by writing the whole file out again in order.
    /// </summary>
    /// <remarks>
    /// Taking it out and putting it back would put it last, and where a block sits is not
    /// nothing: a map asks its zones in order and the first that covers a key wins, so a zone
    /// renamed would quietly change what the instrument plays.
    /// </remarks>
    private void Rename(string wanted)
    {
        var was = _held.ToList();

        _held.Clear();

        foreach (var (key, node) in was)
            _held[key == Key ? wanted : key] = node?.DeepClone();
    }

    /// <summary>The values assigned to this thing, in the order the machine declares them.</summary>
    public ObservableCollection<PresetLine> Lines { get; }

    /// <summary>Takes it out of the preset, and everything assigned to it with it.</summary>
    /// <remarks>Always enabled: a block is on the page only while it is in the file.</remarks>
    public IRelayCommand RemoveCommand => new RelayCommand(() =>
    {
        _held.Remove(Key);

        _changed();
    });
}

/// <summary>
/// A preset: values assigned to things on the machine, added one at a time.
/// </summary>
/// <remarks>
/// Everything is called what the file calls it. A pad's block is headed by the key the machine
/// gave that button and by nothing else: turning it into a note would be this program deciding
/// the machine is a keyboard, which a grid of ninety six buttons is not, and would put a name on
/// screen that is nowhere in the file.
///
/// The relation the page turns on. A machine declares its controls; the panel makes knobs and
/// pads of them and this makes lines of them. What a preset holds is whichever of them somebody
/// assigned a value to, which is why it is built rather than filled in: a kit that sets four
/// drums says four drums, and the twelve empty pads are not the preset's business.
///
/// It is the file. Every line reads and writes the JSON directly, so what is on screen and what
/// is on disc are one thing rather than two that have to be kept in step.
/// </remarks>
public sealed partial class MachinePresetForm : ObservableObject
{
    /// <summary>Whether a path is inside a machine, and what it is called in there.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly ISoundMachinePaths MachineFolder = new SoundMachinePaths();

    /// <summary>The preset itself, which every line on the page reads and writes.</summary>
    private readonly JsonObject _held;

    /// <summary>What the machine's own description says its presets can hold.</summary>
    private readonly MachineProjectShape _machine;

    /// <summary>Told after every change, so the desk knows the preset no longer matches its file.</summary>
    private readonly Action _changed;

    /// <summary>The machine's presets folder, so a recording can be named from it.</summary>
    private readonly string _home;

    /// <summary>Builds the form off a preset and the machine it belongs to.</summary>
    /// <param name="held">The preset, written into directly.</param>
    /// <param name="machine">What the machine's description says its presets can hold.</param>
    /// <param name="changed">Told after every change.</param>
    /// <param name="home">The presets folder, or nothing where the machine has not been saved.</param>
    public MachinePresetForm(JsonObject held, MachineProjectShape machine, Action changed, string home = "")
    {
        _held = held;
        _machine = machine;
        _changed = changed;
        _home = home;

        Sections = new ObservableCollection<PresetSection>();
        Offers = new ObservableCollection<PresetOffer>();

        Rebuild();
    }

    /// <summary>What the preset holds, in the order the file holds it.</summary>
    public ObservableCollection<PresetSection> Sections { get; }

    /// <summary>What can still be given a value: the machine's things of that sort, less the used.</summary>
    public ObservableCollection<PresetOffer> Offers { get; }

    /// <summary>True when there is anything left to add, so the button can be greyed.</summary>
    public bool HasOffers => Offers.Count > 0;

    /// <summary>Which of them the button would add.</summary>
    [ObservableProperty] private PresetOffer? offered;

    /// <summary>Gives that thing a value, so the preset now says something about it.</summary>
    /// <remarks>
    /// A pad arrives with the lines a pad has and nothing in them; a setting arrives at whatever
    /// the machine says it rests at. Both are the least that can be said about the thing, which
    /// is what somebody who has just added it wants to start from.
    ///
    /// A fresh offer is one more of something the machine does not name: the name is the builder's,
    /// so it starts at whatever is free and is typed over.
    ///
    /// Something the preset already speaks about is refused. It cannot be reached from the list any
    /// more, but a stale selection can still arrive here, and adding it again would replace what is
    /// already there.
    ///
    /// Always enabled; with nothing offered it does nothing.
    /// </remarks>
    public IRelayCommand AddCommand => new RelayCommand(() =>
    {
        if (Offered is not { } offer) return;

        if (offer.Fresh)
        {
            _held[Spare(_machine.ThingCalled)] = Started();

            _changed();

            Rebuild();

            return;
        }

        if (_held.ContainsKey(offer.Key)) return;

        if (_machine.ThingKeys.Contains(offer.Key))
        {
            _held[offer.Key] = Started();
        }
        else if (_machine.Parameters.FirstOrDefault(one => one.Key == offer.Key) is { } setting)
        {
            _held[offer.Key] = JsonValue.Create(setting.Default);
        }
        else
        {
            _held[offer.Key] = "";
        }

        _changed();

        Rebuild();
    });

    /// <summary>
    /// What one of the machine's things starts as: its lines, and nothing in them.
    /// </summary>
    /// <remarks>
    /// The least that can be said about the thing, which is what somebody who has just added it
    /// wants to start from. Off the machine's own declaration, so a machine that gave its pads a
    /// sixth setting starts a sixth line without this being told about it.
    /// </remarks>
    private JsonObject Started()
    {
        var block = new JsonObject();

        foreach (string word in _machine.ThingWords) block[word] = "";

        foreach (var parameter in _machine.ThingParameters)
            block[parameter.Key] = JsonValue.Create(parameter.Default);

        return block;
    }

    /// <summary>
    /// That word, or that word with a number after it where the preset already uses it.
    /// </summary>
    /// <remarks>
    /// A name to type over rather than a name to keep. Two blocks of one file cannot share a
    /// name, and a new one that silently replaced the last is the one thing this must not do.
    /// </remarks>
    private string Spare(string word)
    {
        if (!_held.ContainsKey(word)) return word;

        for (int at = 2; ; at++)
        {
            string tried = word + " " + at.ToString(CultureInfo.InvariantCulture);

            if (!_held.ContainsKey(tried)) return tried;
        }
    }

    /// <summary>
    /// Reads the preset again: what it holds, and what it could still be given.
    /// </summary>
    /// <remarks>
    /// The whole list rather than the one line that moved, because adding or removing a thing
    /// changes both halves at once and the two have to agree.
    ///
    /// What is offered is everything the machine has, whether the preset speaks about it yet or
    /// not. That does not change as a preset is filled in: a list that shrank while you worked
    /// would be a different list every time you looked at it, and the one thing you wanted would
    /// have moved. Which of them can still be added is <see cref="Narrow"/>'s question.
    ///
    /// Either a list of the machine's things, or one offer that makes another. A grid says what
    /// its buttons are called and a preset says what is on each; a map does not and cannot, since
    /// how many zones an instrument is is what the preset decides.
    /// </remarks>
    public void Rebuild()
    {
        Sections.Clear();
        Offers.Clear();

        foreach (var (key, node) in _held.ToList())
        {
            if (key is MachinePresetWords.Name or MachinePresetWords.Machine or MachinePresetWords.Browse) continue;

            if (node is JsonObject block)
            {
                Sections.Add(new PresetSection(
                    _held, key, key, Thing(block), Told, canRename: !_machine.NamesThings));

                continue;
            }

            Sections.Add(new PresetSection(_held, key, Called(key), new[] { Line(_held, key) }, Told));
        }

        var everything = new List<PresetOffer>();

        if (_machine.NamesThings)
        {
            foreach (string key in _machine.ThingKeys)
                everything.Add(new PresetOffer(key, key, _machine.ThingKind));
        }
        else if (_machine.HasThings)
        {
            everything.Add(new PresetOffer(
                _machine.ThingCalled, "Another " + _machine.ThingCalled.ToLowerInvariant(),
                _machine.ThingKind, Fresh: true));
        }

        foreach (var parameter in _machine.Parameters)
        {
            if (_machine.ThingParameters.Any(one => one.Key == parameter.Key)) continue;

            everything.Add(new PresetOffer(parameter.Key, parameter.Name, _machine.Drawn(parameter.Key)));
        }

        foreach (string word in _machine.Words)
        {
            if (_machine.ThingWords.Contains(word)) continue;

            everything.Add(new PresetOffer(word, _machine.Called(word), _machine.Drawn(word)));
        }

        _everything = everything;

        Kinds.Clear();

        Kinds.Add(AnyKind);

        foreach (string kind in everything.Select(one => one.Kind).Distinct(StringComparer.Ordinal))
            Kinds.Add(kind);

        if (!Kinds.Contains(Kind)) Kind = AnyKind;

        Narrow();

        OnPropertyChanged(nameof(HasOffers));
    }

    /// <summary>What the list says for "any of them at all".</summary>
    public const string AnyKind = "Anything";

    /// <summary>The sorts of thing this machine has, for the dropdown that narrows the list.</summary>
    /// <remarks>
    /// Off the machine, not out of a fixed list: a machine with no pads offers no pads, and one
    /// with a control this program has never heard of offers that. What a thing is, is what the
    /// panel draws it as.
    /// </remarks>
    public ObservableCollection<string> Kinds { get; } = new();

    /// <summary>Which sort the list is narrowed to.</summary>
    [ObservableProperty] private string kind = AnyKind;

    /// <summary>Shows the things of the newly picked sort.</summary>
    partial void OnKindChanged(string value) => Narrow();

    /// <summary>Everything the machine can be given a value for, before any narrowing.</summary>
    private IReadOnlyList<PresetOffer> _everything = Array.Empty<PresetOffer>();

    /// <summary>
    /// Shows the things of that sort the preset has nothing to say about yet.
    /// </summary>
    /// <remarks>
    /// Used ones are out of it. The list is what can still be given a value, and something the
    /// preset already speaks about is on the page above with its own lines and its own way of
    /// being taken out again: offering it here as well would be the same thing in two places,
    /// one of which does nothing.
    ///
    /// The sorts are not narrowed the same way. A machine has the sorts it has whether or not
    /// every one of them has been used, and a list of sorts that changed shape while you worked
    /// would be a different list every time you looked at it.
    /// </remarks>
    private void Narrow()
    {
        Offers.Clear();

        foreach (var one in _everything)
            if ((Kind == AnyKind || one.Kind == Kind) && (one.Fresh || !_held.ContainsKey(one.Key)))
                Offers.Add(one);

        Offered = Offers.FirstOrDefault();

        OnPropertyChanged(nameof(HasOffers));
    }

    /// <summary>A line or a block moved: say so, and read the preset again.</summary>
    /// <remarks>
    /// Read again because a block removed or renamed changes what is offered as well as what is
    /// held, and the two halves of the page have to agree.
    /// </remarks>
    private void Told()
    {
        _changed();

        Rebuild();
    }

    /// <summary>The lines one of the machine's things has, which is what it puts beside them.</summary>
    /// <remarks>
    /// Anything the machine no longer has a control for is still shown at the end, so a preset
    /// written against a later version of the machine can be read here rather than quietly losing
    /// half of itself.
    /// </remarks>
    private IReadOnlyList<PresetLine> Thing(JsonObject block)
    {
        var lines = new List<PresetLine>();

        foreach (string word in _machine.ThingWords) lines.Add(Line(block, word));

        foreach (var parameter in _machine.ThingParameters) lines.Add(Line(block, parameter.Key));

        foreach (var (key, _) in block.ToList())
        {
            if (lines.Any(one => one.Name == Called(key))) continue;

            if (_machine.ThingWords.Contains(key) || _machine.ThingParameters.Any(one => one.Key == key)) continue;

            lines.Add(Line(block, key));
        }

        return lines;
    }

    /// <summary>One line, typed the way the machine says that thing is.</summary>
    private PresetLine Line(JsonObject held, string key)
    {
        if (_machine.IsTake(key))
            return new PresetLine(held, key, Called(key), PresetLine.WaveKind, "", _changed);

        if (_machine.Parameters.FirstOrDefault(one => one.Key == key) is { } parameter)
            return new PresetLine(held, key, parameter.Name, PresetLine.NumberKind, parameter.Unit, _changed);

        return new PresetLine(held, key, Called(key), PresetLine.WordsKind, "", _changed);
    }

    /// <summary>What the machine writes beside that setting.</summary>
    private string Called(string key) => _machine.Called(key);

    /// <summary>That recording said from the machine's presets folder, where it is under it.</summary>
    public string Inside(string path)
    {
        if (path.Length == 0 || _home.Length == 0) return path;

        return MachineFolder.Named(path, _home) ?? path;
    }
}

/// <summary>The words a preset file uses for itself rather than for the machine.</summary>
public static class MachinePresetWords
{
    /// <summary>What the preset calls itself, which is the name on the picker.</summary>
    public const string Name = "Name";

    /// <summary>Which machine it is for, and that it is written the way that machine is drawn.</summary>
    public const string Machine = "Machine";

    /// <summary>
    /// That this preset is the one saying the picker offers your own recordings.
    /// </summary>
    /// <remarks>
    /// Not a preset in the ordinary sense: it sets nothing on the machine. It is how a machine
    /// whose whole sound is a recording of yours says which browser it has, in the one place a
    /// machine says what it ships with.
    /// </remarks>
    public const string Browse = "Browse";
}

/// <summary>
/// What a machine's description says about the shape of its presets.
/// </summary>
/// <remarks>
/// Read off the panel once. It is the same reading the panel does when it draws itself: a Pads
/// element means a kit, the buttons in it are what a preset can assign a drum to, and the
/// controls standing beside the grid are what a pad is set by.
/// </remarks>
public sealed class MachineProjectShape
{
    /// <summary>
    /// Reads a machine's panel once and works out what shape its presets are.
    /// </summary>
    /// <remarks>
    /// What one of the machine's things is set by is either the settings its own element names, or
    /// all of them where it names none. A kit means all of them and says nothing, because every
    /// knob on it is about the pad in hand and there is nothing else for one to be about; a sampler
    /// has a filter as well as its zones, and no reader could tell which key is which by looking,
    /// so it says.
    /// </remarks>
    /// <param name="panel">The machine's face, or null for one that has none.</param>
    /// <param name="parameters">Everything it declares, in panel order.</param>
    public MachineProjectShape(Panel? panel, IReadOnlyList<Parameter> parameters)
    {
        Parameters = parameters;

        if (panel?.Root is not { } root) return;

        var keys = new List<string>();
        var words = new List<string>();

        Walk(root, keys, words);

        ThingKeys = keys;
        Words = words;

        HasThings = _holder != null;

        var named = Declared();

        ThingParameters = parameters
            .Where(one => named == null || named.Contains(one.Key))
            .ToList();

        ThingWords = words.Where(word => named == null || named.Contains(word)).ToList();

        if (!HasThings)
        {
            ThingParameters = Array.Empty<Parameter>();
            ThingWords = Array.Empty<string>();
        }
    }

    /// <summary>
    /// The settings the machine says belong to one of its things, or nothing where it says none.
    /// </summary>
    /// <remarks>
    /// Nothing is not the same as an empty list. A machine that has not spoken means all of them,
    /// which is what every kit means; a machine that named none of them would have things with no
    /// settings at all, which is not a machine anybody would draw.
    /// </remarks>
    private HashSet<string>? Declared()
    {
        if (_holder is not { } holder) return null;

        if (!holder.Properties.TryGetValue(Devices.SoundMachines.SoundMachinePresetFile.SettingsProperty, out string? said)
            || said.Trim().Length == 0)
            return null;

        return said
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The element the machine's things stand on: its grid of pads, or its map of zones.</summary>
    private PanelElement? _holder;

    /// <summary>Everything the machine declares, in panel order.</summary>
    public IReadOnlyList<Parameter> Parameters { get; }

    /// <summary>What one of the machine's things is set by.</summary>
    public IReadOnlyList<Parameter> ThingParameters { get; private set; } = Array.Empty<Parameter>();

    /// <summary>
    /// The names of the machine's things, as the machine wrote them.
    /// </summary>
    /// <remarks>
    /// Words rather than numbers, because a machine writes what its buttons answer to and this
    /// does not get to decide what that looks like. A preset is keyed by the same words, so what
    /// is on the page is what is in the file with nothing turned into anything.
    ///
    /// Empty on a machine whose things it does not declare. A map has as many zones as the
    /// instrument turned out to need, so there is no list of them to write down and the names are
    /// whoever is building the preset's to choose.
    /// </remarks>
    public IReadOnlyList<string> ThingKeys { get; private set; } = Array.Empty<string>();

    /// <summary>Every setting the machine holds as words, by key.</summary>
    public IReadOnlyList<string> Words { get; private set; } = Array.Empty<string>();

    /// <summary>Which of those belong to one of its things.</summary>
    public IReadOnlyList<string> ThingWords { get; private set; } = Array.Empty<string>();

    /// <summary>True when the machine holds a set of things, which makes its presets blocks.</summary>
    public bool HasThings { get; private set; }

    /// <summary>
    /// True when the machine says what its things are called, so the page can offer them by name.
    /// </summary>
    /// <remarks>
    /// A pad grid does: the buttons are declared, and a preset says what is on "C-4". A map does
    /// not, and cannot: how many zones an instrument is is what the preset decides, so the page
    /// offers one more zone rather than a list of them and the name is typed.
    /// </remarks>
    public bool NamesThings => ThingKeys.Count > 0;

    /// <summary>What sort of thing one of them is, for the dropdown that narrows the list.</summary>
    public string ThingKind => _holder?.Element == ElementKinds.Zones
        ? ElementKinds.Zones
        : ElementKinds.Pad;

    /// <summary>
    /// And what one of them is called, in the singular.
    /// </summary>
    /// <remarks>
    /// Written out rather than trimmed off the element's name, so the word on the page is a word
    /// somebody chose. "Zones" without its s is a coincidence and not a rule: the next machine to
    /// hold a set of things will not be so obliging.
    /// </remarks>
    public string ThingCalled => _holder?.Element == ElementKinds.Zones ? "Zone" : "Pad";

    /// <summary>True when that setting is a recording, which is picked rather than typed.</summary>
    public bool IsTake(string key) => _takes.Contains(key);

    /// <inheritdoc cref="IsTake"/>
    private readonly HashSet<string> _takes = new(StringComparer.Ordinal);

    /// <summary>What the panel draws that setting as: a knob, a fader, a recording, a pad.</summary>
    /// <remarks>
    /// The element kind the machine used, and nothing worked out from the name. A machine that
    /// puts its level on a fader and its tune on a knob says so, and the list can be narrowed to
    /// one sort at a time without this program having opinions about which is which.
    /// </remarks>
    public string Drawn(string key) => _drawn.TryGetValue(key, out string? kind) ? kind : "Setting";

    /// <inheritdoc cref="Drawn"/>
    private readonly Dictionary<string, string> _drawn = new(StringComparer.Ordinal);

    /// <summary>What the panel writes beside that setting, or the key when it says nothing.</summary>
    public string Called(string key)
    {
        if (_called.TryGetValue(key, out string? said)) return said;

        return Parameters.FirstOrDefault(one => one.Key == key)?.Name ?? key;
    }

    /// <inheritdoc cref="Called"/>
    private readonly Dictionary<string, string> _called = new(StringComparer.Ordinal);

    /// <summary>
    /// Walks the panel once, gathering what its presets can hold.
    /// </summary>
    /// <remarks>
    /// The grid or the map is taken as the holder, whichever the machine has. One or neither and
    /// never both: a machine that did both would be two machines wearing one panel.
    ///
    /// A control can name settings other than the one it turns: a picture of a recording has four
    /// handles on it, each a fraction of the file, and an envelope curve names the four the faders
    /// beside it move. Those belong to the control that draws them, which is what somebody looking
    /// for them would say they are, so its own properties are read for parameter keys as well.
    /// </remarks>
    private void Walk(PanelElement element, List<string> keys, List<string> words)
    {
        if (_holder == null
            && element.Element is ElementKinds.Pads or ElementKinds.Zones)
        {
            _holder = element;

            foreach (var child in element.Children)
            {
                if (child.Element != ElementKinds.Pad) continue;

                if (child.Properties.TryGetValue("key", out string? said) && said.Length > 0)
                    keys.Add(said);
            }
        }

        if (element.Parameter.Length > 0 && !_drawn.ContainsKey(element.Parameter))
            _drawn[element.Parameter] = element.Element;

        foreach (var (_, said) in element.Properties)
        {
            if (said.Length == 0 || _drawn.ContainsKey(said)) continue;

            if (Parameters.Any(one => one.Key == said)) _drawn[said] = element.Element;
        }

        if (element.Element is ElementKinds.Take or ElementKinds.Text
            && element.Parameter.Length > 0
            && !words.Contains(element.Parameter))
        {
            words.Add(element.Parameter);

            if (element.Element == ElementKinds.Take)
            {
                _takes.Add(element.Parameter);

                _called[element.Parameter] =
                    element.Properties.TryGetValue("caption", out string? caption) && caption.Length > 0
                        ? caption
                        : element.Label.Length > 0 ? element.Label : "Recording";
            }
            else
            {
                _called[element.Parameter] = element.Label.Length > 0 ? element.Label : "Name";
            }
        }

        foreach (var child in element.Children) Walk(child, keys, words);
    }
}
