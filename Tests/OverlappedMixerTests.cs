using System;
using System.Collections.Generic;
using JingleBox2.Audio;
using JingleBox2.Audio.Plugins;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Synth;
using JingleBox2.Tracker.Synth.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The mixer with its plugin crossings overlapped, against the same mixer without.
/// </summary>
/// <remarks>
/// One claim and it is absolute: **the block that leaves is the same block**. What the switch
/// moves is when a plugin in its own process is asked for its audio, and two tracks were never in
/// any order to begin with, since each works on its own bus and nothing on one reads the other.
/// So the two paths are rendered side by side and compared sample for sample rather than nearly.
///
/// Built out of doubles, because a plugin cannot be started in a test and the rule has no plugin
/// in it. What the double stands for is the only thing that matters here, which is a box whose
/// work happens when it is collected rather than when it is asked.
/// </remarks>
public class OverlappedMixerTests : IDisposable
{
    /// <summary>An insert whose work happens at the collection, standing for a bridged plugin.</summary>
    private sealed class Deferred(float by, List<string>? said = null, string name = "") : IAudioInsert, IOverlappable
    {
        private bool _flying;

        public void Process(float[] buffer, int frames) => Scale(buffer, frames);

        public bool Begin(float[] buffer, int frames)
        {
            said?.Add(name + " begun");
            _flying = true;
            return true;
        }

        public bool Advance(float[] buffer, int frames)
        {
            if (!_flying) return false;

            _flying = false;
            said?.Add(name + " collected");
            Scale(buffer, frames);

            return false;
        }

        private void Scale(float[] buffer, int frames)
        {
            for (int at = 0; at < frames * 2; at++) buffer[at] *= by;
        }
    }

    private static SynthPatch Patch() => new()
    {
        Wave = SynthWave.Saw, Drive = 1, FilterCutoffHz = 20000, FilterResonance = 0,
        AttackMs = 1, DecayMs = 200, Sustain = 0.9, ReleaseMs = 200
    };

    private static float[] Rendered(bool overlap)
    {
        OverlapSwitch.Wants(overlap);

        var mixer = new TrackMixer(44100);

        for (int track = 0; track < 3; track++)
        {
            var chain = new PluginChain();

            chain.Add(new Deferred(0.5f));
            chain.Add(new Deferred(1.5f));

            mixer.SetInsert(track, chain);
            mixer.NoteOn(track, 0, Patch(), new Note(48 + track * 4), 0.8f, 0f, VoiceEnding.Sustain);
        }

        var buffer = new float[441 * 2];

        for (int at = 0; at < 20; at++) mixer.Render(buffer, 441);

        return buffer;
    }

    /// <summary>Every sample of the block is the same either way.</summary>
    [Fact]
    public void Overlapping_the_crossings_changes_no_sample()
    {
        float[] straight = Rendered(false);
        float[] overlapped = Rendered(true);

        Assert.Equal(straight, overlapped);
        Assert.Contains(straight, sample => sample != 0f);
    }

    /// <summary>
    /// With the switch on, every track's chain is started before any of them is waited for.
    /// </summary>
    /// <remarks>
    /// The whole of what this buys, and the one thing a comparison of the audio cannot see: a run
    /// that collected each track before starting the next would leave an identical block and save
    /// nothing at all. What is asserted is that the first three entries are the three beginnings.
    /// </remarks>
    [Fact]
    public void Every_track_is_started_before_any_is_waited_for()
    {
        OverlapSwitch.Wants(true);

        var said = new List<string>();
        var mixer = new TrackMixer(44100);

        for (int track = 0; track < 3; track++)
        {
            var chain = new PluginChain();

            chain.Add(new Deferred(1f, said, "track " + track));

            mixer.SetInsert(track, chain);
            mixer.NoteOn(track, 0, Patch(), new Note(48), 0.8f, 0f, VoiceEnding.Sustain);
        }

        mixer.Render(new float[441 * 2], 441);

        Assert.Equal(
            new[] { "track 0 begun", "track 1 begun", "track 2 begun" },
            said.GetRange(0, 3));

        Assert.Equal(6, said.Count);
    }

    /// <summary>With the switch off, a track is finished before the next is started.</summary>
    /// <remarks>
    /// The arrangement this application has always had, kept as the default and pinned here, so a
    /// change that quietly turned overlapping on for everybody would be caught by a test rather
    /// than by somebody's song.
    /// </remarks>
    [Fact]
    public void With_the_switch_off_the_tracks_are_taken_in_turn()
    {
        OverlapSwitch.Wants(false);

        var said = new List<string>();
        var mixer = new TrackMixer(44100);

        for (int track = 0; track < 2; track++)
        {
            var chain = new PluginChain();

            chain.Add(new Deferred(1f, said, "track " + track));

            mixer.SetInsert(track, chain);
            mixer.NoteOn(track, 0, Patch(), new Note(48), 0.8f, 0f, VoiceEnding.Sustain);
        }

        mixer.Render(new float[441 * 2], 441);

        Assert.Empty(said);
    }

