using System;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Whether a mono source really comes out wide, and whether it survives being heard in mono
/// afterwards.
/// </summary>
/// <remarks>
/// Width is the one audio claim that cannot be checked by listening to a number: a picture that
/// sounds huge in headphones and disappears on a phone is the ordinary way this kind of effect
/// fails, and it fails silently. So both halves are measured here. The two sides are asked
/// whether they differ at all, which is whether it did anything; and the sum of the two is asked
/// whether it still holds its energy, which is whether it did anything you can broadcast.
///
/// Everything is fed dual mono, since that is what <c>StereoFloats</c> hands over for a one
/// channel capture and is the whole case this effect exists for.
/// </remarks>
public class WidenTests
{
    /// <summary>What everything here is measured at.</summary>
    private const int Rate = 48000;

    /// <summary>A widener with its knobs where the test wants them.</summary>
    private static Widen Made(double width, double depth, double rate, double haas,
                              double side = 0, double mix = 1)
    {
        var widen = new Widen(Rate);

        widen.SetValue(Widen.Width, width);
        widen.SetValue(Widen.Depth, depth);
        widen.SetValue(Widen.Rate, rate);
        widen.SetValue(Widen.Haas, haas);
        widen.SetValue(Widen.Side, side);
        widen.SetValue(Widen.Mix, mix);

        return widen;
    }

    /// <summary>Runs a dual mono signal through in blocks and hands back what came out.</summary>
    /// <param name="widen">The effect under test.</param>
    /// <param name="frames">How many frames to render.</param>
    /// <param name="block">How many at a time, since a block is the caller's business.</param>
    /// <param name="input">What both channels hold at each frame.</param>
    private static float[] Through(ISoundEffectEngine widen, int frames, int block, Func<int, float> input)
    {
        var caught = new float[frames * 2];
        var buffer = new float[block * 2];

        for (int at = 0; at < frames; at += block)
        {
            int run = Math.Min(block, frames - at);

            for (int frame = 0; frame < run; frame++)
            {
                float value = input(at + frame);

                buffer[frame * 2] = value;
                buffer[(frame * 2) + 1] = value;
            }

            widen.Process(buffer, run);

            Array.Copy(buffer, 0, caught, at * 2, run * 2);
        }

        return caught;
    }

    /// <summary>A steady tone, for the things that are clearest on one.</summary>
    private static Func<int, float> Tone(double hertz) =>
        at => (float)Math.Sin(2 * Math.PI * hertz * at / Rate);

    /// <summary>
    /// Broadband material, which is what width has to be measured on.
    /// </summary>
    /// <remarks>
    /// A tone is the wrong signal for this and measuring on one is what sent the first version of
    /// this effect wrong. Two taps of a sine subtracted give a comb, so the side signal swings
    /// with the tap separation and reads as the depth knob controlling the amount; on broadband
    /// material it is flat, which is the truth.
    ///
    /// **The numbers are made here rather than asked of the runtime**, and that is deliberate.
    /// The figures this file pins are properties of the effect, so the material they are measured
    /// on has to be the same everywhere or they are properties of whatever generated it.
    /// <c>System.Random</c> with a seed happens to be stable today, because seeded construction
    /// keeps an older algorithm on purpose, but that is a promise somebody else makes and could
    /// take back. Eight lines of arithmetic that cannot change is the whole cost of not depending
    /// on it, and the suite runs on Linux as well as here.
    ///
    /// The upper bits are taken rather than the whole word: the low bits of a linear congruential
    /// generator have short cycles, and a low bit that alternates would put a tone in what is
    /// supposed to be noise.
    /// </remarks>
    private static Func<int, float> Noise()
    {
        var made = new float[Rate * 2];

        uint state = 7;

        for (int at = 0; at < made.Length; at++)
        {
            state = (state * 1664525u) + 1013904223u;

            made[at] = (float)((((state >> 8) / (double)(1 << 24)) * 1.2) - 0.6);
        }

        return at => made[at % made.Length];
    }

