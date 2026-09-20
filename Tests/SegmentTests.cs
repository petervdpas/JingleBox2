using System;
using System.Linq;
using System.Text.Json;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Synth;
using JingleBox2.Tracker.Synth.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Lighttower's engine, measured rather than listened to.
///
/// The engine and not the machine. Lighttower's face stopped shipping, so what was asked of that
/// face went with it: that its eleven controls each moved the patch, and that its own presets
/// read back the two drawn waves. Those were about shipped content, and there is none to walk.
/// Everything here is about <see cref="TrackerInstrumentKind.Segments"/> itself, which is still
/// compiled in and still what every song that used it names.
/// </summary>
/// <remarks>
/// A drawn wave is arithmetic that can be checked without ears: a flat line is silence, a note
/// sweeping from a sine to a flat line gets quieter as it goes and is silent once it arrives, the
/// grit leaves every sample on an eight bit step, and a line drawn off the middle is heard centred.
/// What else is asked is what happens when the voice is handed a patch nobody meant to write.
/// </remarks>
public sealed class SegmentTests
{
    /// <summary>What everything here is rendered at.</summary>
    private const int Rate = 48000;

    /// <summary>A-4, four hundred and forty to the second.</summary>
    private static readonly Note A4 = new(57);

    /// <summary>A line of the drawing's length, each point worked out from its place.</summary>
    private static double[] Line(Func<int, double> at) =>
        Enumerable.Range(0, WaveSegments.Points).Select(at).ToArray();

    /// <summary>A patch that holds at full from the first sample, going from one line to the other.</summary>
    private static SegmentPatch Held(double[] begin, double[] end, double sweepMs = 100,
                                     SegmentMotion motion = SegmentMotion.Once, bool grit = false) => new()
    {
        Begin = begin,
        End = end,
        SweepMs = sweepMs,
        Motion = motion,
        Grit = grit,
        AttackMs = 0,
        DecayMs = 0,
        Sustain = 1,
        ReleaseMs = 20,
        Volume = 1,
    };

    /// <summary>Renders a voice for that many frames, one side only, in blocks of 512.</summary>
    private static double[] Played(SegmentVoice voice, int frames)
    {
        var heard = new double[frames];
        var block = new float[512 * 2];

        for (int at = 0; at < frames; at += 512)
        {
            int room = Math.Min(512, frames - at);

            Array.Clear(block);
            voice.Render(block, room);

            for (int i = 0; i < room; i++) heard[at + i] = block[i * 2];
        }

        return heard;
    }

    /// <summary>A voice on that patch, centred and at full gain.</summary>
    private static SegmentVoice Voice(SegmentPatch patch) => new(patch, A4, SegmentVoice.NoTrack, 1f, 0f, Rate);

    /// <summary>The loudest sample between two moments, in seconds.</summary>
    private static double Peak(double[] audio, double from, double to) =>
        audio.Skip((int)(from * Rate)).Take((int)((to - from) * Rate)).Max(Math.Abs);

    /// <summary>The average between two moments, which is how far the sound sits off nought.</summary>
    private static double Mean(double[] audio, double from, double to) =>
        audio.Skip((int)(from * Rate)).Take((int)((to - from) * Rate)).Average();

    /// <summary>A flat line from beginning to end is silence.</summary>
    [Fact]
    public void Two_flat_lines_are_silence()
    {
        var audio = Played(Voice(Held(Line(_ => 0), Line(_ => 0))), Rate / 4);

        Assert.All(audio, sample => Assert.Equal(0, sample));
    }

    /// <summary>A sine drawn at both ends is a sine at the note.</summary>
    [Fact]
    public void A_sine_at_both_ends_is_the_note()
    {
        var sine = SegmentPatch.Sine();
        var audio = Played(Voice(Held(sine, sine)), Rate);

        int crossings = 0;

        for (int at = 1; at < audio.Length; at++)
            if ((audio[at - 1] < 0) != (audio[at] < 0)) crossings++;

        Assert.InRange(crossings, 870, 890);
        Assert.InRange(Peak(audio, 0.1, 0.9), 0.95, 1.0);
    }

