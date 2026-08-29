using JingleBox2.Machines;
using JingleBox2.Tracker.Synth;
using System;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.UI;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// The Recording machine's panel, wired to a real instrument.
/// </summary>
/// <remarks>
/// A described panel knows only keys and numbers: it reads "attack", turns a knob, and writes
/// "attack" back. This is the one place that says what each of those keys is on the instrument
/// underneath, so the panel can be redrawn, reordered or rewritten without any of it reaching
/// <see cref="TrackerInstrument"/>.
///
/// The words are <see cref="RecordingPatch"/>'s words. That class writes the same settings into
/// a machine's own file and this one puts them on knobs, so a key means the same thing in both:
/// the take is the recording, the level is the gain on top of the volume column, one voice is
/// whether a key played by hand cuts the last. Only the spelling differs, because machine.json
/// writes its keys with underscores and the patch file writes its own the way it always has,
/// and neither can be respelled without going back on what is already in people's files.
///
/// It holds the instrument rather than a copy of its settings, for the reason RecordingPatch
/// does: the player, the editor and the song file are all still reading that object, so a
/// second copy here would be a second truth to keep in step.
///
/// A key it does not know reads as zero and swallows the write. That is not politeness, it is
/// what lets a machine.json written by a later version open on an older app: the panel draws
/// the knob it was told about, the knob turns, and the setting this build has no field for
/// simply does not go anywhere. The alternative is a crash on a file somebody has already
/// shipped.
/// </remarks>
/// <param name="instrument">The instrument being read and written, held rather than copied.</param>
/// <param name="shelf">
/// Where the recordings are, for the line that says how long the take is. Without one that line
/// is empty, which is what a panel being designed against no shelf should show.
/// </param>
public sealed class RecordingValues(TrackerInstrument instrument, TakeLibrary? shelf = null) : MachineValues
{
    /// <summary>The fader scale, so a level in decibels can be checked without a window.</summary>
    private readonly IGainScale _gain = new GainScale();

    /// <summary>The filter sweep, so a knob position can be checked without a window.</summary>
    private readonly IFrequencyScale _hz = new FrequencyScale();

    /// <summary>The recording itself, by the path the instrument holds.</summary>
    /// <remarks>
    /// The keys are written out one by one, never built from a name or a loop, so every key in
    /// the application can be found by searching for the string that is in the machine's own
    /// file. A key assembled at the call site never appears in the source at all, and both the
    /// tools that hunt for an orphaned key and anybody grepping would miss it.
    /// </remarks>
    private const string TakeKey = "take";

    /// <summary>How long it is and what rate it was recorded at, read off the file.</summary>
    private const string TakeDetailsKey = "take_details";

    /// <summary>The pitch it was recorded at, so a key can be played in tune against it.</summary>
    private const string BaseNoteKey = "base_note";

    /// <summary>Where in the file playing starts, nought to one.</summary>
    private const string StartKey = "start";

    /// <summary>And where it ends.</summary>
    private const string EndKey = "end";

    /// <summary>Whether it repeats, and which way round.</summary>
    private const string LoopModeKey = "loop_mode";

    /// <summary>Where the repeat goes back to.</summary>
    private const string LoopStartKey = "loop_start";

    /// <summary>And where it turns round.</summary>
    private const string LoopEndKey = "loop_end";

    /// <summary>Whether the recording plays backwards.</summary>
    private const string ReverseKey = "reverse";

    /// <summary>Whether a new key cuts the one still ringing.</summary>
    private const string OneVoiceKey = "one_voice";

    /// <summary>What a new note does to the one the track is still sounding.</summary>
    private const string NewNoteKey = "new_note";

    /// <summary>The amplifier envelope: how long the note takes to come up.</summary>
    private const string AttackKey = "attack";

    /// <summary>How long it takes to fall to where it holds.</summary>
    private const string DecayKey = "decay";

    /// <summary>Where it holds while the key is down.</summary>
    private const string SustainKey = "sustain";

    /// <summary>And how long it takes to go quiet after the key comes up.</summary>
    private const string ReleaseKey = "release";

    /// <summary>Coarse tuning, in semitones.</summary>
    private const string TuneKey = "tune";

    /// <summary>And fine tuning, in cents.</summary>
    private const string FineKey = "fine";

    /// <summary>How fast the pitch wobbles.</summary>
    private const string VibratoRateKey = "vibrato_rate";

    /// <summary>And how far, in cents.</summary>
    private const string VibratoDepthKey = "vibrato_depth";

    /// <summary>How far the pitch falls or rises at the start of a note.</summary>
    private const string PitchEnvKey = "pitch_env";

    /// <summary>And how long it takes to get there.</summary>
    private const string PitchTimeKey = "pitch_time";

