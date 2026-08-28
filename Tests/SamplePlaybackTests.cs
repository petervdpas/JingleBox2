using System;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Synth;
using JingleBox2.Tracker.Synth.Interfaces;
using JingleBox2.Tracker.Synth.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The three pieces a sampled voice is made of: the recording it reads, the read head that walks
/// through it, and everything that moves the pitch away from the note that was asked for.
/// </summary>
/// <remarks>
/// All three are deliberately free of audio types so the awkward cases can be asked about
/// without a card, a file or a note. The awkward cases are the point: a take that failed to
/// decode, a window somebody dragged inside out, a loop shorter than a single step, and a step
/// worked out from a pitch nobody meant to type.
///
/// Several tests below record what the code does today rather than what it should do; each one
/// says so in its own documentation.
/// </remarks>
public class SamplePlaybackTests
{
    /// <summary>The read head, reached the way a voice reaches it.</summary>
    private readonly ISamplePlayback _play = new SamplePlayback();

    /// <summary>The pitch, reached the way a voice and a scope both reach it.</summary>
    private readonly IPitchMotion _pitch = new PitchMotion();

    /// <summary>A plain one-shot window over a hundred frames, for the head to walk about in.</summary>
    private static SampleWindow OneShot =>
        new(0, 100, 0, 100, SampleLoopMode.None, false);

    /// <summary>The same hundred frames with a forward loop set well inside them.</summary>
    private static SampleWindow Looping =>
        new(0, 100, 10, 90, SampleLoopMode.Forward, false);

    /// <summary>And with the loop turning round at each end instead of jumping.</summary>
    private static SampleWindow PingPong =>
        new(0, 100, 10, 90, SampleLoopMode.PingPong, false);

    /// <summary>A take that never decoded is empty, and everything asked of it is silence.</summary>
    /// <remarks>
    /// A null array is what a decoder that gave up hands back, and it must leave an instrument
    /// silent rather than stopping the audio thread.
    /// </remarks>
    [Fact]
    public void A_take_that_never_decoded_is_empty()
    {
        foreach (ISampleData data in new ISampleData[]
        {
            new SampleData(null!, 2, 44100),
            new SampleData(Array.Empty<short>(), 2, 44100)
        })
        {
            Assert.True(data.IsEmpty);
            Assert.Equal(0L, data.FrameCount);
            Assert.Equal(0.0, data.Seconds, 12);
            Assert.Equal(0.0, (double)data.At(0, 0), 12);
            Assert.Equal(0.0, (double)data.Between(0, 0), 12);
            Assert.Equal(0.0, (double)data.Between(5, 0), 12);
            Assert.Equal(0.0, (double)data.Between(-5, 0), 12);
        }
    }

    /// <summary>A take with no channel count and no rate is corrected rather than refused.</summary>
    [Fact]
    public void No_channels_and_no_rate_are_corrected()
    {
        ISampleData none = new SampleData(new short[] { 100, 200 }, 0, 0);

        Assert.Equal(1, none.Channels);
        Assert.Equal(44100, none.SampleRate);
        Assert.Equal(2L, none.FrameCount);

        ISampleData negative = new SampleData(new short[] { 100, 200 }, -4, -48000);

        Assert.Equal(1, negative.Channels);
        Assert.Equal(44100, negative.SampleRate);
    }

    /// <summary>A take of one frame is a take, and reads that frame wherever it is asked.</summary>
    [Fact]
    public void A_take_of_one_frame_is_still_a_take()
    {
        ISampleData data = new SampleData(new short[] { 16384 }, 1, 22050);

        Assert.False(data.IsEmpty);
        Assert.Equal(1L, data.FrameCount);
        Assert.Equal(1.0 / 22050, data.Seconds, 12);

        Assert.Equal(0.5, (double)data.At(0, 0), 6);
        Assert.Equal(0.5, (double)data.Between(0, 0), 6);
        Assert.Equal(0.5, (double)data.Between(0.5, 0), 6);
        Assert.Equal(0.5, (double)data.Between(-3, 0), 6);
        Assert.Equal(0.5, (double)data.Between(1e9, 0), 6);
    }

