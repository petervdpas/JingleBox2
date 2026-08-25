using JingleBox2.Machines;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// A preset written the way the machine is drawn: one small piece of JSON per control.
/// </summary>
/// <remarks>
/// Every knob on a machine names a parameter, and every pad button names itself. So a preset is
/// those names with values against them, and nothing else. What the machine already says is not
/// said again: which key a pad answers to is on the button, so it is not in the preset, and
/// reordering the grid cannot silently move every drum along one the way a list of sixteen could.
///
/// It replaces a preset written as a whole instrument. That shape carried a plugin path, a plugin
/// id and a base note into a drum kit, none of which a kit has, and it keyed its pads by their
/// place in a list.
///
/// The machine's id at the top is what says which shape a file is in. A file without one is the
/// older kind and is read as an instrument, which is what every preset on the machines that have
/// not been converted still is.
/// </remarks>
public static class MachinePresetFile
{
    /// <summary>What the preset is called on the picker.</summary>
    public const string NameKey = "Name";

    /// <summary>Which machine it is for, and the mark that says it is written this way.</summary>
    public const string MachineKey = "Machine";

    /// <summary>The word that says the picker offers your own recordings instead of presets.</summary>
    public const string BrowseKey = "Browse";

    /// <summary>True when that file is written the new way.</summary>
    public static bool Keyed(JsonNode? read) =>
        read is JsonObject held && held.ContainsKey(MachineKey);