    /// <summary>The energy in the difference between the two sides, which is the width.</summary>
    /// <remarks>
    /// The side signal, which is what a mono fold-down throws away and is therefore exactly the
    /// part that has to exist for anything to sound wide. Nought means dead centre whatever the
    /// two channels hold.
    /// </remarks>
    private static double Side(float[] audio, int from)
    {
        double sum = 0;

        for (int frame = from; frame < audio.Length / 2; frame++)
        {
            double difference = audio[frame * 2] - audio[(frame * 2) + 1];

            sum += difference * difference;
        }

        return Math.Sqrt(sum / Math.Max(1, (audio.Length / 2) - from));
    }

    /// <summary>The energy in the sum of the two sides, which is what mono keeps.</summary>
    private static double Middle(float[] audio, int from)
    {
        double sum = 0;

        for (int frame = from; frame < audio.Length / 2; frame++)
        {
            double both = (audio[frame * 2] + audio[(frame * 2) + 1]) * 0.5;

            sum += both * both;
        }

        return Math.Sqrt(sum / Math.Max(1, (audio.Length / 2) - from));
    }

    /// <summary>A fresh one widens, and does it without the delay that collapses.</summary>
    /// <remarks>
    /// Pinned because it is the whole of why this can be dropped on a chain without a warning. A
    /// fresh one that arrived with a Haas delay on it would be an effect that quietly ruins a
    /// mono fold-down for anybody who never opened its help.
    /// </remarks>
    [Fact]
    public void A_fresh_one_widens_without_the_delay_that_collapses()
    {
        var widen = new Widen(Rate);

        Assert.Equal(Widen.WidthThen, widen.ValueOf(Widen.Width), 3);
        Assert.Equal(Widen.DepthThen, widen.ValueOf(Widen.Depth), 3);
        Assert.Equal(0, widen.ValueOf(Widen.Haas), 3);
        Assert.Equal(1, widen.ValueOf(Widen.Mix), 3);
        Assert.True(widen.ValueOf(Widen.Width) > 0);
    }

    /// <summary>Dual mono in comes out with two sides that differ, which is the whole job.</summary>
    [Fact]
    public void A_mono_source_comes_out_with_a_side_signal()
    {
        var flat = Through(Made(0, 3, 0.35, 0), 24000, 512, Noise());
        var wide = Through(Made(0.5, 3, 0.35, 0), 24000, 512, Noise());

        Assert.True(Side(flat, 4000) < 1e-6, "no width asked for should stay dead centre");
        Assert.True(Side(wide, 4000) > 0.05, "the two sides should differ: " + Side(wide, 4000));
    }

    /// <summary>Width is the amount, and turning it up is more of it.</summary>
    /// <remarks>
    /// Measured on broadband material rather than on a tone, which is the whole reason
    /// <see cref="Noise"/> exists: two taps of a sine subtracted are a comb, so on a tone this
    /// answer swings with the tap separation and says something that is not true of music.
    /// </remarks>
    [Fact]
    public void More_width_is_more_width()
    {
        double little = Side(Through(Made(0.25, 3, 0.35, 0), 24000, 512, Noise()), 4000);
        double lots = Side(Through(Made(1, 3, 0.35, 0), 24000, 512, Noise()), 4000);

        Assert.True(lots > little * 2, little + " should be well under " + lots);
    }

    /// <summary>
    /// And depth is not the amount, which is the finding this effect was rebuilt around.
    /// </summary>
    /// <remarks>
    /// Two copies of a signal are unrelated at any separation past a few samples, so on
    /// broadband material the width barely moves with the travel. Said as a test rather than as a
    /// remark because the first version read the amount off this knob, which measured as a knob
    /// that did almost nothing on music and everything on a sine.
    /// </remarks>
    [Fact]
    public void Depth_is_the_character_rather_than_the_amount()
    {
        double near = Side(Through(Made(0.5, 1, 0.35, 0), 24000, 512, Noise()), 4000);
        double far = Side(Through(Made(0.5, 5, 0.35, 0), 24000, 512, Noise()), 4000);

        Assert.Equal(near, far, 1);
    }

