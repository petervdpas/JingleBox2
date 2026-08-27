using JingleBox2.Machines;
using JingleBox2.Tracker.Synth;
using JingleBox2.ViewModels;

namespace JingleBox2.Tracker.Machines;

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
public static class MachineValuesFor
{
    /// <summary>
    /// The adapter for this instrument, or nothing for a machine that has none.
    /// </summary>
    /// <param name="shelf">Where a recording's takes are found. Only a sample wants it.</param>
    public static MachineValues? Instrument(
        TrackerInstrument? instrument,
        TakeLibrary? shelf = null,
        DrumKitViewModel? kit = null,
        SynthPatchViewModel? patch = null,
        MonoSynthPatchViewModel? mono = null,
        ZoneMapViewModel? zones = null,
        SamplerPatchViewModel? sampler = null)
    {
        if (instrument is null) return null;

        switch (instrument.Kind)
        {
            case TrackerInstrumentKind.Synth:
                return new SynthValues(patch ?? new SynthPatchViewModel(instrument.Patch, Nothing), instrument);

            case TrackerInstrumentKind.MonoSynth:
                instrument.MonoSynth ??= new MonoSynthPatch();

                return new MonoSynthValues(mono ?? new MonoSynthPatchViewModel(instrument.MonoSynth, Nothing));

            case TrackerInstrumentKind.Kit:
                instrument.Kit ??= DrumKit.Empty(1);

                return new KitValues(kit ?? new DrumKitViewModel(instrument.Kit, Nothing, _ => { }));

            case TrackerInstrumentKind.Sampler:
                instrument.Zones ??= ZoneMap.Empty();
                instrument.Sampler ??= new SamplerPatch();

                return new SamplerValues(
                    zones ?? new ZoneMapViewModel(instrument.Zones, Nothing, _ => { }),
                    sampler ?? new SamplerPatchViewModel(instrument.Sampler, Nothing));

            case TrackerInstrumentKind.Sample:
                return new RecordingValues(instrument, shelf);

            default:
                // A plugin, whose settings are its own and are read through the plugin rather
                // than through any of this.
                return null;
        }
    }

    /// <summary>For a throwaway view model, which has nobody to tell.</summary>
    private static void Nothing()
    {
    }
}