    /// <summary>A frame off either end of the file reads silent rather than throwing.</summary>
    [Fact]
    public void A_frame_off_either_end_reads_silent()
    {
        ISampleData data = new SampleData(new short[] { 16384, 16384, 16384 }, 1, 44100);

        Assert.Equal(0.0, (double)data.At(-1, 0), 12);
        Assert.Equal(0.0, (double)data.At(3, 0), 12);
        Assert.Equal(0.0, (double)data.At(long.MinValue, 0), 12);
        Assert.Equal(0.0, (double)data.At(long.MaxValue, 0), 12);

        Assert.Equal(0.5, (double)data.At(2, 0), 6);
    }

    /// <summary>A channel the take has not got reads the nearest one it has.</summary>
    [Fact]
    public void A_channel_the_take_has_not_got_reads_the_nearest_one()
    {
        ISampleData stereo = new SampleData(new short[] { 16384, -16384 }, 2, 44100);

        Assert.Equal(0.5, (double)stereo.At(0, 0), 6);
        Assert.Equal(-0.5, (double)stereo.At(0, 1), 6);
        Assert.Equal(-0.5, (double)stereo.At(0, 7), 6);
        Assert.Equal(0.5, (double)stereo.At(0, -3), 6);

        Assert.Equal(-0.5, (double)stereo.Between(0, 9), 6);
    }

    /// <summary>
    /// A stereo take with an odd number of values in it does not read off the end of the array.
    /// </summary>
    /// <remarks>
    /// A decoder that stopped half way through a frame leaves exactly this. The frame count is
    /// worked out by division, so the half frame is dropped and nothing reaches past it.
    /// </remarks>
    [Fact]
    public void A_ragged_stereo_take_does_not_read_off_the_end()
    {
        ISampleData ragged = new SampleData(new short[] { 100, 200, 300 }, 2, 44100);

        Assert.Equal(1L, ragged.FrameCount);
        Assert.Equal(100 / 32768.0, (double)ragged.At(0, 0), 9);
        Assert.Equal(200 / 32768.0, (double)ragged.At(0, 1), 9);
        Assert.Equal(0.0, (double)ragged.At(1, 0), 12);
    }

    /// <summary>Full scale in the file lands on full scale in the mix, both ways up.</summary>
    /// <remarks>
    /// The negative end is exactly minus one and the positive end is a hair under one, because
    /// there is one more code below nought than above it and the scale is the same for both.
    /// </remarks>
    [Fact]
    public void Full_scale_in_the_file_lands_on_full_scale()
    {
        ISampleData data = new SampleData(new short[] { short.MinValue, 0, short.MaxValue }, 1, 44100);

        Assert.Equal(-1.0, (double)data.At(0, 0), 12);
        Assert.Equal(0.0, (double)data.At(1, 0), 12);
        Assert.Equal(32767 / 32768.0, (double)data.At(2, 0), 12);
    }

    /// <summary>Between two frames is the two of them mixed in proportion.</summary>
    [Fact]
    public void Between_two_frames_is_the_mix_of_them()
    {
        ISampleData data = new SampleData(new short[] { 0, 16384 }, 1, 44100);

        Assert.Equal(0.0, (double)data.Between(0, 0), 6);
        Assert.Equal(0.125, (double)data.Between(0.25, 0), 6);
        Assert.Equal(0.25, (double)data.Between(0.5, 0), 6);
        Assert.Equal(0.5, (double)data.Between(1.0, 0), 6);
    }

    /// <summary>A read head off either end of the file is held at the end it went past.</summary>
    [Fact]
    public void A_read_head_off_either_end_is_held_at_that_end()
    {
        ISampleData data = new SampleData(new short[] { -16384, 0, 16384 }, 1, 44100);

        Assert.Equal(-0.5, (double)data.Between(-100, 0), 6);
        Assert.Equal(-0.5, (double)data.Between(double.NegativeInfinity, 0), 6);
        Assert.Equal(0.5, (double)data.Between(1e9, 0), 6);
        Assert.Equal(0.5, (double)data.Between(double.PositiveInfinity, 0), 6);
    }