    /// <summary>An insert that cannot be left in flight still gets its block.</summary>
    /// <remarks>
    /// Every effect of ours is one of these, and so is every test double written before this
    /// existed. The overlapped pass has to do them where it stands rather than skipping them,
    /// which is exactly the fault the first version of it had.
    /// </remarks>
    [Fact]
    public void An_insert_that_cannot_wait_is_still_run()
    {
        OverlapSwitch.Wants(true);

        var mixer = new TrackMixer(44100);

        var deferred = new PluginChain();
        deferred.Add(new Deferred(0f));

        var here = new PluginChain();
        here.Add(new Silencer());

        mixer.SetInsert(0, deferred);
        mixer.SetInsert(1, here);

        mixer.NoteOn(0, 0, Patch(), new Note(48), 1f, 0f, VoiceEnding.Sustain);
        mixer.NoteOn(1, 0, Patch(), new Note(52), 1f, 0f, VoiceEnding.Sustain);

        var buffer = new float[441 * 2];

        for (int at = 0; at < 10; at++) mixer.Render(buffer, 441);

        foreach (float sample in buffer) Assert.Equal(0f, sample);
    }

    /// <summary>An ordinary insert of ours: it works where it stands and is never in flight.</summary>
    private sealed class Silencer : IAudioInsert
    {
        public void Process(float[] buffer, int frames) => Array.Clear(buffer, 0, frames * 2);
    }

    /// <summary>
    /// A plugin instrument whose work happens at the collection, standing for a bridged one.
    /// </summary>
    /// <remarks>
    /// It fills the bus rather than adding to it, which is what a plugin instrument does and is
    /// the reason a track's voices cannot be played before it.
    /// </remarks>
    private sealed class Sounder(float at, List<string>? said = null, string name = "")
        : IPluginInstrument, IOverlappable
    {
        private bool _flying;

        public PluginInfo Info { get; } = new("sounder", "Sounder", "", "", "");

        public void NoteOn(int semitone, float velocity) { }
        public void NoteOff(int semitone) { }
        public void AllNotesOff() { }

        public event Action<uint, double>? Edited { add { } remove { } }
        public event Action? Reloaded { add { } remove { } }

        public IReadOnlyList<PluginParameter> Parameters() => Array.Empty<PluginParameter>();
        public double ValueOf(uint id) => 0;
        public string TextFor(uint id, double value) => "";
        public void SetValue(uint id, double value) { }
        public byte[] SaveState() => Array.Empty<byte>();
        public void LoadState(byte[]? state) { }
        public void Dispose() { }

        public void Render(float[] buffer, int frames) => Fill(buffer, frames);

        public bool Begin(float[] buffer, int frames)
        {
            said?.Add(name + " begun");
            _flying = true;
            return true;
        }

        public bool Advance(float[] buffer, int frames)
        {
            if (!_flying) return false;

            _flying = false;
            said?.Add(name + " collected");
            Fill(buffer, frames);

            return false;
        }

        private void Fill(float[] buffer, int frames)
        {
            for (int index = 0; index < frames * 2; index++) buffer[index] = at;
        }
    }

    /// <summary>
    /// An instrument on one track and an insert on another are in flight at the same time.
    /// </summary>
    /// <remarks>
    /// **The whole reason the phases went.** Every bus used to be rendered before any insert was
    /// applied, so the two crossings worth overlapping were usually on opposite sides of that
    /// boundary and waited for each other for no reason: nothing on one track reads another.
    /// What is asserted is that both are begun before either is collected, which is the one
    /// thing a comparison of the audio cannot see.
    /// </remarks>
    [Fact]
    public void An_instrument_and_another_tracks_insert_are_in_flight_together()
    {
        OverlapSwitch.Wants(true);

        var said = new List<string>();
        var mixer = new TrackMixer(44100);

        mixer.SetInstrument(0, new Sounder(0.25f, said, "instrument"));

        var chain = new PluginChain();
        chain.Add(new Deferred(1f, said, "insert"));

        mixer.SetInsert(1, chain);
        mixer.NoteOn(1, 0, Patch(), new Note(48), 0.8f, 0f, VoiceEnding.Sustain);

        mixer.Render(new float[441 * 2], 441);

        Assert.Equal(new[] { "instrument begun", "insert begun" }, said.GetRange(0, 2));
        Assert.Equal(4, said.Count);
    }

