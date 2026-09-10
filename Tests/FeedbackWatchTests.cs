using System;
using JingleBox2.Audio;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Telling a room that has started to ring from a room that is merely being played in.
/// </summary>
/// <remarks>
/// **The whole point of the seam is that this can be asked without a microphone, a speaker or a
/// room.** Signals are made here and handed over, so the question is put to the rule rather than
/// to somebody's afternoon, and the awkward cases can be built on purpose rather than waited for.
///
/// The one that earns it is <see cref="A_steady_tone_is_not_a_ring"/>. Everything that makes
/// feedback recognisable — narrow, tonal, persistent, no harmonics — is equally true of a sine
/// played into a microphone deliberately, and the only thing that separates them is that one of
/// them climbs. A version without that test passes every other test in this file and mutes
/// somebody's monitoring the first time they check a tone.
/// </remarks>
public class FeedbackWatchTests
{
    /// <summary>The rate everything here is imagined at.</summary>
    private const int Rate = 44100;

    /// <summary>How many frames one handed-over block holds.</summary>
    private const int Block = 256;

    /// <summary>
    /// Plays a signal through the watch a block at a time and says whether it ever rang.
    /// </summary>
    /// <param name="seconds">How long to play for.</param>
    /// <param name="at">What the sample at that frame is.</param>
    /// <param name="rate">What it is all running at.</param>
    private static bool Heard(double seconds, Func<int, float> at, int rate = Rate)
    {
        var watch = new FeedbackWatch();
        var block = new float[Block * 2];

        int frames = (int)(rate * seconds);

        for (int start = 0; start < frames; start += Block)
        {
            for (int one = 0; one < Block; one++)
            {
                float sample = at(start + one);

                block[one * 2] = sample;
                block[(one * 2) + 1] = sample;
            }

            if (watch.Ringing(block, block.Length, rate)) return true;
        }

        return false;
    }

    /// <summary>One sine at that pitch and that level.</summary>
    private static float Sine(int frame, double hertz, float level, int rate = Rate) =>
        level * MathF.Sin((float)(2.0 * Math.PI * hertz * frame / rate));

    /// <summary>
    /// **A tone climbing the way a room does is a ring.**
    /// </summary>
    /// <remarks>
    /// Six decibels every tenth of a second, which is a tight loop between a laptop's own
    /// microphone and its own speakers. It has to be caught while it is still quiet, which is
    /// what the level here is about: it starts well under a tenth of full scale and is said long
    /// before it reaches the top.
    /// </remarks>
    [Fact]
    public void A_climbing_tone_is_a_ring()
    {
        bool rang = Heard(0.6, frame =>
        {
            double seconds = (double)frame / Rate;
            float level = (float)(0.02 * Math.Pow(2, seconds / 0.1));

            return Sine(frame, 2100, Math.Min(level, 0.9f));
        });

        Assert.True(rang, "a room ringing was not caught");
    }

    /// <summary>
    /// **A steady tone is not a ring, however tonal it is.**
    /// </summary>
    /// <remarks>
    /// The test tone case, and the reason growth is asked for rather than persistence. Played for
    /// a good deal longer than the four frames a ring is said in, so a version that only counted
    /// agreeing frames would have every chance to fire.
    /// </remarks>
    [Fact]
    public void A_steady_tone_is_not_a_ring()
    {
        Assert.False(Heard(1.5, frame => Sine(frame, 2100, 0.4f)),
            "a tone somebody was playing on purpose was called feedback");
    }

    /// <summary>
    /// A note with a harmonic series over it is an instrument, however loud it gets.
    /// </summary>
    /// <remarks>
    /// The same climb as the ring, so the only thing left to tell them apart is the series. This
    /// is the case that would otherwise mute somebody the moment a singer leaned into a phrase.
    /// </remarks>
    [Fact]
    public void A_note_with_harmonics_is_not_a_ring()
    {
        Assert.False(Heard(0.6, frame =>
        {
            double seconds = (double)frame / Rate;
            float level = (float)Math.Min(0.02 * Math.Pow(2, seconds / 0.1), 0.3);

            return Sine(frame, 440, level)
                   + Sine(frame, 880, level * 0.8f)
                   + Sine(frame, 1320, level * 0.6f)
                   + Sine(frame, 1760, level * 0.4f);
        }), "an instrument getting louder was called feedback");
    }

    /// <summary>Noise is not a ring, whatever level it is at.</summary>
    /// <remarks>
    /// Its own generator rather than the runtime's, so the same numbers are read on every machine
    /// and twice running: a test that answers differently on somebody else's computer says
    /// nothing about the code.
    /// </remarks>
    [Fact]
    public void Noise_is_not_a_ring()
    {
        uint state = 12345;

        Assert.False(Heard(1.0, _ =>
        {
            state = (state * 1664525u) + 1013904223u;

            return (((state >> 8) & 0xFFFFFF) / 8388608f) - 1f;
        }), "a noisy room was called feedback");
    }

    /// <summary>And silence is not a ring.</summary>
    /// <remarks>
    /// The floor earns its own test because a very quiet signal is all shape and no substance: a
    /// spectrum read off nearly nothing has a loudest bin like any other, and without the floor
    /// the peak-over-neighbours test would pass on the arithmetic of rounding.
    /// </remarks>
    [Fact]
    public void Silence_is_not_a_ring()
    {
        Assert.False(Heard(1.0, _ => 0f), "silence was called feedback");
    }

