using System;
using System.Collections.Generic;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Routing.Records;
using JingleBox2.UI;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The busses are made to agree with the routing as it is drawn, and nothing remembers anything.
/// </summary>
/// <remarks>
/// **There is one description of how audio moves through this application and the patchbay is
/// that description.** The picture draws it, the flow rule reads it, the levels are keyed on its
/// points, and this executes it: four readings of one thing rather than four things kept in step.
///
/// The first attempt at this kept a private record inside the engine of where each source had
/// been put, beside the stored settings and beside the picture. Three records of one fact, and
/// the one that decided what you heard was the one nobody could see. So what is pinned hardest
/// here is that following the same routing twice does nothing the second time **because the
/// busses were asked**, not because anything wrote down what it did.
/// </remarks>
public sealed class PatchedAudioTests
{
    /// <summary>A bus that remembers what is on it and can be told to refuse.</summary>
    private sealed class Bus : IOutputBus
    {
        /// <summary>Every bus in play, so a channel can belong to exactly one of them.</summary>
        /// <remarks>
        /// **A channel belongs to one bus and the bus is what enforces that**, which is the
        /// mixer's own rule: asked to take a channel still on another, the add-on answers
        /// `Already` and does nothing at all. A double that let a channel sit on two would pass
        /// over the one thing this is about, so it keeps the rule rather than a flag saying it
        /// does.
        /// </remarks>
        public List<Bus> All { get; } = new();

        /// <summary>What has been put on it.</summary>
        public HashSet<int> Sources { get; } = new();

        /// <summary>How many times something was put on it, so a second pass shows up.</summary>
        public int Added { get; private set; }

        /// <summary>Whether it will take anything at all.</summary>
        public bool Takes { get; set; } = true;

        /// <inheritdoc/>
        /// <remarks>Takes the channel off whatever held it, which is what a mixer does.</remarks>
        public bool Add(int source)
        {
            if (!Takes) return false;

            foreach (var bus in All) bus.Sources.Remove(source);

            Added++;
            Sources.Add(source);

            return true;
        }

        /// <inheritdoc/>
        public void Remove(int source) => Sources.Remove(source);
        /// <inheritdoc/>
        public bool Holds(int source) => Sources.Contains(source);
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
        public (float Left, float Right) Reading => (0, 0);
        /// <inheritdoc/>
        public bool IsOpen => true;
        /// <inheritdoc/>
        public float Level { get; set; } = 1f;
        /// <inheritdoc/>
        public bool Open(int rate, int channels, bool pulled) => true;
        /// <inheritdoc/>
        public void HearOnly(IReadOnlyCollection<int> sources) { }
        /// <inheritdoc/>
        public void Close() { }
        /// <inheritdoc/>
        public void Dispose() { }
    }

    /// <summary>The song's stream, as a number a bus can hold.</summary>
    private const int SongStream = 77;

    /// <summary>The pads' bus, as another.</summary>
    private const int PadStream = 88;

    /// <summary>The two busses and the follower over them.</summary>
    private sealed class Bench
    {
        /// <summary>The desk's bus.</summary>
        public Bus Desk { get; } = new();

        /// <summary>The recorder's bus.</summary>
        public Bus Recorder { get; } = new();

        /// <summary>What the song's stream answers, so it can be told to be shut.</summary>
        public int Song { get; set; } = SongStream;

        /// <summary>The follower.</summary>
        public PatchedAudio Audio { get; }

        /// <summary>Builds one, with both sources starting on the desk as they really do.</summary>
        public Bench()
        {
            Desk.All.Add(Desk);
            Desk.All.Add(Recorder);
            Recorder.All.Add(Desk);
            Recorder.All.Add(Recorder);

            Desk.Add(SongStream);
            Desk.Add(PadStream);

            Audio = new PatchedAudio(
                new Dictionary<string, IOutputBus>(StringComparer.Ordinal)
                {
                    [PatchNodes.Mixer] = Desk,
                    [PatchNodes.Record] = Recorder
                },
                new Dictionary<string, Func<int>>(StringComparer.Ordinal)
                {
                    [PatchNodes.Song] = () => Song,
                    [PatchNodes.Fire] = () => PadStream
                });
        }

        /// <summary>Follows the routing with whichever sources have been moved to the recorder.</summary>
        public void Follow(params string[] moved) =>
            Audio.Follow(new PatchGraph().Read(
                Array.Empty<AudioRoute>(), null, null, new[] { "TR-01" }, moved));
    }

    /// <summary>Drawn to the recorder, the song is on the recorder and off the desk.</summary>
    [Fact]
    public void A_cable_to_the_recorder_moves_the_song_there()
    {
        var bench = new Bench();

        bench.Follow(PatchNodes.Song);

        Assert.True(bench.Recorder.Holds(SongStream));
        Assert.False(bench.Desk.Holds(SongStream));
    }