    /// <summary>A read head that is not a number reads back as not a number.</summary>
    /// <remarks>
    /// This records what the code does today rather than what it should do. Both ends are
    /// guarded by comparisons, and every comparison against NaN is false, so a NaN position
    /// falls through to the interpolation and comes out of it as NaN. The two ends catch a
    /// position that has run off the file and do not catch this one.
    /// </remarks>
    [Fact]
    public void A_read_head_that_is_not_a_number_reads_back_the_same()
    {
        ISampleData data = new SampleData(new short[] { -16384, 0, 16384 }, 1, 44100);

        Assert.True(float.IsNaN(data.Between(double.NaN, 0)));
    }

    /// <summary>An instrument with no window at all plays the whole file, forwards and once.</summary>
    [Fact]
    public void No_window_at_all_is_the_whole_file()
    {
        SampleWindow window = _play.WindowFor(null, 1000);

        Assert.Equal(0.0, window.Start, 9);
        Assert.Equal(999.0, window.End, 9);
        Assert.Equal(0.0, window.LoopStart, 9);
        Assert.Equal(999.0, window.LoopEnd, 9);
        Assert.Equal(SampleLoopMode.None, window.Loop);
        Assert.False(window.Reverse);
        Assert.False(window.IsLooping);
        Assert.Equal(0.0, window.Entry, 9);
        Assert.Equal(1, window.Direction);
    }

    /// <summary>A file of nothing, of one frame, or of a negative length is a window of nothing.</summary>
    /// <remarks>
    /// One frame counts as nothing here because the interpolation reads the frame after the one
    /// it is on, so a file that cannot hold two positions has nothing to play between.
    /// </remarks>
    [Fact]
    public void A_file_with_nothing_in_it_is_a_window_of_nothing()
    {
        foreach (long frames in new long[] { 0, 1, -5 })
        {
            SampleWindow window = _play.WindowFor(null, frames);

            Assert.Equal(0.0, window.Start, 9);
            Assert.Equal(0.0, window.End, 9);
            Assert.Equal(0.0, window.LoopStart, 9);
            Assert.Equal(0.0, window.LoopEnd, 9);
            Assert.False(window.IsLooping);
        }
    }

    /// <summary>A window read backwards starts at the far end and walks towards the near one.</summary>
    [Fact]
    public void A_window_read_backwards_starts_at_the_far_end()
    {
        SampleWindow window = _play.WindowFor(new SampleShape { Reverse = true }, 1000);

        Assert.True(window.Reverse);
        Assert.Equal(999.0, window.Entry, 9);
        Assert.Equal(-1, window.Direction);
    }

    /// <summary>A window whose ends are the wrong way round is straightened rather than kept.</summary>
    [Fact]
    public void A_window_the_wrong_way_round_is_straightened()
    {
        SampleWindow window = _play.WindowFor(new SampleShape { Start = 0.9, End = 0.1 }, 1001);

        Assert.Equal(100.0, window.Start, 6);
        Assert.Equal(900.0, window.End, 6);
    }

    /// <summary>A window with nothing in it opens back out to the whole file.</summary>
    /// <remarks>
    /// Almost always a mistake with the handles rather than a request for silence, and a silent
    /// instrument with no way to say why is the worst of the two answers.
    /// </remarks>
    [Fact]
    public void A_window_with_nothing_in_it_opens_back_out()
    {
        SampleWindow empty = _play.WindowFor(new SampleShape { Start = 0.5, End = 0.5 }, 1001);

        Assert.Equal(0.0, empty.Start, 6);
        Assert.Equal(1000.0, empty.End, 6);

        SampleWindow tiny = _play.WindowFor(new SampleShape { Start = 0, End = 0.0005 }, 1001);

        Assert.Equal(0.0, tiny.Start, 6);
        Assert.Equal(1000.0, tiny.End, 6);
    }

    /// <summary>A window of exactly one frame is the shortest one that is kept.</summary>
    [Fact]
    public void A_window_of_exactly_one_frame_is_kept()
    {
        SampleWindow window = _play.WindowFor(new SampleShape { Start = 0, End = 0.001 }, 1001);

        Assert.Equal(0.0, window.Start, 9);
        Assert.Equal(1.0, window.End, 9);
    }

