using System;
using JingleBox2.SoundDevices.SoundEffects;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The filter, measured rather than listened to.
/// </summary>
/// <remarks>
/// A filter's whole job is that some frequencies come out quieter than they went in, which is a
/// thing a test can measure exactly: play a steady tone, wait for the state to settle, and read
/// what is left. So none of this asks whether it sounds good, it asks how much of a known tone
/// survives, which is what the maths actually promises.
///
/// The rest is what happens when it is lied to. This runs on the audio thread, where a fault is
/// the process gone rather than a message on a status line.
/// </remarks>
public class SweepTests
{
    /// <summary>What everything here is measured at.</summary>
    private const int Rate = 48000;

    /// <summary>How long a tone is run before it is read, so the poles have settled.</summary>
    private const int Settle = 4096;

    /// <summary>A filter with its knobs where the test wants them.</summary>
    private static Sweep Made(double cutoff, double resonance = 0, double drive = 1, double mode = 0, double mix = 1)
    {
        var sweep = new Sweep(Rate);

        sweep.SetValue(Sweep.Cutoff, cutoff);
        sweep.SetValue(Sweep.Resonance, resonance);
        sweep.SetValue(Sweep.Drive, drive);
        sweep.SetValue(Sweep.Mode, mode);
        sweep.SetValue(Sweep.Mix, mix);

        return sweep;
    }

    /// <summary>
    /// Plays a tone through and answers the loudest it got over the last stretch.
    /// </summary>
    /// <remarks>
    /// Read off the tail rather than the whole run, since the first blocks are the filter
    /// settling and the cutoff gliding to where it was put, and neither is what is being asked
    /// about.
    /// </remarks>
    /// <param name="sweep">The filter under test.</param>
    /// <param name="hz">What tone to play it.</param>
    private static double Survives(Sweep sweep, double hz)
    {
        const int block = 512;
        var buffer = new float[block * 2];

        double peak = 0;
        long at = 0;

        for (int round = 0; round < 40; round++)
        {
            for (int frame = 0; frame < block; frame++, at++)
            {
                float value = (float)Math.Sin(2 * Math.PI * hz * at / Rate);
                buffer[frame * 2] = value;
                buffer[frame * 2 + 1] = value;
            }

            sweep.Process(buffer, block);

            if (at < Settle) continue;

            for (int frame = 0; frame < block; frame++)
                peak = Math.Max(peak, Math.Abs(buffer[frame * 2]));
        }

        return peak;
    }

    /// <summary>A low pass keeps what is under the cutoff and takes away what is over it.</summary>
    [Fact]
    public void The_low_pass_keeps_the_bottom()
    {
        Assert.True(Survives(Made(1000), 100) > 0.7, "a tone well under the cutoff is left alone");
        Assert.True(Survives(Made(1000), 8000) < 0.1, "one well over it is taken away");
    }

    /// <summary>And a high pass is the other way about.</summary>
    [Fact]
    public void The_high_pass_keeps_the_top()
    {
        Assert.True(Survives(Made(1000, mode: 2), 8000) > 0.7, "a tone well over the cutoff is left alone");
        Assert.True(Survives(Made(1000, mode: 2), 100) < 0.2, "one well under it is taken away");
    }

    /// <summary>A band pass takes both ends and keeps what is around the cutoff.</summary>
    [Fact]
    public void The_band_pass_keeps_the_middle()
    {
        var made = Made(1000, mode: 1);

        Assert.True(Survives(made, 100) < 0.5, "the bottom goes");
        Assert.True(Survives(Made(1000, mode: 1), 9000) < 0.5, "and so does the top");
    }

    /// <summary>
    /// Resonance lifts what is at the cutoff rather than what is anywhere else.
    /// </summary>
    /// <remarks>
    /// Which is the whole of what a resonant filter is, and the thing that would be silently
    /// wrong if the damping were worked out from the wrong number.
    /// </remarks>
    [Fact]
    public void Resonance_lifts_the_cutoff()
    {
        double flat = Survives(Made(1000), 1000);
        double ringing = Survives(Made(1000, resonance: 0.9), 1000);

        Assert.True(ringing > flat * 1.5, "at the cutoff it is louder: " + ringing + " against " + flat);
    }

