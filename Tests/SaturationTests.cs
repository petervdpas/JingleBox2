using System;
using JingleBox2.Tracker.Synth;
using JingleBox2.Tracker.Synth.Enums;
using JingleBox2.Tracker.Synth.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The drive curve and the waveform shapes, which are the two pieces of a synth voice that hold
/// nothing between calls and so can be asked anything at all without a note being started.
/// </summary>
/// <remarks>
/// Both run once per sample per voice on the audio thread, so what matters is not that they are
/// right in the middle of their range but that they are still finite at the ends of it. A patch
/// is JSON on disc and a phase is a running sum, so nought, a negative, a value past the end and
/// a value that is not a number at all all arrive here eventually.
///
/// Several tests below record what the code does today rather than what it ought to do; each one
/// says so in its own documentation.
/// </remarks>
public class SaturationTests
{
    /// <summary>The curve, reached the way a voice reaches it.</summary>
    private readonly ISaturation _drive = new Saturation();

    /// <summary>The shapes, reached the way a voice reaches them.</summary>
    private readonly IOscillator _osc = new Oscillator();

    /// <summary>
    /// A drive of one or less is the control switched off, and the wave comes out as it went in.
    /// </summary>
    /// <remarks>
    /// One is the bottom of the patch's own range. Nought and a negative come off a file
    /// somebody has edited, and read as the same thing rather than as an inversion.
    /// </remarks>
    [Fact]
    public void No_drive_leaves_the_wave_alone()
    {
        Assert.Equal(0.5, _drive.Apply(0.5, 1.0), 12);
        Assert.Equal(0.5, _drive.Apply(0.5, 0.0), 12);
        Assert.Equal(0.5, _drive.Apply(0.5, -3.0), 12);
        Assert.Equal(-0.75, _drive.Apply(-0.75, SynthPatch.MinDrive), 12);
    }

    /// <summary>The makeup is one until there is something to make up for.</summary>
    [Fact]
    public void Makeup_is_one_until_the_drive_is()
    {
        Assert.Equal(1.0, _drive.Makeup(1.0), 12);
        Assert.Equal(1.0, _drive.Makeup(0.0), 12);
        Assert.Equal(1.0, _drive.Makeup(-10.0), 12);
        Assert.True(_drive.Makeup(4.0) > 1.0);
    }

    /// <summary>
    /// Full scale in is full scale out, at every drive there is. That is the whole point of the
    /// makeup: the drive fills the tone out without making it louder.
    /// </summary>
    [Fact]
    public void Full_scale_comes_back_full_scale()
    {
        foreach (double drive in new[] { 1.0, 1.5, 2.0, 5.0, SynthPatch.MaxDrive })
        {
            Assert.Equal(1.0, _drive.Apply(1.0, drive), 12);
            Assert.Equal(-1.0, _drive.Apply(-1.0, drive), 12);
        }
    }

    /// <summary>The curve is the same shape either side of nought, so it adds no even harmonics.</summary>
    [Fact]
    public void The_curve_is_the_same_shape_either_side()
    {
        for (double sample = 0.05; sample <= 1.0; sample += 0.05)
            Assert.Equal(-_drive.Apply(sample, 3.0), _drive.Apply(-sample, 3.0), 12);
    }

    /// <summary>
    /// Nothing inside the patch's own range can push a sample past full scale, at any drive.
    /// </summary>
    /// <remarks>
    /// Two thousand readings across the whole of both controls. A curve that went above one
    /// anywhere in there would clip the mix on a patch nobody had done anything unusual to.
    /// </remarks>
    [Fact]
    public void A_driven_sample_never_leaves_its_bounds()
    {
        for (double drive = SynthPatch.MinDrive; drive <= SynthPatch.MaxDrive; drive += 0.1)
        {
            double makeup = _drive.Makeup(drive);

            for (double sample = -1.0; sample <= 1.0; sample += 0.02)
            {
                double driven = _drive.Apply(sample, drive, makeup);

                Assert.True(double.IsFinite(driven));
                Assert.True(Math.Abs(driven) <= 1.0 + 1e-12);
            }
        }
    }

