using System;
using System.Collections.Generic;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Machines;
using JingleBox2.Tracker.Synth;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What happens to the note a track is still sounding when the next one lands on it.
/// </summary>
/// <remarks>
/// A tracker plays one note to a track and has always cut the note before. Three answers now,
/// and the two new ones are the ones that can go wrong quietly: a release that is really a cut
/// sounds like nothing changed, and a sustain that never lets go fills the mix with notes
/// nobody can hear the end of.
///
/// Voices are counted rather than listened to. A cut is four milliseconds, a release is the
/// patch's own, and a sustain is neither, so how many voices are still alive after a given
/// stretch of rendering says which of the three happened without anybody having to measure a
/// waveform.
///
/// The plugin half cannot be counted that way, because a plugin holds its own voices. It is
/// asked instead: <see cref="Ear"/> writes down every note on and note off it is sent.
/// </remarks>
public class NewNoteActionTests
{
    /// <summary>The sample rate everything here is rendered at.</summary>
    private const int Rate = 44100;

    /// <summary>A patch that sounds at once, holds, and takes a while to let go.</summary>
    /// <remarks>
    /// The long release is what makes the three endings tell each other apart: under a cut the
    /// voice is gone in four milliseconds, under a release it is still going a block later, and
    /// under a sustain it is still going after the release would have finished.
    /// </remarks>
    private static SynthPatch Held() => new()
    {
        AttackMs = 0,
        DecayMs = 0,
        Sustain = 1,
        ReleaseMs = 400
    };

    /// <summary>Renders a stretch of audio, in milliseconds, a block at a time.</summary>
    private static void Play(TrackMixer mixer, int milliseconds)
    {
        int frames = 512;
        var buffer = new float[frames * 2];

        for (int done = 0; done < Rate * milliseconds / 1000; done += frames)
            mixer.Render(buffer, frames);
    }

    /// <summary>Two notes on one track, the second arriving with the ending under test.</summary>
    private static TrackMixer After(VoiceEnding ending, int second = 64)
    {
        var mixer = new TrackMixer(Rate);

        mixer.NoteOn(0, 0, Held(), new Note(60), 1f, 0f);
        Play(mixer, 20);

        mixer.NoteOn(0, 0, Held(), new Note(second), 1f, 0f, ending);

        return mixer;
    }

    /// <summary>Cut is what a tracker has always done, and is still the default.</summary>
    [Fact]
    public void Cut_takes_the_note_before_it_away()
    {
        var mixer = After(VoiceEnding.Cut);

        Play(mixer, 50);

        Assert.Equal(1, mixer.VoiceCount);
    }

    /// <summary>Release leaves the note before it decaying under the new one.</summary>
    [Fact]
    public void Release_leaves_the_note_before_it_ringing()
    {
        var mixer = After(VoiceEnding.Release);

        Play(mixer, 50);

        Assert.Equal(2, mixer.VoiceCount);
    }

    /// <summary>And that ringing is a release, so it ends on its own.</summary>
    [Fact]
    public void A_released_note_ends_when_its_release_does()
    {
        var mixer = After(VoiceEnding.Release);

        Play(mixer, 600);

        Assert.Equal(1, mixer.VoiceCount);
    }

    /// <summary>Sustain lets go of nothing, so the track holds a chord.</summary>
    [Fact]
    public void Sustain_holds_the_note_before_it()
    {
        var mixer = After(VoiceEnding.Sustain);

        Play(mixer, 600);

        Assert.Equal(2, mixer.VoiceCount);
    }

    /// <summary>
    /// The same note arriving again is a retrigger under every one of the three.
    /// </summary>
    /// <remarks>
    /// Without this a part left sustaining walks into the voice limit and starts stealing notes
    /// somebody meant to hear, and two copies of one note beat against each other on the way.
    /// </remarks>
    [Fact]
    public void The_same_note_again_is_cut_whatever_the_ending()
    {
        var mixer = After(VoiceEnding.Sustain, second: 60);

        Play(mixer, 50);

        Assert.Equal(1, mixer.VoiceCount);
    }

    /// <summary>
    /// And the difference is audible, which counting voices does not by itself prove.
    /// </summary>
    /// <remarks>
    /// Two sine voices a fifth apart are uncorrelated, so summing them puts the loudness up by
    /// about the square root of two. Measured rather than assumed: a voice that is alive in the
    /// list and silent in the buffer would pass every test above this one.
    /// </remarks>
    [Fact]
    public void A_sustained_note_is_in_the_buffer_and_a_cut_one_is_not()
    {
        Assert.True(Loudness(VoiceEnding.Sustain) > Loudness(VoiceEnding.Cut) * 1.3);
    }

