using JingleBox2.Audio.Plugins;
using JingleBox2.ViewModels;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// Somewhere a chain of effects can run: a tracker track, a pad, or anything else that owns a
/// piece of audio.
/// </summary>
/// <remarks>
/// The host owns the chain and knows what rate it runs at; it does not know what is in it, and
/// the chain does not know what it is hanging off. That is the whole point of the seam. The
/// tracker points one chain view at the track under the cursor and moves it as the cursor
/// moves, a pad points one at itself, and the master points one at strip minus one; all three
/// get the same control and the same behaviour, and none of them had to be written for.
///
/// Asked rather than reached for, because the alternative was a chain that knew about both
/// halves of the application: a switch on whether it was a track or a pad, in every method that
/// wanted a name, a chain or a rate. Three small questions on an interface are cheaper than one
/// class holding a tracker and an audio engine and choosing between them.
///
/// A host is a view onto something that already exists rather than a thing of its own, so it is
/// cheap to make and can be thrown away and made again: <see cref="TrackPluginTarget"/> holds a
/// track number and <see cref="PadPluginTarget"/> a pad number, and both go back to the player
/// or the engine for every answer.
/// </remarks>
public interface IPluginHost
{
    /// <summary>What this chain is called on screen: "TR-01", "MASTER", "Pad 03".</summary>
    /// <remarks>
    /// For a person reading a strip, so it is the same name the pattern header and the mixer
    /// use rather than an index. It is also what the log writes when a chain says it changed,
    /// which is the one place a chain on the wrong track would show up.
    /// </remarks>
    string Label { get; }

    /// <summary>The chain, made and put into the audio path the first time it is asked for.</summary>
    /// <remarks>
    /// Never null: a host that has no chain yet makes one and hangs it where its audio runs, so
    /// nothing above has to deal with "not yet". Asking for it twice gives the same chain back,
    /// which is what lets the view be pointed away from a track and back again without losing
    /// what is on it.
    /// </remarks>
    PluginChain Chain { get; }

    /// <summary>The rate the audio here runs at, which is what a plugin has to be built for.</summary>
    /// <remarks>
    /// The rate of the thing actually being processed, not the device's. A plugin works out its
    /// filters and its delay lines once, from the rate it was given, so a pad playing a 48 kHz
    /// take through an effect built for 44.1 kHz is an effect that is wrong by a fixed ratio for
    /// as long as it is loaded.
    /// </remarks>
    int SampleRate { get; }
}
