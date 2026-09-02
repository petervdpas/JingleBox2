using JingleBox2.Rack.Faces;
using JingleBox2.Tracker.Synth;
using JingleBox2.ViewModels;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Machines.Interfaces;

namespace JingleBox2.Tracker.Machines;

/// <inheritdoc/>
public sealed class MachineValuesFor : IMachineValuesFor
{
    /// <inheritdoc/>
    public PanelValues? Instrument(
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

                return new MonoSynthValues(mono ?? new MonoSynthPatchViewModel(instrument.MonoSynth, Nothing), instrument);

            case TrackerInstrumentKind.Kit:
                instrument.Kit ??= DrumKit.Empty(1);

                return new KitValues(kit ?? new DrumKitViewModel(instrument.Kit, Nothing, _ => { }));

            case TrackerInstrumentKind.Sampler:
                instrument.Zones ??= ZoneMap.Empty();
                instrument.Sampler ??= new SamplerPatch();

                return new SamplerValues(
                    zones ?? new ZoneMapViewModel(instrument.Zones, Nothing, _ => { }),
                    sampler ?? new SamplerPatchViewModel(instrument.Sampler, Nothing),
                    instrument);

            case TrackerInstrumentKind.Sample:
                return new RecordingValues(instrument, shelf);

            default:
                return null;
        }
    }

    /// <summary>For a throwaway view model, which has nobody to tell.</summary>
    private static void Nothing()
    {
    }
}