    /// <summary>
    /// Low enough that two voices sum rather than meet the saturation on the master.
    /// </summary>
    /// <remarks>
    /// At full level this measurement says the opposite of the truth. One loud sine is driven
    /// most of the way to a square by the master's saturation and reads high; two of them a
    /// fifth apart cancel each other at the bottom of every beat, so the pair reads lower than
    /// the one. That is the saturation being measured rather than the mix.
    /// </remarks>
    private const float Quiet = 0.15f;

    /// <summary>How loud one block is, a while after a second note landed under that ending.</summary>
    private static double Loudness(VoiceEnding ending)
    {
        var mixer = new TrackMixer(Rate);
        var quiet = new SynthPatch { AttackMs = 0, DecayMs = 0, Sustain = 1, ReleaseMs = 400 };

        mixer.NoteOn(0, 0, quiet, new Note(60), Quiet, 0f);
        Play(mixer, 20);

        mixer.NoteOn(0, 0, quiet, new Note(67), Quiet, 0f, ending);
        Play(mixer, 600);

        int frames = 4096;
        var buffer = new float[frames * 2];

        mixer.Render(buffer, frames);

        double sum = 0;
        foreach (float sample in buffer) sum += sample * sample;

        return Math.Sqrt(sum / buffer.Length);
    }

    /// <summary>A note on another track is nobody's business but that track's.</summary>
    [Fact]
    public void A_note_on_another_track_ends_nothing_here()
    {
        var mixer = new TrackMixer(Rate);

        mixer.NoteOn(0, 0, Held(), new Note(60), 1f, 0f);
        mixer.NoteOn(1, 0, Held(), new Note(64), 1f, 0f);

        Play(mixer, 50);

        Assert.Equal(2, mixer.VoiceCount);
    }

    /// <summary>Cut is what an instrument does until somebody says otherwise.</summary>
    [Fact]
    public void An_instrument_cuts_until_it_is_told_not_to()
    {
        Assert.Equal(VoiceEnding.Cut, new TrackerInstrument().NewNoteAction);
    }

    /// <summary>It is part of the sound, so it travels with a preset and with a copy.</summary>
    [Fact]
    public void The_ending_travels_with_the_sound()
    {
        var from = new TrackerInstrument
        {
            Kind = TrackerInstrumentKind.Synth,
            NewNoteAction = VoiceEnding.Release
        };

        var onto = new TrackerInstrument { Kind = TrackerInstrumentKind.Synth };

        onto.TakeSoundFrom(from);

        Assert.Equal(VoiceEnding.Release, onto.NewNoteAction);
        Assert.Equal(VoiceEnding.Release, from.Clone().NewNoteAction);
    }

    /// <summary>
    /// A song remembers it, since a part that overlaps is not the same part without it.
    /// </summary>
    /// <remarks>
    /// The number in the file is the enum's own, which is why those numbers do not move.
    /// </remarks>
    [Fact]
    public void A_song_carries_the_ending()
    {
        var song = new Song();

        var instrument = new TrackerInstrument
        {
            Name = "Piano",
            Kind = TrackerInstrumentKind.Synth,
            NewNoteAction = VoiceEnding.Release
        };

        instrument.EnsureId();
        song.Instruments.Add(instrument);

        var back = SongStore.Uncopy(SongStore.Copy(song));

        Assert.Equal(VoiceEnding.Release, back!.Instruments[0].NewNoteAction);
    }

    /// <summary>And a song written before it existed opens as it always played.</summary>
    [Fact]
    public void A_song_that_never_heard_of_it_cuts()
    {
        var song = new Song();

        var instrument = new TrackerInstrument { Name = "Bass", Kind = TrackerInstrumentKind.Synth };
        instrument.EnsureId();
        song.Instruments.Add(instrument);

        string written = SongStore.Copy(song).Replace("\"NewNoteAction\": 0,", "");

        Assert.DoesNotContain("NewNoteAction", written);
        Assert.Equal(VoiceEnding.Cut, SongStore.Uncopy(written)!.Instruments[0].NewNoteAction);
    }