    /// <summary>
    /// Reopening a window that had nothing in it opens the loop out with it.
    /// </summary>
    /// <remarks>
    /// The loop used to be left where the old window had put it. The window opens back out to
    /// the whole file, which is what it says it does; the loop was then merely held inside the
    /// new window rather than opened out with it, so a shape whose handles had been dragged
    /// together came back playing the whole file with a half frame loop at its very start, and
    /// a forward loop on one is a voice that never leaves the first frame. Nothing reaches this
    /// from the editor, which does not let the handles cross; a preset or a song written by
    /// hand does.
    /// </remarks>
    [Fact]
    public void Reopening_a_window_reopens_the_loop_with_it()
    {
        SampleWindow window = _play.WindowFor(
            new SampleShape { Start = 0, End = 0.0005, LoopMode = SampleLoopMode.Forward },
            1001);

        Assert.Equal(0.0, window.Start, 6);
        Assert.Equal(1000.0, window.End, 6);
        Assert.Equal(0.0, window.LoopStart, 6);
        Assert.Equal(1000.0, window.LoopEnd, 6);
        Assert.True(window.IsLooping);
    }

    /// <summary>A loop with nothing in it to play is the whole window, not a buzz at one frame.</summary>
    /// <remarks>
    /// The window itself is wide open here, so the only thing collapsed is the loop. Shorter
    /// than a frame there is nothing between the two marks to read, and
    /// <see cref="SampleWindow.IsLooping"/> only refuses a loop of no length at all, so half a
    /// frame passed as a real loop and the voice repeated inside it until the note was let go.
    /// </remarks>
    [Fact]
    public void A_loop_shorter_than_a_frame_becomes_the_whole_window()
    {
        SampleWindow window = _play.WindowFor(
            new SampleShape
            {
                Start = 0,
                End = 1,
                LoopStart = 0.4,
                LoopEnd = 0.4004,
                LoopMode = SampleLoopMode.Forward
            },
            1001);

        Assert.Equal(0.0, window.LoopStart, 6);
        Assert.Equal(1000.0, window.LoopEnd, 6);
        Assert.True(window.IsLooping);
    }

    /// <summary>The loop is held inside the window, whatever the shape asked for.</summary>
    [Fact]
    public void The_loop_is_held_inside_the_window()
    {
        SampleWindow window = _play.WindowFor(
            new SampleShape
            {
                Start = 0.2,
                End = 0.8,
                LoopMode = SampleLoopMode.Forward,
                LoopStart = 0,
                LoopEnd = 1
            },
            1001);

        Assert.Equal(200.0, window.Start, 6);
        Assert.Equal(800.0, window.End, 6);
        Assert.Equal(200.0, window.LoopStart, 6);
        Assert.Equal(800.0, window.LoopEnd, 6);
    }

    /// <summary>Positions that are not numbers fall back to the ends of the file.</summary>
    [Fact]
    public void Handles_that_are_not_numbers_read_as_the_whole_file()
    {
        SampleWindow window = _play.WindowFor(
            new SampleShape { Start = double.NaN, End = double.NaN },
            1001);

        Assert.Equal(0.0, window.Start, 6);
        Assert.Equal(1000.0, window.End, 6);
    }

    /// <summary>A step of nought moves nothing and does not end the note.</summary>
    /// <remarks>
    /// A step is a pitch expressed as a speed, so nought is a note nobody can hear rather than a
    /// note that is over. What matters is that the call returns: a voice holding a stopped head
    /// is silent, and a loop that never returned would take the audio thread with it.
    /// </remarks>
    [Fact]
    public void A_step_of_nought_moves_nothing()
    {
        double position = 50;
        int direction = 1;

        for (int i = 0; i < 1000; i++)
            Assert.True(_play.Advance(ref position, ref direction, 0, OneShot));

        Assert.Equal(50.0, position, 12);
        Assert.Equal(1, direction);
    }