    /// <summary>And the source that was not drawn there stays exactly where it was.</summary>
    [Fact]
    public void A_source_that_was_not_moved_stays_on_the_desk()
    {
        var bench = new Bench();

        bench.Follow(PatchNodes.Song);

        Assert.True(bench.Desk.Holds(PadStream));
        Assert.False(bench.Recorder.Holds(PadStream));
    }

    /// <summary>Drawn back, it goes back.</summary>
    [Fact]
    public void A_cable_back_to_the_desk_moves_it_back()
    {
        var bench = new Bench();

        bench.Follow(PatchNodes.Song);
        bench.Follow();

        Assert.True(bench.Desk.Holds(SongStream));
        Assert.False(bench.Recorder.Holds(SongStream));
    }

    /// <summary>
    /// Following the same routing twice does nothing the second time, and it is the busses that
    /// say so rather than anything written down here.
    /// </summary>
    /// <remarks>
    /// The whole of why there is no mirror: a follower with a memory of its own is a third record
    /// of where things are, beside the picture and beside the settings, and it is the one that
    /// decides what you hear.
    /// </remarks>
    [Fact]
    public void Following_the_same_routing_twice_does_nothing()
    {
        var bench = new Bench();

        bench.Follow(PatchNodes.Song);

        int put = bench.Recorder.Added;

        bench.Follow(PatchNodes.Song);
        bench.Follow(PatchNodes.Song);

        Assert.Equal(put, bench.Recorder.Added);
        Assert.Single(bench.Recorder.Sources);
    }

    /// <summary>
    /// A source that is not running yet is left where it is, and moves the moment it starts,
    /// since the routing is read again and again rather than answered once.
    /// </summary>
    [Fact]
    public void A_source_that_starts_later_is_moved_when_it_has()
    {
        var bench = new Bench { Song = 0 };

        bench.Follow(PatchNodes.Song);

        Assert.Empty(bench.Recorder.Sources);
        Assert.True(bench.Desk.Holds(SongStream));

        bench.Song = SongStream;
        bench.Follow(PatchNodes.Song);

        Assert.True(bench.Recorder.Holds(SongStream));
        Assert.False(bench.Desk.Holds(SongStream));
    }

    /// <summary>Both drawn to the recorder, both go, and neither is left on the desk.</summary>
    [Fact]
    public void Both_sources_can_be_on_the_recorder()
    {
        var bench = new Bench();

        bench.Follow(PatchNodes.Song, PatchNodes.Fire);

        Assert.True(bench.Recorder.Holds(SongStream));
        Assert.True(bench.Recorder.Holds(PadStream));
        Assert.Empty(bench.Desk.Sources);
    }

    /// <summary>
    /// A channel is on one bus and one only, which is the mixer's own rule and the reason nothing
    /// above it coordinates two busses to move one channel.
    /// </summary>
    /// <remarks>
    /// Coordinating them is what failed: a mixer refuses a channel that is still on another, so
    /// adding before removing could never work, and the song stayed on the desk with its cable
    /// drawn to the recorder and a line in the log saying so. The rule lives with the bus now, and
    /// what is pinned here is that it holds from either side.
    /// </remarks>
    [Fact]
    public void A_channel_is_on_one_bus_only()
    {
        var bench = new Bench();

        bench.Follow(PatchNodes.Song);

        Assert.True(bench.Recorder.Holds(SongStream));
        Assert.False(bench.Desk.Holds(SongStream));

        bench.Follow();

        Assert.True(bench.Desk.Holds(SongStream));
        Assert.False(bench.Recorder.Holds(SongStream));
    }

    /// <summary>
    /// A bus that will not take it leaves the source where it was rather than nowhere, which is
    /// the one way this could silence something by trying to move it.
    /// </summary>
    /// <remarks>
    /// Nothing has to be undone: taking the channel off whatever held it is part of taking it on,
    /// so a refusal never gets that far.
    /// </remarks>
    [Fact]
    public void A_bus_that_refuses_leaves_the_source_where_it_was()
    {
        var bench = new Bench();

        bench.Recorder.Takes = false;

        bench.Follow(PatchNodes.Song);

        Assert.True(bench.Desk.Holds(SongStream));
        Assert.Empty(bench.Recorder.Sources);
    }

    /// <summary>A routing with nothing in it leaves the busses alone.</summary>
    [Fact]
    public void No_routing_at_all_touches_nothing()
    {
        var bench = new Bench();

        bench.Audio.Follow(null!);

        Assert.True(bench.Desk.Holds(SongStream));
        Assert.Empty(bench.Recorder.Sources);
    }
}