    /// <summary>A drive that is not a number at all is read as no drive rather than as a fault.</summary>
    /// <remarks>
    /// Not by a guard, but because every comparison against NaN is false and the curve is only
    /// reached when the drive is greater than one. It comes out right for the wrong reason,
    /// which is worth a test in case the comparison is ever rewritten as a clamp.
    /// </remarks>
    [Fact]
    public void A_drive_that_is_not_a_number_is_no_drive()
    {
        Assert.Equal(0.5, _drive.Apply(0.5, double.NaN), 12);
        Assert.Equal(1.0, _drive.Makeup(double.NaN), 12);
    }

    /// <summary>An endless drive is a hard clip, silence included.</summary>
    /// <remarks>
    /// Silence used to come back as not a number. Nought times infinity is NaN before the
    /// tangent ever sees it, so the one sample no curve can move was the one that came back
    /// poisoned. Nothing in the patch can reach an infinite drive; a file somebody has edited
    /// can. Silence is answered before the curve is reached now, which is free for every other
    /// drive as well, since a curve through the origin has nothing to say about nought.
    /// </remarks>
    [Fact]
    public void An_endless_drive_clips_and_leaves_silence_alone()
    {
        Assert.Equal(1.0, _drive.Apply(0.5, double.PositiveInfinity), 12);
        Assert.Equal(-1.0, _drive.Apply(-0.5, double.PositiveInfinity), 12);

        Assert.Equal(0.0, _drive.Apply(0.0, double.PositiveInfinity), 12);
        Assert.Equal(0.0, _drive.Apply(0.0, 4.0), 12);
        Assert.Equal(0.0, _drive.Apply(0.0, 1.0), 12);
    }

    /// <summary>A sample that is not a number stays that way, driven or not.</summary>
    /// <remarks>
    /// The curve holds nothing between calls, so one bad sample is one bad sample and the next
    /// one is fine: there is nothing here to poison. The ordinary sample after it is checked in
    /// the same test for exactly that reason, and it is checked against what the curve really
    /// does rather than against the value that went in, since driving a sample is meant to move
    /// it. Half of full scale at a drive of four comes out at tanh(2) over tanh(4), which is
    /// most of the way to the top; that is the saturation, not a fault.
    /// </remarks>
    [Fact]
    public void A_sample_that_is_not_a_number_comes_back_the_same()
    {
        Assert.True(double.IsNaN(_drive.Apply(double.NaN, 4.0)));
        Assert.True(double.IsNaN(_drive.Apply(double.NaN, 1.0)));

        Assert.Equal(Math.Tanh(2.0) / Math.Tanh(4.0), _drive.Apply(0.5, 4.0), 12);
        Assert.False(double.IsNaN(_drive.Apply(0.5, 4.0)));
    }

    /// <summary>The bottom of the drive range is a slope rather than a step.</summary>
    /// <remarks>
    /// It used to be a step. The makeup levels the curve at full scale and nowhere else, so
    /// everything below full scale was lifted the moment the control left its bottom end, and
    /// half scale jumped from 0.5 to 0.607, about a decibel and a half, on a knob movement of
    /// nothing at all. The curve is faded in over the first unit of the range instead, so a
    /// drive of two and anything above it is exactly what it always was.
    /// </remarks>
    [Fact]
    public void The_bottom_of_the_drive_range_is_a_slope()
    {
        Assert.Equal(0.5, _drive.Apply(0.5, 1.0), 6);
        Assert.Equal(0.5, _drive.Apply(0.5, 1.0000001), 6);

        Assert.Equal(Math.Tanh(1.0) / Math.Tanh(2.0), _drive.Apply(0.5, 2.0), 12);
        Assert.Equal(Math.Tanh(2.0) / Math.Tanh(4.0), _drive.Apply(0.5, 4.0), 12);
    }

