using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.ViewModels;
using System;
using JingleBox2.Tracker.Enums;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.Tracker;

namespace JingleBox2.SoundDevices.SoundMachines;

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
/// <param name="instrument">Whose new note action it is, which belongs to none of the zones.</param>
/// <param name="about">
/// Which zone, or nothing for whichever is in hand. The panel wants the zone in hand, because
/// that is what a front panel shows; a preset wants a named one, because a preset holds the
/// whole map. Both are the same mapping from a key to a thing on a zone, so they are the same
/// class asked about a different zone.
/// </param>
public sealed class SamplerValues(
    ZoneMapViewModel map,
    SamplerPatchViewModel patch,
    TrackerInstrument instrument,
    Func<SampleZoneViewModel?>? about = null) : PanelValues
{
    /// <summary>What a new note does to the one the track is still sounding.</summary>
    /// <remarks>
    /// The instrument's rather than this machine's, and not a zone's: a map is one instrument
    /// however many recordings are on it, and a track plays one of them at a time.
    /// </remarks>
    private const string NewNoteKey = "new_note";
    /// <summary>Whether two paths are one file, by this machine's rules.</summary>
    private readonly IFilePaths _paths = new FilePaths();

    /// <summary>How loud the zone in hand plays.</summary>
    /// <remarks>
    /// The keys are written out one by one, never built from a name or a loop, so every key in
    /// the application can be found by searching for the string that is in the machine's own
    /// file. A key assembled at the call site never appears in the source at all, and both the
    /// tools that hunt for an orphaned key and anybody grepping would miss it.
    ///
    /// Everything named "zone_" is about the zone in hand and changes meaning when another zone
    /// is picked. Everything after them is about the instrument and is true of every zone.
    /// </remarks>
    private const string LevelKey = "zone_level";

    /// <summary>Where it sits across the stereo picture.</summary>
    private const string PanKey = "zone_pan";

    /// <summary>How far it is tuned off its root, in cents.</summary>
    private const string FineKey = "zone_fine";

    /// <summary>The lowest key it answers to.</summary>
    private const string LowKey = "zone_low";

    /// <summary>And the highest.</summary>
    private const string HighKey = "zone_high";

    /// <summary>The key its recording was made at, which everything else is pitched against.</summary>
    private const string RootKey = "zone_root";

    /// <summary>Where in the recording it starts.</summary>
    private const string StartKey = "zone_start";

    /// <summary>And where it ends.</summary>
    private const string EndKey = "zone_end";

    /// <summary>Whether it repeats, and which way round.</summary>
    private const string LoopKey = "zone_loop";

    /// <summary>Where the repeat goes back to.</summary>
    private const string LoopStartKey = "zone_loop_start";

    /// <summary>And where it turns round.</summary>
    private const string LoopEndKey = "zone_loop_end";

    /// <summary>The recording on the zone in hand, which the Take control puts there.</summary>
    private const string TakeKey = "zone_take";

    /// <summary>What that zone is called, which is yours to type.</summary>
    private const string NameKey = "zone_name";

    /// <summary>The file it plays, said in one line, which the panel reads out rather than sets.</summary>
    private const string FileKey = "zone_file";

    /// <summary>The stretch of keyboard it covers, in words.</summary>
    private const string KeysKey = "zone_keys";

    /// <summary>And its root, in words.</summary>
    private const string PitchKey = "zone_pitch";

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

    /// <summary>The one filter every zone goes through: where it opens to.</summary>
    private const string CutoffKey = "cutoff";

    /// <summary>The same, worded for a panel to print, since a frequency needs its unit.</summary>
    private const string CutoffTextKey = "cutoff_text";

    /// <summary>How much it rings at the corner.</summary>
    private const string ResonanceKey = "resonance";

    /// <summary>How far the filter envelope moves it.</summary>
    private const string EnvelopeAmountKey = "env_amount";

    /// <summary>And whether it moves it up or down.</summary>
    private const string EnvelopePolarityKey = "env_polarity";

    /// <summary>How much the key played opens it, so a map stays even across the keyboard.</summary>
    private const string KeyFollowKey = "key_follow";

    /// <summary>The filter envelope: how long it takes to come up.</summary>
    private const string FilterAttackKey = "filter_attack";

    /// <summary>How long it takes to fall to where it holds.</summary>
    private const string FilterDecayKey = "filter_decay";

    /// <summary>Where it holds while the key is down.</summary>
    private const string FilterSustainKey = "filter_sustain";

    /// <summary>And how long it takes to fall away after the key comes up.</summary>
    private const string FilterReleaseKey = "filter_release";

    /// <summary>The amplifier envelope: how long the note takes to come up.</summary>
    private const string AttackKey = "attack";

    /// <summary>How long it takes to fall to where it holds.</summary>
    private const string DecayKey = "decay";

    /// <summary>Where it holds while the key is down.</summary>
    private const string SustainKey = "sustain";

    /// <summary>And how long it takes to go quiet after the key comes up.</summary>
    private const string ReleaseKey = "release";

    /// <summary>How loud the whole instrument plays, after everything else.</summary>
    /// <remarks>
    /// Named for what it is rather than after its key, since <see cref="LevelKey"/> is the zone's
    /// and the two would otherwise read as the same setting.
    /// </remarks>
    private const string LevelOutKey = "level";

    /// <summary>The highest key on a keyboard, which is where a zone with no map of its own ends.</summary>
    private const int TopKey = 119;

    /// <summary>And the root a zone falls back to, which is the middle of that.</summary>
    private const int MiddleKey = 48;

    /// <summary>The last of the ways a window can repeat, so a file cannot name one past it.</summary>
    private const double LastLoopMode = 2;

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

    /// <inheritdoc/>
    /// <remarks>
    /// With no zone picked, the zone half reads as the settings' own resting values rather than
    /// as nought: a map covers the whole keyboard by default, so an unpicked zone reads back as
    /// one that does.
    /// </remarks>
    public override double Get(string key) => key switch
    {
        LevelKey => Zone?.Volume ?? 1,
        PanKey => Zone?.Pan ?? 0,
        FineKey => Zone?.FineCents ?? 0,

        LowKey => Zone?.Zone.Low ?? 0,
        HighKey => Zone?.Zone.High ?? TopKey,
        RootKey => Zone?.Zone.Root ?? MiddleKey,

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

        NewNoteKey => (double)instrument.NewNoteAction,

        _ => 0,
    };

    /// <inheritdoc/>
    /// <remarks>
    /// The instrument's own half is tried first, because it is there whether or not a zone is
    /// picked: a map with nothing selected still has a filter, and a knob on it should still
    /// turn.
    ///
    /// The two edges of a zone are written the way the map moves them, whichever is travelling
    /// outwards going first, or the two cross on the way and are turned round behind our back.
    /// </remarks>
    protected override bool Write(string key, double value)
    {
        if (Patch(key, value) is { } mine) return mine;

        if (Zone is not { } zone) return false;

        return key switch
        {
            LevelKey => Moved(zone.Volume, value, () => zone.Volume = value),
            PanKey => Moved(zone.Pan, value, () => zone.Pan = value),
            FineKey => Moved(zone.FineCents, value, () => zone.FineCents = value),

            LowKey => Moved(zone.Low, value, () => zone.Low = value),
            HighKey => Moved(zone.High, value, () => zone.High = value),
            RootKey => Moved(zone.Root, value, () => zone.Root = value),

            StartKey => Window(shape => shape.Start = value),
            EndKey => Window(shape => shape.End = value),
            LoopKey => Window(
                shape => shape.LoopMode = (SampleLoopMode)Math.Clamp(Math.Round(value), 0, LastLoopMode)),
            LoopStartKey => Window(shape => shape.LoopStart = value),
            LoopEndKey => Window(shape => shape.LoopEnd = value),

            _ => false,
        };
    }

    /// <summary>
    /// The half of the machine that belongs to the instrument rather than to a zone.
    /// </summary>
    /// <param name="key">Which setting is being written.</param>
    /// <param name="value">Where it is being put.</param>
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

            case NewNoteKey:
                moved = Moved((int)instrument.NewNoteAction, value, 0, (int)VoiceEnding.Sustain,
                    at => instrument.NewNoteAction = (VoiceEnding)at);
                break;

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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    /// <remarks>
    /// Only the take and the name are yours to set. The other three are what the panel reads out
    /// about the zone, and there is nothing to write them into.
    ///
    /// A take goes on through the zone's own way of taking one, which names the zone after the
    /// file unless the zone has a name somebody chose. A zone still called after the recording it
    /// used to hold says the old recording is still on it.
    /// </remarks>
    protected override bool WriteText(string key, string value)
    {
        if (Zone is not { } zone) return false;

        switch (key)
        {
            case TakeKey:
                if (_paths.Same(zone.Zone.FilePath, value)) return false;

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
    /// <param name="write">Which end to move, and where to.</param>
    private bool Window(Action<SampleShape> write)
    {
        if (Shape is not { } shape) return false;

        write(shape);
        shape.Clamp();

        Zone?.Reread();

        return true;
    }

}