    /// <summary>A negative step is ignored entirely; backwards is the direction's job.</summary>
    [Fact]
    public void A_negative_step_is_ignored()
    {
        double position = 50;
        int direction = 1;

        Assert.True(_play.Advance(ref position, ref direction, -10, OneShot));

        Assert.Equal(50.0, position, 12);
        Assert.Equal(1, direction);
    }

    /// <summary>A one-shot walks forwards and is over when it leaves the window.</summary>
    [Fact]
    public void A_one_shot_ends_when_it_leaves_the_window()
    {
        double position = 50;
        int direction = 1;

        Assert.True(_play.Advance(ref position, ref direction, 10, OneShot));
        Assert.Equal(60.0, position, 12);

        position = 100;
        Assert.False(_play.Advance(ref position, ref direction, 1, OneShot));

        position = 95;
        Assert.True(_play.Advance(ref position, ref direction, 5, OneShot));
        Assert.Equal(100.0, position, 12);
    }

    /// <summary>And backwards it is over when it walks off the near end.</summary>
    [Fact]
    public void A_one_shot_played_backwards_ends_at_the_near_end()
    {
        double position = 50;
        int direction = -1;

        Assert.True(_play.Advance(ref position, ref direction, 10, OneShot));
        Assert.Equal(40.0, position, 12);

        position = 5;
        Assert.False(_play.Advance(ref position, ref direction, 10, OneShot));
    }

    /// <summary>A direction of nought counts as forwards rather than as standing still.</summary>
    [Fact]
    public void A_direction_of_nought_counts_as_forwards()
    {
        double position = 50;
        int direction = 0;

        Assert.True(_play.Advance(ref position, ref direction, 10, OneShot));

        Assert.Equal(60.0, position, 12);
        Assert.Equal(0, direction);
    }

    /// <summary>A forward loop comes back round to its own start, however far past the end it went.</summary>
    [Fact]
    public void A_forward_loop_comes_back_round()
    {
        double position = 85;
        int direction = 1;

        Assert.True(_play.Advance(ref position, ref direction, 10, Looping));
        Assert.Equal(15.0, position, 12);
        Assert.Equal(1, direction);

        position = 10;
        Assert.True(_play.Advance(ref position, ref direction, 1000, Looping));
        Assert.Equal(50.0, position, 12);
    }

    /// <summary>A forward loop reached from behind comes round the other way.</summary>
    /// <remarks>
    /// Reachable because the window can be read backwards, which sets the direction and then
    /// leaves the loop to do what it does.
    /// </remarks>
    [Fact]
    public void A_forward_loop_walked_backwards_comes_round_too()
    {
        double position = 15;
        int direction = -1;

        Assert.True(_play.Advance(ref position, ref direction, 10, Looping));

        Assert.Equal(85.0, position, 12);
        Assert.Equal(-1, direction);
    }

    /// <summary>A ping-pong turns round at each end rather than jumping to the other one.</summary>
    [Fact]
    public void A_ping_pong_turns_round_at_both_ends()
    {
        double position = 85;
        int direction = 1;

        Assert.True(_play.Advance(ref position, ref direction, 10, PingPong));
        Assert.Equal(85.0, position, 12);
        Assert.Equal(-1, direction);

        position = 15;
        Assert.True(_play.Advance(ref position, ref direction, 10, PingPong));
        Assert.Equal(15.0, position, 12);
        Assert.Equal(1, direction);
    }

    /// <summary>
    /// A step longer than the whole loop stays inside it rather than walking out of the window.
    /// </summary>
    /// <remarks>
    /// A very high note on a very short loop is exactly this, and without the reflection being
    /// held the voice leaves the window on its first step and is never heard from again.
    /// </remarks>
    [Fact]
    public void A_step_longer_than_the_loop_stays_inside_it()
    {
        var window = new SampleWindow(0, 100, 0, 10, SampleLoopMode.PingPong, false);

        double position = 0;
        int direction = 1;

        for (int i = 0; i < 1000; i++)
        {
            Assert.True(_play.Advance(ref position, ref direction, 1000, window));

            Assert.True(double.IsFinite(position));
            Assert.True(position >= window.LoopStart);
            Assert.True(position <= window.LoopEnd);
        }
    }