    /// <summary>Nothing in the drive range jumps: the curve walks the knob without a break.</summary>
    /// <remarks>
    /// Ten thousand steps from the bottom of the range to the top, at three points on the wave.
    /// A step of a thousandth of the range may not move a sample by more than a hundredth,
    /// which is far looser than the curve really is and far tighter than the 0.107 the bottom
    /// end used to jump by.
    /// </remarks>
    [Fact]
    public void The_whole_drive_range_is_continuous()
    {
        const int steps = 10000;
        double span = (SynthPatch.MaxDrive - SynthPatch.MinDrive) / steps;

        foreach (double sample in new[] { 0.25, 0.5, 0.9 })
        {
            double last = _drive.Apply(sample, SynthPatch.MinDrive);

            for (int i = 1; i <= steps; i++)
            {
                double next = _drive.Apply(sample, SynthPatch.MinDrive + i * span);

                Assert.True(Math.Abs(next - last) < 0.01, $"a jump of {Math.Abs(next - last)} at {sample}");
                last = next;
            }
        }
    }

    /// <summary>Full scale still comes out at full scale, which is what the makeup is for.</summary>
    [Fact]
    public void Full_scale_survives_every_drive()
    {
        for (double drive = SynthPatch.MinDrive; drive <= SynthPatch.MaxDrive; drive += 0.25)
        {
            Assert.Equal(1.0, _drive.Apply(1.0, drive), 12);
            Assert.Equal(-1.0, _drive.Apply(-1.0, drive), 12);
        }
    }

    /// <summary>
    /// A makeup handed in is used exactly as given, and is not checked against the drive.
    /// </summary>
    /// <remarks>
    /// A voice works its makeup out once when the note starts and passes it in on every sample
    /// after that, so the curve has to trust it. Handing in nought is silence rather than an
    /// argument.
    /// </remarks>
    [Fact]
    public void A_makeup_handed_in_is_used_as_given()
    {
        Assert.Equal(0.0, _drive.Apply(0.5, 4.0, 0.0), 12);
        Assert.Equal(2 * _drive.Apply(0.5, 4.0, 1.0), _drive.Apply(0.5, 4.0, 2.0), 12);
    }

    /// <summary>Every shape stays inside full scale right across a cycle.</summary>
    /// <remarks>
    /// A thousand readings a shape. The noise is handed a value inside the range, since noise is
    /// whatever the voice's own generator gives it and the shape does not bound it.
    /// </remarks>
    [Fact]
    public void Every_shape_stays_inside_full_scale()
    {
        foreach (SynthWave wave in Enum.GetValues<SynthWave>())
        {
            for (int step = 0; step < 1000; step++)
            {
                double phase = step / 1000.0;
                double value = _osc.Sample(wave, phase, 0.3, 0.5);

                Assert.True(double.IsFinite(value));
                Assert.True(Math.Abs(value) <= 1.0);
            }
        }
    }

