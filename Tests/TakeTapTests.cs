using System.Collections.Generic;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// When a take is read off the recorder's bus rather than off the capture.
/// </summary>
/// <remarks>
/// The audio itself is not here and cannot be: reading the bus is a hook BASS runs as it pulls,
/// and this suite has no card. What is asked here is the one rule that decides whether the bus is
/// read at all, and it is the rule that protects every take anybody has made until now: with
/// nothing patched into RECORD the capture's own bytes are the take, exactly as before.
/// </remarks>
public sealed class TakeTapTests
{
    /// <summary>A bus that says how many sources it has and nothing else.</summary>
    private sealed class Carrying : IOutputBus
    {
        /// <summary>How many it answers with.</summary>
        public int Sources { get; set; }

        /// <inheritdoc/>
        public int Handle => 1;

        /// <inheritdoc/>
        public bool Present => true;

        /// <inheritdoc/>
        public bool IsOpen => true;

        /// <inheritdoc/>
        public float Level { get; set; }

        /// <inheritdoc/>
        public double Pan { get; set; }

        /// <inheritdoc/>
        public bool Mute { get; set; }

        /// <inheritdoc/>
        public int BufferMs { get; set; }

        /// <inheritdoc/>
        public (float Left, float Right) Reading => (0, 0);

        /// <inheritdoc/>
        public bool Open(int rate, int channels, bool pulled) => true;

        /// <inheritdoc/>
        public bool Add(int source) => true;

        /// <inheritdoc/>
        public void Remove(int source) { }

        /// <inheritdoc/>
        public bool Holds(int source) => false;

        /// <inheritdoc/>
        public void HearOnly(IReadOnlyCollection<int> sources) { }

        /// <inheritdoc/>
        public void Close() { }

        /// <inheritdoc/>
        public void Dispose() { }
    }

    /// <summary>
    /// A bus carrying the capture and nothing else is not read, so the take is the capture's.
    /// </summary>
    /// <remarks>
    /// The capture's own bytes are the better copy where there is nothing to add to them: at the
    /// rate they arrived at, never resampled, and unreachable by anything happening on the mixing
    /// thread. So this is what says that a take made the way every take has been made is written
    /// the way it always was.
    /// </remarks>
    [Fact]
    public void Nothing_patched_in_leaves_the_take_to_the_capture()
    {
        var tap = new TakeTap();

        tap.Follow(new Carrying { Sources = 1 });
        tap.Start();

        Assert.False(tap.Mixed, "the bus was read where it was carrying only the capture");
        Assert.Empty(tap.Stop());
    }

    /// <summary>And a bus with nothing on it at all is not read either.</summary>
    [Fact]
    public void An_empty_bus_is_not_read()
    {
        var tap = new TakeTap();

        tap.Follow(new Carrying { Sources = 0 });
        tap.Start();

        Assert.False(tap.Mixed);
        Assert.Empty(tap.Stop());
    }

    /// <summary>A tap nobody has given a bus is not read, which is every test double's recorder.</summary>
    [Fact]
    public void A_tap_with_no_bus_is_quiet()
    {
        var tap = new TakeTap();

        tap.Start();

        Assert.False(tap.Mixed);
        Assert.Empty(tap.Stop());
    }
}