    /// <summary>Once, the sound goes from the first line to the last and stays there.</summary>
    [Fact]
    public void Once_arrives_at_the_end_and_stays()
    {
        var audio = Played(Voice(Held(SegmentPatch.Sine(), Line(_ => 0))), Rate / 2);

        Assert.InRange(Peak(audio, 0.000, 0.010), 0.85, 1.0);
        Assert.InRange(Peak(audio, 0.045, 0.055), 0.4, 0.6);
        Assert.True(Peak(audio, 0.11, 0.5) < 1e-9);
    }

    /// <summary>Loop goes straight back to the first line; bounce comes back the way it went.</summary>
    [Fact]
    public void Loop_starts_again_and_bounce_turns_round()
    {
        var looped = Played(Voice(Held(SegmentPatch.Sine(), Line(_ => 0), motion: SegmentMotion.Loop)), Rate / 4);
        var bounced = Played(Voice(Held(SegmentPatch.Sine(), Line(_ => 0), motion: SegmentMotion.Bounce)), Rate / 4);

        Assert.True(Peak(looped, 0.192, 0.198) < 0.1);
        Assert.InRange(Peak(bounced, 0.192, 0.198), 0.85, 1.0);

        Assert.InRange(Peak(looped, 0.102, 0.108), 0.85, 1.0);
        Assert.True(Peak(bounced, 0.098, 0.104) < 0.1);
    }

    /// <summary>Where along the way a note is, by each motion, including nonsense.</summary>
    [Fact]
    public void Along_follows_the_motion()
    {
        Assert.Equal(0.5, SegmentVoice.Along(0.5, 1, SegmentMotion.Once), 12);
        Assert.Equal(1, SegmentVoice.Along(5, 1, SegmentMotion.Once));
        Assert.Equal(0.25, SegmentVoice.Along(1.25, 1, SegmentMotion.Loop), 12);
        Assert.Equal(0.75, SegmentVoice.Along(1.25, 1, SegmentMotion.Bounce), 12);
        Assert.Equal(0, SegmentVoice.Along(double.NaN, 1, SegmentMotion.Bounce));
        Assert.Equal(0, SegmentVoice.Along(-3, 1, SegmentMotion.Loop));
        Assert.Equal(1, SegmentVoice.Along(1, 0, SegmentMotion.Once));
    }

    /// <summary>With the grit on, every sample is on one of the eight bit steps.</summary>
    [Fact]
    public void Grit_leaves_every_sample_on_an_eight_bit_step()
    {
        var gritty = Played(Voice(Held(SegmentPatch.Sine(), Line(_ => 0), sweepMs: 400, grit: true)), Rate / 4);
        var smooth = Played(Voice(Held(SegmentPatch.Sine(), Line(_ => 0), sweepMs: 400)), Rate / 4);

        Assert.All(gritty, sample =>
            Assert.True(Math.Abs((sample * WaveSegments.Steps) - Math.Round(sample * WaveSegments.Steps)) < 1e-3,
                sample + " is between two steps"));

        Assert.Contains(smooth, sample =>
            Math.Abs((sample * WaveSegments.Steps) - Math.Round(sample * WaveSegments.Steps)) > 0.05);
    }

