using System;
using System.IO;
using System.Linq;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundEffects.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The level every effect of ours hands its audio back at.
/// </summary>
/// <remarks>
/// The one promise worth pinning first is that nought decibels is nothing at all, sample for
/// sample, since every chain on anybody's disc was saved before the knob existed and reads it back
/// at nought. The second is that a move is a ramp rather than a step, which is the difference
/// between a knob and a zipper.
/// </remarks>
public sealed class EffectLevelTests
{
    /// <summary>What everything here is measured at.</summary>
    private const int Rate = 48000;

    /// <summary>A block of that many frames with a steady value in both channels.</summary>
    private static float[] Steady(int frames, float value = 0.8f) => Enumerable.Repeat(value, frames * 2).ToArray();

    /// <summary>Nought decibels hands a block back untouched.</summary>
    [Fact]
    public void Nought_is_nothing_at_all()
    {
        var level = new EffectLevel();
        var input = new float[512 * 2];
        var random = new Random(3);

        for (int at = 0; at < input.Length; at++) input[at] = (float)random.NextDouble();

        var block = (float[])input.Clone();

        level.Apply(block, 512);
        level.Apply(block, 512);

        Assert.Equal(0, level.Db);
        Assert.Equal(input, block);
    }

    /// <summary>A level set before any audio is where the first block starts, not somewhere to ramp to.</summary>
    [Fact]
    public void A_level_set_before_any_audio_is_where_it_starts()
    {
        var level = new EffectLevel();

        level.Set(-6);

        var block = Steady(256);

        level.Apply(block, 256);

        double gain = Math.Pow(10, -6 / 20.0);

        Assert.All(block, sample => Assert.Equal(0.8 * gain, sample, 5));
    }

    /// <summary>A move is a ramp across the next block, ending where the knob is.</summary>
    [Fact]
    public void A_move_is_a_ramp_rather_than_a_step()
    {
        var level = new EffectLevel();

        level.Apply(Steady(256), 256);
        level.Set(-24);

        var block = Steady(256, 1f);

        level.Apply(block, 256);

        for (int at = 1; at < 256; at++) Assert.True(block[at * 2] < block[(at - 1) * 2]);

        Assert.True(block[0] > 0.99f);
        Assert.Equal(Math.Pow(10, -24 / 20.0), block[255 * 2], 5);

        var next = Steady(16, 1f);

        level.Apply(next, 16);

        Assert.All(next, sample => Assert.Equal(Math.Pow(10, -24 / 20.0), sample, 5));
    }

    /// <summary>It is held to its ends, and something that is not a number is refused.</summary>
    [Fact]
    public void It_is_held_to_its_ends_and_nonsense_is_refused()
    {
        var level = new EffectLevel();

        level.Set(40);
        Assert.Equal(IEffectLevel.MostDb, level.Db);

        level.Set(-400);
        Assert.Equal(IEffectLevel.LeastDb, level.Db);

        level.Set(double.NaN);
        level.Set(double.NegativeInfinity);
        Assert.Equal(IEffectLevel.LeastDb, level.Db);
    }

    /// <summary>A block that lies about itself is refused quietly.</summary>
    [Fact]
    public void A_block_that_lies_is_refused_quietly()
    {
        var level = new EffectLevel();

        level.Set(6);
        level.Apply(null!, 10);
        level.Apply(new float[8], -3);
        level.Apply(new float[3], 5);
        level.Apply(new float[8], 1000);
    }

    /// <summary>Every engine of ours, fresh.</summary>
    public static TheoryData<string> Engines => new()
    {
        SoundEffectEngines.Delayed, SoundEffectEngines.Filtered, SoundEffectEngines.Shifted,
        SoundEffectEngines.Ringed, SoundEffectEngines.Widened, SoundEffectEngines.Phased,
    };

    /// <summary>One of our engines by its engine name.</summary>
    private static ISoundEffectEngine Made(string engine) => engine switch
    {
        SoundEffectEngines.Delayed => new Delay(Rate),
        SoundEffectEngines.Filtered => new Sweep(Rate),
        SoundEffectEngines.Shifted => new Shift(Rate),
        SoundEffectEngines.Ringed => new Ring(Rate),
        SoundEffectEngines.Widened => new Widen(Rate),
        _ => new Phase(Rate),
    };

    /// <summary>
    /// Each effect answers the level knob, and six decibels down is the same sound at half the size.
    /// </summary>
    [Theory]
    [MemberData(nameof(Engines))]
    public void Each_effect_turns_down_what_it_hands_back(string engine)
    {
        var loud = Made(engine);
        var quiet = Made(engine);

        quiet.SetValue(IEffectLevel.Key, -6);

        Assert.Contains(IEffectLevel.Key, quiet.Keys);
        Assert.Equal(-6, quiet.ValueOf(IEffectLevel.Key), 5);

        var random = new Random(11);
        double gain = Math.Pow(10, -6 / 20.0);

        for (int block = 0; block < 8; block++)
        {
            var one = new float[512 * 2];

            for (int at = 0; at < one.Length; at++) one[at] = (float)((random.NextDouble() * 2) - 1) * 0.5f;

            var two = (float[])one.Clone();

            loud.Process(one, 512);
            quiet.Process(two, 512);

            for (int at = 0; at < one.Length; at++) Assert.Equal(one[at] * gain, two[at], 1e-5);
        }
    }

    /// <summary>Every effect that ships has a level knob, and it is the same knob on each.</summary>
    [Fact]
    public void Every_shipped_effect_has_the_same_level_knob()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at != null && !Directory.Exists(Path.Combine(at.FullName, "rack", "effects"))) at = at.Parent;

        Assert.NotNull(at);

        var folders = Directory.GetDirectories(Path.Combine(at!.FullName, "rack", "effects"));

        Assert.NotEmpty(folders);

        foreach (string folder in folders)
        {
            var effect = SoundEffectProject.Open(folder);

            Assert.NotNull(effect);

            var knob = effect!.Parameters.SingleOrDefault(one => one.Key == IEffectLevel.Key);

            Assert.True(knob is not null, Path.GetFileName(folder) + " has no level");
            Assert.Equal(IEffectLevel.LeastDb, knob!.Min);
            Assert.Equal(IEffectLevel.MostDb, knob.Max);
            Assert.Equal(0, knob.Default);
        }
    }
}
