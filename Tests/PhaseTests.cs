using System;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The phaser, measured rather than listened to.
/// </summary>
/// <remarks>
/// A row of all-pass stages is arithmetic that can be done on paper: a stage shifts a quarter of a
/// turn at the frequency it is set to, so four of them shift a whole turn there and half a turn at
/// a frequency that can be worked out exactly. Held still, a tone at the first is let through at
/// full level and a tone at the second is cancelled. So what is asked here is not whether it
/// swooshes but whether the notch is where the arithmetic says, which is the thing that would be
/// quietly wrong with a stage written the wrong way round.
///
/// The rest is what happens when it is lied to, since this runs on the audio thread.
/// </remarks>
public sealed class PhaseTests
{
    /// <summary>What everything here is measured at.</summary>
    private const int Rate = 48000;

    /// <summary>A phaser held still at a thousand hertz, with the rest where the test wants it.</summary>
    private static Phase Still(double feedback = 0, double mix = 0.5, double stages = 0)
    {
        var phase = new Phase(Rate);

        phase.SetValue(Phase.Depth, 0);
        phase.SetValue(Phase.Centre, 1000);
        phase.SetValue(Phase.Feedback, feedback);
        phase.SetValue(Phase.Mix, mix);
        phase.SetValue(Phase.Stages, stages);
        phase.SetValue(Phase.Spread, 0);

        return phase;
    }

