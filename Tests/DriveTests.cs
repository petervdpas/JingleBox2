using System;
using JingleBox2.SoundDevices.SoundEffects;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The drive, measured rather than listened to.
/// </summary>
/// <remarks>
/// What a drive does is countable even though what it is for is not: peaks that were sharp come
/// back rounded, a sine comes back with harmonics in it that were not there, and the level it
/// costs is given back. None of this asks whether it sounds good.
///
/// The trap it was written around is the one the synth's own drive recorded: the makeup levels
/// the curve at full scale and nowhere else, so without a fade the knob steps as it leaves its
/// stop. That is measured here rather than believed, because it is invisible until somebody
/// turns the knob slowly and hears the effect switch on.
/// </remarks>
public class DriveTests
{
    /// <summary>What everything here is measured at.</summary>
    private const int Rate = 48000;

    /// <summary>A drive with its knobs where the test wants them.</summary>
    private static Drive Made(double amount, double tilt = 0, double bias = 0, double level = 0, double mix = 1)
    {
        var drive = new Drive(Rate);

        drive.SetValue(Drive.Amount, amount);
        drive.SetValue(Drive.Tilt, tilt);
        drive.SetValue(Drive.Bias, bias);
        drive.SetValue(Drive.Level, level);
        drive.SetValue(Drive.Mix, mix);

        return drive;
    }

    /// <summary>Plays a tone through and answers the loudest it got once settled.</summary>
    /// <param name="drive">The effect under test.</param>
    /// <param name="hz">What tone to play it.</param>
    /// <param name="level">How loud the tone is.</param>
    private static double Peak(Drive drive, double hz = 200, double level = 1)
    {
        const int block = 512;
        var buffer = new float[block * 2];

        double peak = 0;
        long at = 0;

        for (int round = 0; round < 8; round++)
        {
            for (int frame = 0; frame < block; frame++, at++)
            {
                float value = (float)(Math.Sin(2 * Math.PI * hz * at / Rate) * level);
                buffer[frame * 2] = value;
                buffer[frame * 2 + 1] = value;
            }

            drive.Process(buffer, block);

            if (round < 4) continue;

            for (int frame = 0; frame < block; frame++)
                peak = Math.Max(peak, Math.Abs(buffer[frame * 2]));
        }

        return peak;
    }

