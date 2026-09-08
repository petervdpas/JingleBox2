using System;
using JingleBox2.SoundDevices.SoundEffects;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The pitch shifter, measured rather than listened to.
/// </summary>
/// <remarks>
/// What can be said about a shifter without ears is what it does at its edges: that nought really
/// is nothing, that the block size decides nothing, and that nothing it is handed makes it produce
/// something that is not a number. Whether an octave up sounds like an octave up is a listening
/// question and is not pretended to be one here.
/// </remarks>
public sealed class ShiftTests
{
    /// <summary>A block of a tone, so there is something with a shape to move.</summary>
    private static float[] Tone(int frames, double hz = 220, int rate = 48000)
    {
        var block = new float[frames * 2];

        for (int at = 0; at < frames; at++)
        {
            float value = (float)(0.5 * Math.Sin(2 * Math.PI * hz * at / rate));

            block[at * 2] = value;
            block[(at * 2) + 1] = value;
        }

        return block;
    }

    /// <summary>
    /// No shift at all is the signal itself, sample for sample.
    /// </summary>
    /// <remarks>
    /// The rule the engine is built around. With the taps standing still what would come out is a
    /// copy a fraction of a window late, which against the dry signal is a comb filter: an effect
    /// at a setting that says no effect. So nought is passed straight through, and this is what
    /// says it still is.
    /// </remarks>
    [Fact]
    public void Nought_is_the_signal_itself()
    {
        var shift = new Shift(48000);
        float[] block = Tone(512);
        float[] was = (float[])block.Clone();

        shift.SetValue(Shift.Steps, 0);
        shift.SetValue(Shift.Cents, 0);
        shift.SetValue(Shift.Mix, 1);
        shift.Process(block, 512);

        Assert.Equal(was, block);
    }

    /// <summary>And a shift is not the signal itself, which is the other half of that.</summary>
    [Fact]
    public void A_shift_is_not_the_signal_itself()
    {
        var shift = new Shift(48000);
        float[] block = Tone(4096);
        float[] was = (float[])block.Clone();

        shift.SetValue(Shift.Steps, 12);
        shift.SetValue(Shift.Mix, 1);
        shift.Process(block, 4096);

        Assert.NotEqual(was, block);
    }

    /// <summary>
    /// The same audio through the same settings comes out the same however it is cut into blocks.
    /// </summary>
    /// <remarks>
    /// Which is what makes an effect an effect rather than something that depends on the buffer
    /// size in SETTINGS. The two ways a pass over blocks goes wrong both leave a block of the
    /// right length: a frame worked on twice and a frame skipped.
    /// </remarks>
    [Fact]
    public void The_block_size_decides_nothing()
    {
        var whole = new Shift(48000);
        var pieces = new Shift(48000);

        foreach (var one in new[] { whole, pieces })
        {
            one.SetValue(Shift.Steps, -7);
            one.SetValue(Shift.Window, 40);
            one.SetValue(Shift.Mix, 1);
        }

        float[] first = Tone(2048);
        float[] second = (float[])first.Clone();

        whole.Process(first, 2048);

        for (int at = 0; at < 2048; at += 256)
        {
            var piece = new float[256 * 2];

            Array.Copy(second, at * 2, piece, 0, 256 * 2);
            pieces.Process(piece, 256);
            Array.Copy(piece, 0, second, at * 2, 256 * 2);
        }

        Assert.Equal(first, second);
    }

    /// <summary>Nothing it can be set to makes it hand back something that is not a number.</summary>
    /// <remarks>
    /// It sits on a bus, and what a NaN reaching the output costs is written down elsewhere in
    /// this application: full scale noise in a room with somebody in it.
    /// </remarks>
    [Theory]
    [InlineData(24, 100, 10)]
    [InlineData(-24, -100, 120)]
    [InlineData(0, 1, 10)]
    public void Nothing_it_is_set_to_makes_it_answer_nonsense(double steps, double cents, double window)
    {
        var shift = new Shift(44100);

        shift.SetValue(Shift.Steps, steps);
        shift.SetValue(Shift.Cents, cents);
        shift.SetValue(Shift.Window, window);
        shift.SetValue(Shift.Mix, 1);

        for (int round = 0; round < 8; round++)
        {
            float[] block = Tone(1024, 440, 44100);

            shift.Process(block, 1024);

            foreach (float sample in block)
                Assert.True(float.IsFinite(sample), "the shifter answered something that is not a number");
        }
    }

    /// <summary>What is not a number is refused rather than clamped, since a clamp hands it back.</summary>
    [Fact]
    public void Nonsense_is_refused_rather_than_kept()
    {
        var shift = new Shift(48000);

        shift.SetValue(Shift.Steps, 5);
        shift.SetValue(Shift.Steps, double.NaN);
        shift.SetValue(Shift.Window, double.PositiveInfinity);

        Assert.Equal(5, shift.ValueOf(Shift.Steps));
        Assert.Equal(Shift.WindowThen, shift.ValueOf(Shift.Window));
    }

    /// <summary>Every knob is held to its own ends, whatever a file or a lane says.</summary>
    [Fact]
    public void Every_knob_is_held_to_its_ends()
    {
        var shift = new Shift(48000);

        shift.SetValue(Shift.Steps, 9000);
        shift.SetValue(Shift.Cents, -9000);
        shift.SetValue(Shift.Window, 9000);
        shift.SetValue(Shift.Mix, 9000);

        Assert.Equal(Shift.MostSteps, shift.ValueOf(Shift.Steps));
        Assert.Equal(-Shift.MostCents, shift.ValueOf(Shift.Cents));
        Assert.Equal(Shift.MostWindowMs, shift.ValueOf(Shift.Window));
        Assert.Equal(1, shift.ValueOf(Shift.Mix));
    }

    /// <summary>A key it has not got reads as nought and writes nothing.</summary>
    [Fact]
    public void A_key_it_has_not_got_is_nothing()
    {
        var shift = new Shift(48000);

        shift.SetValue("feedback", 0.5);

        Assert.Equal(0, shift.ValueOf("feedback"));
        Assert.Equal(0, shift.ValueOf(null));
    }

    /// <summary>No buffer, no frames and a count past the end are all answered rather than thrown.</summary>
    [Fact]
    public void It_is_handed_nothing_and_says_nothing()
    {
        var shift = new Shift(48000);

        shift.SetValue(Shift.Steps, 12);

        shift.Process(null!, 128);
        shift.Process(Array.Empty<float>(), 128);

        float[] block = Tone(16);

        shift.Process(block, 9000);

        foreach (float sample in block) Assert.True(float.IsFinite(sample));
    }
}