    /// <summary>The same for a forward loop, which wraps rather than reflecting.</summary>
    [Fact]
    public void A_long_step_through_a_short_forward_loop_stays_inside_it()
    {
        var window = new SampleWindow(0, 100, 0, 10, SampleLoopMode.Forward, false);

        double position = 0;
        int direction = 1;

        for (int i = 0; i < 1000; i++)
        {
            Assert.True(_play.Advance(ref position, ref direction, 997.5, window));

            Assert.True(position >= window.LoopStart);
            Assert.True(position <= window.LoopEnd);
        }
    }

    /// <summary>A loop with no length at all is not a loop, and the window plays once.</summary>
    /// <remarks>
    /// It would be a division by nought and a note that never ended, which is why the window
    /// answers the question with the length rather than with the mode.
    /// </remarks>
    [Fact]
    public void A_loop_with_no_length_is_a_one_shot()
    {
        var window = new SampleWindow(0, 100, 50, 50, SampleLoopMode.Forward, false);

        Assert.False(window.IsLooping);

        double position = 99;
        int direction = 1;

        Assert.False(_play.Advance(ref position, ref direction, 5, window));
    }

    /// <summary>A loop shorter than a single frame still holds the head inside itself.</summary>
    [Fact]
    public void A_loop_shorter_than_a_frame_still_holds_the_head()
    {
        var window = new SampleWindow(0, 100, 50, 50.5, SampleLoopMode.Forward, false);

        Assert.True(window.IsLooping);

        double position = 50;
        int direction = 1;

        for (int i = 0; i < 1000; i++)
        {
            Assert.True(_play.Advance(ref position, ref direction, 0.3, window));

            Assert.True(position >= 50);
            Assert.True(position <= 50.5);
        }
    }

    /// <summary>A window built inside out by hand ends the note at once rather than playing it.</summary>
    /// <remarks>
    /// Nothing from <see cref="ISamplePlayback.WindowFor"/> can be in this state, since a shape
    /// is straightened on the way through. A window built anywhere else can be.
    /// </remarks>
    [Fact]
    public void A_window_built_inside_out_ends_at_once()
    {
        var window = new SampleWindow(100, 0, 0, 0, SampleLoopMode.None, false);

        double position = 50;
        int direction = 1;

        Assert.False(_play.Advance(ref position, ref direction, 1, window));
    }

    /// <summary>A step that is not a finite number ends the note, looping or not.</summary>
    /// <remarks>
    /// A one-shot used to come off best by accident: both of its bounds are comparisons, both
    /// are false for NaN, so the note was simply over. A looping window asked the same kind of
    /// question, got the same false, and reported that there was more to play with a read head
    /// that was nowhere, so the voice read silence for ever and never ended itself. A step is a
    /// pitch ratio times a rate ratio, so a tuning read off a damaged file is the way in, and
    /// the position is left where it was rather than being made nonsense along with the step.
    /// </remarks>
    [Fact]
    public void A_step_that_is_not_a_finite_number_ends_the_note()
    {
        double oneShot = 50;
        int forwards = 1;

        Assert.False(_play.Advance(ref oneShot, ref forwards, double.NaN, OneShot));
        Assert.Equal(50.0, oneShot, 12);

        double looping = 50;
        int direction = 1;

        Assert.False(_play.Advance(ref looping, ref direction, double.NaN, Looping));
        Assert.Equal(50.0, looping, 12);

        double endless = 50;
        Assert.False(_play.Advance(ref endless, ref direction, double.PositiveInfinity, Looping));
        Assert.Equal(50.0, endless, 12);
    }

    /// <summary>No patch at all is no tuning and no movement.</summary>
    [Fact]
    public void No_patch_at_all_is_no_movement()
    {
        Assert.Equal(0.0, _pitch.Tuning(null!), 12);
        Assert.Equal(0.0, _pitch.MotionAt(null!, 0.5), 12);
    }

