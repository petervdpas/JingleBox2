using System;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The controls that give each effect its character, measured rather than listened to.
/// </summary>
/// <remarks>
/// Every one of them starts at a setting that leaves the effect exactly as it was, which is what
/// the tests beside each engine already pin: those tests set nothing new and still pass. What is
/// asked here is the other half, that each new control does the one thing it says when it is
/// turned, and that nothing it can be set to hands back something that is not a number.
/// </remarks>
public sealed class EffectCharacterTests
{
    /// <summary>What everything here is measured at.</summary>
    private const int Rate = 48000;

    /// <summary>Runs a signal through in blocks and hands back what came out.</summary>
    /// <param name="effect">The effect under test.</param>
    /// <param name="frames">How many frames to render.</param>
    /// <param name="block">How many at a time.</param>
    /// <param name="left">What the left channel holds at each frame.</param>
    /// <param name="right">What the right holds, or the same as the left.</param>
    private static float[] Through(ISoundEffectEngine effect, int frames, int block,
                                   Func<int, float> left, Func<int, float>? right = null)
    {
        var caught = new float[frames * 2];
        var buffer = new float[block * 2];

        for (int at = 0; at < frames; at += block)
        {
            int room = Math.Min(block, frames - at);

            Array.Clear(buffer);

            for (int i = 0; i < room; i++)
            {
                buffer[i * 2] = left(at + i);
                buffer[(i * 2) + 1] = (right ?? left)(at + i);
            }

            effect.Process(buffer, room);

            Array.Copy(buffer, 0, caught, at * 2, room * 2);
        }

        return caught;
    }

    /// <summary>A tone at that frequency and size.</summary>
    private static Func<int, float> Tone(double hertz, double size = 1) =>
        at => (float)(size * Math.Sin(2 * Math.PI * hertz * at / Rate));

    /// <summary>Noise that is the same every run.</summary>
    private static Func<int, float> Noise(double size = 1)
    {
        var random = new Random(5);
        var held = new float[Rate];

        for (int at = 0; at < held.Length; at++) held[at] = (float)(((random.NextDouble() * 2) - 1) * size);

        return at => held[at % held.Length];
    }

    /// <summary>The louder part of one side between two frames: the frame and how loud.</summary>
    private static (int At, double Level) Loudest(float[] audio, int side, int from, int to)
    {
        int where = from;
        double most = 0;

        for (int at = from; at < to; at++)
        {
            double level = Math.Abs(audio[(at * 2) + side]);

            if (level <= most) continue;

            most = level;
            where = at;
        }

        return (where, most);
    }

    /// <summary>The root mean square of one side between two frames.</summary>
    private static double Loudness(float[] audio, int side, int from, int to)
    {
        double sum = 0;

        for (int at = from; at < to; at++) sum += audio[(at * 2) + side] * (double)audio[(at * 2) + side];

        return Math.Sqrt(sum / Math.Max(1, to - from));
    }

    /// <summary>How many times one side crosses nought between two frames.</summary>
    private static int Crossings(float[] audio, int side, int from, int to)
    {
        int count = 0;

        for (int at = from + 1; at < to; at++)
            if ((audio[((at - 1) * 2) + side] < 0) != (audio[(at * 2) + side] < 0)) count++;

        return count;
    }

    /// <summary>Every frame of both sides is a number.</summary>
    private static void AllNumbers(float[] audio) => Assert.All(audio, sample => Assert.True(float.IsFinite(sample)));

    /// <summary>An echo at a tenth of a second with nothing in its way.</summary>
    private static Delay Echo(double feedback = 0)
    {
        var delay = new Delay(Rate);

        delay.SetValue(Delay.Time, 100);
        delay.SetValue(Delay.Feedback, feedback);
        delay.SetValue(Delay.Mix, 1);
        delay.SetValue(Delay.Damp, 0);

        return delay;
    }

    /// <summary>Ping-pong puts the first repeat on the left and the next on the right.</summary>
    [Fact]
    public void Ping_pong_puts_the_first_repeat_left_and_the_next_right()
    {
        var delay = Echo(0.5);

        delay.SetValue(Delay.Ping, 1);

        var caught = Through(delay, 12000, 512, at => at == 0 ? 1f : 0f);

        var first = Loudest(caught, 0, 4000, 6000);
        var second = Loudest(caught, 1, 8000, 11000);

        Assert.Equal(4800, first.At);
        Assert.Equal(1, first.Level, 3);
        Assert.True(Loudest(caught, 1, 4000, 6000).Level < 1e-6);

        Assert.Equal(9600, second.At);
        Assert.Equal(0.5, second.Level, 3);
        Assert.True(Loudest(caught, 0, 8000, 11000).Level < 1e-6);
    }

