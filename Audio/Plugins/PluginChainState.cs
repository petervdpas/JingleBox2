using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JingleBox2.Audio.Plugins;

/// <summary>One saved effect: which plugin, whether it was switched off, and its settings.</summary>
public sealed class PluginDeviceConfig
{
    /// <summary>The file it came from. Found again by id first, since a path moves.</summary>
    public string Path { get; set; } = "";

    public string Id { get; set; } = "";

    /// <summary>
    /// Which standard the plugin speaks. Absent from anything saved before VST3 was hosted,
    /// which reads back as CLAP, and that is what those chains were.
    /// </summary>
    public PluginFormat Format { get; set; } = PluginFormat.Clap;

    /// <summary>Kept so a missing plugin can be named rather than silently dropped.</summary>
    public string Name { get; set; } = "";

    public bool Bypassed { get; set; }

    /// <summary>
    /// Parameter values by id. Keyed as text because that is what JSON can hold, and because
    /// a plugin's ids are its own business.
    /// </summary>
    public Dictionary<string, double> Parameters { get; set; } = new();

    /// <summary>
    /// Everything the plugin keeps that its parameters do not describe: the preset it was
    /// on, its wavetables, whatever it has loaded inside itself.
    /// </summary>
    /// <remarks>
    /// Empty for the many effects that are nothing but their knobs, and for a chain saved
    /// before this existed, which reads back as an effect on its parameters alone. That is
    /// exactly what those chains were.
    ///
    /// Base64 through <see cref="PluginStateJson"/>, which treats text it cannot read as no
    /// state rather than throwing: one bad character in a patch should cost the patch and not
    /// the song it is written in.
    /// </remarks>
    [JsonConverter(typeof(PluginStateJson))]
    public byte[] State { get; set; } = Array.Empty<byte>();
}

/// <summary>A saved chain: the devices, in the order the audio went through them.</summary>
public sealed class PluginChainConfig
{
    public List<PluginDeviceConfig> Devices { get; set; } = new();

    public bool IsEmpty => Devices.Count == 0;

    public PluginChainConfig Clone()
    {
        var copy = new PluginChainConfig();

        foreach (var device in Devices)
        {
            copy.Devices.Add(new PluginDeviceConfig
            {
                Path = device.Path,
                Id = device.Id,
                Format = device.Format,
                Name = device.Name,
                Bypassed = device.Bypassed,
                Parameters = new Dictionary<string, double>(device.Parameters),
                State = device.State
            });
        }

        return copy;
    }

    /// <summary>
    /// The same chain with the patches taken out: what it is, rather than what is inside it.
    /// </summary>
    /// <remarks>
    /// For comparing two chains. A plugin's own lump is not stable enough to compare: Serum
    /// asked twice hands back two arrays that need not match byte for byte, and a chain that
    /// looked different every time it was asked would be torn down and rebuilt on every undo,
    /// which is seconds a plugin. What the description says is what a chain is.
    /// </remarks>
    public PluginChainConfig Described()
    {
        var copy = Clone();
        foreach (var device in copy.Devices) device.State = Array.Empty<byte>();
        return copy;
    }
}