    /// <summary>How fast the level wobbles.</summary>
    private const string TremoloRateKey = "tremolo_rate";

    /// <summary>And how far.</summary>
    private const string TremoloDepthKey = "tremolo_depth";

    /// <summary>Where the filter opens to, as a position on the knob rather than in hertz.</summary>
    private const string CutoffKey = "cutoff";

    /// <summary>What the cutoff knob writes under itself, since a position of 0.62 means nothing.</summary>
    private const string CutoffTextKey = "cutoff_text";

    /// <summary>How much the filter rings at the corner.</summary>
    private const string ResonanceKey = "resonance";

    /// <summary>How loud the instrument plays, in decibels.</summary>
    private const string LevelKey = "level";

    /// <summary>How hard the result is pushed into the saturation at the end of it.</summary>
    private const string DriveKey = "drive";

    /// <summary>The last of the ways a window can repeat, so a file cannot name one past it.</summary>
    private const double LastLoopMode = 2;

    /// <summary>Where a switch turns on, since a panel hands one over as a sweep.</summary>
    private const double SwitchOn = 0.5;

    /// <summary>The floor of anything measured nought to one: a window's edges, a knob position.</summary>
    private const double Least = 0;

    /// <summary>And the ceiling.</summary>
    private const double Most = 1;

    /// <summary>
    /// The voice the recording plays through: its envelope, its filter and its modulation.
    /// </summary>
    /// <remarks>
    /// Both machines that play a recording run it through this same patch, which is why a
    /// sample has an attack at all. Made if it is missing, since an instrument read from a
    /// file that says nothing about a patch would otherwise take every envelope key with it.
    /// </remarks>
    private SynthPatch Voice => instrument.Patch ??= new SynthPatch();

    /// <summary>
    /// Which part of the file plays, made if it is not there yet.
    /// </summary>
    /// <remarks>
    /// Not through <see cref="TrackerInstrument.EnsureShape"/>, which flattens the envelope of
    /// anything that never had a shape. That is right when an old instrument is read off disc
    /// and wrong here: the panel is setting the envelope in the same breath, and flattening it
    /// would throw away the attack that was written a moment ago.
    /// </remarks>
    private SampleShape Window => instrument.Shape ??= new SampleShape();

    /// <inheritdoc/>
    /// <remarks>
    /// A flag reads as zero or one, because a panel has only numbers. A key this build does
    /// not have reads as zero, which is the far end of every range the machine declares, so a
    /// knob for a setting that is not here sits where it does nothing.
    ///
    /// The cutoff and the level are not the numbers the instrument keeps. A filter knob marked
    /// in hertz does nothing for three quarters of its travel, and a level fader is marked in
    /// decibels on every desk ever built, so the machine file declares those two the way they
    /// are read and the conversion happens here, where the instrument's own units are known.
    /// </remarks>
    public override double Get(string key) => key switch
    {
        BaseNoteKey => instrument.BaseNoteSemitone,
        StartKey => instrument.Shape?.Start ?? 0,
        EndKey => instrument.Shape?.End ?? 1,
        LoopModeKey => (double)(instrument.Shape?.LoopMode ?? SampleLoopMode.None),
        LoopStartKey => instrument.Shape?.LoopStart ?? 0,
        LoopEndKey => instrument.Shape?.LoopEnd ?? 1,
        ReverseKey => instrument.Shape?.Reverse == true ? 1 : 0,
        OneVoiceKey => instrument.OneVoice ? 1 : 0,
        NewNoteKey => (double)instrument.NewNoteAction,
        AttackKey => Voice.AttackMs,
        DecayKey => Voice.DecayMs,
        SustainKey => Voice.Sustain,
        ReleaseKey => Voice.ReleaseMs,
        TuneKey => Voice.TuneSemitones,
        FineKey => Voice.FineCents,
        VibratoRateKey => Voice.VibratoRateHz,
        VibratoDepthKey => Voice.VibratoDepthCents,
        PitchEnvKey => Voice.PitchEnvSemitones,
        PitchTimeKey => Voice.PitchEnvMs,
        TremoloRateKey => Voice.TremoloRateHz,
        TremoloDepthKey => Voice.TremoloDepth,
        CutoffKey => _hz.ToPosition(Voice.FilterCutoffHz),
        ResonanceKey => Voice.FilterResonance,
        LevelKey => _gain.ToDecibels(instrument.Volume),
        DriveKey => Voice.Drive,
        _ => 0
    };