    /// <summary>
    /// Fully open, dry and undriven, what comes out is what went in to within a hair.
    /// </summary>
    /// <remarks>
    /// Not bit for bit, and deliberately: the poles run whatever the knobs say, because a block
    /// they did not see is a block their memory is stale after, and that is a click the next
    /// time the effect is asked to do something. A filter at the top of its range passes
    /// everything, so what is left is arithmetic rather than sound.
    /// </remarks>
    [Fact]
    public void Doing_nothing_changes_almost_nothing()
    {
        var sweep = Made(Sweep.MostHz);

        Assert.InRange(Survives(sweep, 1000), 0.95, 1.05);
    }

    /// <summary>Mix at nothing hands back what it was given, exactly.</summary>
    /// <remarks>
    /// The mix is a crossfade, so at nought the filtered signal contributes nothing at all. The
    /// poles still hear the input, which is the whole point: coming off nought is then a fade
    /// from where the sound really is rather than a jump from wherever it was last.
    /// </remarks>
    [Fact]
    public void No_mix_is_no_effect()
    {
        var sweep = Made(200, resonance: 0.9, drive: 8, mix: 0);
        var buffer = new float[] { 0.5f, -0.25f };

        sweep.Process(buffer, 1);

        Assert.Equal(new float[] { 0.5f, -0.25f }, buffer);
    }

    /// <summary>
    /// A stretch spent dry does not leave the filter stale, which is what ticked.
    /// </summary>
    /// <remarks>
    /// The fault this pins was a fast path: a block skipped because the effect was doing nothing
    /// is a block the poles did not see, so their memory was from whenever it last did
    /// something. Coming back was then a step rather than a continuation, which is heard as a
    /// click and is very hard to find afterwards.
    ///
    /// Measured as the difference between two filters that have heard exactly the same audio and
    /// differ only in having been dry for a while. If the dry stretch leaves the state stale the
    /// two disagree; if the poles kept listening they agree.
    /// </remarks>
    [Fact]
    public void A_dry_stretch_does_not_leave_it_stale()
    {
        var kept = Made(600, resonance: 0.7);
        var went = Made(600, resonance: 0.7);

        const int block = 256;
        var one = new float[block * 2];
        var two = new float[block * 2];

        for (int round = 0; round < 12; round++)
        {
            for (int frame = 0; frame < block; frame++)
            {
                float value = (float)Math.Sin(2 * Math.PI * 300 * (round * block + frame) / Rate);
                one[frame * 2] = one[frame * 2 + 1] = value;
                two[frame * 2] = two[frame * 2 + 1] = value;
            }

            if (round is >= 4 and < 8) went.SetValue(Sweep.Mix, 0);
            else went.SetValue(Sweep.Mix, 1);

            kept.Process(one, block);
            went.Process(two, block);
        }

        for (int frame = 0; frame < block; frame++)
            Assert.Equal(one[frame * 2], two[frame * 2], 4);
    }

    /// <summary>The drive is before the poles, so it is what the resonance leans on.</summary>
    /// <remarks>
    /// Measured rather than asserted about: driving into a ringing filter changes what comes out
    /// at the cutoff, and if the drive were after the filter it would change everything equally.
    /// </remarks>
    [Fact]
    public void The_drive_is_in_front_of_the_filter()
    {
        double plain = Survives(Made(1000, resonance: 0.8), 1000);
        double driven = Survives(Made(1000, resonance: 0.8, drive: 6), 1000);

        Assert.NotEqual(plain, driven, 3);
    }

