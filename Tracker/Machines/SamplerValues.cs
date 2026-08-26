using JingleBox2.Machines;
using JingleBox2.Tracker.Synth;
using JingleBox2.UI;
using JingleBox2.ViewModels;
using System;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// Zampler's panel, wired to a real map and a real patch.
/// </summary>
/// <remarks>
/// Two halves, and the machine is the pair of them. Half the keys are about the zone in hand, so
/// the same knob is about a different recording once another zone is picked, exactly as a kit's
/// are (<see cref="KitValues"/>). The other half are about the instrument: there is one filter
/// and there are two envelopes, and every zone in the map goes through them.
///
/// Which half a key is in is not worked out here. The machine says it, on the element that draws
/// the map, and this only knows what each name means. That matters because a preset has to be
/// written in the same two halves: the filter once, and every zone in its own block.
///
/// The zone's window is among the settings even though no knob turns it. It is set by dragging a
/// boundary on the picture, and it has to travel with the preset: both presets this machine
/// ships are one recording cut into pieces, and a preset that forgot where the cuts were would
/// be eleven zones all playing the whole file.
///
/// A key it does not know reads as zero and swallows the write, for the reason the others do: a
/// machine.json written by a later version has to open on an older app rather than take it down.
/// </remarks>
/// <param name="map">The zones these settings are on.</param>
/// <param name="patch">The filter and the two envelopes they all go through.</param>
/// <param name="about">
/// Which zone, or nothing for whichever is in hand.
/// </param>
/// <remarks>
/// The panel wants the zone in hand, because that is what a front panel shows; a preset wants a
/// named one, because a preset holds the whole map. Both are the same mapping from a key to a
/// thing on a zone, so they are the same class asked about a different zone.
/// </remarks>
public sealed class SamplerValues(
    ZoneMapViewModel map,
    SamplerPatchViewModel patch,
    Func<SampleZoneViewModel?>? about = null) : MachineValues
{
    // Written out one by one, never built from a name or a loop, so every key in the app can be
    // found by searching for the string that is in the file.

    // ---- the zone in hand -------------------------------------------------

    private const string LevelKey = "zone_level";
    private const string PanKey = "zone_pan";
    private const string FineKey = "zone_fine";

    /// <summary>Which keys it answers to, and the one its recording was made at.</summary>
    private const string LowKey = "zone_low";
    private const string HighKey = "zone_high";
    private const string RootKey = "zone_root";

    /// <summary>Which part of the recording plays, and how it repeats.</summary>
    private const string StartKey = "zone_start";
    private const string EndKey = "zone_end";
    private const string LoopKey = "zone_loop";
    private const string LoopStartKey = "zone_loop_start";
    private const string LoopEndKey = "zone_loop_end";

    /// <summary>The recording on the zone in hand, which the Take control puts there.</summary>
    private const string TakeKey = "zone_take";

    /// <summary>What that zone is called, which is yours to type.</summary>
    private const string NameKey = "zone_name";

    /// <summary>And the three things the panel reads out rather than setting.</summary>
    private const string FileKey = "zone_file";
    private const string KeysKey = "zone_keys";
    private const string PitchKey = "zone_pitch";

    // ---- the instrument ---------------------------------------------------

    /// <summary>
    /// Whether the zones are pieces of one recording rather than separate recordings.
    /// </summary>
    /// <remarks>
    /// About the map and not about any one zone, which is why it stands out here with the filter
    /// rather than in a zone's block. It has to be said: a map of one zone holding one file looks
    /// exactly like a map cut into one piece, so nothing can work it out, and a preset that did
    /// not carry it came back as a set of separate recordings with the cuts gone.
    /// </remarks>
    private const string ChoppedKey = "chopped";

    private const string CutoffKey = "cutoff";
    private const string CutoffTextKey = "cutoff_text";
    private const string ResonanceKey = "resonance";
    private const string EnvelopeAmountKey = "env_amount";
    private const string EnvelopePolarityKey = "env_polarity";
    private const string KeyFollowKey = "key_follow";

    private const string FilterAttackKey = "filter_attack";
    private const string FilterDecayKey = "filter_decay";
    private const string FilterSustainKey = "filter_sustain";
    private const string FilterReleaseKey = "filter_release";

    private const string AttackKey = "attack";
    private const string DecayKey = "decay";
    private const string SustainKey = "sustain";
    private const string ReleaseKey = "release";
    private const string LevelOutKey = "level";

    /// <summary>Told when something moved, for saving the song and redrawing what else shows it.</summary>

    /// <summary>The zone every zone key is about, or nothing before one is picked.</summary>
    private SampleZoneViewModel? Zone => about != null ? about() : map.Selected;

    /// <summary>And the window on it, made if the zone has never had one.</summary>
    private SampleShape? Shape
    {
        get
        {
            if (Zone is not { } zone) return null;

            return zone.Zone.Shape ??= new SampleShape();
        }
    }

    public override double Get(string key) => key switch
    {
        LevelKey => Zone?.Volume ?? 1,
        PanKey => Zone?.Pan ?? 0,
        FineKey => Zone?.FineCents ?? 0,

        LowKey => Zone?.Zone.Low ?? 0,
        HighKey => Zone?.Zone.High ?? 119,
        RootKey => Zone?.Zone.Root ?? 48,

        StartKey => Shape?.Start ?? 0,
        EndKey => Shape?.End ?? 1,
        LoopKey => (double)(Shape?.LoopMode ?? SampleLoopMode.None),
        LoopStartKey => Shape?.LoopStart ?? 0,
        LoopEndKey => Shape?.LoopEnd ?? 1,

        ChoppedKey => map.Map.Sliced ? 1 : 0,

        CutoffKey => patch.Cutoff,
        ResonanceKey => patch.Resonance,
        EnvelopeAmountKey => patch.EnvelopeAmount,
        EnvelopePolarityKey => patch.EnvelopeInverted ? 1 : 0,
        KeyFollowKey => patch.KeyFollow,

        FilterAttackKey => patch.FilterAttackMs,
        FilterDecayKey => patch.FilterDecayMs,
        FilterSustainKey => patch.FilterSustain,
        FilterReleaseKey => patch.FilterReleaseMs,

        AttackKey => patch.AttackMs,
        DecayKey => patch.DecayMs,
        SustainKey => patch.Sustain,
        ReleaseKey => patch.ReleaseMs,
        LevelOutKey => patch.Volume,

        _ => 0,
    };

    protected override bool Write(string key, double value)
    {
        // The instrument's own half first, because it is there whether or not a zone is picked:
        // a map with nothing selected still has a filter.
        if (Patch(key, value) is { } mine) return mine;

        if (Zone is not { } zone) return false;

        return key switch
        {
            LevelKey => Moved(zone.Volume, value, () => zone.Volume = value),
            PanKey => Moved(zone.Pan, value, () => zone.Pan = value),
            FineKey => Moved(zone.FineCents, value, () => zone.FineCents = value),

            // The edges the way the map moves them: whichever is travelling outwards goes first,
            // or the two cross on the way and are turned round behind our back.
            LowKey => Moved(zone.Low, value, () => zone.Low = value),
            HighKey => Moved(zone.High, value, () => zone.High = value),
            RootKey => Moved(zone.Root, value, () => zone.Root = value),

            StartKey => Window(shape => shape.Start = value),
            EndKey => Window(shape => shape.End = value),
            LoopKey => Window(shape => shape.LoopMode = (SampleLoopMode)Math.Clamp(Math.Round(value), 0, 2)),
            LoopStartKey => Window(shape => shape.LoopStart = value),
            LoopEndKey => Window(shape => shape.LoopEnd = value),

            _ => false,
        };
    }

    /// <summary>
    /// The half of the machine that belongs to the instrument rather than to a zone.
    /// </summary>
    /// <returns>Whether it moved, or nothing at all when the key is not one of these.</returns>
    /// <remarks>
    /// Every one of these says so when it moves, and that is the whole of why this exists. They
    /// were written straight into the patch and returned, which is right for the sound and
    /// wrong for everything watching: nothing was told, so nothing redrew. Turned by hand it
    /// never showed, because the knob you are dragging draws itself from your hand and not from
    /// the setting. Turned from a controller it showed at once: the sound followed and the panel
    /// went on displaying where the knob used to be.
    /// </remarks>
    private bool? Patch(string key, double value)
    {
        bool moved;

        switch (key)
        {
            case ChoppedKey: moved = Moved(map.Map.Sliced, value, on => map.Map.Sliced = on); break;

            case CutoffKey: moved = Moved(patch.Cutoff, value, () => patch.Cutoff = value); break;
            case ResonanceKey: moved = Moved(patch.Resonance, value, () => patch.Resonance = value); break;
            case EnvelopeAmountKey: moved = Moved(patch.EnvelopeAmount, value, () => patch.EnvelopeAmount = value); break;

            case EnvelopePolarityKey: moved = Moved(patch.EnvelopeInverted, value, on => patch.EnvelopeInverted = on); break;

            case KeyFollowKey: moved = Moved(patch.KeyFollow, value, () => patch.KeyFollow = value); break;

            case FilterAttackKey: moved = Moved(patch.FilterAttackMs, value, () => patch.FilterAttackMs = value); break;
            case FilterDecayKey: moved = Moved(patch.FilterDecayMs, value, () => patch.FilterDecayMs = value); break;
            case FilterSustainKey: moved = Moved(patch.FilterSustain, value, () => patch.FilterSustain = value); break;
            case FilterReleaseKey: moved = Moved(patch.FilterReleaseMs, value, () => patch.FilterReleaseMs = value); break;

            case AttackKey: moved = Moved(patch.AttackMs, value, () => patch.AttackMs = value); break;
            case DecayKey: moved = Moved(patch.DecayMs, value, () => patch.DecayMs = value); break;
            case SustainKey: moved = Moved(patch.Sustain, value, () => patch.Sustain = value); break;
            case ReleaseKey: moved = Moved(patch.ReleaseMs, value, () => patch.ReleaseMs = value); break;
            case LevelOutKey: moved = Moved(patch.Volume, value, () => patch.Volume = value); break;

            default: return null;
        }

        return moved;
    }

    public override string GetText(string key) => key switch
    {
        TakeKey => Zone?.Zone.FilePath ?? "",
        NameKey => Zone?.Name ?? "",
        FileKey => Zone?.FileText ?? "",
        KeysKey => Zone?.RangeText ?? "",
        PitchKey => Zone?.RootText ?? "",
        CutoffTextKey => patch.CutoffText,
        _ => "",
    };

    protected override bool WriteText(string key, string value)
    {
        if (Zone is not { } zone) return false;

        switch (key)
        {
            case TakeKey:
                if (FilePaths.Same(zone.Zone.FilePath, value)) return false;

                // Through the zone's own way of taking one, which names it after the file unless
                // the zone has a name somebody chose. A zone still called after the recording it
                // used to hold says the old recording is still on it.
                zone.Take(value);

                return true;

            case NameKey:
                if (zone.Name == value) return false;

                zone.Name = value;

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Writes one end of the window and says whether it moved.
    /// </summary>
    /// <remarks>
    /// Clamped by the shape itself, which straightens a window that has been turned inside out,
    /// and then said again so the picture and the map follow. The zone view model holds no
    /// property for any of this: the window is dragged on the picture rather than typed, and the
    /// picture reads the shape.
    /// </remarks>
    private bool Window(Action<SampleShape> write)
    {
        if (Shape is not { } shape) return false;

        write(shape);
        shape.Clamp();

        Zone?.Reread();

        return true;
    }

}