    /// <inheritdoc/>
    /// <remarks>
    /// Every setting is clamped to what the instrument will actually play rather than to what
    /// the machine file claims. A panel that declares a wider range than this build understands
    /// is the same problem as an unknown key, one version further on, and the answer is the
    /// same: take what can be taken and do not break.
    /// </remarks>
    protected override bool Write(string key, double value)
    {
        return key switch
        {
            BaseNoteKey => Whole(instrument.BaseNoteSemitone, value, Note.MinSemitone, Note.MaxSemitone, v => instrument.BaseNoteSemitone = v),
            StartKey => Number(instrument.Shape?.Start ?? Least, value, Least, Most, v => Window.Start = v),
            EndKey => Number(instrument.Shape?.End ?? Most, value, Least, Most, v => Window.End = v),
            LoopModeKey => Loop(value),
            LoopStartKey => Number(instrument.Shape?.LoopStart ?? Least, value, Least, Most, v => Window.LoopStart = v),
            LoopEndKey => Number(instrument.Shape?.LoopEnd ?? Most, value, Least, Most, v => Window.LoopEnd = v),
            ReverseKey => Flag(instrument.Shape?.Reverse == true, value, v => Window.Reverse = v),
            OneVoiceKey => Flag(instrument.OneVoice, value, v => instrument.OneVoice = v),
            NewNoteKey => Moved((int)instrument.NewNoteAction, value, 0, (int)VoiceEnding.Sustain,
                at => instrument.NewNoteAction = (VoiceEnding)at),
            AttackKey => Number(Voice.AttackMs, value, SynthPatch.MinTimeMs, SynthPatch.MaxAttackMs, v => Voice.AttackMs = v),
            DecayKey => Number(Voice.DecayMs, value, SynthPatch.MinTimeMs, SynthPatch.MaxDecayMs, v => Voice.DecayMs = v),
            SustainKey => Number(Voice.Sustain, value, SynthPatch.MinSustain, SynthPatch.MaxSustain, v => Voice.Sustain = v),
            ReleaseKey => Number(Voice.ReleaseMs, value, SynthPatch.MinTimeMs, SynthPatch.MaxReleaseMs, v => Voice.ReleaseMs = v),
            TuneKey => Number(Voice.TuneSemitones, value, SynthPatch.MinTuneSemitones, SynthPatch.MaxTuneSemitones, v => Voice.TuneSemitones = v),
            FineKey => Number(Voice.FineCents, value, SynthPatch.MinFineCents, SynthPatch.MaxFineCents, v => Voice.FineCents = v),
            VibratoRateKey => Number(Voice.VibratoRateHz, value, SynthPatch.MinRateHz, SynthPatch.MaxRateHz, v => Voice.VibratoRateHz = v),
            VibratoDepthKey => Number(Voice.VibratoDepthCents, value, SynthPatch.MinVibratoDepthCents, SynthPatch.MaxVibratoDepthCents, v => Voice.VibratoDepthCents = v),
            PitchEnvKey => Number(Voice.PitchEnvSemitones, value, SynthPatch.MinPitchEnvSemitones, SynthPatch.MaxPitchEnvSemitones, v => Voice.PitchEnvSemitones = v),
            PitchTimeKey => Number(Voice.PitchEnvMs, value, SynthPatch.MinTimeMs, SynthPatch.MaxPitchEnvMs, v => Voice.PitchEnvMs = v),
            TremoloRateKey => Number(Voice.TremoloRateHz, value, SynthPatch.MinRateHz, SynthPatch.MaxRateHz, v => Voice.TremoloRateHz = v),
            TremoloDepthKey => Number(Voice.TremoloDepth, value, SynthPatch.MinTremoloDepth, SynthPatch.MaxTremoloDepth, v => Voice.TremoloDepth = v),
            CutoffKey => Number(_hz.ToPosition(Voice.FilterCutoffHz), value, Least, Most, v => Voice.FilterCutoffHz = _hz.ToHz(v)),
            ResonanceKey => Number(Voice.FilterResonance, value, SynthPatch.MinResonance, SynthPatch.MaxResonance, v => Voice.FilterResonance = v),
            LevelKey => Number(_gain.ToDecibels(instrument.Volume), value, UI.GainScale.MinimumDecibels, UI.GainScale.MaximumDecibels, v => instrument.Volume = _gain.ToAmplitude(v)),
            DriveKey => Number(Voice.Drive, value, SynthPatch.MinDrive, SynthPatch.MaxDrive, v => Voice.Drive = v),

            _ => false,
        };
    }