    /// <summary>The tuning is the coarse control and the fine one added together.</summary>
    [Fact]
    public void The_tuning_is_the_coarse_and_the_fine_together()
    {
        Assert.Equal(0.0, _pitch.Tuning(new SynthPatch()), 12);

        Assert.Equal(
            2.5,
            _pitch.Tuning(new SynthPatch { TuneSemitones = 3, FineCents = -50 }),
            12);

        Assert.Equal(
            SynthPatch.MinTuneSemitones + SynthPatch.MinFineCents / 100.0,
            _pitch.Tuning(new SynthPatch
            {
                TuneSemitones = SynthPatch.MinTuneSemitones,
                FineCents = SynthPatch.MinFineCents
            }),
            12);
    }

    /// <summary>A patch with neither the vibrato nor the pitch envelope on does not move at all.</summary>
    [Fact]
    public void A_patch_with_nothing_switched_on_does_not_move()
    {
        var patch = new SynthPatch();

        Assert.Equal(0.0, _pitch.MotionAt(patch, 0.0), 12);
        Assert.Equal(0.0, _pitch.MotionAt(patch, 0.5), 12);
        Assert.Equal(0.0, _pitch.MotionAt(patch, 100.0), 12);
    }

    /// <summary>The vibrato starts at nothing and swings the same distance either side.</summary>
    [Fact]
    public void The_vibrato_swings_both_ways_from_nothing()
    {
        var patch = new SynthPatch { VibratoRateHz = 1, VibratoDepthCents = 100 };

        Assert.Equal(0.0, _pitch.MotionAt(patch, 0.0), 9);
        Assert.Equal(1.0, _pitch.MotionAt(patch, 0.25), 9);
        Assert.Equal(0.0, _pitch.MotionAt(patch, 0.5), 9);
        Assert.Equal(-1.0, _pitch.MotionAt(patch, 0.75), 9);
    }

    /// <summary>A rate or a depth of nought, or below it, is the vibrato switched off.</summary>
    [Fact]
    public void A_vibrato_at_nought_or_below_is_switched_off()
    {
        Assert.Equal(
            0.0,
            _pitch.MotionAt(new SynthPatch { VibratoRateHz = 0, VibratoDepthCents = 100 }, 0.25),
            12);

        Assert.Equal(
            0.0,
            _pitch.MotionAt(new SynthPatch { VibratoRateHz = 5, VibratoDepthCents = 0 }, 0.25),
            12);

        Assert.Equal(
            0.0,
            _pitch.MotionAt(new SynthPatch { VibratoRateHz = -5, VibratoDepthCents = 100 }, 0.25),
            12);

        Assert.Equal(
            0.0,
            _pitch.MotionAt(new SynthPatch { VibratoRateHz = 5, VibratoDepthCents = -100 }, 0.25),
            12);
    }

    /// <summary>The pitch envelope falls in a straight line from its offset to nothing.</summary>
    [Fact]
    public void The_pitch_envelope_falls_from_its_offset_to_nothing()
    {
        var patch = new SynthPatch { PitchEnvSemitones = -12, PitchEnvMs = 100 };

        Assert.Equal(-12.0, _pitch.MotionAt(patch, 0.0), 9);
        Assert.Equal(-6.0, _pitch.MotionAt(patch, 0.05), 9);
        Assert.Equal(-1.2, _pitch.MotionAt(patch, 0.09), 9);
    }

    /// <summary>And it is over exactly at its own length, not somewhere after it.</summary>
    [Fact]
    public void The_pitch_envelope_is_over_at_its_own_length()
    {
        var patch = new SynthPatch { PitchEnvSemitones = 12, PitchEnvMs = 100 };

        Assert.Equal(0.0, _pitch.MotionAt(patch, 0.1), 12);
        Assert.Equal(0.0, _pitch.MotionAt(patch, 0.2), 12);
        Assert.Equal(0.0, _pitch.MotionAt(patch, 1000.0), 12);
    }

