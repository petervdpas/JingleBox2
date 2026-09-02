using System;
using JingleBox2.Tracker.Effects;
using JingleBox2.Tracker.Effects.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The first engine of ours, measured rather than listened to.
/// </summary>
/// <remarks>
/// A delay is the one effect whose whole behaviour can be counted: an impulse goes in and comes
/// back a known number of frames later, at a known level, and the level of the one after that is
/// the feedback. So none of this asks whether it sounds right, it asks where the sound is and
/// how loud, which is what a test can actually say.
///
/// The rest of it is what happens when it is lied to. This runs on the audio thread, where a
/// fault is the process gone rather than a message on a status line, and the lesson written into
/// <c>Tests/MixerRenderTests.cs</c> holds here: half a guard is worse than none, since the one
/// that is there reads as the question having been asked.
/// </remarks>
public class DelayTests
{
    /// <summary>What everything here is measured at.</summary>
    private const int Rate = 48000;

    /// <summary>A delay with its knobs where the test wants them, before anything is rendered.</summary>
    private static Delay Made(double time, double feedback, double mix, double damp = 0)
    {
        var delay = new Delay(Rate);

        delay.SetValue(Delay.Time, time);
        delay.SetValue(Delay.Feedback, feedback);
        delay.SetValue(Delay.Mix, mix);
        delay.SetValue(Delay.Damp, damp);

        return delay;
    }

    /// <summary>Runs a signal through in blocks and hands back what came out.</summary>
    /// <param name="delay">The effect under test.</param>
    /// <param name="frames">How many frames to render.</param>
    /// <param name="block">How many at a time, since a block is the caller's business.</param>
    /// <param name="input">What the left channel holds at each frame; the right holds the same.</param>
    private static float[] Through(IEffectEngine delay, int frames, int block, Func<int, float> input)
    {
        var caught = new float[frames * 2];
        var buffer = new float[block * 2];

        for (int at = 0; at < frames; at += block)
        {
            int room = Math.Min(block, frames - at);

            Array.Clear(buffer);

            for (int i = 0; i < room; i++)
            {
                buffer[i * 2] = input(at + i);
                buffer[i * 2 + 1] = input(at + i);
            }

            delay.Process(buffer, room);

            Array.Copy(buffer, 0, caught, at * 2, room * 2);
        }

        return caught;
    }

    /// <summary>Where the loudest left-channel frame is, and how loud.</summary>
    private static (int At, double Level) Loudest(float[] audio, int from = 0)
    {
        int where = from;
        double most = 0;

        for (int at = from; at < audio.Length / 2; at++)
        {
            double level = Math.Abs(audio[at * 2]);

            if (level <= most) continue;

            most = level;
            where = at;
        }

        return (where, most);
    }

    /// <summary>Nothing in, nothing out, however long it is left running.</summary>
    [Fact]
    public void Silence_stays_silence()
    {
        var audio = Through(Made(120, 0.9, 1, 0.5), Rate / 2, 256, _ => 0f);

        Assert.All(audio, one => Assert.Equal(0, one));
    }

    /// <summary>An impulse comes back where the time says, to the frame.</summary>
    [Fact]
    public void The_repeat_arrives_where_the_time_says()
    {
        var audio = Through(Made(100, 0, 1), Rate / 2, 256, at => at == 0 ? 1f : 0f);

        var (where, level) = Loudest(audio, 1);

        Assert.InRange(where, Rate / 10 - 1, Rate / 10 + 1);
        Assert.InRange(level, 0.9, 1.0);
    }

    /// <summary>And a different time moves it, which is the knob doing the one thing it does.</summary>
    [Fact]
    public void A_shorter_time_brings_it_back_sooner()
    {
        var audio = Through(Made(50, 0, 1), Rate / 2, 256, at => at == 0 ? 1f : 0f);

        Assert.InRange(Loudest(audio, 1).At, Rate / 20 - 1, Rate / 20 + 1);
    }

    /// <summary>Each repeat is the one before it times the feedback, which is what feedback means.</summary>
    [Fact]
    public void The_repeats_fall_away_by_the_feedback()
    {
        var audio = Through(Made(50, 0.5, 1), Rate / 2, 256, at => at == 0 ? 1f : 0f);

        var first = Loudest(audio, 1);
        var second = Loudest(audio, first.At + 100);

        Assert.InRange(second.Level / first.Level, 0.45, 0.55);
        Assert.InRange(second.At - first.At, Rate / 20 - 2, Rate / 20 + 2);
    }