    /// <summary>
    /// The middle comes through untouched, which is the claim the whole effect rests on.
    /// </summary>
    /// <remarks>
    /// Not nearly, and not on average: the two sides sum to exactly what arrived, because the
    /// side signal is added to one and taken off the other and cancels by arithmetic. Held to
    /// five places, which is a 32-bit float rounding rather than a tolerance anybody chose.
    ///
    /// The version before this read two opposite taps one to a side, which is the usual way, and
    /// kept between 45% and 87% of the middle depending on where the depth knob sat. That is what
    /// this test would have caught and what it exists for.
    /// </remarks>
    [Theory]
    [InlineData(0.25, 1)]
    [InlineData(0.5, 3)]
    [InlineData(1, 5)]
    public void The_middle_folds_down_to_exactly_what_arrived(double width, double depth)
    {
        var input = Noise();
        var wide = Through(Made(width, depth, 0.35, 0), 24000, 512, input);

        for (int frame = 0; frame < 24000; frame++)
        {
            double both = (wide[frame * 2] + wide[(frame * 2) + 1]) * 0.5;

            Assert.True(Math.Abs(input(frame) - both) < 1e-6,
                "frame " + frame + " folded down to " + both + " rather than " + input(frame));
        }
    }

    /// <summary>The plain delay does not, and that is why it is off unless asked for.</summary>
    /// <remarks>
    /// A tone delayed by half its own period on one side and added to the other cancels, which is
    /// the comb this warns about, said as one number. Four hundred and forty cycles a second has
    /// a period of 2.27 ms, so 1.14 ms of Haas is the worst case for exactly this tone.
    /// </remarks>
    [Fact]
    public void The_plain_delay_does_not()
    {
        var dry = Through(Made(0, 3, 0.35, 0), 24000, 512, Tone(440));
        var haas = Through(Made(0, 3, 0.35, 1000.0 / 440 / 2), 24000, 512, Tone(440));

        double kept = Middle(haas, 4000) / Middle(dry, 4000);

        Assert.True(kept < 0.3, "a half period of Haas should cancel in mono, kept " + kept);
    }

    /// <summary>Which side the plain delay goes on decides which way the sound leans.</summary>
    [Fact]
    public void The_side_switch_swaps_which_channel_is_late()
    {
        var right = Through(Made(0, 3, 0.35, 10, side: 0), 4800, 512, at => at == 100 ? 1f : 0f);
        var left = Through(Made(0, 3, 0.35, 10, side: 1), 4800, 512, at => at == 100 ? 1f : 0f);

        int late = 100 + (int)(10 * 0.001 * Rate);

        Assert.True(right[100 * 2] > 0.4, "the left should arrive first with the delay on the right");
        Assert.True(right[(late * 2) + 1] > 0.4, "and the right should arrive late");

        Assert.True(left[(100 * 2) + 1] > 0.4, "and the other way round with it on the left");
        Assert.True(left[late * 2] > 0.4);
    }

