using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;

namespace JingleBox2.SoundDevices.SoundMachines.Interfaces;

/// <summary>
/// Which values adapter reads an instrument's settings, in one place.
/// </summary>
/// <remarks>
/// There are five of them and which one an instrument wants is a fact about its machine: a synth
/// generates its wave one way, a mono synth another, a kit and a map hold pieces, and a sample
/// plays a recording back. That fact was written out twice, in the editor that opens a panel over
/// it and in the reader that loads a preset into it, and both wrote it while doing something
/// else. A third copy for the block at the head of a track's chain is how three places come to
/// disagree about one instrument.
///
/// The pieces are optional because the two callers differ in exactly one way. An editor already
/// owns the view model the panel edits and has to pass that one, or the panel and the values
/// would be looking at two copies of the same patch. Anything only reading has none and wants a
/// throwaway. The dispatch is the same for both, and it is the dispatch that was duplicated.
/// </remarks>
public interface ISoundMachineValuesFor
{
    /// <summary>
    /// The adapter for this instrument, or nothing for a machine that has none.
    /// </summary>
    /// <remarks>
    /// Nothing comes back for a plugin, whose settings are its own and are read through the
    /// plugin rather than through any of this. That is also the one machine whose panel this
    /// program draws itself, since a plugin is not edited in the designer and has no description
    /// to be drawn from.
    ///
    /// The instrument's own missing parts are filled in on the way past, since a machine that
    /// has never been opened has no kit, no zones and no patch, and every reader below would
    /// otherwise have to hold against nothing.
    /// </remarks>
    /// <param name="instrument">The instrument being read, or nothing.</param>
    /// <param name="shelf">Where a recording's takes are found. Only a sample wants it.</param>
    /// <param name="kit">The kit the editor is already on, or nothing for a throwaway.</param>
    /// <param name="patch">The synth patch the editor is already on, or nothing.</param>
    /// <param name="mono">The mono synth patch the editor is already on, or nothing.</param>
    /// <param name="zones">The key map the editor is already on, or nothing.</param>
    /// <param name="sampler">The sampler's own settings, as the editor holds them, or nothing.</param>
    PanelValues? Instrument(
        TrackerInstrument? instrument,
        TakeLibrary? shelf = null,
        DrumKitViewModel? kit = null,
        SynthPatchViewModel? patch = null,
        MonoSynthPatchViewModel? mono = null,
        ZoneMapViewModel? zones = null,
        SamplerPatchViewModel? sampler = null);
}
