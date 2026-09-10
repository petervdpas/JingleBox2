using System;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using ManagedBass;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Hear it goes on working after the output has been changed.
/// </summary>
/// <remarks>
/// **Opening another output frees everything the old one had**, and two of those things are held
/// elsewhere: the tracker's stream, which was already asked for again, and the path the input is
/// heard through, which was not. What that came to is Hear it working from a fresh start and
/// never again once anybody touched the output picker, with the tick still reading on. Turning it
/// off and on did not help, since that writes a level rather than making a stream.
///
/// Two separate faults in one symptom and both are here. The stream has to be made again, which
/// is <c>ReopenMonitor</c>; and the bus is a fresh one opened silent on purpose, so the level has
/// to be written again as well, which is the moment the stream is put back on it.
/// </remarks>
public sealed class HearItSurvivesTests : IDisposable
{
    /// <summary>What the tests run at.</summary>
    private const int Rate = 44100;

    /// <summary>Stereo, which is what the path deals in.</summary>
    private const int Stereo = 2;

    /// <summary>BASS's own device that decodes and plays nothing, so this needs no card.</summary>
    private const int NoSound = 0;

    /// <summary>Whether BASS came up, since without it there is no stream to lose.</summary>
    private readonly bool _bass;

    /// <summary>Opens BASS on the device that plays nothing.</summary>
    public HearItSurvivesTests()
    {
        try
        {
            _bass = Bass.Init(NoSound, Rate) || Bass.LastError == Errors.Already;
        }
        catch (Exception)
        {
            _bass = false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            Bass.Free();
        }
        catch (Exception)
        {
        }
    }

    /// <summary>An open bus that remembers what is on it and what it is set to.</summary>
    /// <remarks>
    /// Open, unlike the one <see cref="MonitorFeedTests"/> uses, since what is asked here is what
    /// lands on a bus rather than what happens where there is none.
    /// </remarks>
    private sealed class Desk : IOutputBus
    {
        /// <summary>What is plugged in.</summary>
        private readonly System.Collections.Generic.HashSet<int> _on = new();

        /// <summary>Forgets its sources and opens silent, which is a bus made again.</summary>
        /// <remarks>
        /// Exactly what the engine does for a new output: a fresh mixer stream, and the input's
        /// bus written down to nothing on purpose so a source patched across cannot be heard
        /// before anybody has asked for it.
        /// </remarks>
        public void Remade()
        {
            _on.Clear();

            Level = 0f;
        }

        /// <inheritdoc/>
        public bool Present => true;

        /// <inheritdoc/>
        public double Pan { get; set; }

        /// <inheritdoc/>
        public bool Mute { get; set; }

        /// <inheritdoc/>
        public int Handle => 1;

        /// <inheritdoc/>
        public int BufferMs { get; set; }

        /// <inheritdoc/>
        public (float Left, float Right) Reading => (0f, 0f);

        /// <inheritdoc/>
        public bool IsOpen => true;

        /// <inheritdoc/>
        public float Level { get; set; }

        /// <inheritdoc/>
        public bool Open(int rate, int channels, bool pulled) => true;

        /// <inheritdoc/>
        public bool Add(int source) => _on.Add(source);

        /// <inheritdoc/>
        public void Remove(int source) => _on.Remove(source);

        /// <inheritdoc/>
        public void HearOnly(System.Collections.Generic.IReadOnlyCollection<int> sources) { }

        /// <inheritdoc/>
        public int Sources => _on.Count;

        /// <inheritdoc/>
        public bool Holds(int source) => _on.Contains(source);

        /// <inheritdoc/>
        public void Close() { }

        /// <inheritdoc/>
        public void Dispose() { }
    }

    /// <summary>One block of something, which is what the capture hands over.</summary>
    private static byte[] Block() => new byte[Rate / 10 * Stereo * sizeof(short)];

    /// <summary>
    /// A bus made again gets the capture and the level back.
    /// </summary>
    /// <remarks>
    /// The half that is about the level. The bus a new output brings is opened silent, so the
    /// one this last wrote is gone with the old bus: without writing it where the stream is put
    /// back, the tick reads on and the path carries nothing.
    /// </remarks>
    [Fact]
    public void A_bus_made_again_gets_the_capture_and_the_level_back()
    {
        if (!_bass) return;

        var bus = new Desk();
        var feed = new MonitorFeed(bus);

        Assert.True(feed.Open(Rate, Stereo), "the path would not open on the null device");

        feed.Heard = true;

        Assert.Equal(1f, bus.Level);

        bus.Remade();

        feed.Push(Block(), Block().Length);

        Assert.Equal(1, bus.Sources);
        Assert.Equal(1f, bus.Level);
    }

    /// <summary>And one made again while nothing is being heard stays silent.</summary>
    /// <remarks>
    /// The other end of it, and the one that would fail if the level were simply written to
    /// unity wherever the stream is put back: a bus that carries the capture at full level with
    /// the tick off is the recorder in the mix without anybody having asked.
    /// </remarks>
    [Fact]
    public void A_bus_made_again_while_nothing_is_heard_stays_silent()
    {
        if (!_bass) return;

        var bus = new Desk();
        var feed = new MonitorFeed(bus);

        Assert.True(feed.Open(Rate, Stereo));

        bus.Remade();

        feed.Push(Block(), Block().Length);

        Assert.Equal(0f, bus.Level);
    }

    /// <summary>The path is opened again after the device it lived on was taken down.</summary>
    /// <remarks>
    /// The half that is about the stream. A path told to open again reports itself open, whatever
    /// it was holding before, and that is what the output picker leans on: it says so every time
    /// the output moves without asking what state anything was in.
    /// </remarks>
    [Fact]
    public void The_path_is_made_again_when_it_is_asked_for()
    {
        if (!_bass) return;

        var bus = new Desk();
        var feed = new MonitorFeed(bus);

        Assert.True(feed.Open(Rate, Stereo));

        feed.Close();

        Assert.False(feed.IsOpen);

        Assert.True(feed.Open(Rate, Stereo), "the path would not open a second time");
        Assert.True(feed.IsOpen);
    }
}
