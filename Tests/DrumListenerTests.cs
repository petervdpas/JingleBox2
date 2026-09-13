using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Synth;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Listening to a beat and finding the kit in it.
/// </summary>
/// <remarks>
/// The beat is made here rather than read off a disc, so what is in it is known exactly: a kick, a
/// snare, a closed hat and an open hat, each built the way the real thing sounds, laid on a grid and
/// added together so they overlap the way a played beat does. Two bars, a hit every quarter second.
/// </remarks>
public sealed class DrumListenerTests
{
    /// <summary>The rate everything is made at.</summary>
    private const int Rate = 44100;

    /// <summary>How far apart the grid's steps are.</summary>
    private const double Step = 0.25;

    /// <summary>One bar of the beat, a drum to a step.</summary>
    private static readonly DrumSound[] Bar =
    {
        DrumSound.Kick, DrumSound.ClosedHat, DrumSound.Snare, DrumSound.ClosedHat,
        DrumSound.Kick, DrumSound.OpenHat, DrumSound.Snare, DrumSound.ClosedHat,
    };

    /// <summary>The listener under test.</summary>
    private readonly DrumListener _listener = new();

    /// <summary>Noise that is the same every run.</summary>
    private static double[] Noise(int length, int seed)
    {
        var random = new Random(seed);
        var noise = new double[length];

        for (int i = 0; i < length; i++) noise[i] = (random.NextDouble() * 2) - 1;

        return noise;
    }

    /// <summary>Adds one drum into the mix, starting at that sample.</summary>
    private static void Hit(double[] mix, DrumSound sound, int at, int seed, double level = 1)
    {
        int length = (int)(Rate * 0.7);
        var noise = Noise(length, seed);
        double top = 0;
        double pole = 1 - Math.Exp(-2 * Math.PI * 6000 / Rate);

        for (int i = 0; i < length && at + i < mix.Length; i++)
        {
            double t = i / (double)Rate;

            top += (noise[i] - top) * pole;

            double metal = noise[i] - top;

            double value = sound switch
            {
                DrumSound.Kick => 0.8 * Math.Sin(2 * Math.PI * (55 * t + (100 * 0.02 * (1 - Math.Exp(-t / 0.02))))) * Math.Exp(-t / 0.12),
                DrumSound.Snare => (0.5 * Math.Sin(2 * Math.PI * 190 * t) * Math.Exp(-t / 0.05)) + (0.6 * noise[i] * Math.Exp(-t / 0.08)),
                DrumSound.ClosedHat => 0.6 * metal * Math.Exp(-t / 0.02),
                _ => 0.6 * metal * Math.Exp(-t / 0.2),
            };

            mix[at + i] += value * level;
        }
    }

    /// <summary>Two bars of the beat, and where each hit was put.</summary>
    private static (SampleData Sample, List<(double At, DrumSound Sound)> Hits) Beat(int channels = 2)
    {
        int length = (int)(Rate * Step * Bar.Length * 2);
        var mix = new double[length];
        var hits = new List<(double, DrumSound)>();

        for (int step = 0; step < Bar.Length * 2; step++)
        {
            var sound = Bar[step % Bar.Length];

            Hit(mix, sound, (int)(step * Step * Rate), step + 1);
            hits.Add((step * Step, sound));
        }

        return (Sampled(mix, channels), hits);
    }

    /// <summary>A mix of doubles as a recording of that many channels.</summary>
    private static SampleData Sampled(double[] mix, int channels)
    {
        var samples = new short[mix.Length * channels];

        for (int i = 0; i < mix.Length; i++)
            for (int c = 0; c < channels; c++)
                samples[(i * channels) + c] = (short)Math.Clamp(mix[i] * 0.5 * 32767, short.MinValue, short.MaxValue);

        return new SampleData(samples, channels, Rate);
    }

    /// <summary>Every hit is found, within a few milliseconds of where it was put, and nothing else.</summary>
    [Fact]
    public void Every_hit_is_found_where_it_was_put()
    {
        var (sample, put) = Beat();
        var found = _listener.Listen(sample);

        Assert.Equal(put.Count, found.Count);

        for (int i = 0; i < put.Count; i++)
            Assert.InRange(found[i].Start * sample.Seconds, put[i].At - 0.012, put[i].At + 0.012);
    }

