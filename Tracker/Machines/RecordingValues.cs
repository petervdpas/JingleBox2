using JingleBox2.Machines;
using JingleBox2.Tracker.Synth;
using System;

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
public sealed class RecordingValues(TrackerInstrument instrument, TakeLibrary? shelf = null) : IMachineValues
{
    // Written out one by one, never built from a name or a loop, so every key in the app can
    // be found by searching for the string that is in the file.
    private const string TakeKey = "take";
    private const string TakeDetailsKey = "take_details";
    private const string BaseNoteKey = "base_note";
    private const string StartKey = "start";
    private const string EndKey = "end";
    private const string LoopModeKey = "loop_mode";
    private const string LoopStartKey = "loop_start";
    private const string LoopEndKey = "loop_end";
    private const string ReverseKey = "reverse";
    private const string OneVoiceKey = "one_voice";
    private const string AttackKey = "attack";
    private const string DecayKey = "decay";
    private const string SustainKey = "sustain";
    private const string ReleaseKey = "release";
    private const string TuneKey = "tune";
    private const string FineKey = "fine";
    private const string VibratoRateKey = "vibrato_rate";
    private const string VibratoDepthKey = "vibrato_depth";
    private const string PitchEnvKey = "pitch_env";
    private const string PitchTimeKey = "pitch_time";
    private const string TremoloRateKey = "tremolo_rate";
    private const string TremoloDepthKey = "tremolo_depth";
    private const string CutoffKey = "cutoff";

    /// <summary>What the cutoff knob writes under itself, since a position of 0.62 means nothing.</summary>
    private const string CutoffTextKey = "cutoff_text";
    private const string ResonanceKey = "resonance";
    private const string LevelKey = "level";
    private const string DriveKey = "drive";

    /// <summary>
    /// Told that something moved, if anybody is listening.
    /// </summary>
    /// <remarks>
    /// The instrument is plain data and says nothing when it is written to, so everything that
    /// has to happen after a change, saving the song, redrawing a scope, retuning a voice in
    /// the air, happens because something called this. It is the same callback the instrument
    /// editor's view models are handed, and it is set by whoever builds this, not here.
    ///
    /// It fires only when a write really moved something. A knob that reports the value it
    /// already had, which happens on every mouse move that did not cross a step, is not a
    /// change and must not mark a song dirty.
    /// </remarks>
    public Action? Changed { get; set; }

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

    /// <summary>What the knob for that key should be showing.</summary>
    /// <remarks>
    /// A flag reads as zero or one, because a panel has only numbers. A key this build does
    /// not have reads as zero, which is the far end of every range the machine declares, so a
    /// knob for a setting that is not here sits where it does nothing.
    /// </remarks>
    public double Get(string key) => key switch
    {
        BaseNoteKey => instrument.BaseNoteSemitone,
        StartKey => instrument.Shape?.Start ?? 0,
        EndKey => instrument.Shape?.End ?? 1,
        LoopModeKey => (double)(instrument.Shape?.LoopMode ?? SampleLoopMode.None),
        LoopStartKey => instrument.Shape?.LoopStart ?? 0,
        LoopEndKey => instrument.Shape?.LoopEnd ?? 1,
        ReverseKey => instrument.Shape?.Reverse == true ? 1 : 0,
        OneVoiceKey => instrument.OneVoice ? 1 : 0,
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
        // The last two are not the numbers the instrument keeps. A filter knob marked in hertz
        // does nothing for three quarters of its travel, and a level fader is marked in decibels
        // on every desk ever built, so the machine file declares the two the way they are read
        // and the conversion happens here, where the instrument's own units are known.
        CutoffKey => UI.FrequencyScale.ToPosition(Voice.FilterCutoffHz),
        ResonanceKey => Voice.FilterResonance,
        LevelKey => UI.GainScale.ToDecibels(instrument.Volume),
        DriveKey => Voice.Drive,
        _ => 0
    };

