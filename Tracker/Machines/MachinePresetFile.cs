using JingleBox2.Machines;
using JingleBox2.Tracker.Synth;
using JingleBox2.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Tracker.Enums;
using JingleBox2.Machines.Interfaces;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Machines.Interfaces;

namespace JingleBox2.Tracker.Machines;

/// <inheritdoc/>
/// <param name="paths">
/// The two questions asked of a recording's path inside a machine. Left out, the ordinary one,
/// which reads the path rule off this system.
/// </param>
public sealed class MachinePresetFile(IMachinePaths? paths = null) : IMachinePresetFile
{
    /// <summary>What a key is called, both ways round.</summary>
    private readonly IMachineNotes _notes = new MachineNotes();

    /// <summary>Where a name written inside a machine really is, and back again.</summary>
    private readonly IMachinePaths _paths = paths ?? new MachinePaths();

    /// <summary>What the preset is called on the picker.</summary>
    public const string NameKey = "Name";

    /// <summary>Which machine it is for, and the mark that says it is written this way.</summary>
    public const string MachineKey = "Machine";

    /// <summary>The word that says the picker offers your own recordings instead of presets.</summary>
    public const string BrowseKey = "Browse";

    /// <summary>
    /// What the element holding a machine's things calls the settings that belong to one of them.
    /// </summary>
    /// <remarks>
    /// A kit has nothing else: every knob on BongaBong is about the pad in hand, so it says
    /// nothing and all of them are the pad's. A sampler has both halves at once, one filter and
    /// as many zones as it turned out to need, and no reader could tell which key is which by
    /// looking. So the machine says.
    /// </remarks>
    public const string SettingsProperty = "settings";

    /// <summary>What a pad button calls the key it answers to.</summary>
    /// <remarks>
    /// Written out rather than built, so the one property a kit's keyboard depends on can be
    /// found by looking for it, here and in every machine.json that names it.
    /// </remarks>
    public const string KeyProperty = "key";

    /// <inheritdoc/>
    /// <remarks>
    /// The five keys are constants as well as answers, since they are facts about the file
    /// format and a caller that has never held one of these still names them.
    /// </remarks>
    string IMachinePresetFile.NameKey => NameKey;

    /// <inheritdoc/>
    string IMachinePresetFile.MachineKey => MachineKey;

    /// <inheritdoc/>
    string IMachinePresetFile.BrowseKey => BrowseKey;

    /// <inheritdoc/>
    string IMachinePresetFile.SettingsProperty => SettingsProperty;

    /// <inheritdoc/>
    string IMachinePresetFile.KeyProperty => KeyProperty;

    /// <inheritdoc/>
    public bool Keyed(JsonNode? read) =>
        read is JsonObject held && held.ContainsKey(MachineKey);