    /// <summary>Each hit is heard as the drum it is.</summary>
    [Fact]
    public void Each_hit_is_heard_as_the_drum_it_is()
    {
        var (sample, put) = Beat();
        var found = _listener.Listen(sample);

        Assert.Equal(put.Count, found.Count);

        string all = string.Join("\n", found.Select(h => $"{h.Start * sample.Seconds:F3} {h.Sound} L{h.Low:F2} M{h.Middle:F2} H{h.High:F2} d{h.DecaySeconds:F3} z{h.Noise:F0} p{h.Peak:F2}"));

        for (int i = 0; i < put.Count; i++)
            Assert.True(put[i].Sound == found[i].Sound, all + "\n" +
                $"the hit at {put[i].At:F2} is a {put[i].Sound} and was heard as a {found[i].Sound} " +
                $"(low {found[i].Low:F2}, middle {found[i].Middle:F2}, high {found[i].High:F2}, ring {found[i].DecaySeconds:F3}, noise {found[i].Noise:F0})");
    }

    /// <summary>The kit is one of each drum, laid out kick first, and named for what it is.</summary>
    [Fact]
    public void The_kit_is_one_of_each_drum_kick_first()
    {
        var (sample, _) = Beat();
        var found = _listener.Listen(sample);
        var kit = _listener.Kit(found, 16);

        Assert.True(kit.Count == 4, string.Join("\n", found.Select(h => $"{h.Start * sample.Seconds:F3} {h.Sound} L{h.Low:F2} M{h.Middle:F2} H{h.High:F2} d{h.DecaySeconds:F3} z{h.Noise:F0} p{h.Peak:F2}")) + "\nkit: " + string.Join(", ", kit.Select(h => $"{h.Sound}@{h.Start * sample.Seconds:F2}")));

        Assert.Equal(
            new[] { DrumSound.Kick, DrumSound.Snare, DrumSound.ClosedHat, DrumSound.OpenHat },
            kit.Select(hit => hit.Sound));

        Assert.Equal(
            new[] { "Kick", "Snare", "Hat closed", "Hat open" },
            Enumerable.Range(0, kit.Count).Select(at => _listener.NameOf(kit, at)));

        Assert.All(kit, hit => Assert.True(hit.End > hit.Start));
    }

    /// <summary>Fewer pads than sounds keeps the first sounds in kit order.</summary>
    [Fact]
    public void Fewer_pads_keep_the_first_sounds()
    {
        var (sample, _) = Beat();
        var kit = _listener.Kit(_listener.Listen(sample), 2);

        Assert.Equal(new[] { DrumSound.Kick, DrumSound.Snare }, kit.Select(hit => hit.Sound));
        Assert.Empty(_listener.Kit(_listener.Listen(sample), 0));
    }

    /// <summary>A ghost note far quieter than the beat is heard, and is not given a pad.</summary>
    [Fact]
    public void A_ghost_is_heard_but_given_no_pad()
    {
        int length = Rate * 2;
        var mix = new double[length];

        Hit(mix, DrumSound.Snare, 0, 1);
        Hit(mix, DrumSound.Kick, Rate, 2, level: 0.02);

        var sample = Sampled(mix, 1);
        var found = _listener.Listen(sample);

        Assert.Equal(2, found.Count);
        Assert.Equal(new[] { DrumSound.Snare }, _listener.Kit(found, 16).Select(hit => hit.Sound));
    }

    /// <summary>One channel and two hear the same hits.</summary>
    [Fact]
    public void Mono_and_stereo_hear_the_same()
    {
        var mono = _listener.Listen(Beat(1).Sample);
        var stereo = _listener.Listen(Beat(2).Sample);

        Assert.Equal(mono.Select(hit => (hit.Sound, Math.Round(hit.Start, 4))), stereo.Select(hit => (hit.Sound, Math.Round(hit.Start, 4))));
    }

    /// <summary>Pieces cut in playing order are named for the drum each starts with, numbered where one repeats.</summary>
    [Fact]
    public void Pieces_are_named_for_the_drum_they_start_with()
    {
        var (sample, _) = Beat();
        var hits = _listener.Listen(sample);

        double seconds = sample.Seconds;
        var windows = Enumerable.Range(0, Bar.Length)
            .Select(step => (step * Step / seconds, (step + 1) * Step / seconds))
            .ToList();

        Assert.Equal(
            new[] { "Kick 1", "Hat closed 1", "Snare 1", "Hat closed 2", "Kick 2", "Hat open", "Snare 2", "Hat closed 3" },
            _listener.Names(hits, windows));
    }

