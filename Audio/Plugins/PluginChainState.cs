using System;
using System.Collections.Generic;

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
                Parameters = new Dictionary<string, double>(device.Parameters)
            });
        }

        return copy;
    }
}

/// <summary>
/// Turning a running chain into something a song or a profile can hold, and back again.
/// </summary>
/// <remarks>
/// Parameter values, not plugin state. Most effects are nothing but their parameters, and
/// these are readable, diffable and survive a plugin being updated. A plugin that keeps
/// something else inside it, a sampler with a file loaded or a sequencer with a pattern,
/// would need the state extension either standard offers, which this does not do yet.
/// </remarks>
public static class PluginChainState
{
    public static PluginChainConfig Capture(PluginChain? chain)
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

            config.Devices.Add(saved);
        }

        return config;
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