    /// <inheritdoc/>
    public TrackerInstrument? Read(string path, MachineProject machine)
    {
        try
        {
            var read = JsonNode.Parse(File.ReadAllText(path));

            if (!Keyed(read)) return null;

            var held = (JsonObject)read!;

            var kind = Machine.SlotFor(machine.Id)?.Kind ?? TrackerInstrumentKind.Sample;
            var sound = new TrackerInstrument { Kind = kind, Name = Said(held, NameKey) };

            if (sound.Name.Length == 0) sound.Name = Path.GetFileNameWithoutExtension(path);

            string home = Path.GetDirectoryName(path) ?? "";

            var blocks = Blocks(held);

            var buttons = Buttons(machine);

            var owned = Owned(machine);

            IMachineValues? wide = null;
            RecordingValues? loose = null;
            Func<int, IMachineValues>? inside = null;
            Func<string, int>? which = null;

            if (buttons.Count > 0)
            {
                sound.Kit ??= DrumKit.Empty(buttons.Count);
                sound.Kit.Clamp(buttons.Count);

                for (int at = 0; at < buttons.Count && at < sound.Kit.Pads.Count; at++)
                    if (buttons[at].Semitone >= 0) sound.Kit.Pads[at].Semitone = buttons[at].Semitone;

                var pads = new DrumKitViewModel(sound.Kit, () => { }, _ => { });

                loose = new RecordingValues(sound);
                inside = at => new KitValues(pads, () => pads.Pads[at]);

                which = key =>
                {
                    int at = buttons.FindIndex(one => one.Key == key);

                    if (at < 0) at = buttons.FindIndex(one => one.Name == key);

                    return at < pads.Pads.Count ? at : -1;
                };
            }
            else if (Map(machine) != null)
            {
                var made = new ZoneMap();

                foreach (string named in blocks)
                    made.Zones.Add(new SampleZone { Name = named, Shape = new SampleShape() });

                sound.Zones = made.Zones.Count > 0 ? made : ZoneMap.Empty();
                sound.Sampler ??= new SamplerPatch();

                var zones = new ZoneMapViewModel(sound.Zones, () => { }, _ => { });
                var patch = new SamplerPatchViewModel(sound.Sampler, () => { });

                wide = new SamplerValues(zones, patch);
                inside = at => new SamplerValues(zones, patch, () => zones.Zones[at]);
                which = blocks.IndexOf;
            }
            else
            {
                if (kind == TrackerInstrumentKind.Synth)
                    wide = new SynthValues(new ViewModels.SynthPatchViewModel(sound.Patch, () => { }), sound);
                else if (kind == TrackerInstrumentKind.MonoSynth)
                    wide = new MonoSynthValues(Mono(sound));
                else
                    loose = new RecordingValues(sound);
            }

            foreach (var (key, node) in held)
            {
                if (key is NameKey or MachineKey or BrowseKey) continue;

                if (node is JsonObject block)
                {
                    if (inside is null || which is null) continue;

                    int at = which(key);

                    if (at >= 0) Apply(inside(at), owned, block, home);

                    continue;
                }

                if (loose != null) Put(loose, sound, key, node, home);
                else if (wide != null) Line(wide, owned.Outside, owned.OutsideWords, key, node, home);
            }

            sound.Patch.Clamp();
            sound.Kit?.Clamp(buttons.Count);
            sound.Zones?.Clamp();
            sound.Sampler?.Clamp();

            return sound;
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Machines, "A preset could not be read: " + path, ex);

            return null;
        }
    }

    /// <inheritdoc/>
    public string Write(TrackerInstrument sound, MachineProject machine)
    {
        var held = new JsonObject
        {
            [NameKey] = sound.Name,
            [MachineKey] = machine.Id,
        };

        var buttons = Buttons(machine);
        string home = Path.Combine(machine.Folder, MachineProject.PresetsFolder);

        var owned = Owned(machine);

        if (buttons.Count > 0)
        {
            var kit = new DrumKitViewModel(
                sound.Kit ??= DrumKit.Empty(buttons.Count), () => { }, _ => { });

            for (int at = 0; at < buttons.Count && at < kit.Pads.Count; at++)
            {
                var one = kit.Pads[at];

                string named = buttons[at].Key.Length > 0 ? buttons[at].Key : buttons[at].Name;

                held[named] = Block(new KitValues(kit, () => one), owned, home);
            }

            return held.ToJsonString(Layout);
        }

        if (Map(machine) != null)
        {
            sound.Zones ??= ZoneMap.Empty();
            sound.Sampler ??= new SamplerPatch();

            var zones = new ZoneMapViewModel(sound.Zones, () => { }, _ => { });
            var patch = new SamplerPatchViewModel(sound.Sampler, () => { });

            var settings = new SamplerValues(zones, patch);

            foreach (string key in owned.OutsideWords) held[key] = settings.GetText(key);
            foreach (string key in owned.Outside) held[key] = JsonValue.Create(settings.Get(key));

            var used = new HashSet<string>(StringComparer.Ordinal);

            foreach (var one in zones.Zones)
            {
                var zone = one;

                held[Once(zone.Title, used)] =
                    Block(new SamplerValues(zones, patch, () => zone), owned, home);
            }

            return held.ToJsonString(Layout);
        }

        var kind = Machine.SlotFor(machine.Id)?.Kind ?? TrackerInstrumentKind.Sample;

        IMachineValues plain = kind switch
        {
            TrackerInstrumentKind.Synth =>
                new SynthValues(new ViewModels.SynthPatchViewModel(sound.Patch, () => { }), sound),
            TrackerInstrumentKind.MonoSynth => new MonoSynthValues(Mono(sound)),
            _ => new RecordingValues(sound),
        };

        if (Named(machine, MachineElementKinds.Take) is { Length: > 0 } take)
            held[take] = Inside(sound.FilePath, home);

        foreach (string key in owned.Outside) held[key] = JsonValue.Create(plain.Get(key));

        return held.ToJsonString(Layout);
    }

    /// <summary>How a preset is written, which is laid out for reading.</summary>
    /// <remarks>
    /// A preset is a file somebody opens to see what a machine is doing, and one machine here
    /// ships two presets that are a whole chop apiece. Indented, both are still readable.
    /// </remarks>
    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    /// <summary>
    /// The mono synth's patch, wrapped for reading and writing and nothing else.
    /// </summary>
    /// <remarks>
    /// Its lamp is stopped straight away. Reading a file is not showing a panel, and a view model
    /// made to answer twenty questions and then dropped would otherwise leave a timer running
    /// once per preset for as long as the app is up.
    /// </remarks>
    private static ViewModels.MonoSynthPatchViewModel Mono(TrackerInstrument sound)
    {
        sound.MonoSynth ??= new Synth.MonoSynthPatch();

        var patch = new ViewModels.MonoSynthPatchViewModel(sound.MonoSynth, () => { });

        patch.Close();

        return patch;
    }

    /// <summary>
    /// One of the machine's things, as the block of JSON it stands for.
    /// </summary>
    /// <remarks>
    /// Every line comes off what the machine declares and goes through the adapter that binds a
    /// key to a thing on it. Nothing here is named: a machine that gave its pads a sixth setting
    /// writes a sixth line without this being told about it, and there is one place in the program
    /// that knows what "pad_pan" means.
    /// </remarks>
    private JsonObject Block(IMachineValues values, Settings owned, string home)
    {
        var block = new JsonObject();

        foreach (string key in owned.Words)
        {
            string said = values.GetText(key);

            block[key] = said.Length > 0 && (said.Contains('/') || Path.IsPathRooted(said))
                ? Inside(said, home)
                : said;
        }

        foreach (string key in owned.Numbers) block[key] = JsonValue.Create(values.Get(key));

        return block;
    }

    /// <summary>Puts a block back on the thing it is about, by what the machine calls each line.</summary>
    private void Apply(IMachineValues values, Settings owned, JsonObject block, string home)
    {
        foreach (var (key, node) in block) Line(values, owned.Numbers, owned.Words, key, node, home);
    }

    /// <summary>
    /// One line of a preset, put wherever the machine says that name lives.
    /// </summary>
    /// <remarks>
    /// A name the machine does not declare is dropped rather than guessed at. A preset written by
    /// a later version of a machine has lines this one has no control for, and a knob turned by a
    /// name nobody recognises is worse than a knob left alone.
    /// </remarks>
    private void Line(
        IMachineValues values,
        ICollection<string> numbers, ICollection<string> words,
        string key, JsonNode? node, string home)
    {
        if (numbers.Contains(key))
        {
            values.Set(key, Number(node));

            return;
        }

        if (!words.Contains(key)) return;

        if (node is JsonValue said && said.TryGetValue(out string? spoken))
            values.SetText(key, spoken.Length > 0 && spoken.Contains('/') ? Outside(spoken, home) : spoken);
    }

    /// <summary>
    /// Which settings belong to one of the machine's things, and which to the machine itself.
    /// </summary>
    /// <remarks>
    /// Split once and handed round, because every part of writing a preset asks the same question
    /// and asking it three times is three chances to answer it differently.
    /// </remarks>
    /// <param name="Numbers">The values one of the machine's things holds.</param>
    /// <param name="Words">And the words it holds.</param>
    /// <param name="Outside">The values the machine itself holds, which no thing on it owns.</param>
    /// <param name="OutsideWords">And the words. Empty on every machine written so far.</param>
    private sealed record Settings(
        List<string> Numbers, List<string> Words,
        List<string> Outside, List<string> OutsideWords);

    /// <summary>
    /// Reads that split off the machine, once.
    /// </summary>
    /// <remarks>
    /// Only what the machine says is part of the sound is taken at all. A knob that says how
    /// much of the wave the picture shows is a knob on the face like any other and is no more
    /// part of the instrument than which way you happen to be looking, so no preset carries it:
    /// loading a sound would otherwise set somebody else's view. See
    /// <see cref="MachineParameter.Saved"/>.
    ///
    /// Three cases, and the middle one is the one that is easy to get wrong. A machine that
    /// holds no set of things has no blocks, so all of it is the machine's own. A machine that
    /// holds things and names no settings means all of them are the thing's, which is what a kit
    /// means: every knob on BongaBong is about the pad in hand and there is nothing else for one
    /// to be about. Only a machine that holds things and names some settings has both halves,
    /// which is the sampler, with one filter and as many zones as it turned out to need.
    /// </remarks>
    /// <param name="machine">The machine being read or written.</param>
    private static Settings Owned(MachineProject machine)
    {
        var words = Words(machine);

        var keys = machine.Parameters.Where(one => one.Saved).Select(one => one.Key).ToList();

        if (Held(machine) is not { } holder)
            return new Settings(new List<string>(), new List<string>(), keys, words);

        if (!holder.Properties.TryGetValue(SettingsProperty, out string? said) || said.Trim().Length == 0)
            return new Settings(keys, words, new List<string>(), new List<string>());

        var named = said
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        return new Settings(
            keys.Where(named.Contains).ToList(),
            words.Where(named.Contains).ToList(),
            keys.Where(one => !named.Contains(one)).ToList(),
            words.Where(one => !named.Contains(one)).ToList());
    }

    /// <summary>The names of the blocks a file holds, in the order it writes them.</summary>
    private static List<string> Blocks(JsonObject held) =>
        held.Where(one => one.Value is JsonObject && one.Key is not (NameKey or MachineKey or BrowseKey))
            .Select(one => one.Key)
            .ToList();

    /// <summary>
    /// That name, or that name with a number after it where it has been used already.
    /// </summary>
    /// <remarks>
    /// Two zones can be called the same thing, and two lines of one file cannot. The alternative
    /// was to number them all, which would make every preset unreadable to save the one map where
    /// somebody has two zones called Piano.
    /// </remarks>
    private static string Once(string named, ISet<string> used)
    {
        string want = named.Length > 0 ? named : "zone";

        if (used.Add(want)) return want;

        for (int at = 2; ; at++)
        {
            string tried = want + " " + at.ToString(CultureInfo.InvariantCulture);

            if (used.Add(tried)) return tried;
        }
    }

    /// <summary>
    /// The settings a machine holds as words, read off the controls that hold them.
    /// </summary>
    /// <remarks>
    /// A Take is a recording and a Text is something typed. Both are words, and which key each
    /// is kept under is the machine's to say, so it is asked rather than assumed. The face is
    /// walked in reading order and each key is named once.
    /// </remarks>
    private static List<string> Words(MachineProject machine)
    {
        var found = new List<string>();

        if (machine.Panel.Root is { } root) Walk(root, found);

        return found;

        static void Walk(MachineElement element, List<string> found)
        {
            if (element.Element is MachineElementKinds.Take or MachineElementKinds.Text
                && element.Parameter.Length > 0
                && !found.Contains(element.Parameter))
                found.Add(element.Parameter);

            foreach (var child in element.Children) Walk(child, found);
        }
    }

    /// <summary>
    /// One line of a preset for the machine that plays a single recording.
    /// </summary>
    /// <remarks>
    /// That machine keeps its take at the top level rather than in a block, and whether a line
    /// is that take is a question about the line rather than about the machine: anything holding
    /// a separator is a path and goes on the instrument's file, and everything else goes through
    /// the adapter like any other setting. So it has a reader of its own rather than a case in
    /// <see cref="Line"/>.
    /// </remarks>
    private void Put(RecordingValues values, TrackerInstrument sound, string key, JsonNode? node, string home)
    {
        if (node is JsonValue said && said.TryGetValue(out string? words))
        {
            if (words.Length > 0 && (words.Contains('/') || words.Contains('\\')))
                sound.FilePath = Outside(words, home);
            else
                values.SetText(key, words);

            return;
        }

        values.Set(key, Number(node));
    }

    /// <summary>That line read as a number, whatever it was written as.</summary>
    /// <remarks>
    /// A flag and a number in quotes are both taken, because a preset is a file somebody can
    /// write by hand and all three spellings are what people actually type. Anything that will
    /// not read at all is nought, which every machine's adapter clamps into its own range.
    /// </remarks>
    private static double Number(JsonNode? node)
    {
        if (node is not JsonValue value) return 0;

        if (value.TryGetValue(out double held)) return held;

        if (value.TryGetValue(out bool flag)) return flag ? 1 : 0;

        if (value.TryGetValue(out string? said)
            && double.TryParse(said, NumberStyles.Float, CultureInfo.InvariantCulture, out double read))
            return read;

        return 0;
    }

    /// <summary>That property read as words, or nothing when it is missing or is not words.</summary>
    private static string Said(JsonObject held, string key) =>
        held.TryGetPropertyValue(key, out var node) && node is JsonValue value
        && value.TryGetValue(out string? said)
            ? said
            : "";

    /// <summary>That recording said from the presets folder, so the preset travels with the machine.</summary>
    private string Inside(string path, string home)
    {
        return _paths.Named(path, home) ?? path;
    }

    /// <summary>And back: where that name really is on this disc.</summary>
    private string Outside(string named, string home) => _paths.Outside(named, home);

    /// <inheritdoc/>
    public List<(string Name, string Key, int Semitone)> Buttons(MachineProject machine)
    {
        var found = new List<(string, string, int)>();

        if (Pads(machine) is not { } pads) return found;

        foreach (var child in pads.Children)
        {
            if (child.Element != MachineElementKinds.Pad) continue;

            string said = child.Properties.TryGetValue(KeyProperty, out string? held) ? held : "";

            found.Add((child.Parameter, said, _notes.Semitone(said)));
        }

        return found;
    }

    /// <summary>The grid of pads a machine draws, if it draws one.</summary>
    private static MachineElement? Pads(MachineProject machine) =>
        Find(machine, MachineElementKinds.Pads);

    /// <summary>The map of zones a machine draws, if it draws one.</summary>
    private static MachineElement? Map(MachineProject machine) =>
        Find(machine, MachineElementKinds.Zones);

    /// <summary>
    /// Whichever of the two a machine has: the thing its things stand on.
    /// </summary>
    /// <remarks>
    /// A machine has one or neither and never both. A grid of pads and a map of zones are two
    /// ways of holding a set of recordings, and a machine that did both would be two machines
    /// wearing one panel.
    /// </remarks>
    private static MachineElement? Held(MachineProject machine) => Pads(machine) ?? Map(machine);

    /// <summary>The first element of that kind anywhere on the machine's face, or nothing.</summary>
    /// <remarks>
    /// A machine with two grids or two maps on it is a machine nobody has finished, and there is
    /// no sensible second answer to give. Found by walking down from the root, so the first in
    /// reading order is the one that comes back.
    /// </remarks>
    private static MachineElement? Find(MachineProject machine, string kind)
    {
        return machine.Panel.Root is { } root ? Look(root, kind) : null;

        static MachineElement? Look(MachineElement element, string kind)
        {
            if (element.Element == kind) return element;

            foreach (var child in element.Children)
                if (Look(child, kind) is { } found) return found;

            return null;
        }
    }

    /// <summary>What the machine calls the setting behind the first control of that kind.</summary>
    private static string? Named(MachineProject machine, string kind) =>
        Find(machine, kind) is { Parameter.Length: > 0 } found ? found.Parameter : null;
}
