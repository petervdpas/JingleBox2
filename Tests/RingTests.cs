using System;
using JingleBox2.SoundDevices.SoundEffects;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The ring modulator, measured rather than listened to.
/// </summary>
/// <remarks>
/// This one can be measured further than most, because multiplication by a square is arithmetic
/// anybody can do on paper: hand it a steady one and what comes back is the carrier itself. So
/// what is asked here is not "does it sound like a robot" but "is it really multiplying", which
/// is the thing that would be quietly wrong if it were adding, or modulating one side only, or
/// running its carrier at the wrong speed.
/// </remarks>
public sealed class RingTests
{
    /// <summary>A block holding the same value in both channels.</summary>
    private static float[] Steady(int frames, float value = 1f)
    {
        var block = new float[frames * 2];

        for (int at = 0; at < block.Length; at++) block[at] = value;

        return block;
    }

    /// <summary>
    /// A square carrier through a steady signal comes back as the square itself.
    /// </summary>
    /// <remarks>
    /// Multiplying by one is the carrier, so this reads the carrier straight off the output: half
    /// a cycle at plus one and half at minus one, and the crossing where the arithmetic says it is.
    /// An effect that added rather than multiplied would answer two and nought.
    /// </remarks>
    [Fact]
    public void A_square_through_a_steady_signal_is_the_square()
    {
        var ring = new Ring(48000);

        ring.SetValue(Ring.Carrier, 1000);
        ring.SetValue(Ring.Square, 1);
        ring.SetValue(Ring.Mix, 1);

        float[] block = Steady(96);

        ring.Process(block, 96);

        for (int at = 0; at < 96; at++)
            Assert.Equal(at < 24 ? 1f : at < 48 ? -1f : at < 72 ? 1f : -1f, block[at * 2], 3);
    }

    /// <summary>Nothing mixed in is the signal itself, sample for sample.</summary>
    [Fact]
    public void Nothing_mixed_in_is_the_signal_itself()
    {
        var ring = new Ring(48000);

        ring.SetValue(Ring.Carrier, 200);
        ring.SetValue(Ring.Mix, 0);

        float[] block = Steady(64, 0.25f);
        float[] was = (float[])block.Clone();

        ring.Process(block, 64);

        Assert.Equal(was, block);
    }

    /// <summary>
    /// A spread carries the two sides at different speeds, so they stop agreeing.
    /// </summary>
    /// <remarks>
    /// The one thing a stereo effect can get wrong invisibly is doing the same thing to both
    /// sides while claiming not to. With no spread they must agree exactly, since the same
    /// carrier is the same carrier.
    /// </remarks>
    [Fact]
    public void A_spread_is_what_makes_the_sides_differ()
    {
        var together = new Ring(48000);
        var apart = new Ring(48000);

        foreach (var one in new[] { together, apart })
        {
            one.SetValue(Ring.Carrier, 300);
            one.SetValue(Ring.Mix, 1);
        }

        apart.SetValue(Ring.Spread, 40);

        float[] first = Steady(512);
        float[] second = Steady(512);

        together.Process(first, 512);
        apart.Process(second, 512);

        for (int at = 0; at < 512; at++)
            Assert.Equal(first[at * 2], first[(at * 2) + 1], 5);

        bool differ = false;

        for (int at = 0; at < 512 && !differ; at++)
            differ = Math.Abs(second[at * 2] - second[(at * 2) + 1]) > 0.01;

        Assert.True(differ, "the two sides were carried at the same speed with a spread on");
    }

    /// <summary>
    /// Crushed all the way, what comes out lands on a handful of levels and no others.
    /// </summary>
    /// <remarks>
    /// Two bits is four steps either side of nought, which is what the top of the knob says it
    /// is. Counted rather than eyeballed, since a crush that quietly did nothing would look
    /// exactly like a crush.
    /// </remarks>
    [Fact]
    public void Crushed_all_the_way_lands_on_a_few_levels()
    {
        var ring = new Ring(48000);

        ring.SetValue(Ring.Carrier, 130);
        ring.SetValue(Ring.Crush, 1);
        ring.SetValue(Ring.Mix, 1);

        float[] block = Steady(2048, 0.9f);

        ring.Process(block, 2048);

        var levels = new System.Collections.Generic.HashSet<float>();

        foreach (float sample in block) levels.Add(sample);

        Assert.True(levels.Count <= 9, "crushed to two bits and still holding " + levels.Count + " levels");
        Assert.True(levels.Count > 1, "the crush flattened the signal to one level");
    }

    /// <summary>The block size decides nothing.</summary>
    [Fact]
    public void The_block_size_decides_nothing()
    {
        var whole = new Ring(48000);
        var pieces = new Ring(48000);

        foreach (var one in new[] { whole, pieces })
        {
            one.SetValue(Ring.Carrier, 173);
            one.SetValue(Ring.Spread, 11);
            one.SetValue(Ring.Mix, 1);
        }

        float[] first = Steady(1024, 0.4f);
        float[] second = Steady(1024, 0.4f);

        whole.Process(first, 1024);

        for (int at = 0; at < 1024; at += 128)
        {
            var piece = new float[128 * 2];

            Array.Copy(second, at * 2, piece, 0, 128 * 2);
            pieces.Process(piece, 128);
            Array.Copy(piece, 0, second, at * 2, 128 * 2);
        }

        Assert.Equal(first, second);
    }

    /// <summary>What is not a number is refused rather than clamped.</summary>
    [Fact]
    public void Nonsense_is_refused_rather_than_kept()
    {
        var ring = new Ring(48000);

        ring.SetValue(Ring.Carrier, 440);
        ring.SetValue(Ring.Carrier, double.NaN);
        ring.SetValue(Ring.Crush, double.NegativeInfinity);

        Assert.Equal(440, ring.ValueOf(Ring.Carrier));
        Assert.Equal(Ring.CrushThen, ring.ValueOf(Ring.Crush));
    }

    /// <summary>Every knob is held to its own ends.</summary>
    [Fact]
    public void Every_knob_is_held_to_its_ends()
    {
        var ring = new Ring(48000);

        ring.SetValue(Ring.Carrier, 90000);
        ring.SetValue(Ring.Spread, -90000);
        ring.SetValue(Ring.Crush, 9);
        ring.SetValue(Ring.Mix, -9);

        Assert.Equal(Ring.MostCarrier, ring.ValueOf(Ring.Carrier));
        Assert.Equal(-Ring.MostSpread, ring.ValueOf(Ring.Spread));
        Assert.Equal(1, ring.ValueOf(Ring.Crush));
        Assert.Equal(0, ring.ValueOf(Ring.Mix));
    }

    /// <summary>A key it has not got reads as nought and writes nothing.</summary>
    [Fact]
    public void A_key_it_has_not_got_is_nothing()
    {
        var ring = new Ring(48000);

        ring.SetValue("time", 500);

        Assert.Equal(0, ring.ValueOf("time"));
        Assert.Equal(0, ring.ValueOf(null));
    }

    /// <summary>No buffer, no frames and a count past the end are all answered rather than thrown.</summary>
    [Fact]
    public void It_is_handed_nothing_and_says_nothing()
    {
        var ring = new Ring(48000);

        ring.Process(null!, 64);
        ring.Process(Array.Empty<float>(), 64);

        float[] block = Steady(16, 0.3f);

        ring.Process(block, 9000);

        foreach (float sample in block) Assert.True(float.IsFinite(sample));
    }
}