    /// <summary>It is said once, and clearing is what arms it again.</summary>
    /// <remarks>
    /// A caller that goes on handing over blocks after it has acted must not be told again on
    /// every one of them, and the run either side of a gap is not one run: somebody who stopped
    /// listening an hour ago has not been ringing since.
    /// </remarks>
    [Fact]
    public void It_is_said_once_and_cleared_to_arm_again()
    {
        var watch = new FeedbackWatch();
        var block = new float[Block * 2];

        int said = 0;

        for (int start = 0; start < Rate; start += Block)
        {
            for (int one = 0; one < Block; one++)
            {
                double seconds = (double)(start + one) / Rate;
                float level = (float)Math.Min(0.02 * Math.Pow(2, seconds / 0.1), 0.9);
                float sample = Sine(start + one, 2100, level);

                block[one * 2] = sample;
                block[(one * 2) + 1] = sample;
            }

            if (watch.Ringing(block, block.Length, Rate)) said++;
        }

        Assert.Equal(1, said);

        watch.Clear();

        int again = 0;

        for (int start = 0; start < Rate; start += Block)
        {
            for (int one = 0; one < Block; one++)
            {
                double seconds = (double)(start + one) / Rate;
                float level = (float)Math.Min(0.02 * Math.Pow(2, seconds / 0.1), 0.9);
                float sample = Sine(start + one, 2100, level);

                block[one * 2] = sample;
                block[(one * 2) + 1] = sample;
            }

            if (watch.Ringing(block, block.Length, Rate)) again++;
        }

        Assert.Equal(1, again);
    }

    /// <summary>Nothing handed over is nothing said.</summary>
    [Fact]
    public void Nothing_handed_over_is_nothing_said()
    {
        var watch = new FeedbackWatch();

        Assert.False(watch.Ringing(null!, 0, Rate));
        Assert.False(watch.Ringing(new float[8], 0, Rate));
        Assert.False(watch.Ringing(new float[8], -4, Rate));
    }

    /// <summary>
    /// **A mix coming off another card is not a ring, however it swells.**
    /// </summary>
    /// <remarks>
    /// The case this was reported on: capturing what a second card is playing, with no microphone
    /// anywhere and no way for anything to come back round, and listening shut off at once.
    ///
    /// A bass note, a chord over it and a little noise for the room, all swelling together the way
    /// a piece of music does. It passes the crest test, because over eleven milliseconds a
    /// sustained mix is as tonal as anything, and it passes growth because music swells. What it
    /// cannot pass is being one frequency.
    /// </remarks>
    [Fact]
    public void A_mix_swelling_is_not_a_ring()
    {
        uint state = 999;

        Assert.False(Heard(1.2, frame =>
        {
            double seconds = (double)frame / Rate;
            float level = (float)Math.Min(0.05 * Math.Pow(2, seconds / 0.2), 0.5);

            state = (state * 1664525u) + 1013904223u;

            float hiss = ((((state >> 8) & 0xFFFFFF) / 8388608f) - 1f) * level * 0.02f;

            return Sine(frame, 110, level)
                   + Sine(frame, 220, level * 0.5f)
                   + Sine(frame, 330, level * 0.35f)
                   + Sine(frame, 415, level * 0.4f)
                   + Sine(frame, 523, level * 0.3f)
                   + hiss;
        }), "a piece of music was called feedback");
    }

    /// <summary>
    /// And one sustained note with almost nothing over it is still not a ring.
    /// </summary>
    /// <remarks>
    /// The hardest honest case, and the one the octave test alone would let through: a bass
    /// synth swelling, with a weak second partial and no third. What separates it is that even a
    /// weak partial is far closer to the fundamental than a room's noise floor is.
    /// </remarks>
    [Fact]
    public void A_swelling_bass_note_is_not_a_ring()
    {
        Assert.False(Heard(0.8, frame =>
        {
            double seconds = (double)frame / Rate;
            float level = (float)Math.Min(0.03 * Math.Pow(2, seconds / 0.15), 0.6);

            return Sine(frame, 82, level) + Sine(frame, 247, level * 0.2f);
        }), "a bass note swelling was called feedback");
    }

    /// <summary>
    /// **A low swell off another card is not a ring, which is what it was reported as.**
    /// </summary>
    /// <remarks>
    /// The measurement this was written from, at the rate it happened at:
    /// <c>ringing at bin 3, 67 over the mean, 33 over the bins beside it, 58 over the next peak,
    /// crest 1.66, grown 1.50</c>. Bin 3 of a 48 kHz stream is 281 Hz, which was the lowest bin
    /// the candidate was allowed, and the bins beside a candidate are skipped when the next peak
    /// is looked for — so the whole of the bass this signal is made of was hidden from the test
    /// that was supposed to see it.
    ///
    /// At 48 kHz deliberately, since the fault only appears where the floor and the bin width put
    /// the candidate hard against the bottom.
    /// </remarks>
    [Fact]
    public void A_low_swell_at_48k_is_not_a_ring()
    {
        const int Fast = 48000;

        Assert.False(Heard(1.2, frame =>
        {
            double seconds = (double)frame / Fast;
            float level = (float)Math.Min(0.04 * Math.Pow(2, seconds / 0.2), 0.6);

            return Sine(frame, 281, level, Fast)
                   + Sine(frame, 140, level * 0.7f, Fast)
                   + Sine(frame, 92, level * 0.5f, Fast);
        }, Fast), "a low swell off another card was called feedback");
    }
}