    /// <summary>The four fixed shapes are where a picture of them would put them.</summary>
    /// <remarks>
    /// The triangle starts at the top rather than at nought, which is a phase offset and not a
    /// fault: it is the shape as this codebase draws and sounds it.
    /// </remarks>
    [Fact]
    public void The_shapes_are_where_they_should_be()
    {
        Assert.Equal(0.0, _osc.Sample(SynthWave.Sine, 0.0, 0.5, 0), 12);
        Assert.Equal(1.0, _osc.Sample(SynthWave.Sine, 0.25, 0.5, 0), 12);
        Assert.Equal(-1.0, _osc.Sample(SynthWave.Sine, 0.75, 0.5, 0), 12);

        Assert.Equal(1.0, _osc.Sample(SynthWave.Square, 0.0, 0.5, 0), 12);
        Assert.Equal(-1.0, _osc.Sample(SynthWave.Square, 0.5, 0.5, 0), 12);

        Assert.Equal(-1.0, _osc.Sample(SynthWave.Saw, 0.0, 0.5, 0), 12);
        Assert.Equal(0.0, _osc.Sample(SynthWave.Saw, 0.5, 0.5, 0), 12);
        Assert.Equal(1.0, _osc.Sample(SynthWave.Saw, 1.0, 0.5, 0), 12);

        Assert.Equal(1.0, _osc.Sample(SynthWave.Triangle, 0.0, 0.5, 0), 12);
        Assert.Equal(-1.0, _osc.Sample(SynthWave.Triangle, 0.5, 0.5, 0), 12);
        Assert.Equal(1.0, _osc.Sample(SynthWave.Triangle, 1.0, 0.5, 0), 12);
    }

    /// <summary>
    /// A pulse with no width never goes high and one at full width never comes down.
    /// </summary>
    /// <remarks>
    /// Neither is reachable from the panel, which stops at <see cref="SynthPatch.MinDuty"/> and
    /// <see cref="SynthPatch.MaxDuty"/> for exactly this reason: a pulse at either end is a
    /// square wave that has stopped being a wave.
    /// </remarks>
    [Fact]
    public void A_pulse_at_either_end_of_its_width_is_a_flat_line()
    {
        for (double phase = 0; phase < 1.0; phase += 0.05)
        {
            Assert.Equal(-1.0, _osc.Sample(SynthWave.Pulse, phase, 0.0, 0), 12);
            Assert.Equal(1.0, _osc.Sample(SynthWave.Pulse, phase, 1.0, 0), 12);
        }

        Assert.Equal(-1.0, _osc.Sample(SynthWave.Pulse, 1.0, 1.0, 0), 12);
    }

    /// <summary>A width that is not a number leaves the pulse down for the whole cycle.</summary>
    /// <remarks>
    /// This records what the code does today. Every comparison against NaN is false, so the
    /// pulse is never high; a silent voice rather than a poisoned one, which is the right way
    /// round for a fault nobody guarded against.
    /// </remarks>
    [Fact]
    public void A_width_that_is_not_a_number_leaves_the_pulse_down()
    {
        Assert.Equal(-1.0, _osc.Sample(SynthWave.Pulse, 0.1, double.NaN, 0), 12);
        Assert.Equal(-1.0, _osc.Sample(SynthWave.Pulse, 0.9, double.NaN, 0), 12);
    }

    /// <summary>
    /// The noise value is handed straight back, whatever it is, and no other shape looks at it.
    /// </summary>
    /// <remarks>
    /// The shape holds no generator, so two noise voices started at the same instant are not
    /// the same noise. The cost of that is that nothing here bounds the value: a generator
    /// handing back four gets four.
    /// </remarks>
    [Fact]
    public void Noise_is_whatever_the_voice_handed_in()
    {
        Assert.Equal(0.25, _osc.Sample(SynthWave.Noise, 0.0, 0.5, 0.25), 12);
        Assert.Equal(-0.9, _osc.Sample(SynthWave.Noise, 0.7, 0.5, -0.9), 12);
        Assert.Equal(4.0, _osc.Sample(SynthWave.Noise, 0.7, 0.5, 4.0), 12);
        Assert.True(double.IsNaN(_osc.Sample(SynthWave.Noise, 0.7, 0.5, double.NaN)));

        Assert.Equal(
            _osc.Sample(SynthWave.Saw, 0.3, 0.5, 0.0),
            _osc.Sample(SynthWave.Saw, 0.3, 0.5, 0.9),
            12);
    }