    /// <summary>The cutoff glides rather than jumping, so a sweep is not a click a message.</summary>
    /// <remarks>
    /// Shown by what a jump would do: moved from wide open to nearly shut between two blocks,
    /// the first block after the move still has plenty of the tone in it, because the filter is
    /// on its way rather than already there.
    /// </remarks>
    [Fact]
    public void The_cutoff_glides()
    {
        var sweep = Made(Sweep.MostHz);

        Survives(sweep, 4000);

        sweep.SetValue(Sweep.Cutoff, 100);

        var buffer = new float[512 * 2];

        for (int frame = 0; frame < 512; frame++)
        {
            float value = (float)Math.Sin(2 * Math.PI * 4000 * frame / Rate);
            buffer[frame * 2] = value;
            buffer[frame * 2 + 1] = value;
        }

        sweep.Process(buffer, 512);

        double peak = 0;
        for (int frame = 0; frame < 512; frame++) peak = Math.Max(peak, Math.Abs(buffer[frame * 2]));

        Assert.True(peak > 0.2, "one block after the move it is still on its way, not already shut: " + peak);
    }

    /// <summary>Every knob is held to its own ends, whatever it is handed.</summary>
    [Theory]
    [InlineData(Sweep.Cutoff, 1e9, Sweep.MostHz)]
    [InlineData(Sweep.Cutoff, -5, Sweep.LeastHz)]
    [InlineData(Sweep.Resonance, 4, Sweep.MostResonance)]
    [InlineData(Sweep.Resonance, -1, 0)]
    [InlineData(Sweep.Drive, 1000, Sweep.MostDrive)]
    [InlineData(Sweep.Drive, 0, 1)]
    [InlineData(Sweep.Mode, 9, Sweep.MostMode)]
    [InlineData(Sweep.Mix, 3, 1)]
    public void A_knob_is_held_to_its_ends(string key, double asked, double lands)
    {
        var sweep = new Sweep(Rate);

        sweep.SetValue(key, asked);

        Assert.Equal(lands, sweep.ValueOf(key), 4);
    }

    /// <summary>
    /// Anything that is not a number is refused outright.
    /// </summary>
    /// <remarks>
    /// <c>Math.Clamp</c> hands NaN back by design, and one NaN in a filter's own state is
    /// silence for the rest of the session.
    /// </remarks>
    [Fact]
    public void Nonsense_is_refused()
    {
        var sweep = Made(1000);

        sweep.SetValue(Sweep.Cutoff, double.NaN);

        Assert.Equal(1000, sweep.ValueOf(Sweep.Cutoff), 4);
    }

    /// <summary>A key it has not got reads as nought and writes nothing.</summary>
    /// <remarks>
    /// Which is what a chain saved by a later version looks like: read as far as it goes rather
    /// than refused.
    /// </remarks>
    [Fact]
    public void A_key_it_has_not_got_is_harmless()
    {
        var sweep = Made(1000);

        sweep.SetValue("wobble", 0.5);

        Assert.Equal(0, sweep.ValueOf("wobble"));
        Assert.Equal(0, sweep.ValueOf(null));
    }

    /// <summary>Every unhappy shape of a block is survived rather than thrown on.</summary>
    [Fact]
    public void A_block_it_cannot_use_is_left_alone()
    {
        var sweep = Made(1000);

        sweep.Process(null!, 64);
        sweep.Process(Array.Empty<float>(), 64);
        sweep.Process(new float[4], 0);
        sweep.Process(new float[4], -1);
        sweep.Process(new float[4], int.MaxValue);
    }

    /// <summary>The two sides are filtered apart, so a stereo take stays stereo.</summary>
    [Fact]
    public void The_sides_are_kept_apart()
    {
        var sweep = Made(400, resonance: 0.5);
        var buffer = new float[256 * 2];

        for (int frame = 0; frame < 256; frame++)
        {
            buffer[frame * 2] = 1;
            buffer[frame * 2 + 1] = 0;
        }

        sweep.Process(buffer, 256);

        bool anyRight = false;
        for (int frame = 0; frame < 256; frame++)
            if (Math.Abs(buffer[frame * 2 + 1]) > 1e-6) anyRight = true;

        Assert.False(anyRight, "silence on the right stays silent");
    }

    /// <summary>It says which effect it is standing for, since that is what a chain writes down.</summary>
    [Fact]
    public void It_knows_what_it_is_standing_for()
    {
        Assert.Equal("effect.sweeper", new Sweep(Rate, "effect.sweeper").Id);
        Assert.Equal("", new Sweep(Rate).Id);
    }
}