    /// <summary>
    /// The makeup gives back what the curve costs, so driving does not just mean quieter.
    /// </summary>
    /// <remarks>
    /// Which is the whole reason it exists: deciding whether you like an effect while it is also
    /// six decibels down is not a comparison at all.
    /// </remarks>
    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(24)]
    public void Driving_does_not_mean_quieter(double amount)
    {
        double peak = Peak(Made(amount));

        Assert.InRange(peak, 0.85, 1.15);
    }

    /// <summary>
    /// The knob does not step as it leaves its stop.
    /// </summary>
    /// <remarks>
    /// The synth's drive stepped 1.6 decibels here, because the makeup levels the curve at full
    /// scale and nowhere else. The fade over the first unit of the range is what fixes it, and
    /// this is the measurement that would catch its going missing.
    /// </remarks>
    [Fact]
    public void The_knob_does_not_step_off_its_stop()
    {
        double none = Peak(Made(1));
        double barely = Peak(Made(1.05));

        Assert.InRange(barely / none, 0.9, 1.1);
    }

    /// <summary>It really does round the peaks, which is what the curve is for.</summary>
    /// <remarks>
    /// A sine driven hard comes back nearer a square, so what is under the curve goes up while
    /// the peak stays where the makeup put it. Measured as the average against the peak, since
    /// that ratio is what "squarer" means without a spectrum.
    /// </remarks>
    [Fact]
    public void It_rounds_the_peaks()
    {
        Assert.True(Fullness(Made(1)) < Fullness(Made(20)), "driven hard, there is more under the curve");
    }

    /// <summary>How full the shape is: the average against the peak, over one settled run.</summary>
    /// <param name="drive">The effect under test.</param>
    private static double Fullness(Drive drive)
    {
        const int block = 1024;
        var buffer = new float[block * 2];

        for (int frame = 0; frame < block; frame++)
        {
            float value = (float)Math.Sin(2 * Math.PI * 200 * frame / Rate);
            buffer[frame * 2] = value;
            buffer[frame * 2 + 1] = value;
        }

        drive.Process(buffer, block);

        double sum = 0, peak = 0;

        for (int frame = 0; frame < block; frame++)
        {
            double value = Math.Abs(buffer[frame * 2]);
            sum += value;
            peak = Math.Max(peak, value);
        }

        return peak <= 0 ? 0 : sum / block / peak;
    }

    /// <summary>
    /// The bias is taken back out, so the effect does not put a step in the output.
    /// </summary>
    /// <remarks>
    /// A leaned curve is what adds the even harmonics, and leaving the lean in afterwards is a
    /// direct offset that every speaker in the building would try to reproduce.
    ///
    /// Measured once it has settled rather than from cold, because what takes the offset out is
    /// a filter and a filter takes a moment: what is being asked is whether an offset is left
    /// standing, not whether one exists during the first forty milliseconds. From cold it reads
    /// about a tenth, which is the filter on its way rather than a fault.
    /// </remarks>
    [Fact]
    public void The_bias_does_not_leave_an_offset()
    {
        const int block = 4096;
        var buffer = new float[block * 2];
        var drive = Made(8, bias: 0.4);

        double mean = 0;

        for (int round = 0; round < 8; round++)
        {
            for (int frame = 0; frame < block; frame++)
            {
                float value = (float)Math.Sin(2 * Math.PI * 200 * (round * block + frame) / Rate);
                buffer[frame * 2] = value;
                buffer[frame * 2 + 1] = value;
            }

            drive.Process(buffer, block);

            double sum = 0;
            for (int frame = 0; frame < block; frame++) sum += buffer[frame * 2];

            mean = sum / block;
        }

        Assert.InRange(mean, -0.02, 0.02);
    }

    /// <summary>The tilt decides which end gets bitten.</summary>
    /// <remarks>
    /// Leaning it up drives the top, so a bass tone comes back less changed than it does leaning
    /// down. Compared against each other rather than against a number, since what matters is
    /// that the two ends differ and in which direction.
    /// </remarks>
    [Fact]
    public void The_tilt_chooses_what_is_bitten()
    {
        double up = Fullness(Made(12, tilt: 1));
        double down = Fullness(Made(12, tilt: -1));

        Assert.True(down > up, "leaning down bites the bass harder: " + down + " against " + up);
    }

    /// <summary>The level knob does what it says, in decibels.</summary>
    [Fact]
    public void The_level_is_decibels()
    {
        double unity = Peak(Made(1, level: 0), level: 0.5);
        double down = Peak(Made(1, level: -6), level: 0.5);

        Assert.InRange(down / unity, 0.45, 0.55);
    }

    /// <summary>Mix keeps the dry signal under the driven one, which is parallel drive.</summary>
    [Fact]
    public void Mix_keeps_the_dry_under_it()
    {
        var buffer = new float[] { 0.5f, 0.5f };

        Made(20, mix: 0).Process(buffer, 1);

        Assert.Equal(new float[] { 0.5f, 0.5f }, buffer);
    }

    /// <summary>At no drive and unity level it hands back what it was given.</summary>
    [Fact]
    public void Doing_nothing_changes_nothing()
    {
        var buffer = new float[] { 0.5f, -0.25f, 0.75f, 1f };

        Made(1).Process(buffer, 2);

        Assert.Equal(new float[] { 0.5f, -0.25f, 0.75f, 1f }, buffer);
    }

    /// <summary>Every knob is held to its own ends, whatever it is handed.</summary>
    [Theory]
    [InlineData(Drive.Amount, 1e6, Drive.MostAmount)]
    [InlineData(Drive.Amount, -3, Drive.LeastAmount)]
    [InlineData(Drive.Tilt, 9, Drive.MostTilt)]
    [InlineData(Drive.Tilt, -9, -Drive.MostTilt)]
    [InlineData(Drive.Bias, 9, Drive.MostBias)]
    [InlineData(Drive.Level, 99, Drive.MostLevelDb)]
    [InlineData(Drive.Level, -99, Drive.LeastLevelDb)]
    [InlineData(Drive.Mix, 3, 1)]
    public void A_knob_is_held_to_its_ends(string key, double asked, double lands)
    {
        var drive = new Drive(Rate);

        drive.SetValue(key, asked);

        Assert.Equal(lands, drive.ValueOf(key), 4);
    }

    /// <summary>Anything that is not a number is refused outright.</summary>
    [Fact]
    public void Nonsense_is_refused()
    {
        var drive = Made(6);

        drive.SetValue(Drive.Amount, double.NaN);

        Assert.Equal(6, drive.ValueOf(Drive.Amount), 4);
    }

    /// <summary>A key it has not got reads as nought and writes nothing.</summary>
    [Fact]
    public void A_key_it_has_not_got_is_harmless()
    {
        var drive = Made(6);

        drive.SetValue("crunchiness", 0.5);

        Assert.Equal(0, drive.ValueOf("crunchiness"));
        Assert.Equal(0, drive.ValueOf(null));
    }

    /// <summary>Every unhappy shape of a block is survived rather than thrown on.</summary>
    [Fact]
    public void A_block_it_cannot_use_is_left_alone()
    {
        var drive = Made(6);

        drive.Process(null!, 64);
        drive.Process(Array.Empty<float>(), 64);
        drive.Process(new float[4], 0);
        drive.Process(new float[4], -1);
        drive.Process(new float[4], int.MaxValue);
    }

    /// <summary>Nothing it hands back is silent or nonsense, however hard it is pushed.</summary>
    [Fact]
    public void It_never_answers_nonsense()
    {
        var buffer = new float[512 * 2];

        for (int frame = 0; frame < 512; frame++)
        {
            buffer[frame * 2] = (float)Math.Sin(2 * Math.PI * 200 * frame / Rate);
            buffer[frame * 2 + 1] = buffer[frame * 2];
        }

        Made(Drive.MostAmount, tilt: 1, bias: Drive.MostBias, level: Drive.MostLevelDb).Process(buffer, 512);

        foreach (float value in buffer)
        {
            Assert.False(float.IsNaN(value));
            Assert.False(float.IsInfinity(value));
        }
    }

    /// <summary>It says which effect it is standing for, since that is what a chain writes down.</summary>
    [Fact]
    public void It_knows_what_it_is_standing_for()
    {
        Assert.Equal("effect.roaster", new Drive(Rate, "effect.roaster").Id);
        Assert.Equal("", new Drive(Rate).Id);
    }
}
