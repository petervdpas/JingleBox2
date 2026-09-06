using System;
using System.IO;
using System.Threading;
using JingleBox2.Audio;
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
///
/// The bus was behind a setting while it was new, and both the setting and the path it guarded
/// are gone: a pad is a source on the bus and there is no other way for one to sound. A machine
/// where the bus will not open says so when the output is opened rather than playing the pads a
/// different way, which is what the third test here is about.
/// </remarks>
public sealed class PadOnBusTests : IDisposable
{
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

        using var engine = new BassAudioEngine(padCount: 4);

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

        using var engine = new BassAudioEngine(padCount: 4);

        engine.EnsureInitialized();

        if (!engine.PadBus.IsOpen) return;

        engine.PlaySample(0, Tone(), 1f);

        Assert.True(engine.IsPadPlaying(0));

        engine.StopSample(0);

        Assert.False(engine.IsPadPlaying(0), "a stopped pad still called itself playing");
    }

    /// <summary>A pad fired on a cold engine opens the output and lands on the bus.</summary>
    /// <remarks>
    /// **There is no second path, and this is what makes sure there is no window either.**
    /// Playing a pad opens BASS on its way in and the busses are opened in the same breath, so
    /// there is no moment where a pad exists and the bus does not. That mattered while the bus
    /// was a setting somebody could turn off, since a pad then played at the card on its own;
    /// with the setting gone, a pad that missed the bus would be a pad that makes no sound.
    /// </remarks>
    [Fact]
    public void A_pad_on_a_cold_engine_opens_the_bus_and_lands_on_it()
    {
        if (!Available()) return;

        using var engine = new BassAudioEngine(padCount: 4);

        engine.PlaySample(0, Tone(), 1f);

        Assert.True(engine.PadBus.IsOpen, "playing a pad did not open the bus under it");
        Assert.True(engine.IsPadPlaying(0), "the pad did not land on the bus it had just opened");

        engine.StopSample(0);

        Assert.False(engine.IsPadPlaying(0));
    }
}
