using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The path from the capture onto the input's bus, where it can be asked without a sound card.
/// </summary>
/// <remarks>
/// What can be asked here is what happens where there is no card, which is the whole of this
/// suite's world and is also a real machine: a checkout with no BASSmix, an output that would not
/// open. **The capture thread calls this on every block whatever the state of anything**, so
/// every one of these is a real path rather than an invented one, and the answer to all of them
/// has to be nothing rather than a fault on the thread that is filling a take.
///
/// What is not here is the audio, since that needs a stream and a bus. The bus and the pass under
/// it have their own tests, and what is left is this class's own bookkeeping.
/// </remarks>
public sealed class MonitorFeedTests
{
    /// <summary>A bus that is not open, which is what a machine with no output has.</summary>
    private sealed class Shut : IOutputBus
    {
        /// <summary>What was plugged in, so a test can say nothing was.</summary>
        public int Added { get; private set; }

        /// <summary>What was unplugged.</summary>
        public int Removed { get; private set; }

        /// <inheritdoc/>
        public bool Present => false;

        /// <inheritdoc/>
        public double Pan { get; set; }

        /// <inheritdoc/>
        public bool Mute { get; set; }

        /// <inheritdoc/>
        public int Handle => 0;

        /// <inheritdoc/>
        public int BufferMs { get; set; }

        /// <inheritdoc/>
        public (float Left, float Right) Reading => (0f, 0f);

        /// <inheritdoc/>
        public bool IsOpen => false;

        /// <inheritdoc/>
        public float Level { get; set; } = 1f;

        /// <inheritdoc/>
        public bool Open(int rate, int channels, bool pulled) => false;

        /// <inheritdoc/>
        public bool Add(int source)
        {
            Added++;

            return false;
        }

        /// <inheritdoc/>
        public void Remove(int source) => Removed++;

        /// <inheritdoc/>
        public void HearOnly(System.Collections.Generic.IReadOnlyCollection<int> sources) { }

        /// <inheritdoc/>
        public bool Holds(int source) => false;

        /// <inheritdoc/>
        public void Close() { }

        /// <inheritdoc/>
        public void Dispose() { }
    }

    /// <summary>With no library behind it the path does not open, and says so rather than throwing.</summary>
    [Fact]
    public void It_does_not_open_where_there_is_no_output()
    {
        var feed = new MonitorFeed(new Shut());

        Assert.False(feed.Open(44100, 2));
        Assert.False(feed.IsOpen);
    }

    /// <summary>Pushing into a path that never opened does nothing, which the capture relies on.</summary>
    /// <remarks>
    /// The capture callback pushes without asking first, deliberately, since the alternative is a
    /// second question on the thread that must not be held up.
    /// </remarks>
    [Fact]
    public void Pushing_into_a_path_that_is_not_open_does_nothing()
    {
        var bus = new Shut();
        var feed = new MonitorFeed(bus);

        feed.Push(new byte[512], 512);

        Assert.Equal(0, bus.Added);
    }

    /// <summary>Nothing at all is nothing, rather than reading past a block.</summary>
    [Fact]
    public void Nothing_to_push_is_nothing()
    {
        var feed = new MonitorFeed(new Shut());

        feed.Push(null!, 64);
        feed.Push(new byte[8], 0);
        feed.Push(new byte[8], -4);

        Assert.False(feed.IsOpen);
    }

    /// <summary>Closing one that was never open does nothing, and does nothing twice.</summary>
    [Fact]
    public void Closing_a_path_that_is_not_open_does_nothing()
    {
        var bus = new Shut();
        var feed = new MonitorFeed(bus);

        feed.Close();
        feed.Close();

        Assert.Equal(0, bus.Removed);
    }

    /// <summary>The chain can be hung on it before there is anything to hear.</summary>
    /// <remarks>
    /// The order the two arrive in is not decided anywhere: a chain is built when the settings are
    /// read and the path opens when a page shows the meter.
    /// </remarks>
    [Fact]
    public void The_chain_can_be_set_before_the_path_opens()
    {
        var feed = new MonitorFeed(new Shut());
        var insert = new Silence();

        feed.Insert = insert;

        Assert.Same(insert, feed.Insert);
    }

    /// <summary>An effect that does nothing, for the one question above.</summary>
    private sealed class Silence : JingleBox2.Audio.Plugins.Interfaces.IAudioInsert
    {
        /// <inheritdoc/>
        public void Process(float[] buffer, int frames) { }
    }
}