    /// <summary>Straight keeps a repeat on the side it arrived on.</summary>
    [Fact]
    public void Straight_keeps_each_side_on_its_side()
    {
        var caught = Through(Echo(0.5), 12000, 512, at => at == 0 ? 1f : 0f, _ => 0f);

        Assert.True(Loudest(caught, 1, 0, 12000).Level == 0);
        Assert.Equal(1, Loudest(caught, 0, 4000, 6000).Level, 3);
    }

    /// <summary>Wow moves where a repeat lands, and without it every repeat lands in the same place.</summary>
    [Fact]
    public void Wow_moves_where_the_repeat_lands()
    {
        int Gap(double wow)
        {
            var delay = Echo();

            delay.SetValue(Delay.Wow, wow);

            var caught = Through(delay, 48000, 512, at => at is 0 or 36000 ? 1f : 0f);

            int first = Loudest(caught, 0, 1, 20000).At;
            int second = Loudest(caught, 0, 36001, 48000).At - 36000;

            return first - second;
        }

        Assert.Equal(0, Gap(0));
        Assert.True(Math.Abs(Gap(1)) > 10, "wow moved a repeat by " + Gap(1) + " frames");
    }

    /// <summary>Grit holds a feedback that would otherwise pile up.</summary>
    [Fact]
    public void Grit_holds_a_feedback_that_would_pile_up()
    {
        double Peak(double grit)
        {
            var delay = new Delay(Rate);

            delay.SetValue(Delay.Time, 10);
            delay.SetValue(Delay.Feedback, Delay.MostFeedback);
            delay.SetValue(Delay.Mix, 1);
            delay.SetValue(Delay.Damp, 0);
            delay.SetValue(Delay.Grit, grit);

            var caught = Through(delay, Rate * 2, 512, Tone(100));

            AllNumbers(caught);

            return Loudest(caught, 0, Rate, Rate * 2).Level;
        }

        Assert.True(Peak(0) > 3, "without grit the feedback reached " + Peak(0));
        Assert.True(Peak(1) < 1.5, "with grit the feedback reached " + Peak(1));
    }

    /// <summary>A sweep with its cutoff at two hundred and the given follow and swing.</summary>
    private static Sweep Filter(double cutoff, double follow = 0, double swing = 0, double speed = 1)
    {
        var sweep = new Sweep(Rate);

        sweep.SetValue(Sweep.Cutoff, cutoff);
        sweep.SetValue(Sweep.Follow, follow);
        sweep.SetValue(Sweep.Swing, swing);
        sweep.SetValue(Sweep.SwingRate, speed);

        return sweep;
    }

    /// <summary>Follow opens the filter on a loud note and leaves it shut on a quiet one.</summary>
    [Fact]
    public void Follow_opens_the_filter_on_a_loud_note()
    {
        double Through_(double follow, double size)
        {
            var caught = Through(Filter(200, follow), Rate / 2, 512, Tone(3000, size));

            return Loudness(caught, 0, Rate / 4, Rate / 2) / (size / Math.Sqrt(2));
        }

        Assert.Equal(Through_(0, 1), Through_(0, 0.05), 3);
        Assert.True(Through_(4, 1) > Through_(4, 0.05) * 3,
            "loud let through " + Through_(4, 1) + " and quiet " + Through_(4, 0.05));
    }

    /// <summary>Swing moves the cutoff over time, and without it the level stands still.</summary>
    [Fact]
    public void Swing_moves_the_cutoff_over_time()
    {
        (double Least, double Most) Spread(double swing)
        {
            var caught = Through(Filter(800, 0, swing, 2), Rate, 512, Tone(1600));
            double least = double.MaxValue, most = 0;

            for (int at = Rate / 4; at + 1200 <= Rate; at += 1200)
            {
                double level = Loudness(caught, 0, at, at + 1200);

                least = Math.Min(least, level);
                most = Math.Max(most, level);
            }

            return (least, most);
        }

        var still = Spread(0);
        var swung = Spread(2);

        Assert.True(still.Most / still.Least < 1.01);
        Assert.True(swung.Most / swung.Least > 2, "the swing moved the level by " + (swung.Most / swung.Least));
    }