    /// <summary>Runs a signal through in blocks and hands back what came out.</summary>
    /// <param name="effect">The effect under test.</param>
    /// <param name="frames">How many frames to render.</param>
    /// <param name="block">How many at a time.</param>
    /// <param name="input">What both channels hold at each frame.</param>
    private static float[] Through(ISoundEffectEngine effect, int frames, int block, Func<int, float> input)
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
                buffer[(i * 2) + 1] = input(at + i);
            }

            effect.Process(buffer, room);

            Array.Copy(buffer, 0, caught, at * 2, room * 2);
        }

        return caught;
    }

    /// <summary>The loudest left-channel frame in the second half, once everything has settled.</summary>
    private static double Settled(float[] audio)
    {
        double most = 0;

        for (int at = audio.Length / 4; at < audio.Length / 2; at++) most = Math.Max(most, Math.Abs(audio[at * 2]));

        return most;
    }

    /// <summary>A tone at that frequency, at full scale.</summary>
    private static Func<int, float> Tone(double hertz) => at => (float)Math.Sin(2 * Math.PI * hertz * at / Rate);

    /// <summary>Noise that is the same every run.</summary>
    private static Func<int, float> Noise()
    {
        var random = new Random(7);
        var held = new float[Rate];

        for (int at = 0; at < held.Length; at++) held[at] = (float)((random.NextDouble() * 2) - 1);

        return at => held[at % held.Length];
    }

    /// <summary>Where four stages set to a thousand hertz shift a tone by half a turn.</summary>
    /// <remarks>Each stage a quarter of that, which is an eighth of a turn apiece.</remarks>
    private static double Notch => Rate / Math.PI * Math.Atan(Math.Tan(Math.PI * 1000 / Rate) * Math.Tan(Math.PI / 8));

    /// <summary>It is the effect it was made as and answers for every control on its face.</summary>
    [Fact]
    public void It_carries_its_id_and_every_control()
    {
        var phase = new Phase(Rate, SoundEffectEngines.Phaser);

        Assert.Equal(SoundEffectEngines.Phaser, phase.Id);
        Assert.Equal(
            new[] { Phase.Rate, Phase.Depth, Phase.Centre, Phase.Feedback, Phase.Stages, Phase.Spread, Phase.Mix, IEffectLevel.Key },
            phase.Keys);
        Assert.True(new SoundEffectEngines().HasEngine(SoundEffectEngines.Phased));
    }

    /// <summary>Held still, a tone where the row turns half a turn is cancelled.</summary>
    [Fact]
    public void A_tone_at_the_notch_is_cancelled()
    {
        var caught = Through(Still(), Rate, 512, Tone(Notch));

        Assert.True(Settled(caught) < 0.001, "the notch let through " + Settled(caught));
    }

    /// <summary>And a tone where the row turns a whole turn comes through at the level it went in.</summary>
    [Fact]
    public void A_tone_where_the_row_turns_right_round_comes_through_whole()
    {
        var caught = Through(Still(), Rate, 512, Tone(1000));

        Assert.InRange(Settled(caught), 0.99, 1.01);
    }

    /// <summary>
    /// Feedback turned right up does not make anything louder than it went in.
    /// </summary>
    /// <remarks>
    /// The loop gives a peak of ten times at nine tenths when nothing pays for it. Swept across the
    /// range a phaser works in, no tone comes out louder than it went in, which is what paying for
    /// the feedback out of the input is for.
    /// </remarks>
    [Fact]
    public void Feedback_right_up_is_no_louder_than_what_went_in()
    {
        for (int hertz = 200; hertz <= 3000; hertz += 100)
        {
            var caught = Through(Still(Phase.MostFeedback, 1), Rate / 2, 512, Tone(hertz));

            Assert.True(Settled(caught) <= 1.05, hertz + " Hz came out at " + Settled(caught));
        }
    }

    /// <summary>With no mix it hands back exactly what went in.</summary>
    [Fact]
    public void No_mix_is_what_went_in()
    {
        var input = Noise();
        var caught = Through(Still(0.5, 0), 4096, 256, input);

        for (int at = 0; at < 4096; at++) Assert.Equal(input(at), caught[at * 2]);
    }

    /// <summary>How the audio is cut into blocks decides nothing about what comes out.</summary>
    [Fact]
    public void The_block_size_decides_nothing()
    {
        ISoundEffectEngine Swept()
        {
            var phase = new Phase(Rate);

            phase.SetValue(Phase.Rate, 3);
            phase.SetValue(Phase.Depth, 1);
            phase.SetValue(Phase.Feedback, 0.7);
            phase.SetValue(Phase.Stages, 3);
            phase.SetValue(Phase.Spread, 90);

            return phase;
        }

        var whole = Through(Swept(), 20000, 20000, Noise());
        var pieces = Through(Swept(), 20000, 37, Noise());

        for (int at = 0; at < whole.Length; at++) Assert.Equal(whole[at], pieces[at], 1e-6);
    }

    /// <summary>Spread apart, the two sides differ; together, they are the same sample for sample.</summary>
    [Fact]
    public void Spread_is_what_parts_the_sides()
    {
        ISoundEffectEngine Spread(double degrees)
        {
            var phase = new Phase(Rate);

            phase.SetValue(Phase.Rate, 2);
            phase.SetValue(Phase.Depth, 1);
            phase.SetValue(Phase.Spread, degrees);

            return phase;
        }

        var together = Through(Spread(0), 8000, 512, Noise());
        var apart = Through(Spread(180), 8000, 512, Noise());

        bool differs = false;

        for (int at = 0; at < 8000; at++)
        {
            Assert.Equal(together[at * 2], together[(at * 2) + 1]);

            differs |= apart[at * 2] != apart[(at * 2) + 1];
        }

        Assert.True(differs);
    }

    /// <summary>A control it has not got reads nought and writes nothing.</summary>
    [Fact]
    public void A_control_it_has_not_got_moves_nothing()
    {
        var phase = new Phase(Rate);

        phase.SetValue("volume", 3);
        phase.SetValue(null, 3);

        Assert.Equal(0, phase.ValueOf("volume"));
        Assert.Equal(0, phase.ValueOf(null));
        Assert.Equal(Phase.RateThen, phase.ValueOf(Phase.Rate), 5);
    }

    /// <summary>Something that is not a number is refused, and everything else is held to its ends.</summary>
    [Fact]
    public void Values_are_held_to_their_ends_and_nonsense_is_refused()
    {
        var phase = new Phase(Rate);

        phase.SetValue(Phase.Feedback, double.NaN);
        phase.SetValue(Phase.Centre, double.PositiveInfinity);

        Assert.Equal(Phase.FeedbackThen, phase.ValueOf(Phase.Feedback), 5);
        Assert.Equal(Phase.CentreThen, phase.ValueOf(Phase.Centre), 5);

        phase.SetValue(Phase.Stages, 99);
        phase.SetValue(Phase.Feedback, -5);
        phase.SetValue(Phase.Rate, 0);
        phase.SetValue(Phase.Spread, 720);

        Assert.Equal(Phase.Rows.Length - 1, phase.ValueOf(Phase.Stages));
        Assert.Equal(-Phase.MostFeedback, phase.ValueOf(Phase.Feedback), 5);
        Assert.Equal(Phase.LeastRate, phase.ValueOf(Phase.Rate), 5);
        Assert.Equal(Phase.MostSpread, phase.ValueOf(Phase.Spread), 5);
    }

    /// <summary>Nothing it can be set to hands back something that is not a number.</summary>
    [Fact]
    public void Nothing_it_can_be_set_to_hands_back_nonsense()
    {
        foreach (double feedback in new[] { -Phase.MostFeedback, 0, Phase.MostFeedback })
        foreach (double stages in new[] { 0.0, 3 })
        foreach (double rate in new[] { Phase.LeastRate, Phase.MostRate })
        foreach (double centre in new[] { Phase.LeastCentre, Phase.MostCentre })
        {
            var phase = new Phase(Rate);

            phase.SetValue(Phase.Feedback, feedback);
            phase.SetValue(Phase.Stages, stages);
            phase.SetValue(Phase.Rate, rate);
            phase.SetValue(Phase.Centre, centre);
            phase.SetValue(Phase.Depth, 1);

            var caught = Through(phase, 4800, 480, at => Noise()(at) * 1.5f);

            Assert.All(caught, sample => Assert.True(float.IsFinite(sample)));
        }
    }

    /// <summary>A block holding something that is not a number costs that block and no more.</summary>
    [Fact]
    public void A_poisoned_block_does_not_outlive_itself()
    {
        var phase = Still(0.8);
        var poisoned = new float[512 * 2];

        poisoned[100] = float.NaN;

        phase.Process(poisoned, 512);

        var after = Through(phase, 4800, 512, Tone(700));

        Assert.All(after, sample => Assert.True(float.IsFinite(sample)));
    }

    /// <summary>A buffer that is not there, no frames, or more frames than the buffer holds, are refused quietly.</summary>
    [Fact]
    public void A_block_that_lies_is_refused_quietly()
    {
        var phase = new Phase(Rate);

        phase.Process(null!, 512);
        phase.Process(new float[64], -1);
        phase.Process(new float[64], 0);
        phase.Process(new float[64], 10_000);
        phase.Process(new float[3], 2);
    }
}