    /// <summary>With the grit on, the wave only moves on when it comes round to its start.</summary>
    /// <remarks>
    /// Read off where the level of a cycle changes: a level taken again in the middle of a cycle is
    /// a step inside one, and at 440 to the second a cycle is a little over a hundred samples.
    /// </remarks>
    [Fact]
    public void Grit_steps_from_wave_to_wave_at_the_start_of_a_cycle()
    {
        var begin = Line(_ => 1);
        var end = Line(_ => -1);
        var patch = Held(begin, end, sweepMs: 200, grit: true);

        var buffer = new float[Rate * 2];
        var voice = new SegmentVoice(patch, A4, SegmentVoice.NoTrack, 1f, 0f, Rate);

        patch.Begin = Line(point => point < WaveSegments.Points / 2 ? 1 : -1);
        patch.End = Line(point => point < WaveSegments.Points / 2 ? 0.5 : -0.5);

        voice.Render(buffer, Rate / 5);

        var left = Enumerable.Range(0, Rate / 5).Select(at => (double)buffer[at * 2]).ToArray();

        int changes = 0;
        int inside = 0;
        double cycle = (double)Rate / 440;

        for (int at = 1; at < left.Length; at++)
        {
            if (Math.Abs(Math.Abs(left[at]) - Math.Abs(left[at - 1])) < 1e-6) continue;

            changes++;

            double place = (at / cycle) - Math.Floor(at / cycle);

            if (place > 0.05 && place < 0.95) inside++;
        }

        Assert.True(changes > 10, changes + " changes of level");
        Assert.Equal(0, inside);
    }

    /// <summary>A line drawn off the middle is heard centred on nought.</summary>
    [Fact]
    public void A_line_off_the_middle_is_heard_centred()
    {
        var lifted = Line(point => 0.5 + (0.4 * Math.Sin(2 * Math.PI * point / WaveSegments.Points)));
        var audio = Played(Voice(Held(lifted, lifted)), Rate / 2);

        Assert.InRange(Mean(audio, 0.1, 0.4), -0.01, 0.01);
        Assert.True(Peak(audio, 0.1, 0.4) < 0.5);
    }

    /// <summary>A note let go of falls away and finishes.</summary>
    [Fact]
    public void A_note_let_go_of_finishes()
    {
        var voice = Voice(Held(SegmentPatch.Sine(), SegmentPatch.Sine()));

        Played(voice, Rate / 10);
        voice.NoteOff();
        Played(voice, Rate / 10);

        Assert.True(voice.IsFinished);
        Assert.Equal(0, voice.Level);
    }

    /// <summary>A patch full of nonsense plays nothing worse than silence, and never throws.</summary>
    [Fact]
    public void A_damaged_patch_plays_nothing_it_should_not()
    {
        var patch = new SegmentPatch
        {
            Begin = null!,
            End = Line(_ => double.NaN),
            SweepMs = double.NaN,
            Volume = double.PositiveInfinity,
            TuneSemitones = double.NaN,
            Motion = (SegmentMotion)99,
            AttackMs = double.NaN,
        };

        var audio = Played(new SegmentVoice(patch, A4, SegmentVoice.NoTrack, 1f, float.NaN, Rate), Rate / 4);

        Assert.All(audio, sample => Assert.True(double.IsFinite(sample) && Math.Abs(sample) <= 1));

        var nothing = new SegmentVoice(null!, A4, SegmentVoice.NoTrack, 1f, 0f, 0);

        nothing.Render(null!, 10);
        nothing.Render(new float[3], 100);
    }

    /// <summary>Clamping puts a damaged patch back in range, lines included.</summary>
    [Fact]
    public void Clamping_mends_a_patch()
    {
        var patch = new SegmentPatch
        {
            Begin = null!,
            End = new[] { -2.0, 2.0 },
            SweepMs = 1e9,
            Sustain = -1,
            Motion = (SegmentMotion)99,
            Volume = double.NaN,
        };

        patch.Clamp();

        Assert.Equal(SegmentPatch.Sine(), patch.Begin);
        Assert.Equal(WaveSegments.Points, patch.End.Length);
        Assert.Equal(-1, patch.End[0]);
        Assert.Equal(1, patch.End[^1]);
        Assert.Equal(SegmentPatch.MostSweepMs, patch.SweepMs);
        Assert.Equal(0, patch.Sustain);
        Assert.Equal(SegmentMotion.Once, patch.Motion);
        Assert.Equal(0.25, patch.Volume);
    }

