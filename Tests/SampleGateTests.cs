using System;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Synth;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A recording played by hand, and what letting go of its key does to it.
/// </summary>
/// <remarks>
/// A recording that does not loop is a one-shot unless it is gated: it plays to its end however
/// briefly the key was down. Gated, the key coming up lets it go. Both are counted in voices rather
/// than listened to, since a voice still in the mixer after its key came up is exactly the
/// difference.
/// </remarks>
public sealed class SampleGateTests
{
    /// <summary>The mixer's rate.</summary>
    private const int Rate = 44100;

    /// <summary>Which panel is playing, which is what a key coming up names.</summary>
    private const string Panel = "panel";

    /// <summary>Four seconds of a steady tone, long enough to still be sounding when the test looks.</summary>
    private static SampleData Take()
    {
        var samples = new short[Rate * 4];

        for (int at = 0; at < samples.Length; at++) samples[at] = (short)(8000 * Math.Sin(at * 0.05));

        return new SampleData(samples, 1, Rate);
    }

    /// <summary>A recording instrument on that take, gated or not, with a short release.</summary>
    private static TrackerInstrument Recording(bool gate)
    {
        var instrument = TrackerInstrument.CreateSample("Piano", "piano.wav", Note.C4);

        instrument.Gate = gate;
        instrument.Patch.ReleaseMs = 20;

        return instrument;
    }

    /// <summary>Plays a note by hand, lets its key up, and renders a moment after.</summary>
    /// <returns>How many voices are still in the mixer.</returns>
    private static int LeftAfterLettingGo(bool gate)
    {
        var mixer = new TrackMixer(Rate);

        mixer.Preview(Recording(gate), Take(), Note.C4, 1f, 10, Panel);

        var block = new float[512 * 2];

        mixer.Render(block, 512);
        mixer.LetAudition(Panel, Note.C4.Semitone);

        for (int at = 0; at < 20; at++) mixer.Render(block, 512);

        return mixer.VoiceCount;
    }

    /// <summary>Without the gate, a recording plays on after its key comes up.</summary>
    [Fact]
    public void A_one_shot_plays_on_after_the_key_comes_up() => Assert.Equal(1, LeftAfterLettingGo(false));

    /// <summary>With it, the key coming up lets the recording go.</summary>
    [Fact]
    public void A_gated_recording_stops_when_the_key_comes_up() => Assert.Equal(0, LeftAfterLettingGo(true));

    /// <summary>
    /// A one-shot is held for its whole length, and a gated one only for what was asked, since its
    /// key is what lets it go.
    /// </summary>
    [Fact]
    public void A_gated_recording_is_held_for_what_was_asked()
    {
        var mixer = new TrackMixer(Rate);

        double oneShot = mixer.Preview(Recording(false), Take(), Note.C4, 1f, 0.4, Panel);
        double gated = mixer.Preview(Recording(true), Take(), Note.C4, 1f, 0.4, Panel);

        Assert.True(oneShot > 3.9, "the one-shot was held for " + oneShot);
        Assert.Equal(0.4, gated);
    }

    /// <summary>The switch on the face moves it, and it travels with a copy and with a sound taken from another.</summary>
    [Fact]
    public void The_switch_moves_it_and_it_travels()
    {
        var instrument = Recording(false);
        var values = new RecordingValues(instrument);

        values.Set("gate", 1);

        Assert.True(instrument.Gate);
        Assert.Equal(1, values.Get("gate"));
        Assert.True(instrument.Clone().Gate);

        var other = Recording(false);

        other.TakeSoundFrom(instrument);

        Assert.True(other.Gate);

        values.Set("gate", 0);

        Assert.False(instrument.Gate);
    }
}