    /// <summary>Nothing over unity, so the repeats can never grow into a howl.</summary>
    [Fact]
    public void Feedback_cannot_be_asked_past_its_end()
    {
        var delay = new Delay(Rate);

        delay.SetValue(Delay.Feedback, 4);

        Assert.Equal(Delay.MostFeedback, delay.ValueOf(Delay.Feedback), 5);

        delay.SetValue(Delay.Feedback, -2);

        Assert.Equal(0, delay.ValueOf(Delay.Feedback), 5);
    }

    /// <summary>Dry is exactly what went in, sample for sample, and not nearly it.</summary>
    /// <remarks>
    /// A mix at nought is somebody switching the effect out of the sound while leaving it in the
    /// chain, so anything at all happening to the audio there is a fault: no level change, no
    /// filtering, and nothing accumulating over a long pass.
    /// </remarks>
    [Fact]
    public void At_no_mix_what_comes_out_is_what_went_in()
    {
        var delay = Made(120, 0.9, 0, 0.5);

        float Wave(int at) => (float)Math.Sin(at * 0.05) * 0.7f;

        var audio = Through(delay, 4096, 128, Wave);

        for (int at = 0; at < 4096; at++) Assert.Equal(Wave(at), audio[at * 2]);
    }

    /// <summary>Damping takes the top off what comes back, so an impulse returns softer.</summary>
    /// <remarks>
    /// Measured on the peak of the first repeat, which is enough for an impulse: a one pole
    /// spreads it out in time, so what was one tall frame comes back as several shorter ones.
    /// </remarks>
    [Fact]
    public void Damping_softens_what_comes_back()
    {
        double bright = Loudest(Through(Made(50, 0, 1, 0), Rate / 4, 256, at => at == 0 ? 1f : 0f), 1).Level;
        double dark = Loudest(Through(Made(50, 0, 1, 1), Rate / 4, 256, at => at == 0 ? 1f : 0f), 1).Level;

        Assert.True(dark < bright, "damping should take the top off a repeat");
        Assert.True(dark > 0, "damping should not silence it");
    }

    /// <summary>The block size is the caller's business and changes nothing about the sound.</summary>
    /// <remarks>
    /// The one thing about an insert that has to be true whatever the sound card is doing. A
    /// buffer setting that changed what a delay sounded like would be a fault nobody could
    /// reproduce.
    /// </remarks>
    [Fact]
    public void The_block_size_makes_no_difference()
    {
        float Wave(int at) => (float)Math.Sin(at * 0.03) * 0.5f;

        var whole = Through(Made(70, 0.4, 0.5, 0.2), 8192, 8192, Wave);
        var pieces = Through(Made(70, 0.4, 0.5, 0.2), 8192, 33, Wave);

        for (int at = 0; at < whole.Length; at++) Assert.Equal(whole[at], pieces[at], 5);
    }

    /// <summary>A time set before anything is rendered is where it starts, not somewhere to slide from.</summary>
    /// <remarks>
    /// This is a song being opened: the chain is built and the values are put in before a block
    /// is asked for. Gliding from wherever a fresh one happened to sit would smear the first
    /// repeats of every song for a moment after it opened.
    /// </remarks>
    [Fact]
    public void A_time_set_before_the_first_block_is_where_it_starts()
    {
        var audio = Through(Made(500, 0, 1), Rate, 256, at => at == 0 ? 1f : 0f);

        Assert.InRange(Loudest(audio, 1).At, Rate / 2 - 1, Rate / 2 + 1);
    }

