using System.Collections.Generic;
using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.Audio.Plugins.Interfaces;

/// <summary>
/// Which plugin on this machine is the one a song is asking for.
/// </summary>
/// <remarks>
/// **A song writes down where a plugin was, and where it was is the one thing about it that does
/// not travel.** Serum lives at <c>/home/somebody/.vst3/Serum2.vst3</c> here and under Program
/// Files on Windows, and a bundle moves between two folders on one machine as often as between
/// two machines. What does not move is the plugin's own identity, which is a VST3 class id or a
/// CLAP id, and a song has always written that down beside the path.
///
/// **It was written down and never read.** Both places a song names a plugin, the instrument and
/// the slot on a chain, built what the host loads straight out of the stored path, and the field
/// documentation on the chain's own id said in as many words that it was tried first. It was not.
/// So a song carried to another machine found its plugins installed, scanned and listed, and
/// asked the host for a path that machine has never had.
///
/// **The order is the id, then the name, and the path last and only where it is not ambiguous.**
/// That the path comes last is not merely about travelling: one bundle can hold several plugins,
/// and this application's own test song is the proof. Serum 2 and Serum 2 FX have different class
/// ids and **the same path**, so a song matched by path alone could be handed the synthesiser
/// where it asked for the effect, on the very machine it was saved on. A path shared by more than
/// one of them therefore decides nothing and the answer falls through.
///
/// The name is second because it travels as well as an id does and tells the two halves of a
/// bundle apart, and it is not first because a plugin renames itself between versions and two
/// plugins may share a name. Both are compared within one format, since the same plugin shipped
/// as a CLAP and as a VST3 is two plugins here with two sets of parameter numbers, and answering
/// one for the other would put a song's knob positions on the wrong control.
///
/// What was asked for is handed back unchanged when nothing matches, and that is not a failure: a
/// plugin this machine has not got has to keep its name so it can be reported as missing rather
/// than disappearing out of the chain.
/// </remarks>
public interface IPluginsHere
{
    /// <summary>
    /// The plugin this installation has that the song means.
    /// </summary>
    /// <remarks>
    /// The format is part of the identity, since the same plugin shipped as both a CLAP and a
    /// VST3 is two plugins here with two sets of parameter numbers, and answering one for the
    /// other would put a song's knob positions on the wrong control.
    /// </remarks>
    /// <param name="asked">What the song wrote down.</param>
    /// <param name="known">What this installation found when it last scanned.</param>
    /// <param name="byPath">
    /// Whether the paths are worth comparing at all, which they are not when the song came from
    /// a different kind of machine: there the answer can only be no, and on the day somebody
    /// carries a settings file between two computers it can be yes and wrong.
    /// </param>
    /// <returns>The one to load, which is <paramref name="asked"/> when nothing here matches.</returns>
    PluginInfo Same(PluginInfo asked, IReadOnlyList<PluginInfo>? known, bool byPath = true);
}
