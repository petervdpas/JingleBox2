using System;
using System.IO;
using System.Linq;
using JingleBox2.Audio;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Cutting the drums out of a recording into files of their own.
/// </summary>
/// <remarks>
/// A beat is written to a file in a folder of the test's own, chopped, and the files that come
/// back are read again: one of each drum, each as long as the hit it was cut from, faded at both
/// ends, and nothing ever written over something already there.
/// </remarks>
public sealed class DrumChopperTests : IDisposable
{
    /// <summary>The rate the beat is made at.</summary>
    private const int Rate = 44100;

    /// <summary>A folder nothing else uses.</summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "jinglebox2-chop-" + Guid.NewGuid().ToString("N"));

    /// <summary>Reads and writes the files.</summary>
    private readonly WavFile _wav = new();

    /// <summary>The chopper under test.</summary>
    private readonly DrumChopper _chopper = new();

    /// <summary>Makes the folder.</summary>
    public DrumChopperTests() => Directory.CreateDirectory(_folder);

    /// <summary>Takes the folder away again.</summary>
    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { }
    }

    /// <summary>A bar of kick, hat, snare and hat, written to a file, and where it is.</summary>
    private string Beat()
    {
        var mix = new double[Rate];
        var random = new Random(3);

        void Add(int at, Func<double, double, double> sound, double seconds)
        {
            for (int i = 0; i < seconds * Rate && at + i < mix.Length; i++)
                mix[at + i] += sound(i / (double)Rate, (random.NextDouble() * 2) - 1);
        }

        double top = 0;
        double pole = 1 - Math.Exp(-2 * Math.PI * 6000 / Rate);

        double Hat(double t, double noise)
        {
            top += (noise - top) * pole;

            return 0.6 * (noise - top) * Math.Exp(-t / 0.02);
        }

        Add(0, (t, _) => 0.8 * Math.Sin(2 * Math.PI * (55 * t + (2 * (1 - Math.Exp(-t / 0.02))))) * Math.Exp(-t / 0.12), 0.25);
        Add(Rate / 4, Hat, 0.1);
        Add(Rate / 2, (t, noise) => (0.5 * Math.Sin(2 * Math.PI * 190 * t) * Math.Exp(-t / 0.05)) + (0.6 * noise * Math.Exp(-t / 0.08)), 0.25);
        Add(Rate * 3 / 4, Hat, 0.1);

        var samples = mix.Select(value => (short)Math.Clamp(value * 0.5 * 32767, short.MinValue, short.MaxValue)).ToArray();
        string path = Path.Combine(_folder, "Beat.wav");

        _wav.Write(path, samples, Rate, 1);

        return path;
    }

    /// <summary>One of each drum comes back as a file of its own, named for what it is, and each file reads.</summary>
    [Fact]
    public void Each_drum_is_cut_into_a_file_of_its_own()
    {
        string into = Path.Combine(_folder, "chopped");
        var chopped = _chopper.Chop(Beat(), into, 16);

        Assert.Equal(new[] { DrumSound.Kick, DrumSound.Snare, DrumSound.ClosedHat }, chopped.Select(one => one.Sound));
        Assert.Equal(new[] { "Kick", "Snare", "Hat closed" }, chopped.Select(one => one.Name));

        foreach (var drum in chopped)
        {
            Assert.Equal(Path.Combine(into, drum.Name + ".wav"), drum.FilePath);

            var (samples, info) = _wav.Read(drum.FilePath);

            Assert.Equal(Rate, info.SampleRate);
            Assert.InRange(samples.Length, Rate / 100, Rate / 3);
            Assert.Equal(0, samples[^1]);
        }
    }

    /// <summary>A second chop of the same recording goes in a folder of its own and writes over nothing.</summary>
    [Fact]
    public void A_second_chop_writes_over_nothing()
    {
        string beat = Beat();
        string chops = Path.Combine(_folder, "chops");

        string first = _chopper.FolderFor(beat, chops);

        Assert.Equal(Path.Combine(chops, "Beat"), first);

        _chopper.Chop(beat, first, 16);

        string second = _chopper.FolderFor(beat, chops);

        Assert.Equal(Path.Combine(chops, "Beat 2"), second);
        Assert.Equal(Path.Combine(chops, "Chop"), _chopper.FolderFor("", chops));
    }

    /// <summary>A recording that is not there, is not a recording, or holds only silence writes nothing.</summary>
    [Fact]
    public void Nothing_to_chop_writes_nothing()
    {
        string into = Path.Combine(_folder, "nothing");

        Assert.Empty(_chopper.Chop(Path.Combine(_folder, "missing.wav"), into, 16));

        string junk = Path.Combine(_folder, "junk.wav");
        File.WriteAllText(junk, "not a recording");
        Assert.Empty(_chopper.Chop(junk, into, 16));

        string silence = Path.Combine(_folder, "silence.wav");
        _wav.Write(silence, new short[Rate], Rate, 1);
        Assert.Empty(_chopper.Chop(silence, into, 16));

        Assert.Empty(_chopper.Chop(Beat(), into, 0));
        Assert.False(Directory.Exists(into));
    }

    /// <summary>Laying drums on a kit fills the pads from the first with the whole of each, empties the rest, and keeps what was set by hand.</summary>
    [Fact]
    public void Laying_drums_fills_from_the_first_and_keeps_levels()
    {
        var kit = DrumKit.Empty(4);

        kit.Pads[0].Volume = 0.5;
        kit.Pads[0].Shape = new SampleShape { Start = 0.3, End = 0.4 };
        kit.Pads[3].FilePath = "old.wav";
        kit.Sliced = true;

        kit.Lay(new[] { ("kick.wav", "Kick"), ("", "Nothing"), ("snare.wav", "Snare") });

        Assert.Equal("kick.wav", kit.Pads[0].FilePath);
        Assert.Equal("Kick", kit.Pads[0].Name);
        Assert.Equal(0, kit.Pads[0].Shape!.Start);
        Assert.Equal(1, kit.Pads[0].Shape!.End);
        Assert.Equal(0.5, kit.Pads[0].Volume);
        Assert.Equal("snare.wav", kit.Pads[1].FilePath);
        Assert.Equal("", kit.Pads[3].FilePath);
        Assert.False(kit.Sliced);

        kit.Lay(Array.Empty<(string, string)>());

        Assert.Equal("kick.wav", kit.Pads[0].FilePath);
    }
}