    /// <summary>
    /// How it loops, which is one of three things rather than a sweep.
    /// </summary>
    /// <remarks>
    /// <see cref="TrackerInstrument.Loop"/> is the older way of saying the same thing and is
    /// still what the player reads on an instrument with no window, so the two are kept in
    /// step here. Letting them disagree means an instrument that says it loops and a window
    /// that says it does not, and which one wins depends on which code asked.
    /// </remarks>
    private bool Loop(double value)
    {
        var wanted = (SampleLoopMode)(int)Math.Clamp(Math.Round(value), 0, LastLoopMode);

        if (wanted == (instrument.Shape?.LoopMode ?? SampleLoopMode.None)) return false;

        Window.LoopMode = wanted;
        instrument.Loop = wanted != SampleLoopMode.None;

        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The settings that are not numbers: which recording this plays, and what is written
    /// beside the base note.
    ///
    /// Three of them, and all but the take are read only. How long the take is and what rate it
    /// was recorded at is read off the file rather than held anywhere, so there is nothing to
    /// write back, and it is answered only when this was given a shelf to ask: an instrument on
    /// its own knows the path and nothing about what is in the file.
    ///
    /// The note name is read only on purpose. It is the base note said in the other language a
    /// musician has for it, C-4 rather than 48, and there is nothing to write back: setting it
    /// is setting the number the panel already has a field for.
    /// </remarks>
    public override string GetText(string key) => key switch
    {
        TakeKey => instrument.FilePath ?? "",
        TakeDetailsKey => shelf?.Details(instrument.FilePath ?? "") ?? "",
        CutoffTextKey => _hz.Text(Voice.FilterCutoffHz),
        BaseNoteKey => instrument.BaseNote.ToString(),
        _ => ""
    };

    /// <inheritdoc/>
    /// <remarks>
    /// Puts a recording on the machine, and nothing else: the other three text keys are what the
    /// panel reads out about the take, and there is nothing to write them into.
    ///
    /// The take is the instrument's file, and it is text because that is what it is: a path to
    /// one of your recordings, not a position in a list that would move the next time you
    /// deleted something above it. Emptying it is allowed, and is what a Recording machine with
    /// nothing on it yet looks like.
    /// </remarks>
    protected override bool WriteText(string key, string value) => key switch
    {
        TakeKey => Text(instrument.FilePath, value, v => instrument.FilePath = v),
        _ => false,
    };

    /// <summary>A setting that sweeps: clamped to what the instrument will play, then written.</summary>
    /// <remarks>
    /// Clamped to this build's own ends rather than to whatever the machine file claims, which is
    /// the same rule as an unknown key one version further on: take what can be taken, and do
    /// not break on a file somebody has already shipped.
    /// </remarks>
    /// <param name="current">Where the setting stands now, in the units the panel deals in.</param>
    /// <param name="value">Where the panel is asking to put it.</param>
    /// <param name="min">The lowest this build will play.</param>
    /// <param name="max">And the highest.</param>
    /// <param name="apply">What to do with the clamped value once it is known to have moved.</param>
    private static bool Number(double current, double value, double min, double max, Action<double> apply)
    {
        double clamped = Math.Clamp(value, min, max);
        if (clamped.Equals(current)) return false;

        apply(clamped);

        return true;
    }

    /// <summary>
    /// A setting that counts rather than sweeps: the base note, which is a semitone or nothing.
    /// </summary>
    /// <param name="current">Where the setting stands now.</param>
    /// <param name="value">Where the panel is asking to put it, which may be between two steps.</param>
    /// <param name="min">The lowest step this build will take.</param>
    /// <param name="max">And the highest.</param>
    /// <param name="apply">What to do with the rounded value once it is known to have moved.</param>
    private static bool Whole(int current, double value, int min, int max, Action<int> apply)
    {
        int rounded = (int)Math.Clamp(Math.Round(value), min, max);
        if (rounded == current) return false;

        apply(rounded);

        return true;
    }

    /// <summary>
    /// A switch, arriving as a number because that is all a panel has. Half way up is on, so a
    /// control that sweeps rather than clicks still lands somewhere definite.
    /// </summary>
    /// <param name="current">Whether it is on now.</param>
    /// <param name="value">What the panel says, which is a number either side of a half.</param>
    /// <param name="apply">What to do once it is known to have moved.</param>
    private static bool Flag(bool current, double value, Action<bool> apply)
    {
        bool on = value >= SwitchOn;
        if (on == current) return false;

        apply(on);

        return true;
    }

    /// <summary>A setting that is words: written if it really is different, and said so if it was.</summary>
    /// <remarks>
    /// Null and the empty string are the same thing here, since an instrument with nothing on it
    /// has been spelled both ways over the years and a panel should not see the difference.
    /// </remarks>
    /// <param name="current">What it says now.</param>
    /// <param name="value">What the panel is asking it to say.</param>
    /// <param name="apply">What to do once it is known to have changed.</param>
    private static bool Text(string current, string value, Action<string> apply)
    {
        string wanted = value ?? "";
        if (string.Equals(current ?? "", wanted, StringComparison.Ordinal)) return false;

        apply(wanted);

        return true;
    }
}
