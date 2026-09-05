using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using JingleBox2.Audio.Plugins.Enums;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.Audio.Plugins;

/// <summary>One saved effect: which plugin, whether it was switched off, and its settings.</summary>
public sealed class PluginSlotConfig
{
    /// <summary>
    /// Which effect of ours this is, or empty for somebody else's plugin.
    /// </summary>
    /// <remarks>
    /// The one field that says which of the two worlds a box on the chain came out of, and it is
    /// the id off the effect's own manifest: the same id the rack registers, the same one a
    /// template names, and the only name that is the same on everybody's disc.
    ///
    /// Absent from every chain saved before effects existed, which reads back as a plugin, and
    /// that is what those were.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Effect { get; set; } = "";

    /// <summary>The file it came from. Found again by id first, since a path moves.</summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// The plugin's own identity, its CLAP id or its VST3 class id. Tried before the path,
    /// because a bundle moves between machines and an id does not.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Which standard the plugin speaks. Absent from anything saved before VST3 was hosted,
    /// which reads back as CLAP, and that is what those chains were.
    /// </summary>
    public PluginFormat Format { get; set; } = PluginFormat.Clap;

    /// <summary>Kept so a missing plugin can be named rather than silently dropped.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Whether the effect was switched out of circuit. Kept with the chain rather than in the
    /// parameters, since it is a fact about the slot rather than about the plugin.
    /// </summary>
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
    /// <summary>The devices, first to last, which is the order the audio goes through them.</summary>
    public List<PluginSlotConfig> Devices { get; set; } = new();

    /// <summary>True for a track with nothing on it, which is most tracks.</summary>
    public bool IsEmpty => Devices.Count == 0;

    /// <summary>
    /// A copy nothing is shared with, except the patches, which are treated as immutable: a lump
    /// read off a plugin is never written into afterwards, only replaced.
    /// </summary>
    public PluginChainConfig Clone()
    {
        var copy = new PluginChainConfig();

        foreach (var device in Devices)
        {
            copy.Devices.Add(new PluginSlotConfig
            {
                Effect = device.Effect,
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

/// <inheritdoc/>
public sealed class PluginChainState : IPluginChainState
{
    /// <summary>The one place that knows both plugin standards. Holds nothing, so one is enough.</summary>
    private readonly IPluginHost _plugins = new PluginHost();

    /// <summary>Which effects of ours this build can make, for the boxes that are not plugins.</summary>
    private readonly SoundDevices.SoundEffects.Interfaces.ISoundEffectEngines _engines;

    /// <summary>
    /// Takes the engine list, or the ordinary one, which knows only the effects that shipped.
    /// </summary>
    /// <remarks>
    /// A chain writes down an effect's id and never its engine, so putting one back means looking
    /// the id up in what this installation has registered. Handed one built over that list, an
    /// effect somebody made in the designer comes back off a chain like any other; handed none,
    /// only the three whose ids the application still recognises do. Every caller that has the
    /// list gives it, and the default is there for a test that has no registry at all.
    /// </remarks>
    /// <param name="engines">Which engines can be made, and how an id is resolved to one.</param>
    public PluginChainState(SoundDevices.SoundEffects.Interfaces.ISoundEffectEngines? engines = null) =>
        _engines = engines ?? new SoundDevices.SoundEffects.SoundEffectEngines();

    /// <inheritdoc/>
    public PluginChainConfig Capture(PluginChain? chain, bool patches = false)
    {
        var config = new PluginChainConfig();
        if (chain == null) return config;

        foreach (var device in chain.Slots)
        {
            if (device.Insert is SoundDevices.SoundEffects.Interfaces.ISoundEffectEngine ours)
            {
                config.Devices.Add(Written(ours, device));

                continue;
            }

            if (device.Insert is not IPluginEffect effect) continue;

            var saved = new PluginSlotConfig
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

            if (patches) saved.State = effect.SaveState();

            config.Devices.Add(saved);
        }

        return config;
    }

    /// <summary>
    /// One of ours written down: which effect it is, and what its knobs were at.
    /// </summary>
    /// <remarks>
    /// No path and no state lump. An effect of ours is not a file somewhere on this computer, it
    /// is a box this installation has registered, and everything it holds is in its parameters.
    /// </remarks>
    /// <param name="engine">The effect that is running.</param>
    /// <param name="device">Its place in the chain, which is what carries the bypass.</param>
    private static PluginSlotConfig Written(
        SoundDevices.SoundEffects.Interfaces.ISoundEffectEngine engine,
        PluginChain.Slot device)
    {
        var saved = new PluginSlotConfig
        {
            Effect = engine.Id,
            Name = engine.Id,
            Bypassed = device.Bypassed
        };

        foreach (string key in engine.Keys) saved.Parameters[key] = engine.ValueOf(key);

        return saved;
    }

    /// <inheritdoc/>
    public IReadOnlyList<byte[]> Patches(PluginChain? chain)
    {
        if (chain == null) return Array.Empty<byte[]>();

        var lumps = new List<byte[]>();

        foreach (var device in chain.Slots)
            if (device.Insert is IPluginEffect effect) lumps.Add(effect.SaveState());

        return lumps;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Restore(
        PluginChain chain,
        PluginChainConfig? config,
        int sampleRate,
        int maxFrames)
    {
        var missing = new List<string>();
        if (chain == null) return missing;

        foreach (var device in chain.Slots)
        {
            chain.Remove(device);
            (device.Insert as IPluginEffect)?.Dispose();
        }

        if (config == null || config.IsEmpty) return missing;

        foreach (var saved in config.Devices)
        {
            if (saved.Effect is { Length: > 0 })
            {
                if (_engines.Make(saved.Effect, sampleRate, maxFrames) is not { } engine)
                {
                    missing.Add(saved.Effect);

                    continue;
                }

                foreach (var (key, value) in saved.Parameters) engine.SetValue(key, value);

                chain.Add(engine).Bypassed = saved.Bypassed;

                continue;
            }

            var described = new PluginInfo(saved.Id, saved.Name, "", "", saved.Path, saved.Format);
            var effect = _plugins.Load(described, sampleRate, maxFrames);

            if (effect == null)
            {
                missing.Add(string.IsNullOrWhiteSpace(saved.Name) ? saved.Id : saved.Name);
                continue;
            }

            if (saved.State is { Length: > 0 }) effect.LoadState(saved.State);

            foreach (var (id, value) in saved.Parameters)
            {
                if (uint.TryParse(id, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out uint parameter))
                {
                    effect.SetValue(parameter, value);
                }
            }

            effect.FlushParameters();

            var added = chain.Add(effect);
            added.Bypassed = saved.Bypassed;
        }

        return missing;
    }
}
