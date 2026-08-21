using System.Collections.Generic;

namespace JingleBox2.Audio.Plugins;

/// <summary>Which plugin standard something speaks.</summary>
/// <remarks>
/// CLAP is first because it was here first, and because a saved chain from before VST3 existed
/// has no format written in it and has to read back as the one it was.
/// </remarks>
public enum PluginFormat
{
    Clap = 0,
    Vst3 = 1
}

/// <summary>
/// One plugin as it appears in a picker: what it is called, who made it, and enough to find
/// it again. The id is what a saved song stores, since a path moves between machines.
/// </summary>
public sealed record PluginInfo(
    string Id,
    string Name,
    string Vendor,
    string Version,
    string Path,
    PluginFormat Format = PluginFormat.Clap,
    bool IsInstrument = false)
{
    public override string ToString() => string.IsNullOrWhiteSpace(Vendor) ? Name : Name + " (" + Vendor + ")";

    /// <summary>
    /// The format spelled out, for a list where the same plugin appears twice. Most vendors
    /// ship both, so "ZamComp" on its own says nothing about which one is about to be loaded.
    /// </summary>
    public string FormatName => Format == PluginFormat.Vst3 ? "VST3" : "CLAP";

    /// <summary>
    /// True when this can go in an effect chain: it takes audio in and gives audio back. An
    /// instrument takes notes instead, and putting one in a chain would replace whatever the
    /// track was playing with silence.
    /// </summary>
    public bool CanInsert => !IsInstrument;
}

/// <summary>One plugin parameter, as the host sees it.</summary>
/// <remarks>
/// The two standards describe a parameter differently and this is the shape both fit into.
/// CLAP gives a range in the plugin's own units, so a threshold really does run from -60 to 0.
/// VST3 gives everything as nought to one and keeps the real units to itself, which is what
/// <see cref="Normalized"/> is for.
/// </remarks>
public sealed record PluginParameter(
    uint Id,
    string Name,
    double Minimum,
    double Maximum,
    double Default,
    int Steps,
    bool IsHidden,
    bool IsReadOnly,
    bool IsBypass,
    bool Normalized,
    string Units = "")
{
    /// <summary>Whole positions rather than a sweep: a mode, a count, a switch.</summary>
    public bool IsStepped => Steps > 0;

    /// <summary>One step is two positions, which is an on and an off rather than a dial.</summary>
    public bool IsSwitch => Steps == 1;
}

/// <summary>
/// A loaded plugin with audio running through it, whatever standard it speaks.
/// </summary>
/// <remarks>
/// Process runs on the audio thread; everything else is called from the UI. A parameter move
/// is queued rather than written, because both standards expect values to arrive at the start
/// of a block rather than whenever a knob is dragged.
/// </remarks>
public interface IPluginEffect : IAudioInsert, System.IDisposable
{
    PluginInfo Info { get; }

    /// <summary>True once the plugin has been switched on and can be given audio.</summary>
    bool IsActive { get; }

    /// <summary>Everything this plugin exposes, in the order it lists them.</summary>
    IReadOnlyList<PluginParameter> Parameters();

    /// <summary>What a parameter is set to right now.</summary>
    double ValueOf(uint id);

    /// <summary>How the plugin words a value: "-6.0 dB" rather than -6.</summary>
    string TextFor(uint id, double value);

    /// <summary>Moves a parameter.</summary>
    void SetValue(uint id, double value);

    /// <summary>
    /// Hands over anything queued now rather than on the next block, for a plugin nothing is
    /// being played through.
    /// </summary>
    void FlushParameters();
}
