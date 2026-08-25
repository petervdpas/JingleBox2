using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Machines;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

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

    private readonly JsonObject _held;
    private readonly string _key;
    private readonly Action _changed;

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

    public bool IsWave => Kind == WaveKind;

    /// <summary>What it is set to. Writing it writes the preset.</summary>
    [ObservableProperty] private string text = "";

    private bool _reading;

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
    private readonly JsonObject _held;
    private readonly Action _changed;

    public PresetSection(
        JsonObject held, string key, string heading, IReadOnlyList<PresetLine> lines, Action changed)
    {
        _held = held;
        _changed = changed;

        Key = key;
        Heading = heading;
        Lines = new ObservableCollection<PresetLine>(lines);
    }

    /// <summary>What it is called in the file: a pad's key, or a setting's own key.</summary>
    public string Key { get; }

    /// <summary>What the page writes over it.</summary>
    public string Heading { get; }

    public ObservableCollection<PresetLine> Lines { get; }

    /// <summary>Takes it out of the preset, and everything assigned to it with it.</summary>
    public IRelayCommand RemoveCommand => new RelayCommand(() =>
    {
        _held.Remove(Key);

        _changed();
    });
}

/// <summary>
/// Something on the machine a preset could be given a value for, but has not yet.
/// </summary>
/// <param name="Kind">
/// Which sort of thing on the machine it is: a pad, a knob, a fader, a recording. The kind the
/// machine's own description gave it, so the list can be narrowed to one sort at a time.
/// </param>
public sealed record PresetOffer(string Key, string Said, string Kind)
{
    public override string ToString() => Said;
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
    private readonly JsonObject _held;
    private readonly MachineProjectShape _machine;
    private readonly Action _changed;
    private readonly string _home;

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

    public bool HasOffers => Offers.Count > 0;

    /// <summary>Which of them the button would add.</summary>
    [ObservableProperty] private PresetOffer? offered;