    /// <summary>
    /// A machine's face reads and writes it like any other setting, since that is how it is
    /// reached: a panel deals in numbers, so the three endings are nought, one and two.
    /// </summary>
    [Fact]
    public void A_panel_moves_it_like_any_other_setting()
    {
        var instrument = new TrackerInstrument { Kind = TrackerInstrumentKind.Synth };
        instrument.EnsureId();

        var values = new SynthValues(
            new JingleBox2.ViewModels.SynthPatchViewModel(instrument.Patch, () => { }), instrument);

        values.Set("new_note", (double)VoiceEnding.Sustain);

        Assert.Equal(VoiceEnding.Sustain, instrument.NewNoteAction);
        Assert.Equal((double)VoiceEnding.Sustain, values.Get("new_note"));
    }

    /// <summary>A position past the last one it has picks the last one rather than nothing.</summary>
    /// <remarks>
    /// A machine.json written by a later version has to open on an older application rather
    /// than casting itself into an ending this build does not have.
    /// </remarks>
    [Fact]
    public void A_position_this_build_has_not_got_lands_on_one_it_has()
    {
        var instrument = new TrackerInstrument { Kind = TrackerInstrumentKind.Synth };
        instrument.EnsureId();

        var values = new SynthValues(
            new JingleBox2.ViewModels.SynthPatchViewModel(instrument.Patch, () => { }), instrument);

        values.Set("new_note", 9);

        Assert.Equal(VoiceEnding.Sustain, instrument.NewNoteAction);
    }

    /// <summary>A plugin is told to let go of the note it was holding, by name.</summary>
    [Fact]
    public void A_plugin_is_told_to_let_go_of_the_note_before()
    {
        var (mixer, ear) = Loaded();

        mixer.PluginNoteOn(0, 0, new Note(60), 1f, 0f);
        ear.Said.Clear();

        mixer.PluginNoteOn(0, 0, new Note(64), 1f, 0f);

        Assert.Equal(new[] { "off 60", "on 64" }, ear.Said);
    }

    /// <summary>Under sustain it is told nothing but the new note, so it plays a chord.</summary>
    [Fact]
    public void A_sustaining_plugin_keeps_the_note_before()
    {
        var (mixer, ear) = Loaded();

        mixer.PluginNoteOn(0, 0, new Note(60), 1f, 0f, VoiceEnding.Sustain);
        ear.Said.Clear();

        mixer.PluginNoteOn(0, 0, new Note(64), 1f, 0f, VoiceEnding.Sustain);

        Assert.Equal(new[] { "on 64" }, ear.Said);
    }

    /// <summary>And the same note arriving again is still a retrigger.</summary>
    [Fact]
    public void A_sustaining_plugin_retriggers_the_same_note()
    {
        var (mixer, ear) = Loaded();

        mixer.PluginNoteOn(0, 0, new Note(60), 1f, 0f, VoiceEnding.Sustain);
        ear.Said.Clear();

        mixer.PluginNoteOn(0, 0, new Note(60), 1f, 0f, VoiceEnding.Sustain);

        Assert.Equal(new[] { "off 60", "on 60" }, ear.Said);
    }

    /// <summary>A pattern's OFF ends everything that note column was holding, by name.</summary>
    [Fact]
    public void An_off_ends_what_its_own_column_was_holding()
    {
        var (mixer, ear) = Loaded();

        mixer.PluginNoteOn(0, 0, new Note(60), 1f, 0f, VoiceEnding.Sustain);
        mixer.PluginNoteOn(0, 0, new Note(64), 1f, 0f, VoiceEnding.Sustain);
        ear.Said.Clear();

        mixer.PluginNoteOff(0, 0);

        Assert.Equal(new[] { "off 60", "off 64" }, ear.Said);
    }

    /// <summary>
    /// And leaves the other columns of the track sounding, which is the whole reason the host
    /// keeps a record of what it said.
    /// </summary>
    /// <remarks>
    /// All notes off would take a chord down to end one note of it. There is no other way to
    /// name one note of a plugin's chord: the plugin cannot be asked what it is holding.
    /// </remarks>
    [Fact]
    public void An_off_in_one_column_leaves_the_rest_of_the_chord()
    {
        var (mixer, ear) = Loaded();

        mixer.PluginNoteOn(0, 0, new Note(60), 1f, 0f);
        mixer.PluginNoteOn(0, 1, new Note(64), 1f, 0f);
        mixer.PluginNoteOn(0, 2, new Note(67), 1f, 0f);
        ear.Said.Clear();

        mixer.PluginNoteOff(0, 1);

        Assert.Equal(new[] { "off 64" }, ear.Said);
    }