    /// <summary>A moving cutoff comes out the same however the audio is cut into blocks.</summary>
    [Fact]
    public void A_moving_cutoff_is_the_same_in_any_block_size()
    {
        var whole = Through(Filter(900, 2, 2, 3), 20000, 20000, Noise(0.7));
        var pieces = Through(Filter(900, 2, 2, 3), 20000, 37, Noise(0.7));

        for (int at = 0; at < whole.Length; at++) Assert.Equal(whole[at], pieces[at], 1e-6);
    }

    /// <summary>The three curves are the three shapes they say.</summary>
    [Fact]
    public void The_three_curves_are_warm_hard_and_folded()
    {
        Assert.Equal(Math.Tanh(3), Drive.Curve(3, 0), 9);
        Assert.Equal(1, Drive.Curve(3, 1));
        Assert.Equal(-1, Drive.Curve(-30, 1));
        Assert.Equal(0.4, Drive.Curve(0.4, 1));
        Assert.Equal(Math.Sin(3), Drive.Curve(3, 2), 9);
        Assert.True(Drive.Curve(3, 2) < Drive.Curve(1.5, 2), "a fold comes back down past the top");
    }

    /// <summary>Pushed hard, a fold crosses nought many more times than a warm curve does.</summary>
    [Fact]
    public void A_fold_adds_crossings_a_warm_curve_does_not()
    {
        int Crossed(double shape)
        {
            var drive = new Drive(Rate);

            drive.SetValue(Drive.Amount, 12);
            drive.SetValue(Drive.Shape, shape);

            var caught = Through(drive, Rate / 2, 512, Tone(200));

            AllNumbers(caught);

            return Crossings(caught, 0, Rate / 8, Rate / 2);
        }

        Assert.True(Crossed(2) > Crossed(0) * 3, "fold crossed " + Crossed(2) + " and warm " + Crossed(0));
    }

    /// <summary>Apart pulls the left side down and the right side up.</summary>
    [Fact]
    public void Apart_pulls_the_left_down_and_the_right_up()
    {
        var shift = new Shift(Rate);

        shift.SetValue(Shift.Detune, 50);

        var caught = Through(shift, Rate, 512, Tone(440));

        int left = Crossings(caught, 0, Rate / 4, Rate);
        int right = Crossings(caught, 1, Rate / 4, Rate);

        Assert.True(right > left, "left crossed " + left + " and right " + right);
    }

    /// <summary>Feedback leaves a tail after the sound has gone, and without it the line empties.</summary>
    [Fact]
    public void Feedback_leaves_a_tail()
    {
        double Tail(double feedback)
        {
            var shift = new Shift(Rate);

            shift.SetValue(Shift.Steps, 12);
            shift.SetValue(Shift.Window, 50);
            shift.SetValue(Shift.Feedback, feedback);

            var caught = Through(shift, Rate, 512, at => at < Rate / 4 ? Tone(220)(at) : 0f);

            AllNumbers(caught);

            return Loudness(caught, 0, Rate / 2, Rate / 2 + (Rate / 10));
        }

        Assert.True(Tail(0) < 1e-6);
        Assert.True(Tail(0.8) > 1e-3, "the tail was " + Tail(0.8));
    }

    /// <summary>Feedback all the way up on loud noise stays bounded, and a poisoned block does not go round.</summary>
    [Fact]
    public void Feedback_at_the_top_holds_and_forgets_a_poisoned_block()
    {
        var shift = new Shift(Rate);

        shift.SetValue(Shift.Steps, 12);
        shift.SetValue(Shift.Window, 10);
        shift.SetValue(Shift.Feedback, Shift.MostFeedback);

        var loud = Through(shift, Rate * 2, 512, Noise());

        AllNumbers(loud);
        Assert.True(Loudest(loud, 0, 0, Rate * 2).Level < 4);

        var poisoned = new float[512 * 2];

        poisoned[200] = float.NaN;

        shift.Process(poisoned, 512);

        var after = Through(shift, Rate, 512, Tone(330));

        for (int at = Rate / 2; at < Rate; at++) Assert.True(float.IsFinite(after[at * 2]));
    }