    /// <summary>
    /// What opening a sound costs in peak level, which is a number worth pinning rather than
    /// discovering on somebody's mix.
    /// </summary>
    /// <remarks>
    /// The two sides are the middle plus and minus the side, so the loudest of them rises as the
    /// side grows. Measured on this material: +1.8 dB a quarter of the way up, +3.4 at a half and
    /// +5.9 at the top. That is what widening is rather than a fault in it, and it is pinned here
    /// so that a change which quietly made it worse would have to say so.
    ///
    /// A range rather than a figure, since the peak of random material is a tail and the tail
    /// moves with the length rendered.
    /// </remarks>
    [Theory]
    [InlineData(0.25, 1.4, 2.2)]
    [InlineData(0.5, 3.0, 3.8)]
    [InlineData(1, 5.4, 6.3)]
    public void Width_costs_peak_level_and_this_is_how_much(double width, double least, double most)
    {
        var input = Noise();
        var wide = Through(Made(width, 3, 0.35, 0), 48000, 512, input);

        double dry = 0;
        double peak = 0;

        for (int frame = 8000; frame < 48000; frame++)
        {
            dry = Math.Max(dry, Math.Abs(input(frame)));
            peak = Math.Max(peak, Math.Abs(wide[frame * 2]));
            peak = Math.Max(peak, Math.Abs(wide[(frame * 2) + 1]));
        }

        double decibels = 20 * Math.Log10(peak / dry);

        Assert.InRange(decibels, least, most);
    }

    /// <summary>
    /// And the width the knob asks for is what comes out, in proportion.
    /// </summary>
    /// <remarks>
    /// Measured against a pair of wholly unrelated channels, which is 0.707: the top of the knob
    /// reaches 0.588, so it stops short of no relation at all rather than going past it into one
    /// channel being the other inverted.
    ///
    /// Within a twentieth rather than to a decimal place, since what the side of random material
    /// measures depends on the material and a figure pinned tighter than that would be pinning
    /// this particular noise rather than the effect.
    /// </remarks>
    [Theory]
    [InlineData(0.25, 0.147)]
    [InlineData(0.5, 0.294)]
    [InlineData(1, 0.588)]
    public void The_side_signal_is_what_the_width_knob_asks_for(double width, double wanted)
    {
        var input = Noise();
        var wide = Through(Made(width, 3, 0.35, 0), 48000, 512, input);

        double dry = 0;

        for (int frame = 8000; frame < 48000; frame++) dry += input(frame) * (double)input(frame);

        dry = Math.Sqrt(dry / 40000);

        double measured = Side(wide, 8000) * 0.5 / dry;

        Assert.InRange(measured, wanted * 0.95, wanted * 1.05);
    }

    /// <summary>Mix at nothing hands back exactly what arrived.</summary>
    /// <remarks>
    /// Bit for bit, since what the mix puts back is the signal as it was handed in rather than as
    /// it was written into the line. A widener that coloured its own dry path would be one nobody
    /// could leave on a chain switched off.
    /// </remarks>
    [Fact]
    public void Mix_at_nothing_changes_no_sample()
    {
        var dry = Through(Made(0, 3, 0.35, 0, mix: 0), 4800, 512, Tone(440));
        var wet = Through(Made(1, 5, 2, 30, mix: 0), 4800, 512, Tone(440));

        Assert.Equal(dry, wet);
    }

    /// <summary>How many frames a block holds changes nothing about what comes out.</summary>
    /// <remarks>
    /// The two ways an effect run in pieces goes wrong both leave a run of the right length: a
    /// frame played twice and a frame skipped. Only comparing the samples says which happened.
    /// </remarks>
    [Fact]
    public void The_block_size_changes_nothing()
    {
        var whole = Through(Made(0.6, 3, 0.5, 7), 9600, 9600, Tone(220));
        var pieces = Through(Made(0.6, 3, 0.5, 7), 9600, 64, Tone(220));
        var odd = Through(Made(0.6, 3, 0.5, 7), 9600, 97, Tone(220));

        for (int at = 0; at < whole.Length; at++)
        {
            Assert.Equal(whole[at], pieces[at], 5);
            Assert.Equal(whole[at], odd[at], 5);
        }
    }