    /// <summary>Gives that thing a value, so the preset now says something about it.</summary>
    /// <remarks>
    /// A pad arrives with the lines a pad has and nothing in them; a setting arrives at whatever
    /// the machine says it rests at. Both are the least that can be said about the thing, which
    /// is what somebody who has just added it wants to start from.
    /// </remarks>
    public IRelayCommand AddCommand => new RelayCommand(() =>
    {
        if (Offered is not { } offer) return;

        // Already spoken about, so there is nothing to start. It cannot be reached from the list
        // any more, but a stale selection can still arrive here.
        if (_held.ContainsKey(offer.Key)) return;

        if (_machine.PadKeys.Contains(offer.Key))
        {
            var block = new JsonObject();

            foreach (string word in _machine.PadWords) block[word] = "";

            foreach (var parameter in _machine.PadParameters)
                block[parameter.Key] = JsonValue.Create(parameter.Default);

            _held[offer.Key] = block;
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
    /// Reads the preset again: what it holds, and what it could still be given.
    /// </summary>
    /// <remarks>
    /// The whole list rather than the one line that moved, because adding or removing a thing
    /// changes both halves at once and the two have to agree.
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
                Sections.Add(new PresetSection(_held, key, key, Pad(block), Told));

                continue;
            }

            Sections.Add(new PresetSection(_held, key, Called(key), new[] { Line(_held, key) }, Told));
        }

        // Everything the machine has, whether the preset speaks about it yet or not. The list is
        // what this machine can be given a value for, and that does not change as a preset is
        // filled in: a list that shrank while you worked would be a different list every time you
        // looked at it, and the one thing you wanted would have moved.
        var everything = new List<PresetOffer>();

        foreach (string key in _machine.PadKeys)
            everything.Add(new PresetOffer(key, key, MachineElementKinds.Pad));

        foreach (var parameter in _machine.Parameters)
        {
            if (_machine.PadParameters.Any(one => one.Key == parameter.Key)) continue;

            everything.Add(new PresetOffer(parameter.Key, parameter.Name, _machine.Drawn(parameter.Key)));
        }

        foreach (string word in _machine.Words)
        {
            if (_machine.PadWords.Contains(word)) continue;

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

    partial void OnKindChanged(string value) => Narrow();

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
            if ((Kind == AnyKind || one.Kind == Kind) && !_held.ContainsKey(one.Key))
                Offers.Add(one);

        Offered = Offers.FirstOrDefault();

        OnPropertyChanged(nameof(HasOffers));
    }

    private void Told()
    {
        _changed();

        Rebuild();
    }

    /// <summary>The lines a pad has, which is what the machine puts beside its grid.</summary>
    private IReadOnlyList<PresetLine> Pad(JsonObject block)
    {
        var lines = new List<PresetLine>();

        foreach (string word in _machine.PadWords) lines.Add(Line(block, word));

        foreach (var parameter in _machine.PadParameters) lines.Add(Line(block, parameter.Key));

        // Anything the machine no longer has a control for is still shown, so a preset written
        // against a later version can be read here rather than quietly losing half of itself.
        foreach (var (key, _) in block.ToList())
        {
            if (lines.Any(one => one.Name == Called(key))) continue;

            if (_machine.PadWords.Contains(key) || _machine.PadParameters.Any(one => one.Key == key)) continue;

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

    private string Called(string key) => _machine.Called(key);

    /// <summary>That recording said from the machine's presets folder, where it is under it.</summary>
    public string Inside(string path)
    {
        if (path.Length == 0 || _home.Length == 0) return path;

        try
        {
            string full = Path.GetFullPath(path);
            string root = Path.GetFullPath(_home);

            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return path;

            return full[(root.Length + 1)..].Replace(Path.DirectorySeparatorChar, '/');
        }
        catch (Exception)
        {
            return path;
        }
    }
}

/// <summary>The words a preset file uses for itself rather than for the machine.</summary>
public static class MachinePresetWords
{
    public const string Name = "Name";

    public const string Machine = "Machine";

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
    public MachineProjectShape(MachinePanel? panel, IReadOnlyList<MachineParameter> parameters)
    {
        Parameters = parameters;

        if (panel?.Root is not { } root) return;

        var keys = new List<string>();
        var words = new List<string>();

        Walk(root, keys, words);

        PadKeys = keys;
        Words = words;

        HasPads = keys.Count > 0;

        // A pad's own settings: the parameters the panel names anywhere under a pad's name. What
        // a machine puts beside its grid is, by definition, what a pad is set by.
        PadParameters = _underPad
            .Select(key => parameters.FirstOrDefault(one => one.Key == key))
            .Where(one => one != null)
            .Select(one => one!)
            .ToList();

        PadWords = words.Where(word => _underPad.Contains(word) || word.StartsWith(PadStem, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>What a pad's settings are called, before what they are.</summary>
    private const string PadStem = "pad_";

    private readonly List<string> _underPad = new();

    public IReadOnlyList<MachineParameter> Parameters { get; }

    public IReadOnlyList<MachineParameter> PadParameters { get; private set; } = Array.Empty<MachineParameter>();

    /// <summary>
    /// The keys the machine's pad buttons answer to, as the machine wrote them.
    /// </summary>
    /// <remarks>
    /// Words rather than numbers, because a machine writes what its buttons answer to and this
    /// does not get to decide what that looks like. A preset is keyed by the same words, so what
    /// is on the page is what is in the file with nothing turned into anything.
    /// </remarks>
    public IReadOnlyList<string> PadKeys { get; private set; } = Array.Empty<string>();

    /// <summary>Every setting the machine holds as words, by key.</summary>
    public IReadOnlyList<string> Words { get; private set; } = Array.Empty<string>();

    /// <summary>Which of those belong to a pad.</summary>
    public IReadOnlyList<string> PadWords { get; private set; } = Array.Empty<string>();

    /// <summary>True when the machine has a grid of pads, which makes its presets kits.</summary>
    public bool HasPads { get; private set; }

    /// <summary>True when that setting is a recording, which is picked rather than typed.</summary>
    public bool IsTake(string key) => _takes.Contains(key);

    private readonly HashSet<string> _takes = new(StringComparer.Ordinal);

    /// <summary>What the panel draws that setting as: a knob, a fader, a recording, a pad.</summary>
    /// <remarks>
    /// The element kind the machine used, and nothing worked out from the name. A machine that
    /// puts its level on a fader and its tune on a knob says so, and the list can be narrowed to
    /// one sort at a time without this program having opinions about which is which.
    /// </remarks>
    public string Drawn(string key) => _drawn.TryGetValue(key, out string? kind) ? kind : "Setting";

    private readonly Dictionary<string, string> _drawn = new(StringComparer.Ordinal);

    /// <summary>What the panel writes beside that setting, or the key when it says nothing.</summary>
    public string Called(string key)
    {
        if (_called.TryGetValue(key, out string? said)) return said;

        return Parameters.FirstOrDefault(one => one.Key == key)?.Name ?? key;
    }

    private readonly Dictionary<string, string> _called = new(StringComparer.Ordinal);

    private void Walk(MachineElement element, List<string> keys, List<string> words)
    {
        if (element.Element == MachineElementKinds.Pads)
        {
            foreach (var child in element.Children)
            {
                if (child.Element != MachineElementKinds.Pad) continue;

                if (child.Properties.TryGetValue("key", out string? said) && said.Length > 0)
                    keys.Add(said);
            }
        }

        if (element.Parameter.Length > 0 && !_drawn.ContainsKey(element.Parameter))
            _drawn[element.Parameter] = element.Element;

        // A control can name settings other than the one it turns: a picture of a recording has
        // four handles on it, each a fraction of the file, and an envelope curve names the four
        // the faders beside it move. Those belong to the control that draws them, which is what
        // somebody looking for them would say they are.
        foreach (var (_, said) in element.Properties)
        {
            if (said.Length == 0 || _drawn.ContainsKey(said)) continue;

            if (Parameters.Any(one => one.Key == said)) _drawn[said] = element.Element;
        }

        if (element.Parameter.Length > 0 && element.Parameter.StartsWith(PadStem, StringComparison.Ordinal)
            && !_underPad.Contains(element.Parameter))
            _underPad.Add(element.Parameter);

        if (element.Element is MachineElementKinds.Take or MachineElementKinds.Text
            && element.Parameter.Length > 0
            && !words.Contains(element.Parameter))
        {
            words.Add(element.Parameter);

            if (element.Element == MachineElementKinds.Take)
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
