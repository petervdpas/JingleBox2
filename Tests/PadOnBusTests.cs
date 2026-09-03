using System;
using System.IO;
using System.Threading;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A pad reaching the output bus, which is the fault the bus was built for.
/// </summary>
/// <remarks>
/// Under an ASIO driver the pads were silent. The driver owns the card, so BASS is opened on its
/// own silent device and the only thing anybody hears is what the driver is pulling, which was
/// the tracker's stream and nothing else. Every call in the pad path still answered yes: the
/// stream was made, <c>ChannelPlay</c> returned true, the pad lit up on the screen, and no sound
/// came out. That is the shape of fault this asks about, and it is why the question here is not
/// "did it play" but "is it on the bus and is the bus louder for it".
///
/// It runs on BASS's no-sound device against the real add-on, so it needs no card and no window.
/// Where the add-on is not in this checkout, each test leaves rather than fails.
/// </remarks>
public sealed class PadOnBusTests : IDisposable
{
    /// <summary>Says yes, so the engine sums onto a bus without the environment being touched.</summary>
    private sealed class BusOn : IBusSwitch
    {
        /// <inheritdoc/>
        public bool Wanted => true;
    }

    /// <summary>Says no, which is what every run does until somebody asks for the bus.</summary>
    private sealed class BusOff : IBusSwitch
    {
        /// <inheritdoc/>
        public bool Wanted => false;
    }

    /// <summary>A folder of its own, so a take written here is nobody else's.</summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "jb-padbus-" + Guid.NewGuid().ToString("N"));

    /// <summary>Makes the folder.</summary>
    public PadOnBusTests() => Directory.CreateDirectory(_folder);

    /// <summary>Takes it away again.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>A second of a loud tone, written as a real file for a pad to play.</summary>
    private string Tone()
    {
        string path = Path.Combine(_folder, "tone.wav");

        const int rate = 44100;
        var samples = new short[rate];

        for (int i = 0; i < samples.Length; i++)
            samples[i] = (short)(Math.Sin(i * 2 * Math.PI * 440 / rate) * short.MaxValue * 0.8);

        new WavFile().Write(path, samples, rate, 1);

        return path;
    }

    /// <summary>Whether the add-on is here, since without it there is nothing to ask.</summary>
    private static bool Available() => new OutputBus().Present;

    /// <summary>The pad's audio really arrives on the bus, which under a driver is the only route out.</summary>
    [Fact]
    public void A_pad_played_with_the_bus_on_is_on_the_bus_and_is_heard()
    {
        if (!Available()) return;

        using var engine = new BassAudioEngine(padCount: 4, bus: new BusOn());

        engine.EnsureInitialized();

        Assert.True(engine.Output.IsOpen, "the output bus did not open");
        Assert.True(engine.PadBus.IsOpen, "the pads' own bus did not open");

        engine.PlaySample(0, Tone(), 1f);

        Assert.True(engine.IsPadPlaying(0), "the pad did not report itself as sounding");

        float loudest = 0;

        for (int tries = 0; tries < 40 && loudest <= 0; tries++)
        {
            Thread.Sleep(25);
            loudest = engine.GetOutputLevel();
        }

        Assert.True(loudest > 0, "the pad was playing and nothing of it reached the bus, which is the ASIO silence");
    }

    /// <summary>Stopping really takes it off, rather than leaving a decoder that still reads as playing.</summary>
    /// <remarks>
    /// The trap this pins: a decoding channel answers that it is playing for as long as it holds
    /// data, whether or not anything is pulling it. Asked of the channel rather than of the bus,
    /// a stopped pad would go on reporting itself as sounding until its stream was let go.
    /// </remarks>
    [Fact]
    public void A_pad_stopped_on_the_bus_stops_reporting_itself_as_playing()
    {
        if (!Available()) return;

        using var engine = new BassAudioEngine(padCount: 4, bus: new BusOn());

        engine.EnsureInitialized();

        if (!engine.PadBus.IsOpen) return;

        engine.PlaySample(0, Tone(), 1f);

        Assert.True(engine.IsPadPlaying(0));

        engine.StopSample(0);

        Assert.False(engine.IsPadPlaying(0), "a stopped pad still called itself playing");
    }

    /// <summary>With the switch off not a thing about the old path is different.</summary>
    /// <remarks>
    /// This is the whole promise of the switch, and it is worth a test rather than a reading of
    /// the code: no bus is opened, no sub-bus is opened, and a pad still plays and still says so.
    /// </remarks>
    [Fact]
    public void With_the_switch_off_there_is_no_bus_at_all_and_a_pad_still_plays()
    {
        using var engine = new BassAudioEngine(padCount: 4, bus: new BusOff());

        engine.EnsureInitialized();

        Assert.False(engine.Output.IsOpen);
        Assert.False(engine.PadBus.IsOpen);
        Assert.False(engine.TakeBus.IsOpen);

        engine.PlaySample(0, Tone(), 1f);

        Assert.True(engine.IsPadPlaying(0));

        engine.StopSample(0);

        Assert.False(engine.IsPadPlaying(0));
    }
}