/// <summary>
/// Turning a running chain into something a song or a profile can hold, and back again.
/// </summary>
/// <remarks>
/// Both halves, and they answer different questions. The parameter values are readable,
/// diffable and survive a plugin being updated, and for the many effects that are nothing but
/// their knobs they are the whole of it. The plugin's own lump is everything the parameters
/// do not describe, which for anything with presets inside it is most of what somebody set up:
/// Serum on a track came back sounding right and calling itself "- Init -", because its knobs
/// were saved and its patch was not.
///
/// Reading the lump is a round trip to the plugin's own process and a third of a megabyte, so
/// it is asked for where a save is a save and not where one chain is merely being compared
/// with another. See <see cref="Capture"/>.
/// </remarks>
public static class PluginChainState
{
    /// <param name="patches">
    /// Whether to ask each plugin for its own state as well as its parameters. Off by default
    /// because the cheap half answers most questions.
    /// </param>
    public static PluginChainConfig Capture(PluginChain? chain, bool patches = false)
    {
        var config = new PluginChainConfig();
        if (chain == null) return config;

        foreach (var device in chain.Devices)
        {
            if (device.Insert is not IPluginEffect effect) continue;

            var saved = new PluginDeviceConfig
            {
                Path = effect.Info.Path,
                Id = effect.Info.Id,
                Format = effect.Info.Format,
                Name = effect.Info.Name,
                Bypassed = device.Bypassed
            };

            foreach (var parameter in effect.Parameters())
            {
                saved.Parameters[parameter.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)] =
                    effect.ValueOf(parameter.Id);
            }

            // Last, because it is the expensive one and there is no point paying for it on a
            // plugin whose parameters could not be read either.
            if (patches) saved.State = effect.SaveState();

            config.Devices.Add(saved);
        }

        return config;
    }

    /// <summary>
    /// Just the plugins' own lumps, in chain order, without reading a single knob.
    /// </summary>
    /// <remarks>
    /// For somewhere that is written down far more often than its plugins change. A pad is
    /// saved on every property it has, and a level dragged across its travel is a hundred of
    /// those; the patches are read once when the chain settles and carried onto each of those
    /// saves. Skips whatever <see cref="Capture"/> skips, so the two line up by index.
    /// </remarks>
    public static IReadOnlyList<byte[]> Patches(PluginChain? chain)
    {
        if (chain == null) return Array.Empty<byte[]>();

        var lumps = new List<byte[]>();

        foreach (var device in chain.Devices)
            if (device.Insert is IPluginEffect effect) lumps.Add(effect.SaveState());

        return lumps;
    }

    /// <summary>
    /// Rebuilds a chain from what was saved. Whatever is in the chain now goes first, so this
    /// is also how a chain is replaced when another song is opened.
    /// </summary>
    /// <returns>The names of plugins that could not be loaded, for reporting.</returns>
    public static IReadOnlyList<string> Restore(
        PluginChain chain,
        PluginChainConfig? config,
        int sampleRate,
        int maxFrames)
    {
        var missing = new List<string>();
        if (chain == null) return missing;

        foreach (var device in chain.Devices)
        {
            chain.Remove(device);
            (device.Insert as IPluginEffect)?.Dispose();
        }

        if (config == null || config.IsEmpty) return missing;

        foreach (var saved in config.Devices)
        {
            // Built with the name it was saved under, so anything the host has to say about
            // this plugin later can call it what the user calls it.
            var described = new PluginInfo(saved.Id, saved.Name, "", "", saved.Path, saved.Format);
            var effect = PluginHost.Load(described, sampleRate, maxFrames);

            if (effect == null)
            {
                // A song made on another machine, or a plugin since uninstalled. The rest of
                // the chain is still worth having.
                missing.Add(string.IsNullOrWhiteSpace(saved.Name) ? saved.Id : saved.Name);
                continue;
            }

            // The lump first and the knobs after it. A patch moves every parameter at once, so
            // writing the values afterwards is either agreement or the correction for a plugin
            // whose state did not come back whole. The other order would be a preset landing on
            // top of the values and quietly winning.
            if (saved.State is { Length: > 0 }) effect.LoadState(saved.State);

            foreach (var (id, value) in saved.Parameters)
            {
                if (uint.TryParse(id, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out uint parameter))
                {
                    effect.SetValue(parameter, value);
                }
            }

            // Handed over now rather than on the next block: a chain that is not being played
            // would otherwise sit at the plugin's defaults until it was.
            effect.FlushParameters();

            var added = chain.Add(effect);
            added.Bypassed = saved.Bypassed;
        }

        return missing;
    }
}
