using System;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Peak normalisation: what the loudest moment is, and what to multiply by to move it.
/// </summary>
/// <remarks>
/// Every one of these runs over somebody's whole recording and changes it in place, so the
/// interesting cases are the ones that would be silent: a file that is already silence, a gain
/// that would wrap a sample round to the other end of the range, and a target somebody has
/// edited into a file by hand.
/// </remarks>
public class NormalizationTests
{
    private readonly INormalization _levels = new Normalization();

    /// <summary>Nothing at all has no peak, and does not throw.</summary>
    [Fact]
    public void Nothing_has_no_peak()
    {
        Assert.Equal(0, _levels.PeakOf(null));
        Assert.Equal(0, _levels.PeakOf(Array.Empty<short>()));
    }

    /// <summary>The peak is the loudest magnitude, whichever side of nought it is on.</summary>
    [Fact]
    public void The_peak_is_the_loudest_magnitude()
    {
        Assert.Equal(0.5, _levels.PeakOf(new short[] { 0, 16384, -100 }), 6);
        Assert.Equal(0.5, _levels.PeakOf(new short[] { 0, -16384, 100 }), 6);
    }

    /// <summary>
    /// The lowest short there is has a peak rather than throwing.
    /// </summary>
    /// <remarks>
    /// Its magnitude has no answer in a short, so it is widened to an int first. Left as it was,
    /// this threw on any recording that ever touched the bottom of the range, which is most
    /// recordings that have been normalised before.
    /// </remarks>
    [Fact]
    public void The_bottom_of_the_range_has_a_peak()
    {
        Assert.Equal(1.0, _levels.PeakOf(new[] { short.MinValue }), 6);
        Assert.Equal(32767 / 32768.0, _levels.PeakOf(new[] { short.MaxValue }), 6);
    }

    /// <summary>Silence is left alone, since all that would come up is the noise floor.</summary>
    [Fact]
    public void Silence_is_left_alone()
    {
        Assert.Equal(1.0, _levels.GainFor(0, -1), 9);
        Assert.Equal(1.0, _levels.GainFor(_levels.SilenceAmplitude, -1), 9);
        Assert.Equal(1.0, _levels.GainFor(_levels.SilenceAmplitude / 2, -1), 9);
    }

    /// <summary>A peak that is not a number reads as silence rather than poisoning the gain.</summary>
    [Fact]
    public void A_peak_that_is_not_a_number_is_left_alone()
    {
        Assert.Equal(1.0, _levels.GainFor(double.NaN, -1), 9);
    }

    /// <summary>A target that is not a number reads as the one the page offers.</summary>
    [Fact]
    public void A_target_that_is_not_a_number_reads_as_the_default()
    {
        Assert.Equal(_levels.GainFor(0.5, _levels.DefaultTargetDecibels), _levels.GainFor(0.5, double.NaN), 9);
    }

    /// <summary>A target outside the range is brought back to its end.</summary>
    [Fact]
    public void A_target_out_of_range_is_held_at_its_end()
    {
        Assert.Equal(_levels.GainFor(0.5, _levels.MaxTargetDecibels), _levels.GainFor(0.5, 40), 9);
        Assert.Equal(_levels.GainFor(0.5, _levels.MinTargetDecibels), _levels.GainFor(0.5, -400), 9);
    }

    /// <summary>The gain really does put the peak on the target.</summary>
    [Fact]
    public void The_gain_puts_the_peak_on_the_target()
    {
        foreach (double peak in new[] { 0.1, 0.25, 0.5, 0.9 })
        {
            double gain = _levels.GainFor(peak, -1);

            Assert.Equal(-1.0, _levels.ToDecibels(peak * gain), 6);
        }
    }

    /// <summary>A file already at the target is left where it is.</summary>
    [Fact]
    public void A_file_already_there_is_left_where_it_is()
    {
        double at = _levels.ToAmplitude(-1);

        Assert.Equal(1.0, _levels.GainFor(at, -1), 6);
    }

    /// <summary>The lift is bounded, so a nearly silent file does not bring its noise floor up.</summary>
    [Fact]
    public void The_lift_is_bounded()
    {
        Assert.Equal(_levels.MaxGain, _levels.GainFor(0.00002, 0), 6);
    }

