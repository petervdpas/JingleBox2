using System.Linq;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Synth;
using JingleBox2.Tracker.Synth.Enums;

namespace JingleBox2.SoundDevices.SoundMachines;

/// <summary>
/// Lighttower's panel, wired to a real patch.
/// </summary>
/// <remarks>
/// The patch is written straight into rather than through a view model, since nothing on the
/// face is bound to one: a described panel reads and writes through here and nowhere else.
///
/// **The two drawn waves are text settings**, written the way <see cref="IWaveSegments.Spell"/>
/// writes a line. A line is not a number, and a hundred and twenty eight parameters would be a
/// hundred and twenty eight knobs a controller could be pointed at, each meaning nothing. As text
/// a line goes wherever a machine's words already go: into a preset, into the song, into a step
/// of the instrument's undo, and onto the designer's bench, with nothing taught about any of them.
///
/// A key it does not know reads as nought and swallows the write, so a machine file written by a
/// later version opens rather than taking the application down.
/// </remarks>
/// <param name="patch">The two waves and how a note goes through them.</param>
/// <param name="instrument">Whose new note action it is, which is the instrument's rather than the sound's.</param>
public sealed class SegmentValues(SegmentPatch patch, TrackerInstrument instrument) : PanelValues
{
    /// <summary>The first drawn wave, as words.</summary>
    public const string BeginKey = "begin";

    /// <summary>The last drawn wave, as words.</summary>
    public const string EndKey = "end";

    /// <summary>How long a note takes from the first wave to the last.</summary>
    private const string SweepKey = "sweep";

    /// <summary>Once, loop or bounce, as a place on the switch.</summary>
    private const string MotionKey = "motion";

    /// <summary>Eight bit and stepping, or smooth.</summary>
    private const string GritKey = "grit";

    /// <summary>The envelope's attack.</summary>
    private const string AttackKey = "attack";

    /// <summary>The envelope's decay.</summary>
    private const string DecayKey = "decay";

    /// <summary>The envelope's sustain.</summary>
    private const string SustainKey = "sustain";

    /// <summary>The envelope's release.</summary>
    private const string ReleaseKey = "release";

    /// <summary>Whole semitones added to every note.</summary>
    private const string TuneKey = "tune";

    /// <summary>And the part of a semitone.</summary>
    private const string FineKey = "fine";

    /// <summary>How loud the machine comes out.</summary>
    private const string VolumeKey = "volume";

    /// <summary>What a new note does to the one the track is still sounding.</summary>
    private const string NewNoteKey = "new_note";

    /// <summary>How a line is written and read back. Shared, since it holds nothing.</summary>
    private static readonly IWaveSegments Lines = new WaveSegments();

    /// <inheritdoc/>
    public override double Get(string key) => key switch
    {
        SweepKey => patch.SweepMs,
        MotionKey => (double)patch.Motion,
        GritKey => patch.Grit ? 1 : 0,
        AttackKey => patch.AttackMs,
        DecayKey => patch.DecayMs,
        SustainKey => patch.Sustain,
        ReleaseKey => patch.ReleaseMs,
        TuneKey => patch.TuneSemitones,
        FineKey => patch.FineCents,
        VolumeKey => patch.Volume,
        NewNoteKey => (double)instrument.NewNoteAction,
        _ => 0,
    };

    /// <inheritdoc/>
    /// <remarks>
    /// Something that is not a number is refused before anything is written, and what is written
    /// is brought back into range by the patch's own rules straight afterwards.
    /// </remarks>
    protected override bool Write(string key, double value)
    {
        if (!double.IsFinite(value)) return false;

        bool moved = key switch
        {
            SweepKey => Moved(patch.SweepMs, value, () => patch.SweepMs = value),
            MotionKey => Moved((int)patch.Motion, value, 0, (int)SegmentMotion.Bounce,
                at => patch.Motion = (SegmentMotion)at),
            GritKey => Moved(patch.Grit, value, on => patch.Grit = on),
            AttackKey => Moved(patch.AttackMs, value, () => patch.AttackMs = value),
            DecayKey => Moved(patch.DecayMs, value, () => patch.DecayMs = value),
            SustainKey => Moved(patch.Sustain, value, () => patch.Sustain = value),
            ReleaseKey => Moved(patch.ReleaseMs, value, () => patch.ReleaseMs = value),
            TuneKey => Moved(patch.TuneSemitones, value, () => patch.TuneSemitones = value),
            FineKey => Moved(patch.FineCents, value, () => patch.FineCents = value),
            VolumeKey => Moved(patch.Volume, value, () => patch.Volume = value),
            NewNoteKey => Moved((int)instrument.NewNoteAction, value, 0, (int)VoiceEnding.Sustain,
                at => instrument.NewNoteAction = (VoiceEnding)at),
            _ => false,
        };

        if (moved) patch.Clamp();

        return moved;
    }

    /// <inheritdoc/>
    public override string GetText(string key) => key switch
    {
        BeginKey => Lines.Spell(patch.Begin),
        EndKey => Lines.Spell(patch.End),
        _ => "",
    };

    /// <inheritdoc/>
    /// <remarks>
    /// A line is replaced whole, never written into, since a voice may be reading the old one on
    /// the audio thread. Words that read as no line at all change nothing, and neither does the
    /// line that is already there.
    /// </remarks>
    protected override bool WriteText(string key, string value)
    {
        if (key is not (BeginKey or EndKey)) return false;

        var read = Lines.Read(value);

        if (read.Length == 0) return false;

        var was = key == BeginKey ? patch.Begin : patch.End;

        if (was is not null && was.SequenceEqual(read)) return false;

        if (key == BeginKey) patch.Begin = read;
        else patch.End = read;

        return true;
    }
}