    /// <summary>A copy shares no line with what it came from, and a preset lands on the patch already held.</summary>
    [Fact]
    public void A_copy_shares_nothing_and_a_preset_lands_in_place()
    {
        var instrument = TrackerInstrument.CreateSegments("Test");
        var copy = instrument.Clone();

        copy.Segments!.Begin[3] = 0.5;
        copy.Segments.Motion = SegmentMotion.Bounce;

        Assert.NotEqual(0.5, instrument.Segments!.Begin[3]);

        var held = instrument.Segments;

        instrument.TakeSoundFrom(copy);

        Assert.Same(held, instrument.Segments);
        Assert.Equal(0.5, instrument.Segments.Begin[3]);
        Assert.Equal(SegmentMotion.Bounce, instrument.Segments.Motion);
        Assert.NotSame(copy.Segments.Begin, instrument.Segments.Begin);
        Assert.Equal(TrackerInstrumentKind.Segments, instrument.Kind);
    }

    /// <summary>Written into a file and read back, the lines and the settings come back.</summary>
    [Fact]
    public void An_instrument_survives_its_file()
    {
        var instrument = TrackerInstrument.CreateSegments("Test");

        instrument.Segments!.End = Line(point => point % 2 == 0 ? 1 : -1);
        instrument.Segments.SweepMs = 750;
        instrument.Segments.Grit = false;

        string json = JsonSerializer.Serialize(instrument);
        var back = JsonSerializer.Deserialize<TrackerInstrument>(json)!;

        Assert.Contains("\"Lighttower\"", json);
        Assert.Equal(TrackerInstrumentKind.Segments, back.Kind);
        Assert.Equal(instrument.Segments.End, back.Segments!.End);
        Assert.Equal(instrument.Segments.Begin, back.Segments.Begin);
        Assert.Equal(750, back.Segments.SweepMs);
        Assert.False(back.Segments.Grit);
    }

    /// <summary>The panel's values read and write the lines as words, and refuse words that are no line.</summary>
    [Fact]
    public void The_lines_are_words_on_the_panel()
    {
        var instrument = TrackerInstrument.CreateSegments("Test");
        var values = new SegmentValues(instrument.Segments!, instrument);
        var said = new System.Collections.Generic.List<string>();

        values.Said += said.Add;

        values.SetText(SegmentValues.EndKey, "-127 127");

        Assert.Equal(-1, instrument.Segments!.End[0]);
        Assert.Equal(1, instrument.Segments.End[^1]);
        Assert.Equal(new[] { SegmentValues.EndKey }, said);

        values.SetText(SegmentValues.EndKey, "-127 127");
        values.SetText(SegmentValues.BeginKey, "banana");
        values.SetText("nothing anybody named", "1 2 3");

        Assert.Single(said);
        Assert.Equal(SegmentPatch.Sine(), instrument.Segments.Begin);
        Assert.Equal(new WaveSegments().Spell(instrument.Segments.End), values.GetText(SegmentValues.EndKey));

        values.Set("sweep", double.NaN);
        values.Set("motion", 7);

        Assert.Equal(SegmentMotion.Bounce, instrument.Segments.Motion);
        Assert.Equal(2000, instrument.Segments.SweepMs);
    }

    /// <summary>The mixer takes a note on a patch that is not there as no note.</summary>
    [Fact]
    public void The_mixer_refuses_a_missing_patch()
    {
        var mixer = new TrackMixer(Rate);

        mixer.NoteOn(0, 0, (SegmentPatch)null!, A4, 1f, 0f);
        mixer.Preview((SegmentPatch)null!, A4, 1f, 1, "test");
        mixer.NoteOn(0, 0, new SegmentPatch(), A4, 1f, 0f);

        var buffer = new float[256 * 2];

        mixer.Render(buffer, 256);

        Assert.Contains(buffer, sample => sample != 0);
    }
}