    /// <summary>A pitch envelope with no length, or a negative one, is no envelope at all.</summary>
    [Fact]
    public void A_pitch_envelope_with_no_length_is_no_envelope()
    {
        Assert.Equal(
            0.0,
            _pitch.MotionAt(new SynthPatch { PitchEnvSemitones = 12, PitchEnvMs = 0 }, 0.0),
            12);

        Assert.Equal(
            0.0,
            _pitch.MotionAt(new SynthPatch { PitchEnvSemitones = 12, PitchEnvMs = -100 }, 0.0),
            12);

        Assert.Equal(
            0.0,
            _pitch.MotionAt(new SynthPatch { PitchEnvSemitones = 0, PitchEnvMs = 100 }, 0.0),
            12);
    }

    /// <summary>A moment before the note began reads as the start of it.</summary>
    /// <remarks>
    /// It used to bend the pitch further than the control allows. The envelope is a straight
    /// line worked out from how far through it the moment is, and a moment before the start is
    /// more than none of the way through, so at one envelope length before the note a two
    /// octave drop read as four and it ran away without limit from there. Nothing in the voice
    /// asks for a negative time, since a note's clock starts at nought; a scope drawing a
    /// moment either side of the start does.
    /// </remarks>
    [Fact]
    public void A_moment_before_the_note_began_reads_as_the_start_of_it()
    {
        var patch = new SynthPatch { PitchEnvSemitones = 12, PitchEnvMs = 100 };

        double start = _pitch.MotionAt(patch, 0);

        Assert.Equal(12.0, start, 9);
        Assert.Equal(start, _pitch.MotionAt(patch, -0.1), 9);
        Assert.Equal(start, _pitch.MotionAt(patch, -1.0), 9);
        Assert.Equal(start, _pitch.MotionAt(patch, double.NegativeInfinity), 9);
    }

    /// <summary>A moment that is not a number reads as the start of the note.</summary>
    /// <remarks>
    /// The envelope's guard was a comparison and let it through as no movement, which was right
    /// by accident; the vibrato had no guard at all, and the sine of NaN is NaN, so a patch
    /// with vibrato on it answered with a pitch nobody can play.
    /// </remarks>
    [Fact]
    public void A_moment_that_is_not_a_number_reads_as_the_start_of_the_note()
    {
        var envelopeOnly = new SynthPatch { PitchEnvSemitones = 12, PitchEnvMs = 100 };
        Assert.Equal(_pitch.MotionAt(envelopeOnly, 0), _pitch.MotionAt(envelopeOnly, double.NaN), 12);

        var vibrato = new SynthPatch { VibratoRateHz = 5, VibratoDepthCents = 100 };
        Assert.Equal(0.0, _pitch.MotionAt(vibrato, double.NaN), 12);
        Assert.Equal(0.0, _pitch.MotionAt(vibrato, double.PositiveInfinity), 12);
    }

    /// <summary>A dozen semitones is an octave, which is a doubling, in both directions.</summary>
    [Fact]
    public void A_dozen_semitones_is_a_doubling()
    {
        Assert.Equal(1.0, _pitch.Ratio(0), 12);
        Assert.Equal(2.0, _pitch.Ratio(12), 12);
        Assert.Equal(0.5, _pitch.Ratio(-12), 12);
        Assert.Equal(4.0, _pitch.Ratio(24), 12);
        Assert.Equal(0.25, _pitch.Ratio(-24), 12);
        Assert.Equal(1.498307, _pitch.Ratio(7), 6);
    }

    /// <summary>An offset far past anything a patch can hold runs off to infinity or to nought.</summary>
    /// <remarks>
    /// This records what the code does today rather than what it should do. Nothing here is
    /// bounded, because the ends belong to the patch and this is the arithmetic underneath
    /// them: <see cref="SynthPatch.MaxTuneSemitones"/> is two octaves and the controls stop
    /// there. A ratio of infinity is a step of infinity, which is a phase that never comes back.
    /// </remarks>
    [Fact]
    public void An_offset_far_past_the_controls_runs_off_the_end()
    {
        Assert.True(double.IsPositiveInfinity(_pitch.Ratio(1e6)));
        Assert.Equal(0.0, _pitch.Ratio(-1e6), 12);
        Assert.True(double.IsNaN(_pitch.Ratio(double.NaN)));
        Assert.True(double.IsPositiveInfinity(_pitch.Ratio(double.PositiveInfinity)));
    }
}