    /// <summary>Nonsense is refused rather than clamped, since a clamp hands NaN straight back.</summary>
    /// <remarks>
    /// The trap <c>ToneFilter</c> fell into once already: <c>Math.Clamp</c> answers NaN with NaN
    /// by design, and one NaN in a feedback path is silence for the rest of the session.
    /// </remarks>
    [Fact]
    public void A_value_that_is_not_a_number_is_refused()
    {
        var delay = Made(120, 0.4, 0.5, 0.2);

        delay.SetValue(Delay.Time, double.NaN);
        delay.SetValue(Delay.Feedback, double.NaN);
        delay.SetValue(Delay.Mix, double.PositiveInfinity);
        delay.SetValue(Delay.Damp, double.NaN);

        Assert.Equal(120, delay.ValueOf(Delay.Time), 5);
        Assert.Equal(0.4, delay.ValueOf(Delay.Feedback), 5);
        Assert.Equal(0.5, delay.ValueOf(Delay.Mix), 5);
        Assert.Equal(0.2, delay.ValueOf(Delay.Damp), 5);

        var audio = Through(delay, 2048, 128, at => at == 0 ? 1f : 0f);

        Assert.All(audio, one => Assert.False(float.IsNaN(one)));
    }

    /// <summary>A time past either end is held to it, since the line is only so long.</summary>
    [Fact]
    public void A_time_past_the_ends_is_held_to_them()
    {
        var delay = new Delay(Rate);

        delay.SetValue(Delay.Time, 1_000_000);

        Assert.Equal(Delay.MostMs, delay.ValueOf(Delay.Time), 5);

        delay.SetValue(Delay.Time, -3);

        Assert.Equal(Delay.LeastMs, delay.ValueOf(Delay.Time), 5);
    }

    /// <summary>A word this effect has not got reads as nought and writes nothing.</summary>
    /// <remarks>
    /// Which is what a chain saved by a later version looks like: mostly this version's, plus a
    /// key nobody here has heard of.
    /// </remarks>
    [Fact]
    public void A_key_it_has_not_got_is_nothing_rather_than_a_fault()
    {
        var delay = Made(120, 0.4, 0.5, 0.2);

        delay.SetValue("wobble", 1);
        delay.SetValue(null, 1);

        Assert.Equal(0, delay.ValueOf("wobble"));
        Assert.Equal(0, delay.ValueOf(null));
        Assert.Equal(120, delay.ValueOf(Delay.Time), 5);
    }

    /// <summary>A block bigger than the buffer is worked on as far as the buffer goes.</summary>
    /// <remarks>
    /// A frame count is a promise and a buffer is a measurement, and where the two disagree the
    /// measurement wins. This has already been the shape of a crash on the audio thread once.
    /// </remarks>
    [Fact]
    public void A_block_longer_than_the_buffer_does_not_run_off_the_end()
    {
        var delay = Made(120, 0.4, 1, 0);
        var buffer = new float[8];

        buffer[0] = 1;

        delay.Process(buffer, 1000);

        Assert.All(buffer, one => Assert.False(float.IsNaN(one)));
    }

    /// <summary>No frames, no frames at all, and no buffer are each nothing rather than a fault.</summary>
    [Fact]
    public void Nothing_to_do_is_not_a_fault()
    {
        var delay = Made(120, 0.4, 1, 0);
        var buffer = new float[64];

        buffer[0] = 0.5f;

        delay.Process(buffer, 0);
        delay.Process(buffer, -7);
        delay.Process(null!, 32);

        Assert.Equal(0.5f, buffer[0]);
    }

    /// <summary>The list this build ships knows EchoBox and nothing it has not been told about.</summary>
    [Fact]
    public void The_engine_list_makes_an_echobox()
    {
        var engines = new EffectEngines();

        Assert.True(engines.Has(EffectEngines.EchoBox));
        Assert.True(engines.Has("EFFECT.ECHOBOX"));
        Assert.IsType<Delay>(engines.Make(EffectEngines.EchoBox, Rate, 512));

        Assert.False(engines.Has("effect.somebody-elses"));
        Assert.Null(engines.Make("effect.somebody-elses", Rate, 512));
        Assert.Null(engines.Make(null, Rate, 512));
    }

    /// <summary>Two of one effect are two effects, each with its own line.</summary>
    /// <remarks>
    /// A pedal board has two of the same pedal on it often enough, and they are not one pedal
    /// heard twice: what one is holding is nothing to do with the other.
    /// </remarks>
    [Fact]
    public void Two_of_them_are_two()
    {
        var engines = new EffectEngines();

        var one = engines.Make(EffectEngines.EchoBox, Rate, 512)!;
        var other = engines.Make(EffectEngines.EchoBox, Rate, 512)!;

        one.SetValue(Delay.Time, 111);

        Assert.NotSame(one, other);
        Assert.Equal(Delay.TimeThen, other.ValueOf(Delay.Time), 5);
    }
}