    /// <summary>Puts that value on the instrument, in range, and says so if it moved.</summary>
    /// <remarks>
    /// Every setting is clamped to what the instrument will actually play rather than to what
    /// the machine file claims. A panel that declares a wider range than this build understands
    /// is the same problem as an unknown key, one version further on, and the answer is the
    /// same: take what can be taken and do not break.
    /// </remarks>
    public void Set(string key, double value)
    {
        // A knob cannot produce one; a file can. Letting it through would put a NaN into a
        // voice, where it spreads through the filter and silences the instrument for good.
        if (double.IsNaN(value)) return;

        switch (key)
        {
            case BaseNoteKey: Whole(instrument.BaseNoteSemitone, value, Note.MinSemitone, Note.MaxSemitone, v => instrument.BaseNoteSemitone = v); break;
            case StartKey: Number(instrument.Shape?.Start ?? 0, value, 0, 1, v => Window.Start = v); break;
            case EndKey: Number(instrument.Shape?.End ?? 1, value, 0, 1, v => Window.End = v); break;
            case LoopModeKey: Loop(value); break;
            case LoopStartKey: Number(instrument.Shape?.LoopStart ?? 0, value, 0, 1, v => Window.LoopStart = v); break;
            case LoopEndKey: Number(instrument.Shape?.LoopEnd ?? 1, value, 0, 1, v => Window.LoopEnd = v); break;
            case ReverseKey: Flag(instrument.Shape?.Reverse == true, value, v => Window.Reverse = v); break;
            case OneVoiceKey: Flag(instrument.OneVoice, value, v => instrument.OneVoice = v); break;
            case AttackKey: Number(Voice.AttackMs, value, SynthPatch.MinTimeMs, SynthPatch.MaxAttackMs, v => Voice.AttackMs = v); break;
            case DecayKey: Number(Voice.DecayMs, value, SynthPatch.MinTimeMs, SynthPatch.MaxDecayMs, v => Voice.DecayMs = v); break;
            case SustainKey: Number(Voice.Sustain, value, SynthPatch.MinSustain, SynthPatch.MaxSustain, v => Voice.Sustain = v); break;
            case ReleaseKey: Number(Voice.ReleaseMs, value, SynthPatch.MinTimeMs, SynthPatch.MaxReleaseMs, v => Voice.ReleaseMs = v); break;
            case TuneKey: Number(Voice.TuneSemitones, value, SynthPatch.MinTuneSemitones, SynthPatch.MaxTuneSemitones, v => Voice.TuneSemitones = v); break;
            case FineKey: Number(Voice.FineCents, value, SynthPatch.MinFineCents, SynthPatch.MaxFineCents, v => Voice.FineCents = v); break;
            case VibratoRateKey: Number(Voice.VibratoRateHz, value, SynthPatch.MinRateHz, SynthPatch.MaxRateHz, v => Voice.VibratoRateHz = v); break;
            case VibratoDepthKey: Number(Voice.VibratoDepthCents, value, SynthPatch.MinVibratoDepthCents, SynthPatch.MaxVibratoDepthCents, v => Voice.VibratoDepthCents = v); break;
            case PitchEnvKey: Number(Voice.PitchEnvSemitones, value, SynthPatch.MinPitchEnvSemitones, SynthPatch.MaxPitchEnvSemitones, v => Voice.PitchEnvSemitones = v); break;
            case PitchTimeKey: Number(Voice.PitchEnvMs, value, SynthPatch.MinTimeMs, SynthPatch.MaxPitchEnvMs, v => Voice.PitchEnvMs = v); break;
            case TremoloRateKey: Number(Voice.TremoloRateHz, value, SynthPatch.MinRateHz, SynthPatch.MaxRateHz, v => Voice.TremoloRateHz = v); break;
            case TremoloDepthKey: Number(Voice.TremoloDepth, value, SynthPatch.MinTremoloDepth, SynthPatch.MaxTremoloDepth, v => Voice.TremoloDepth = v); break;
            case CutoffKey: Number(UI.FrequencyScale.ToPosition(Voice.FilterCutoffHz), value, 0, 1, v => Voice.FilterCutoffHz = UI.FrequencyScale.ToHz(v)); break;
            case ResonanceKey: Number(Voice.FilterResonance, value, SynthPatch.MinResonance, SynthPatch.MaxResonance, v => Voice.FilterResonance = v); break;
            case LevelKey: Number(UI.GainScale.ToDecibels(instrument.Volume), value, UI.GainScale.MinimumDecibels, UI.GainScale.MaximumDecibels, v => instrument.Volume = UI.GainScale.ToAmplitude(v)); break;
            case DriveKey: Number(Voice.Drive, value, SynthPatch.MinDrive, SynthPatch.MaxDrive, v => Voice.Drive = v); break;
        }
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
    private void Loop(double value)
    {
        var wanted = (SampleLoopMode)(int)Math.Clamp(Math.Round(value), 0, 2);

        if (wanted == (instrument.Shape?.LoopMode ?? SampleLoopMode.None)) return;

        Window.LoopMode = wanted;
        instrument.Loop = wanted != SampleLoopMode.None;

        Changed?.Invoke();
    }

    /// <summary>
    /// The settings that are not numbers: which recording this plays, and what is written
    /// beside the base note.
    /// </summary>
    /// <remarks>
    /// Three of them, and all but the take are read only. How long the take is and what rate it
    /// was recorded at is read off the file rather than held anywhere, so there is nothing to
    /// write back, and it is answered only when this was given a shelf to ask: an instrument on
    /// its own knows the path and nothing about what is in the file.
    ///
    /// The note name is read only on purpose. It is the base note said in the other language a
    /// musician has for it, C-4 rather than 48, and there is nothing to write back: setting it
    /// is setting the number the panel already has a field for.
    /// </remarks>
    public string GetText(string key) => key switch
    {
        TakeKey => instrument.FilePath ?? "",
        TakeDetailsKey => shelf?.Details(instrument.FilePath ?? "") ?? "",
        CutoffTextKey => UI.FrequencyScale.Text(Voice.FilterCutoffHz),
        BaseNoteKey => instrument.BaseNote.ToString(),
        _ => ""
    };

    /// <summary>
    /// Puts a recording on the machine.
    /// </summary>
    /// <remarks>
    /// The take is the instrument's file, and it is text because that is what it is: a path to
    /// one of your recordings, not a position in a list that would move the next time you
    /// deleted something above it. Emptying it is allowed, and is what a Recording machine with
    /// nothing on it yet looks like.
    /// </remarks>
    public void SetText(string key, string value)
    {
        switch (key)
        {
            case TakeKey: Text(instrument.FilePath, value, v => instrument.FilePath = v); break;
        }
    }

    private void Number(double current, double value, double min, double max, Action<double> apply)
    {
        double clamped = Math.Clamp(value, min, max);
        if (clamped.Equals(current)) return;

        apply(clamped);
        Changed?.Invoke();
    }

    /// <summary>
    /// A setting that counts rather than sweeps: the base note, which is a semitone or nothing.
    /// </summary>
    private void Whole(int current, double value, int min, int max, Action<int> apply)
    {
        int rounded = (int)Math.Clamp(Math.Round(value), min, max);
        if (rounded == current) return;

        apply(rounded);
        Changed?.Invoke();
    }

    /// <summary>
    /// A switch, arriving as a number because that is all a panel has. Half way up is on, so a
    /// control that sweeps rather than clicks still lands somewhere definite.
    /// </summary>
    private void Flag(bool current, double value, Action<bool> apply)
    {
        bool on = value >= 0.5;
        if (on == current) return;

        apply(on);
        Changed?.Invoke();
    }

    private void Text(string current, string value, Action<string> apply)
    {
        string wanted = value ?? "";
        if (string.Equals(current ?? "", wanted, StringComparison.Ordinal)) return;

        apply(wanted);
        Changed?.Invoke();
    }
}