    /// <summary>Swing moves the carrier, so a steady signal crosses nought at a rate that changes.</summary>
    [Fact]
    public void Swing_moves_the_carrier()
    {
        (int Early, int Late) Crossed(double swing)
        {
            var ring = new Ring(Rate);

            ring.SetValue(Ring.Carrier, 400);
            ring.SetValue(Ring.Swing, swing);
            ring.SetValue(Ring.SwingRate, 1);

            var caught = Through(ring, Rate, 512, _ => 0.5f);

            AllNumbers(caught);

            return (Crossings(caught, 0, 0, Rate / 4), Crossings(caught, 0, Rate / 2, Rate * 3 / 4));
        }

        var still = Crossed(0);
        var swung = Crossed(1);

        Assert.InRange(still.Early - still.Late, -2, 2);
        Assert.True(swung.Early > swung.Late * 1.5, "early crossed " + swung.Early + " and late " + swung.Late);
    }

    /// <summary>The side of one tone put through a widener that opens nothing, with the bass kept below that.</summary>
    private static double SideOf(double hertz, double bass)
    {
        var widen = new Widen(Rate);

        widen.SetValue(Widen.Width, 0);
        widen.SetValue(Widen.Haas, 0);
        widen.SetValue(Widen.Mix, 1);
        widen.SetValue(Widen.Bass, bass);

        var caught = Through(widen, Rate, 512, Tone(hertz), _ => 0f);

        AllNumbers(caught);

        double side = 0, middle = 0;

        for (int at = Rate / 2; at < Rate; at++)
        {
            double left = caught[at * 2];
            double right = caught[(at * 2) + 1];

            side += ((left - right) / 2) * ((left - right) / 2);
            middle += ((left + right) / 2) * ((left + right) / 2);
        }

        return Math.Sqrt(side / middle);
    }

    /// <summary>Mono bass puts a low sound hard on one side back in the middle.</summary>
    [Fact]
    public void Mono_bass_puts_a_low_sound_back_in_the_middle()
    {
        Assert.Equal(1, SideOf(60, 0), 3);
        Assert.True(SideOf(60, 300) < 0.1, "the bass kept " + SideOf(60, 300) + " of its side");
    }

    /// <summary>And leaves a high sound where it was.</summary>
    [Fact]
    public void Mono_bass_leaves_a_high_sound_where_it_was() => Assert.True(SideOf(5000, 300) > 0.9);

    /// <summary>Every new control is held to its ends and refuses something that is not a number.</summary>
    [Fact]
    public void Every_new_control_is_held_to_its_ends()
    {
        void Held(ISoundEffectEngine effect, string key, double past, double end, double under, double start)
        {
            double was = effect.ValueOf(key);

            effect.SetValue(key, double.NaN);

            Assert.Equal(was, effect.ValueOf(key), 5);

            effect.SetValue(key, past);
            Assert.Equal(end, effect.ValueOf(key), 5);

            effect.SetValue(key, under);
            Assert.Equal(start, effect.ValueOf(key), 5);

            Assert.Contains(key, effect.Keys);
        }

        Held(new Delay(Rate), Delay.Ping, 0.7, 1, 0.2, 0);
        Held(new Delay(Rate), Delay.Wow, 5, 1, -1, 0);
        Held(new Delay(Rate), Delay.Grit, 5, 1, -1, 0);
        Held(new Sweep(Rate), Sweep.Swing, 50, Sweep.MostSwing, -1, 0);
        Held(new Sweep(Rate), Sweep.SwingRate, 50, Sweep.MostSwingRate, 0, Sweep.LeastSwingRate);
        Held(new Sweep(Rate), Sweep.Follow, 50, Sweep.MostFollow, -50, -Sweep.MostFollow);
        Held(new Drive(Rate), Drive.Shape, 9, Drive.MostShape, -3, 0);
        Held(new Shift(Rate), Shift.Detune, 500, Shift.MostDetune, -5, 0);
        Held(new Shift(Rate), Shift.Feedback, 2, Shift.MostFeedback, -1, 0);
        Held(new Ring(Rate), Ring.Swing, 50, Ring.MostSwing, -1, 0);
        Held(new Ring(Rate), Ring.SwingRate, 50, Ring.MostSwingRate, 0, Ring.LeastSwingRate);
        Held(new Widen(Rate), Widen.Bass, 5000, Widen.MostBass, -5, 0);
    }
}