    /// <summary>
    /// A chord landing column by column does not sweep the plugin on the way in.
    /// </summary>
    /// <remarks>
    /// The sweep is the fallback for a plugin nothing here remembers having played, and a
    /// column that has not played yet is not that: the other columns of the same track are the
    /// notes of the chord being built. Read against the whole track, this was every first note
    /// of every chord taking the chord before it down.
    /// </remarks>
    [Fact]
    public void A_chord_across_columns_does_not_sweep_the_plugin()
    {
        var (mixer, ear) = Loaded();

        mixer.PluginNoteOn(0, 0, new Note(60), 1f, 0f);
        mixer.PluginNoteOn(0, 1, new Note(64), 1f, 0f);
        mixer.PluginNoteOn(0, 2, new Note(67), 1f, 0f);

        Assert.Equal(new[] { "all off", "on 60", "on 64", "on 67" }, ear.Said);
    }

    /// <summary>A note in one column makes room in that column and nowhere else.</summary>
    [Fact]
    public void A_new_note_ends_only_its_own_column()
    {
        var (mixer, ear) = Loaded();

        mixer.PluginNoteOn(0, 0, new Note(60), 1f, 0f);
        mixer.PluginNoteOn(0, 1, new Note(64), 1f, 0f);
        ear.Said.Clear();

        mixer.PluginNoteOn(0, 1, new Note(65), 1f, 0f);

        Assert.Equal(new[] { "off 64", "on 65" }, ear.Said);
    }

    /// <summary>
    /// And what it was holding is forgotten with it, so the next note lets go of nothing that
    /// has already been let go of.
    /// </summary>
    [Fact]
    public void An_off_is_the_end_of_the_record_too()
    {
        var (mixer, ear) = Loaded();

        mixer.PluginNoteOn(0, 0, new Note(60), 1f, 0f);
        mixer.PluginNoteOff(0, 0);
        ear.Said.Clear();

        mixer.PluginNoteOn(0, 0, new Note(64), 1f, 0f);

        Assert.Equal(new[] { "all off", "on 64" }, ear.Said);
    }

    /// <summary>A key coming up ends that key's note and leaves the rest of the hand alone.</summary>
    [Fact]
    public void A_key_coming_up_ends_only_its_own_note()
    {
        var (mixer, ear) = Loaded();

        mixer.PreviewOnTrack(0, new Note(60), 1f, 4);
        mixer.PreviewOnTrack(0, new Note(64), 1f, 4);
        ear.Said.Clear();

        mixer.LetPluginNote(0, 60);
        mixer.LetPluginNote(0, 72);

        Assert.Equal(new[] { "off 60" }, ear.Said);
    }

    /// <summary>A mixer with a plugin on track nought, listening.</summary>
    private static (TrackMixer Mixer, Ear Heard) Loaded()
    {
        var mixer = new TrackMixer(Rate);
        var ear = new Ear();

        mixer.SetInstrument(0, ear);
        ear.Said.Clear();

        return (mixer, ear);
    }

    /// <summary>
    /// A plugin that makes no sound and writes down what it was told.
    /// </summary>
    /// <remarks>
    /// The only way to ask this question: what a plugin does with a note is the plugin's
    /// business, and what the host must get right is which notes it sends and in which order.
    /// </remarks>
    private sealed class Ear : IPluginInstrument
    {
        /// <summary>Every note on and note off, in the order they arrived.</summary>
        public List<string> Said { get; } = new();

        /// <inheritdoc/>
        public PluginInfo Info { get; } = new("ear", "Ear", "", "", "");

        /// <inheritdoc/>
        public void NoteOn(int semitone, float velocity) => Said.Add("on " + semitone);

        /// <inheritdoc/>
        public void NoteOff(int semitone) => Said.Add("off " + semitone);

        /// <inheritdoc/>
        public void AllNotesOff() => Said.Add("all off");

        /// <inheritdoc/>
        public void Render(float[] buffer, int frames)
        {
        }

        /// <inheritdoc/>
        /// <remarks>Nothing here turns a knob, so nothing is ever raised.</remarks>
        public event Action<uint, double>? Edited { add { } remove { } }

        /// <inheritdoc/>
        public event Action? Reloaded { add { } remove { } }

        /// <inheritdoc/>
        public IReadOnlyList<PluginParameter> Parameters() => Array.Empty<PluginParameter>();

        /// <inheritdoc/>
        public double ValueOf(uint id) => 0;

        /// <inheritdoc/>
        public string TextFor(uint id, double value) => "";

        /// <inheritdoc/>
        public void SetValue(uint id, double value)
        {
        }

        /// <inheritdoc/>
        public byte[] SaveState() => Array.Empty<byte>();

        /// <inheritdoc/>
        public void LoadState(byte[]? state)
        {
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