    /// <summary>A shape number nobody has heard of is silence, not a throw.</summary>
    /// <remarks>
    /// The numbers are written into songs and presets, so a song from a later version can name
    /// a shape this one does not have. A quiet voice is recoverable; an exception on the audio
    /// thread is not.
    /// </remarks>
    [Fact]
    public void A_shape_nobody_has_heard_of_is_silence()
    {
        Assert.Equal(0.0, _osc.Sample((SynthWave)99, 0.3, 0.5, 0.7), 12);
        Assert.Equal(0.0, _osc.Sample((SynthWave)(-1), 0.3, 0.5, 0.7), 12);
    }

    /// <summary>A phase that has run off either end comes back inside one cycle.</summary>
    [Fact]
    public void A_phase_off_either_end_comes_back_inside()
    {
        Assert.Equal(0.25, _osc.Wrap(3.25), 12);
        Assert.Equal(0.75, _osc.Wrap(12.75), 12);
        Assert.Equal(0.0, _osc.Wrap(1.0), 12);
        Assert.Equal(0.0, _osc.Wrap(1e18), 12);

        Assert.Equal(0.75, _osc.Wrap(-0.25), 12);
        Assert.Equal(0.5, _osc.Wrap(-2.5), 12);
        Assert.Equal(0.0, _osc.Wrap(-1.0), 12);
        Assert.Equal(0.0, _osc.Wrap(-3.0), 12);

        Assert.Equal(0.0, _osc.Wrap(0.0), 12);
        Assert.Equal(0.5, _osc.Wrap(0.5), 12);
    }

    /// <summary>A phase a hair below nought wraps to exactly one rather than to just under it.</summary>
    /// <remarks>
    /// This records what the code does today rather than what it should do. A cycle is half
    /// open: <c>Wrap(1.0)</c> is nought, so nothing else should ever land on one. Adding one to
    /// a number smaller than the gap between doubles at that magnitude rounds straight to one,
    /// and the caller gets a phase the shapes read as the start of the next cycle. Nothing
    /// sounds wrong, since every shape is defined at one, but a caller counting cycles off the
    /// phase would miscount.
    /// </remarks>
    [Fact]
    public void A_phase_a_hair_below_nought_wraps_to_exactly_one()
    {
        Assert.Equal(1.0, _osc.Wrap(-1e-18), 12);
    }

    /// <summary>A phase that is not a finite number starts the cycle again.</summary>
    /// <remarks>
    /// All three used to come back as not a number and stay that way. Both comparisons are
    /// false for NaN, so it passed through untouched, and infinity took the first branch and
    /// came out as infinity less infinity, which is NaN as well. The phase is a running sum, so
    /// a voice that reached either state was silent for the rest of its life. Nothing in the
    /// patch can produce one: a step worked out from the ratio of a bad tuning can.
    /// </remarks>
    [Fact]
    public void A_phase_that_is_not_a_finite_number_starts_again()
    {
        Assert.Equal(0.0, _osc.Wrap(double.NaN), 12);
        Assert.Equal(0.0, _osc.Wrap(double.PositiveInfinity), 12);
        Assert.Equal(0.0, _osc.Wrap(double.NegativeInfinity), 12);

        Assert.Equal(0.25, _osc.Wrap(0.25), 12);
        Assert.Equal(0.25, _osc.Wrap(4.25), 12);
    }

    /// <summary>Reading a shape does not wrap the phase for its caller.</summary>
    /// <remarks>
    /// This records what the code does today. The two are separate calls and the voice is
    /// expected to wrap before it reads, so a saw read at a phase of three comes back at five,
    /// well outside the range the interface promises. Worth knowing, because it is the shape a
    /// forgotten <c>Wrap</c> would take: a voice that gets louder the longer it plays.
    /// </remarks>
    [Fact]
    public void Reading_a_shape_does_not_wrap_the_phase_first()
    {
        Assert.Equal(5.0, _osc.Sample(SynthWave.Saw, 3.0, 0.5, 0), 12);
        Assert.Equal(9.0, _osc.Sample(SynthWave.Triangle, 3.0, 0.5, 0), 12);
    }
}
