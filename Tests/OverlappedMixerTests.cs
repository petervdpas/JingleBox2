using System;
using System.Collections.Generic;
using JingleBox2.Audio;
using JingleBox2.Audio.Plugins;
using JingleBox2.Audio.Plugins.Interfaces;
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

    /// <summary>Puts the switch back, so a test after this one is not run under it.</summary>
    public void Dispose() => OverlapSwitch.Wants(false);
}
