using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.SoundDevices.Interfaces;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;
using JingleBox2.SoundDevices.SoundEffects.Records;

namespace JingleBox2.SoundDevices.SoundEffects;

/// <inheritdoc/>
/// <param name="files">How a file is written whole. Left out, the ordinary one.</param>
/// <param name="registry">What ships, so an effect's own presets can be told from yours. Left out, the real folders.</param>
public sealed class SoundEffectPresets(ISafeFile? files = null, IRackRegistry<SoundEffectProject>? registry = null)
    : ISoundEffectPresets
{
    /// <summary>What a preset file calls the name inside it.</summary>
    public const string NameKey = "Name";

    /// <summary>What a preset file calls the effect it belongs to.</summary>
    /// <remarks>
    /// Written so a file that has been moved or sent to somebody can say what it is for. It is
    /// not checked on the way in: a file inside an effect's own presets folder is that effect's,
    /// whatever it says, which is the rule the soundmachine's reader already keeps so that a
    /// preset can be dropped in by hand without being edited first.
    /// </remarks>
    public const string EffectKey = "Effect";

    /// <summary>What a preset file is called on disc.</summary>
    private const string Extension = ".json";

    /// <summary>Written the way a person reading the folder would want it.</summary>
    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    /// <summary>How a file is written whole.</summary>
    private readonly ISafeFile _files = files ?? new SafeFile();

    /// <summary>What ships.</summary>
    private readonly IRackRegistry<SoundEffectProject> _registry = registry ?? new SoundEffectRegistry();

    /// <summary>Which names a preset of yours may have, the rule both worlds keep.</summary>
    private static readonly IPresetNames Names = new PresetNames();

    /// <inheritdoc/>
    public IReadOnlyList<SoundEffectPreset> For(SoundEffectProject? effect)
    {
        if (Home(effect) is not { Length: > 0 } home || !Directory.Exists(home))
            return Array.Empty<SoundEffectPreset>();

        var found = new List<SoundEffectPreset>();

        try
        {
            foreach (string path in Directory
                         .EnumerateFiles(home, "*" + Extension)
                         .OrderBy(one => one, StringComparer.Ordinal))
                if (Read(path, effect!) is { } preset)
                    found.Add(preset with { File = path, Yours = !_registry.Ships(path) });
        }
        catch (Exception)
        {
            return found.OrderBy(one => one.Yours).ToList();
        }

        return found.OrderBy(one => one.Yours).ToList();
    }

    /// <inheritdoc/>
    public string Refusal(SoundEffectProject? effect, string name)
    {
        if (Home(effect) is not { Length: > 0 }) return "This effect is not on disc here, so it has nowhere to keep a preset.";

        return Names.Refusal(name, effect!.Name, For(effect).Where(one => !one.Yours).Select(one => (one.Name, one.File)));
    }

    /// <inheritdoc/>
    public SoundEffectPreset? Yours(SoundEffectProject? effect, string name)
    {
        string called = (name ?? "").Trim();

        return For(effect).FirstOrDefault(one =>
            one.Yours && string.Equals(one.Name, called, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public SoundEffectPreset? Keep(SoundEffectProject? effect, string name, IReadOnlyDictionary<string, double> settings)
    {
        if (settings is null || Refusal(effect, name).Length > 0) return null;

        string called = name.Trim();
        string path = Yours(effect, called)?.File is { Length: > 0 } already
            ? already
            : Path.Combine(Home(effect), Names.FileFor(called));

        try
        {
            Directory.CreateDirectory(Home(effect));

            _files.Write(path, Body(effect!, new SoundEffectPreset(called, settings)).ToJsonString(Layout));
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Machines, "A preset could not be kept: " + path, ex);

            return null;
        }

        return For(effect).FirstOrDefault(one => string.Equals(one.File, path, StringComparison.Ordinal));
    }

    /// <inheritdoc/>
    public bool RemoveYours(SoundEffectProject? effect, SoundEffectPreset? preset)
    {
        if (Home(effect) is not { Length: > 0 } home || preset is not { Yours: true, File.Length: > 0 }) return false;

        try
        {
            if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(preset.File)), Path.GetFullPath(home), StringComparison.Ordinal)
                || !File.Exists(preset.File) || _registry.Ships(preset.File))
                return false;

            File.Delete(preset.File);

            return true;
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Machines, "A preset could not be taken off: " + preset.File, ex);

            return false;
        }
    }

    /// <summary>A preset as it is written into its file: the name, the effect, and each setting on its own grid.</summary>
    /// <param name="effect">The effect it belongs to, which says what a key means.</param>
    /// <param name="preset">What to write.</param>
    private static JsonObject Body(SoundEffectProject effect, SoundEffectPreset preset)
    {
        var written = new JsonObject
        {
            [NameKey] = preset.Name.Trim(),
            [EffectKey] = effect.Id
        };

        foreach (var parameter in effect.Parameters)
            if (preset.Settings.TryGetValue(parameter.Key, out double value))
                written[parameter.Key] = Inside(parameter, value);

        return written;
    }

    /// <inheritdoc/>
    public bool Write(SoundEffectProject? effect, SoundEffectPreset preset, int at)
    {
        if (Home(effect) is not { Length: > 0 } home || preset is null) return false;

        string called = (preset.Name ?? "").Trim();

        if (called.Length == 0) return false;

        try
        {
            Directory.CreateDirectory(home);

            _files.Write(Path.Combine(home, Filed(called, at)), Body(effect!, preset with { Name = called }).ToJsonString(Layout));

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public bool Remove(SoundEffectProject? effect, string? name)
    {
        if (Home(effect) is not { Length: > 0 } home || !Directory.Exists(home)) return false;
        if ((name ?? "").Trim() is not { Length: > 0 } called) return false;

        try
        {
            foreach (string path in Directory.EnumerateFiles(home, "*" + Extension))
                if (string.Equals(Called(path), called, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(path);
                    return true;
                }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }

    /// <summary>Where that effect keeps its presets, or nothing when it is not on disc.</summary>
    /// <param name="effect">The effect to ask about.</param>
    private static string Home(SoundEffectProject? effect) =>
        effect is { Folder.Length: > 0 }
            ? Path.Combine(effect.Folder, SoundEffectProject.PresetsFolder)
            : "";

    /// <summary>
    /// One preset file, or nothing when it will not read.
    /// </summary>
    /// <remarks>
    /// The name inside the file wins, and the filename stands in when the file does not say, so
    /// a preset dropped in by hand still shows up under something a person can read.
    /// </remarks>
    /// <param name="path">The file to read.</param>
    /// <param name="effect">The effect it belongs to, which says what a key means.</param>
    private static SoundEffectPreset? Read(string path, SoundEffectProject effect)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject read) return null;

            var settings = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var parameter in effect.Parameters)
                if (read[parameter.Key] is { } said && Number(said) is { } value)
                    settings[parameter.Key] = Inside(parameter, value);

            string called = read[NameKey]?.GetValue<string>() ?? "";

            if (called.Trim().Length == 0) called = Path.GetFileNameWithoutExtension(path);

            return new SoundEffectPreset(called.Trim(), settings);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The name inside a preset file, for finding the one to take away.</summary>
    /// <param name="path">The file to look in.</param>
    private static string Called(string path)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is JsonObject read
                && read[NameKey]?.GetValue<string>() is { } said
                && said.Trim().Length > 0)
                return said.Trim();
        }
        catch (Exception)
        {
            return "";
        }

        return Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>
    /// That value on the control's own grid, inside its own ends, and never NaN.
    /// </summary>
    /// <remarks>
    /// <see cref="Math.Clamp(double,double,double)"/> hands NaN back by design, which is how a
    /// patch off disc once made a whole voice silent for its life, so NaN is answered with the
    /// parameter's own starting place rather than passed on.
    ///
    /// Snapped to the parameter's step as well, because a control that moves in whole
    /// milliseconds cannot stand at 527.2144522144523 and a preset saying it does is a preset
    /// nobody can read. That number is what a slider dragged across the page really produces, so
    /// it is rounded where the value is written down rather than only where it is drawn.
    /// </remarks>
    /// <param name="parameter">The control the value is for.</param>
    /// <param name="value">What the file said, or what a hand moved it to.</param>
    private static double Inside(Rack.SoundDevices.Faces.Parameter parameter, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return parameter.Default;

        double low = Math.Min(parameter.Min, parameter.Max);
        double high = Math.Max(parameter.Min, parameter.Max);
        double held = Math.Clamp(value, low, high);

        if (parameter.Step is not > 0 || double.IsNaN(parameter.Step)) return held;

        double snapped = low + Math.Round((held - low) / parameter.Step) * parameter.Step;

        return Math.Round(Math.Clamp(snapped, low, high), Places(parameter.Step));
    }

    /// <summary>
    /// How many decimals a step of that size can land on.
    /// </summary>
    /// <remarks>
    /// Snapping is division and multiplication, and those leave a tail: nought point three five
    /// on a step of a hundredth comes back as 0.35000000000000003, which is the same number to a
    /// listener and a different one to a file, a comparison and anybody reading it. Rounded to
    /// what the step can actually express, so writing a value twice writes the same characters.
    /// </remarks>
    /// <param name="step">How far one nudge moves the control.</param>
    private static int Places(double step) =>
        step >= 1 ? 0 : Math.Clamp((int)Math.Ceiling(-Math.Log10(step)), 0, 6);

    /// <summary>A number out of whatever the file wrote, or nothing when it is not one.</summary>
    /// <param name="said">The value read.</param>
    private static double? Number(JsonNode said)
    {
        try
        {
            return said.GetValue<double>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// What that preset is called on disc: its place, then its name.
    /// </summary>
    /// <remarks>
    /// The number holds the order the folder is read in, which is the same trick a soundmachine's
    /// presets use. Anything a filesystem will not take is turned into a space, so a preset called
    /// "1/2 speed" writes rather than throwing.
    /// </remarks>
    /// <param name="called">The preset's own name.</param>
    /// <param name="at">Where in the order it goes, counting from nought.</param>
    private static string Filed(string called, int at)
    {
        var plain = called.Select(one => Path.GetInvalidFileNameChars().Contains(one) ? ' ' : one).ToArray();

        return Math.Max(at, 0).ToString("00", CultureInfo.InvariantCulture)
               + " " + new string(plain).Trim() + Extension;
    }
}