    /// <summary>A piece nothing begins in takes the drum still ringing into it, and nothing at all is percussion.</summary>
    [Fact]
    public void A_piece_with_no_hit_of_its_own_is_named_for_what_rings_into_it()
    {
        var hit = new DrumHit(0.1, 0.5, DrumSound.Cymbal, 0, 0.2, 0.8, 1.2, 12000, 0.5, 0.4);

        Assert.Equal(new[] { "Cymbal 1", "Cymbal 2", "Perc" },
            _listener.Names(new[] { hit }, new[] { (0.0, 0.2), (0.2, 0.4), (0.6, 0.9) }));

        Assert.Equal(new[] { "Perc" }, _listener.Names(null, new[] { (0.0, 1.0) }));
        Assert.Empty(_listener.Names(new[] { hit }, null));
    }

    /// <summary>Silence, nothing, and a recording too short to follow find nothing, and a kit of nothing is empty.</summary>
    [Fact]
    public void Nothing_to_hear_finds_nothing()
    {
        Assert.Empty(_listener.Listen(null));
        Assert.Empty(_listener.Listen(new SampleData(new short[Rate * 2], 2, Rate)));
        Assert.Empty(_listener.Listen(new SampleData(new short[] { 30000, -30000, 30000 }, 1, Rate)));
        Assert.Empty(_listener.Listen(new SampleData(Array.Empty<short>(), 2, Rate)));
        Assert.Empty(_listener.Kit(null, 16));
        Assert.Empty(_listener.Kit(Array.Empty<DrumHit>(), 16));
        Assert.Equal("", _listener.NameOf(Array.Empty<DrumHit>(), 0));
    }

    /// <summary>The rules on their own, at the middle of what each drum measures.</summary>
    [Fact]
    public void The_rules_name_the_middle_of_each_drum()
    {
        Assert.Equal(DrumSound.Kick, DrumListener.Sound(0.46, 0.54, 0.0, 0.14, 1000));
        Assert.Equal(DrumSound.Snare, DrumListener.Sound(0.06, 0.92, 0.02, 0.11, 2800));
        Assert.Equal(DrumSound.ClosedHat, DrumListener.Sound(0.01, 0.15, 0.84, 0.09, 11000));
        Assert.Equal(DrumSound.ClosedHat, DrumListener.Sound(0.01, 0.62, 0.37, 0.05, 10800));
        Assert.Equal(DrumSound.OpenHat, DrumListener.Sound(0.01, 0.26, 0.73, 0.23, 11000));
        Assert.Equal(DrumSound.Cymbal, DrumListener.Sound(0.0, 0.2, 0.8, 1.5, 12000));
        Assert.Equal(DrumSound.Tom, DrumListener.Sound(0.1, 0.9, 0.0, 0.4, 900));
        Assert.Equal(DrumSound.Snare, DrumListener.Sound(0.06, 0.92, 0.01, 0.11, 900));
    }

    /// <summary>Laying windows on a kit fills the pads from the first, empties the rest, and keeps what was set by hand.</summary>
    [Fact]
    public void Laying_windows_fills_from_the_first_and_keeps_levels()
    {
        var kit = DrumKit.Empty(4);

        kit.Pads[0].Volume = 0.5;
        kit.Pads[3].FilePath = "old.wav";
        kit.Sliced = true;

        kit.Lay("beat.wav", new[] { (0.1, 0.2, "Kick"), (0.3, 0.25, "Snare") });

        Assert.Equal("beat.wav", kit.Pads[0].FilePath);
        Assert.Equal("Kick", kit.Pads[0].Name);
        Assert.Equal(0.1, kit.Pads[0].Shape!.Start, 6);
        Assert.Equal(0.2, kit.Pads[0].Shape!.End, 6);
        Assert.Equal(0.5, kit.Pads[0].Volume);
        Assert.True(kit.Pads[1].Shape!.End >= kit.Pads[1].Shape!.Start);
        Assert.Equal("", kit.Pads[3].FilePath);
        Assert.False(kit.Sliced);

        kit.Lay("", new[] { (0.0, 1.0, "Nothing") });

        Assert.Equal("beat.wav", kit.Pads[0].FilePath);
    }
}
