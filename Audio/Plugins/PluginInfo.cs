namespace JingleBox2.Audio.Plugins;

/// <summary>Which plugin standard something speaks.</summary>
/// <remarks>
/// CLAP is first because it was here first, and because a saved chain from before VST3 existed
/// has no format written in it and has to read back as the one it was.
/// </remarks>
public enum PluginFormat
{
    /// <summary>CLAP, and the number a chain written before VST3 existed reads back as.</summary>
    Clap = 0,

    /// <summary>VST3, which is also the only format that can be an instrument here.</summary>
    Vst3 = 1
}

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

/// <summary>One plugin parameter, as the host sees it.</summary>
/// <remarks>
/// The two standards describe a parameter differently and this is the shape both fit into.
/// CLAP gives a range in the plugin's own units, so a threshold really does run from -60 to 0.
/// VST3 gives everything as nought to one and keeps the real units to itself, which is what
/// <see cref="Normalized"/> is for.
/// </remarks>
/// <param name="Id">
/// The number the plugin knows this parameter by, and the only thing a saved chain can name it
/// with. Not an index: both standards allow the ids to be scattered, and a plugin is entitled to
/// list its parameters in a different order next version.
/// </param>
/// <param name="Name">What the plugin calls it, for the label under the knob.</param>
/// <param name="Minimum">The bottom of the range, in whatever units the parameter is in.</param>
/// <param name="Maximum">The top of the range, in the same units.</param>
/// <param name="Default">Where the plugin says the parameter sits before anybody touches it.</param>
/// <param name="Steps">
/// How many gaps there are between the positions, nought for a continuous sweep. One means two
/// positions, which is a switch; that off-by-one is the standards' own counting and not a slip.
/// </param>
/// <param name="IsHidden">
/// The plugin asking that this one is not drawn. Usually an internal value the plugin automates
/// itself; still saved and restored, since hiding it does not stop it mattering.
/// </param>
/// <param name="IsReadOnly">
/// A reading rather than a control, such as a compressor's gain reduction. Excluded from the
/// parameters that are polled back off a plugin with its window open, or a song could never
/// settle and so could never be saved.
/// </param>
/// <param name="IsBypass">
/// The parameter the standard reserves for switching the plugin out of circuit.
/// </param>
/// <param name="Normalized">
/// True when the range really is nought to one and the plugin keeps the real units to itself,
/// which is every VST3 parameter. Whoever draws it has to ask the plugin to word the value,
/// since the number says nothing on its own.
/// </param>
/// <param name="Units">The plugin's own name for the units, empty when it does not say.</param>
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