    /// <summary>
    /// Nothing a real recording can ask for is ever turned down to the floor.
    /// </summary>
    /// <remarks>
    /// The quietest target is -24 dB, which is an amplitude of 0.063, and a peak read off a
    /// recording is at most one, so the quietest gain a real pair can produce is 0.063: six
    /// times the floor. The floor is reachable only by a peak above full scale, which nothing
    /// here measures, and it is kept because this takes two doubles from wherever and a gain
    /// that is not bounded at both ends is not bounded.
    /// </remarks>
    [Fact]
    public void The_floor_is_below_anything_a_recording_reaches()
    {
        for (double peak = 0.001; peak <= 1.0; peak += 0.001)
            Assert.True(_levels.GainFor(peak, -400) > 1.0 / _levels.MaxGain);

        Assert.Equal(1.0 / _levels.MaxGain, _levels.GainFor(100, -400), 6);
    }

    /// <summary>Applying nothing changes nothing, and does not walk the array.</summary>
    [Fact]
    public void A_gain_of_one_changes_nothing()
    {
        var samples = new short[] { 1, -1, 1000 };

        _levels.Apply(samples, 1);

        Assert.Equal(new short[] { 1, -1, 1000 }, samples);
    }

    /// <summary>Nothing at all, and a gain that is not a number, are both passed over.</summary>
    [Fact]
    public void Nothing_to_apply_to_is_passed_over()
    {
        _levels.Apply(null, 2);
        _levels.Apply(Array.Empty<short>(), 2);

        var samples = new short[] { 100 };
        _levels.Apply(samples, double.NaN);

        Assert.Equal(new short[] { 100 }, samples);
    }

    /// <summary>A gain is applied where the samples lie.</summary>
    [Fact]
    public void A_gain_is_applied_in_place()
    {
        var samples = new short[] { 100, -100, 0 };

        _levels.Apply(samples, 2);

        Assert.Equal(new short[] { 200, -200, 0 }, samples);
    }

    /// <summary>
    /// A gain that would take a sample past the end holds it there rather than wrapping.
    /// </summary>
    /// <remarks>
    /// The failure this stops is loud and unmistakable: a short that wraps comes out at the
    /// other end of the range, so a peak becomes a full scale click in the opposite direction.
    /// </remarks>
    [Fact]
    public void A_gain_past_the_end_holds_rather_than_wraps()
    {
        var samples = new short[] { 30000, -30000 };

        _levels.Apply(samples, 4);

        Assert.Equal(new short[] { short.MaxValue, short.MinValue }, samples);
    }

    /// <summary>Nothing a real gain can do takes a sample outside the range.</summary>
    [Fact]
    public void No_gain_ever_leaves_the_range()
    {
        foreach (double gain in new[] { 0.001, 0.5, 1.5, 10.0, 100.0 })
        {
            var samples = new short[] { short.MinValue, -1, 0, 1, short.MaxValue };

            _levels.Apply(samples, gain);

            foreach (short sample in samples)
                Assert.InRange(sample, short.MinValue, short.MaxValue);
        }
    }

    /// <summary>Decibels and amplitude are each other's opposite, over the whole range.</summary>
    [Fact]
    public void The_two_scales_undo_each_other()
    {
        for (double db = _levels.MinTargetDecibels; db <= _levels.MaxTargetDecibels; db += 0.5)
            Assert.Equal(db, _levels.ToDecibels(_levels.ToAmplitude(db)), 9);
    }

    /// <summary>Silence reads as the quietest target rather than as minus infinity.</summary>
    [Fact]
    public void Silence_reads_as_the_quietest_target()
    {
        Assert.Equal(_levels.MinTargetDecibels, _levels.ToDecibels(0), 9);
        Assert.Equal(_levels.MinTargetDecibels, _levels.ToDecibels(_levels.SilenceAmplitude), 9);
        Assert.True(double.IsFinite(_levels.ToDecibels(-1)));
    }

    /// <summary>Full scale is nought decibels, which is what the whole scale is hung on.</summary>
    [Fact]
    public void Full_scale_is_nought()
    {
        Assert.Equal(0.0, _levels.ToDecibels(1.0), 9);
        Assert.Equal(1.0, _levels.ToAmplitude(0), 9);
    }

    /// <summary>Reading a peak, working out a gain and applying it lands where it said it would.</summary>
    [Fact]
    public void The_three_steps_together_land_on_the_target()
    {
        var samples = new short[] { 4000, -3000, 1000 };

        double gain = _levels.GainFor(_levels.PeakOf(samples), -1);
        _levels.Apply(samples, gain);

        Assert.Equal(-1.0, _levels.ToDecibels(_levels.PeakOf(samples)), 2);
    }
}