    /// <summary>
    /// A track's voices land on top of what its instrument filled the bus with, and its insert
    /// works on the two of them together.
    /// </summary>
    /// <remarks>
    /// The order within one track, which is the half of this that is not free to move: an
    /// instrument fills a bus, a voice adds to it, and an insert reads what is there. Pinned on
    /// the overlapped path, where the instrument comes back a round after it was asked for and
    /// the voices have to wait for it: playing them first would have them overwritten by the
    /// plugin and heard by nobody.
    /// </remarks>
    [Fact]
    public void Voices_are_played_after_the_instrument_and_before_the_insert()
    {
        OverlapSwitch.Wants(true);

        var mixer = new TrackMixer(44100);
        var reader = new Reader();

        mixer.SetInstrument(0, new Sounder(0.25f));

        var chain = new PluginChain();
        chain.Add(reader);

        mixer.SetInsert(0, chain);
        mixer.NoteOn(0, 0, Patch(), new Note(48), 0.8f, 0f, VoiceEnding.Sustain);

        mixer.Render(new float[441 * 2], 441);

        Assert.True(reader.Saw > 0.25f,
            "the insert read " + reader.Saw + ", which is the instrument with no voice on top of it");
    }

    /// <summary>An insert that says what the loudest thing it was handed was.</summary>
    private sealed class Reader : IAudioInsert
    {
        /// <summary>The largest sample this has ever been given.</summary>
        public float Saw { get; private set; }

        public void Process(float[] buffer, int frames)
        {
            for (int index = 0; index < frames * 2; index++)
                if (buffer[index] > Saw) Saw = buffer[index];
        }
    }

    /// <summary>
    /// With an instrument in the mix as well, every sample is still the same either way.
    /// </summary>
    /// <remarks>
    /// The identity test above has only inserts in it, which is the arrangement the phases
    /// already handled. This is the one the pipeline changed: three tracks, one of them played
    /// by a plugin instrument, all three with a chain on them.
    /// </remarks>
    [Fact]
    public void Overlapping_changes_no_sample_with_an_instrument_in_the_mix()
    {
        float[] straight = WithInstrument(false);
        float[] overlapped = WithInstrument(true);

        Assert.Equal(straight, overlapped);
        Assert.Contains(straight, sample => sample != 0f);
    }

    /// <summary>Three tracks with chains, one of them played by a plugin instrument.</summary>
    private static float[] WithInstrument(bool overlap)
    {
        OverlapSwitch.Wants(overlap);

        var mixer = new TrackMixer(44100);

        for (int track = 0; track < 3; track++)
        {
            var chain = new PluginChain();

            chain.Add(new Deferred(0.5f));
            chain.Add(new Deferred(1.5f));

            mixer.SetInsert(track, chain);
            mixer.NoteOn(track, 0, Patch(), new Note(48 + track * 4), 0.8f, 0f, VoiceEnding.Sustain);
        }

        mixer.SetInstrument(1, new Sounder(0.25f));

        var buffer = new float[441 * 2];

        for (int at = 0; at < 20; at++) mixer.Render(buffer, 441);

        return buffer;
    }

    /// <summary>
    /// A chord on one track is played onto its bus in the order the notes were taken.
    /// </summary>
    /// <remarks>
    /// The pipeline threads each track's voices onto a chain of its own so a track can be played
    /// without walking every voice in the mix, and a chain built by pushing at the head would
    /// play them backwards. **Adding floating point numbers is not associative**, so backwards is
    /// a different mix in the last few digits, which is the kind of change nobody can argue about
    /// after the fact.
    ///
    /// Three notes rather than two, and that is the whole of why the number is three: two floats
    /// added are the same either way round, so a test on a pair would pass with the chain built
    /// backwards and would be testing nothing. It was written with two first and did exactly
    /// that.
    ///
    /// Measured against the same three notes played on three tracks, which the mixer sums in
    /// track order and nothing can reorder, so the two arrangements group the additions
    /// identically.
    /// </remarks>
    [Fact]
    public void A_chord_on_one_track_keeps_the_order_the_notes_were_taken()
    {
        OverlapSwitch.Wants(false);

        var notes = new[] { new Note(48), new Note(55), new Note(62) };

        var together = new TrackMixer(44100);
        var apart = new TrackMixer(44100);

        for (int at = 0; at < notes.Length; at++)
        {
            together.NoteOn(0, at, Patch(), notes[at], 0.8f, 0f, VoiceEnding.Sustain);
            apart.NoteOn(at, 0, Patch(), notes[at], 0.8f, 0f, VoiceEnding.Sustain);
        }

        var one = new float[441 * 2];
        var three = new float[441 * 2];

        for (int at = 0; at < 10; at++)
        {
            together.Render(one, 441);
            apart.Render(three, 441);
        }

        Assert.Equal(three, one);
        Assert.Contains(one, sample => sample != 0f);
    }

    /// <summary>Puts the switch back, so a test after this one is not run under it.</summary>
    public void Dispose() => OverlapSwitch.Wants(false);
}
