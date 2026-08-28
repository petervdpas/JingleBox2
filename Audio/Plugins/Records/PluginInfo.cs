using JingleBox2.Audio.Plugins.Enums;

namespace JingleBox2.Audio.Plugins.Records;

/// <summary>
/// One plugin as it appears in a picker: what it is called, who made it, and enough to find
/// it again. The id is what a saved song stores, since a path moves between machines.
/// </summary>
/// <param name="Id">
/// The plugin's own identity, its CLAP id or its VST3 class id. What a song writes down, because
/// a path is about this machine and an id is about the plugin.
/// </param>
/// <param name="Name">What the plugin calls itself, which is what a person reads in a list.</param>
/// <param name="Vendor">Who made it, empty when the plugin does not say.</param>
/// <param name="Version">The plugin's own version string, as it worded it.</param>
/// <param name="Path">
/// Where the bundle is on this machine. Useful for loading and useless for saving, since the
/// same plugin lives somewhere else on somebody else's computer.
/// </param>
/// <param name="Format">Which of the two standards this one speaks.</param>
/// <param name="IsInstrument">
/// True when the plugin takes notes rather than audio. A scan works this out from the categories
/// the plugin lists about itself.
/// </param>
public sealed record PluginInfo(
    string Id,
    string Name,
    string Vendor,
    string Version,
    string Path,
    PluginFormat Format = PluginFormat.Clap,
    bool IsInstrument = false)
{
    /// <summary>The name with the vendor after it, which is how a picker row reads.</summary>
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