    /// <summary>Nothing that is not a number reaches the line, and none comes out.</summary>
    [Fact]
    public void Nonsense_is_refused_rather_than_stored()
    {
        var widen = Made(0.5, 2.5, 0.35, 5);

        widen.SetValue(Widen.Width, double.NaN);
        widen.SetValue(Widen.Depth, double.NaN);
        widen.SetValue(Widen.Haas, double.PositiveInfinity);
        widen.SetValue(Widen.Mix, double.NegativeInfinity);

        Assert.Equal(0.5, widen.ValueOf(Widen.Width), 3);
        Assert.Equal(2.5, widen.ValueOf(Widen.Depth), 3);
        Assert.Equal(5, widen.ValueOf(Widen.Haas), 3);
        Assert.Equal(1, widen.ValueOf(Widen.Mix), 3);

        var audio = Through(widen, 4800, 512, Tone(440));

        foreach (float sample in audio) Assert.True(float.IsFinite(sample));
    }

    /// <summary>Every knob is held inside its own range, whatever it is handed.</summary>
    [Fact]
    public void Every_knob_is_held_inside_its_range()
    {
        var widen = new Widen(Rate);

        widen.SetValue(Widen.Width, 1000);
        widen.SetValue(Widen.Depth, 1000);
        widen.SetValue(Widen.Haas, -1000);
        widen.SetValue(Widen.Rate, 1000);
        widen.SetValue(Widen.Mix, 5);

        Assert.Equal(1, widen.ValueOf(Widen.Width), 3);
        Assert.Equal(Widen.MostDepthMs, widen.ValueOf(Widen.Depth), 3);
        Assert.Equal(0, widen.ValueOf(Widen.Haas), 3);
        Assert.Equal(Widen.MostRate, widen.ValueOf(Widen.Rate), 3);
        Assert.Equal(1, widen.ValueOf(Widen.Mix), 3);

        widen.SetValue(Widen.Rate, -1000);

        Assert.Equal(Widen.LeastRate, widen.ValueOf(Widen.Rate), 3);
    }

    /// <summary>
    /// A block longer than the buffer claims is held to the buffer rather than read past it.
    /// </summary>
    /// <remarks>
    /// The one place here where a fault is the process gone rather than a message on a status
    /// bar, so a count that is a promise rather than a measurement is refused.
    /// </remarks>
    [Fact]
    public void A_block_longer_than_the_buffer_is_held_to_it()
    {
        var widen = Made(0.7, 3, 0.5, 10);
        var buffer = new float[64];

        widen.Process(buffer, 8000);
        widen.Process(buffer, -1);
        widen.Process(Array.Empty<float>(), 512);
        widen.Process(null!, 512);

        foreach (float sample in buffer) Assert.True(float.IsFinite(sample));
    }

    /// <summary>
    /// Every key the face names is a key the engine answers, and the other way round.
    /// </summary>
    /// <remarks>
    /// A key spelled differently on the two sides is dropped as a preset is read, silently and
    /// correctly, and what somebody hears is a control that did not move.
    /// </remarks>
    [Fact]
    public void The_keys_are_the_words_the_face_uses()
    {
        var widen = new Widen(Rate);

        Assert.Equal(
            new[] { Widen.Width, Widen.Depth, Widen.Rate, Widen.Haas, Widen.Side, Widen.Mix },
            widen.Keys);

        Assert.Equal(0, widen.ValueOf("nothing anybody named"));
    }

    /// <summary>This build has the engine, so a chain naming the effect can be put back.</summary>
    /// <remarks>
    /// The engine and not the effect. Which engine an id plays is read out of the registered
    /// manifest rather than from any table here, so asking <c>Make</c> for the effect by id with
    /// no registry behind it correctly answers nothing; that the shipped manifest names an engine
    /// this build has is <c>ShippedEngineTests</c>, which walks both rack folders.
    ///
    /// What is left to say here is that the engine exists under the word the manifest uses, and
    /// that one built by hand carries the id it was given, since that id is what a chain writes
    /// down and what a link on a knob points at.
    /// </remarks>
    [Fact]
    public void The_widener_is_an_engine_this_build_has()
    {
        Assert.True(new SoundEffectEngines().HasEngine(SoundEffectEngines.Widened));

        Assert.Equal(SoundEffectEngines.Widener, new Widen(Rate, SoundEffectEngines.Widener).Id);
    }
}