    /// <summary>
    /// Reads one, applying it to a fresh instrument of that machine's kind.
    /// </summary>
    /// <remarks>
    /// The instrument is what the engine plays, so a preset has to become one before it can be
    /// heard. What this knows that nothing else does is which name goes where: a machine-wide
    /// key is a setting on the instrument, and a name that matches a pad button is that pad's
    /// block.
    /// </remarks>
    public static TrackerInstrument? Read(string path, MachineProject machine)
    {
        try
        {
            var read = JsonNode.Parse(File.ReadAllText(path));

            if (!Keyed(read)) return null;

            var held = (JsonObject)read!;

            var kind = Machine.SlotFor(machine.Id)?.Kind ?? TrackerInstrumentKind.Sample;
            var sound = new TrackerInstrument { Kind = kind, Name = Said(held, NameKey) };

            if (sound.Name.Length == 0) sound.Name = Path.GetFileNameWithoutExtension(path);

            var buttons = Buttons(machine);
            string home = Path.GetDirectoryName(path) ?? "";

            ViewModels.DrumKitViewModel? pads = null;

            if (buttons.Count > 0)
            {
                sound.Kit ??= DrumKit.Empty();
                sound.Kit.Clamp();

                // The key each pad answers to is on the button, not in the preset. A preset that
                // said it too would be a second place for it to be wrong.
                for (int at = 0; at < buttons.Count && at < sound.Kit.Pads.Count; at++)
                    if (buttons[at].Semitone >= 0) sound.Kit.Pads[at].Semitone = buttons[at].Semitone;

                pads = new ViewModels.DrumKitViewModel(sound.Kit, () => { }, _ => { });
            }

            var values = new RecordingValues(sound);

            foreach (var (key, node) in held)
            {
                if (key is NameKey or MachineKey or BrowseKey) continue;

                if (node is JsonObject block)
                {
                    // By the key the pad answers to, which is what the button carries and what
                    // fires it in a pattern. A name is taken too, for a preset written before
                    // the keys were used and for a machine that names its buttons and nothing
                    // else.
                    // By the key the machine gave the button, as it wrote it. A name is taken
                    // too, for a preset written before the keys were used.
                    int at = buttons.FindIndex(one => one.Key == key);

                    if (at < 0) at = buttons.FindIndex(one => one.Name == key);

                    if (at >= 0 && at < pads!.Pads.Count)
                    {
                        var held2 = pads.Pads[at];

                        Pad(new KitValues(pads, () => held2), machine, block, home);
                    }

                    continue;
                }

                Put(values, sound, key, node, home);
            }

            sound.Patch.Clamp();
            sound.Kit?.Clamp();

            return sound;
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.LogArea.App, "A preset could not be read: " + path, ex);

            return null;
        }
    }

    /// <summary>
    /// Writes one out from an instrument, keyed the way the machine is drawn.
    /// </summary>
    /// <remarks>
    /// Only what the machine declares. A setting the machine has no control for is not written,
    /// because it is not a thing this machine can be set to, and carrying it would put a base
    /// note into a drum kit again.
    /// </remarks>
    public static string Write(TrackerInstrument sound, MachineProject machine)
    {
        var held = new JsonObject
        {
            [NameKey] = sound.Name,
            [MachineKey] = machine.Id,
        };

        var buttons = Buttons(machine);
        string home = Path.Combine(machine.Folder, MachineProject.PresetsFolder);

        if (buttons.Count > 0)
        {
            var kit = new ViewModels.DrumKitViewModel(sound.Kit ??= DrumKit.Empty(), () => { }, _ => { });

            for (int at = 0; at < buttons.Count && at < kit.Pads.Count; at++)
            {
                var held2 = kit.Pads[at];

                // The key the pad answers to is what names its block. It is the one fact about a
                // pad that is true outside the machine as well: it is the note that fires it in a
                // pattern, so a preset can be read against a keyboard rather than against a list
                // of names somebody invented.
                string named = buttons[at].Key.Length > 0 ? buttons[at].Key : buttons[at].Name;

                held[named] = Pad(new KitValues(kit, () => held2), machine, home);
            }

            return held.ToJsonString(Layout);
        }

        var settings = new RecordingValues(sound);

        // The machine that holds one recording says where in its own words, so the panel's key is
        // what the file uses too.
        if (Named(machine, MachineElementKinds.Take) is { Length: > 0 } take)
            held[take] = Inside(sound.FilePath, home);

        foreach (var parameter in machine.Parameters)
            held[parameter.Key] = JsonValue.Create(settings.Get(parameter.Key));

        return held.ToJsonString(Layout);
    }

    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    /// <summary>
    /// One pad, as the block of JSON its button stands for.
    /// </summary>
    /// <remarks>
    /// Every line comes off what the machine declares and goes through the adapter that binds a
    /// key to a thing on a pad. Nothing here is named: a machine that gave its pads a sixth
    /// setting writes a sixth line without this being told about it, and there is one place in
    /// the program that knows what "pad_pan" means.
    /// </remarks>
    private static JsonObject Pad(IMachineValues values, MachineProject machine, string home)
    {
        var block = new JsonObject();

        foreach (string key in Words(machine))
        {
            string said = values.GetText(key);

            block[key] = said.Length > 0 && (said.Contains('/') || Path.IsPathRooted(said))
                ? Inside(said, home)
                : said;
        }

        foreach (var parameter in machine.Parameters)
            block[parameter.Key] = JsonValue.Create(values.Get(parameter.Key));

        return block;
    }

    /// <summary>Puts a pad's block onto the pad in that place, by what the machine calls each line.</summary>
    private static void Pad(IMachineValues values, MachineProject machine, JsonObject block, string home)
    {
        var words = Words(machine);
        var numbers = machine.Parameters.Select(one => one.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var (key, node) in block)
        {
            if (numbers.Contains(key))
            {
                values.Set(key, Number(node));

                continue;
            }

            if (!words.Contains(key)) continue;

            if (node is JsonValue said && said.TryGetValue(out string? words2))
                values.SetText(key, words2.Length > 0 && words2.Contains('/') ? Outside(words2, home) : words2);
        }
    }

    /// <summary>
    /// The settings a machine holds as words, read off the controls that hold them.
    /// </summary>
    /// <remarks>
    /// A Take is a recording and a Text is something typed. Both are words, and which key each
    /// is kept under is the machine's to say, so it is asked rather than assumed.
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

    private static void Put(RecordingValues values, TrackerInstrument sound, string key, JsonNode? node, string home)
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

    private static string Said(JsonObject held, string key) =>
        held.TryGetPropertyValue(key, out var node) && node is JsonValue value
        && value.TryGetValue(out string? said)
            ? said
            : "";

    /// <summary>That recording said from the presets folder, so the preset travels with the machine.</summary>
    private static string Inside(string path, string home)
    {
        if (path.Length == 0 || home.Length == 0) return path;

        try
        {
            string full = Path.GetFullPath(path);
            string root = Path.GetFullPath(home);

            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return path;

            return full[(root.Length + 1)..].Replace(Path.DirectorySeparatorChar, '/');
        }
        catch (Exception)
        {
            return path;
        }
    }

    /// <summary>And back: where that name really is on this disc.</summary>
    private static string Outside(string named, string home)
    {
        if (named.Length == 0 || home.Length == 0 || Path.IsPathRooted(named)) return named;

        return Path.GetFullPath(Path.Combine(home, named.Replace('/', Path.DirectorySeparatorChar)));
    }

    /// <summary>The pad buttons the machine declares, with the key each answers to.</summary>
    public static List<(string Name, string Key, int Semitone)> Buttons(MachineProject machine)
    {
        var found = new List<(string, string, int)>();

        if (machine.Panel.Root is { } root) Walk(root, found);

        return found;
    }

    private static void Walk(MachineElement element, List<(string, string, int)> found)
    {
        if (element.Element == MachineElementKinds.Pads)
        {
            foreach (var child in element.Children)
            {
                if (child.Element != MachineElementKinds.Pad) continue;

                string said = child.Properties.TryGetValue("key", out string? held) ? held : "";

                found.Add((child.Parameter, said, MachineNotes.Semitone(said)));
            }

            return;
        }

        foreach (var child in element.Children) Walk(child, found);
    }

    /// <summary>What the machine calls the setting behind the first control of that kind.</summary>
    private static string? Named(MachineProject machine, string kind)
    {
        return machine.Panel.Root is { } root ? Find(root, kind) : null;

        static string? Find(MachineElement element, string kind)
        {
            if (element.Element == kind && element.Parameter.Length > 0) return element.Parameter;

            foreach (var child in element.Children)
                if (Find(child, kind) is { } found) return found;

            return null;
        }
    }
}
