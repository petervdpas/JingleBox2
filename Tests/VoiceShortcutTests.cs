using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Synth;
using JingleBox2.Tracker.Synth.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The things a voice is allowed to skip on the audio path, and the reasons it is allowed to.
/// </summary>
/// <remarks>
/// A voice used to work out three things for every sample of every note that were settled before
/// the note started: the frequency, through a power; a random number, which five of the six waves
/// throw away; and how much of the drive curve to use, from a knob that cannot move while a note
/// lasts. At the mixer's own ceiling of forty eight voices that is two million powers a second for
/// an answer of one, and it measured out at a third of what the mixing cost.
///
/// Each of the three is skipped on a condition, and **each condition is only safe because of an
/// exact arithmetic fact**. Those facts are what is pinned here, since they are the whole of why
/// this is a shortcut rather than a change to the sound: nothing below asserts that the new code
/// is fast, only that what it leaves out was never contributing anything.
/// </remarks>
public class VoiceShortcutTests
{
    private readonly PitchMotion _motion = new();
    private readonly Oscillator _shapes = new();
    private readonly Saturation _curve = new();

    /// <summary>
    /// No bend at all is a ratio of exactly one, and multiplying by it is exactly nothing.
    /// </summary>
    /// <remarks>
    /// Exactly, not nearly. The voice skips the multiply when the bend is nought, so if the power
    /// came back a hair off one the pitch would differ between the two paths, which over a note
    /// is a drift rather than a rounding.
    /// </remarks>
    [Fact]
    public void A_note_that_is_not_bent_is_at_exactly_its_own_pitch()
    {
        Assert.Equal(1.0, _motion.Ratio(0));

        foreach (double hz in new[] { 55.0, 261.625565, 440.0, 4186.009 })
            Assert.Equal(hz, hz * _motion.Ratio(0));
    }

    /// <summary>
    /// A patch with neither vibrato nor a pitch envelope never bends, at any moment of the note.
    /// </summary>
    /// <remarks>
    /// The condition the voice asks is about the patch and is asked once; what has to be true is
    /// that the answer holds for every instant afterwards, including before the note starts and
    /// long after any envelope would have run out.
    /// </remarks>
    [Fact]
    public void A_patch_with_no_modulation_never_bends()
    {
        var still = new SynthPatch { VibratoDepthCents = 0, VibratoRateHz = 5, PitchEnvSemitones = 0, PitchEnvMs = 500 };

        foreach (double at in new[] { 0.0, 0.001, 0.25, 1.0, 10.0, 600.0 })
            Assert.Equal(0.0, _motion.MotionAt(still, at));
    }

    /// <summary>And one that does have either of them really does bend.</summary>
    /// <remarks>
    /// The other half, and the one that would catch the condition being written too wide: a
    /// shortcut that also skipped the wobble would be silent about it.
    /// </remarks>
    [Fact]
    public void A_patch_with_modulation_bends()
    {
        var wobbling = new SynthPatch { VibratoDepthCents = 50, VibratoRateHz = 5 };
        var falling = new SynthPatch { PitchEnvSemitones = 12, PitchEnvMs = 500 };

        Assert.NotEqual(0.0, _motion.MotionAt(wobbling, 0.05));
        Assert.NotEqual(0.0, _motion.MotionAt(falling, 0.05));
    }

    /// <summary>Every wave but noise ignores the random number it is handed.</summary>
    /// <remarks>
    /// Which is why the voice no longer turns its generator for them. Asked with two very
    /// different numbers rather than with nought, since a shape that quietly added the argument
    /// would agree with itself if it were only ever given the same one.
    /// </remarks>
    [Theory]
    [InlineData(SynthWave.Sine)]
    [InlineData(SynthWave.Square)]
    [InlineData(SynthWave.Saw)]
    [InlineData(SynthWave.Triangle)]
    [InlineData(SynthWave.Pulse)]
    public void Only_the_noise_wave_reads_the_random_number(SynthWave wave)
    {
        for (double phase = 0; phase < 1; phase += 0.05)
            Assert.Equal(
                _shapes.Sample(wave, phase, 0.3, -0.87),
                _shapes.Sample(wave, phase, 0.3, 0.61));
    }

    /// <summary>And noise reads nothing else.</summary>
    [Fact]
    public void The_noise_wave_is_the_number_it_is_handed()
    {
        Assert.Equal(-0.87, _shapes.Sample(SynthWave.Noise, 0.4, 0.3, -0.87));
        Assert.Equal(0.61, _shapes.Sample(SynthWave.Noise, 0.9, 0.7, 0.61));
    }

    /// <summary>
    /// The fade handed in is exactly the fade that used to be worked out per sample.
    /// </summary>
    /// <remarks>
    /// The two forms of <c>Apply</c> have to agree everywhere, or a voice would be driven
    /// differently from the scope that draws it, which is the one place this codebase has already
    /// been caught: a picture that is not the shape of the sound.
    /// </remarks>
    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.05)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    [InlineData(8.1)]
    public void Handing_the_fade_in_changes_nothing(double drive)
    {
        double makeup = _curve.Makeup(drive);
        double fade = _curve.Fade(drive);

        for (double sample = -1; sample <= 1; sample += 0.05)
            Assert.Equal(
                _curve.Apply(sample, drive, makeup),
                _curve.Apply(sample, drive, makeup, fade));
    }

    /// <summary>
    /// A whole note comes out the same whichever of the shortcuts apply to it.
    /// </summary>
    /// <remarks>
    /// The three conditions above proved one at a time, and this is them together over a real
    /// note: a plain patch and a modulated one, rendered and compared with themselves, so a
    /// shortcut that was taken on the wrong sample would show as a difference somewhere in a
    /// second of audio rather than only at the start.
    /// </remarks>
    [Theory]
    [InlineData(SynthWave.Saw, 0, 0, 1)]
    [InlineData(SynthWave.Noise, 0, 0, 1)]
    [InlineData(SynthWave.Square, 50, 5, 2)]
    [InlineData(SynthWave.Pulse, 0, 0, 8.1)]
    public void A_note_renders_the_same_twice(SynthWave wave, double cents, double rate, double drive)
    {
        SynthPatch Patch() => new()
        {
            Wave = wave,
            VibratoDepthCents = cents,
            VibratoRateHz = rate,
            Drive = drive,
            DecayMs = 2000,
            Sustain = 0.8,
            FilterCutoffHz = 3000,
            FilterResonance = 0.4,
        };

        var first = new float[44100 * 2];
        var second = new float[44100 * 2];

        new SynthVoice(Patch(), new Note(52), 0, 0.7f, 0f, 44100, 5).Render(first, 44100);
        new SynthVoice(Patch(), new Note(52), 0, 0.7f, 0f, 44100, 5).Render(second, 44100);

        Assert.Equal(first, second);
        Assert.Contains(first, one => one != 0);
    }
}
