using JingleBox2.Audio.Plugins.Enums;

namespace JingleBox2.Audio.Plugins.Records;

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
