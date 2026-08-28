using System.Collections.Generic;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Audio.Plugins.Interfaces;

/// <summary>
/// A track's chain of effects, written down and read back.
/// </summary>
/// <remarks>
/// A preset is not a set of knob positions, and for a long time only instruments knew that. A
/// plugin on a chain was written down as its parameters and nothing else, so a synth on a track
/// came back sounding roughly right and calling itself "- Init -": the knobs were saved and the
/// patch, which is the wavetables, the effect rack and the preset's own name, was not.
///
/// So the patch is read as well, and put back first: a patch moves every parameter at once, so
/// the values after it are either agreement or the correction for a plugin whose state did not
/// come back whole.
///
/// Reading a patch is a round trip to another process and can be a third of a megabyte, so it
/// is asked for only where a save is a save. A chain compared without its patches is the same
/// chain: a plugin asked for its lump twice is under no obligation to answer the same bytes,
/// and comparing them would report every chain as changed and rebuild all of them on every undo.
/// </remarks>
public interface IPluginChainState
{
    /// <param name="patches">
    /// Whether to ask each plugin for its own state as well as its parameters. Off by default
    /// because the cheap half answers most questions.
    /// </param>
    /// <summary>
    /// Reads a running chain into something that can be written down.
    /// </summary>
    /// <remarks>
    /// The patch is read last, because it is the expensive half and there is no point paying for
    /// it on a plugin whose parameters could not be read either.
    /// </remarks>
    /// <param name="chain">The chain to read, or null for a track with nothing on it.</param>
    PluginChainConfig Capture(PluginChain? chain, bool patches = false);

    /// <summary>
    /// Just the plugins' own lumps, in chain order, without reading a single knob.
    /// </summary>
    /// <remarks>
    /// For somewhere that is written down far more often than its plugins change. A pad is
    /// saved on every property it has, and a level dragged across its travel is a hundred of
    /// those; the patches are read once when the chain settles and carried onto each of those
    /// saves. Skips whatever <see cref="Capture"/> skips, so the two line up by index.
    /// </remarks>
    IReadOnlyList<byte[]> Patches(PluginChain? chain);

    /// <summary>
    /// Rebuilds a chain from what was saved. Whatever is in the chain now goes first, so this
    /// is also how a chain is replaced when another song is opened.
    /// </summary>
    /// <remarks>
    /// Each plugin is built with the name it was saved under, so anything the host has to say
    /// about it later calls it what the user calls it rather than by its id.
    ///
    /// The lump goes in first and the knobs after it. A patch moves every parameter at once, so
    /// writing the values afterwards is either agreement or the correction for a plugin whose
    /// state did not come back whole. The other order would be a preset landing on top of the
    /// values and quietly winning.
    ///
    /// The values are handed over at once rather than on the next block, or a chain that is not
    /// being played would sit at the plugin's defaults until somebody pressed play.
    ///
    /// A plugin that will not load is a song made on another machine, or one since uninstalled.
    /// It is named and stepped over: the rest of the chain is still worth having.
    /// </remarks>
    /// <returns>The names of plugins that could not be loaded, for reporting.</returns>
    IReadOnlyList<string> Restore(
    PluginChain chain,
    PluginChainConfig? config,
    int sampleRate,
    int maxFrames);
}
