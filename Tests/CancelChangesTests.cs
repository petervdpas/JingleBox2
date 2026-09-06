using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Config.Enums;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Cancel changes reads the song back off disc and does not stop the transport.
/// </summary>
/// <remarks>
/// It used to. Cancel changes went through the same path opening a song does, which swaps in a
/// fresh object, and a pass is bound to the object it was started on, so the clock had to be
/// torn down: a song that was playing went silent because somebody threw away a change to it.
/// Cancelling is an undo taken all the way back to the file rather than a different song being
/// opened, so the contents are poured into the song that is open and the pass carries on.
///
/// What is pinned here is the mechanism rather than the button, since the button asks first and
/// a dialog needs a window. Two facts hold the whole thing up: a running pass follows the
/// contents of the object it was started on, and it does not follow a different object being
/// handed over. The first is why pouring works; the second is why it has to be a pour.
/// </remarks>
public class CancelChangesTests
{
    /// <summary>An audio engine that answers nothing and starts nothing.</summary>
    /// <remarks>
    /// The clock is what is under test, and it reaches the audio only to say the device should
    /// be ready. A song with no instruments never asks for a voice, so nothing here is called
    /// but <see cref="EnsureInitialized"/>.
    /// </remarks>
    private sealed class Quiet : IAudioEngine
    {
        /// <inheritdoc/>
        public int PadCount => 0;
        /// <inheritdoc/>
        public float GetOutputLevel() => 0f;
        /// <inheritdoc/>
        public IEnumerable<AudioOutput> GetOutputDevices() => Array.Empty<AudioOutput>();
        /// <inheritdoc/>
        public void SetOutputDevice(int deviceId) { }
        /// <inheritdoc/>
        public void EnsureInitialized() { }
        /// <inheritdoc/>
        public event EventHandler<PadPlaybackChanged>? PadPlaybackChanged { add { } remove { } }
        /// <inheritdoc/>
        public bool IsPadPlaying(int padIndex) => false;
        /// <inheritdoc/>
        public double GetPadProgress(int padIndex) => 0;
        /// <inheritdoc/>
        public float GetPadLevel(int padIndex) => 0f;
        /// <inheritdoc/>
        public float GetPadChannelVolume(int padIndex) => 0f;
        /// <inheritdoc/>
        public IOutputBus Output { get; } = new Nowhere();
        /// <inheritdoc/>
        public IOutputBus PadBus { get; } = new Nowhere();
        /// <inheritdoc/>
        public IOutputBus TakeBus { get; } = new Nowhere();
        /// <inheritdoc/>
        public IOutputBus MonitorBus { get; } = new Nowhere();
        /// <inheritdoc/>
        public JingleBox2.Audio.Interfaces.IMonitorFeed Monitor { get; } = new JingleBox2.Audio.NoMonitorFeed();
        /// <inheritdoc/>
        public void PlaySample(int padIndex, string filePath, float volume) { }
        /// <inheritdoc/>
        public void PlayStream(int padIndex, string url, float volume) { }
        /// <inheritdoc/>
        public void StopSample(int padIndex) { }
        /// <inheritdoc/>
        public void SetPadSource(int padIndex, PadSourceKind kind, string? source) { }
        /// <inheritdoc/>
        public void SetPadVolume(int padIndex, float volume) { }
        /// <inheritdoc/>
        public void SetPadLoop(int padIndex, bool loop) { }
        /// <inheritdoc/>
        public void SetPadFadeIn(int padIndex, double seconds) { }
        /// <inheritdoc/>
        public void SetPadFadeOut(int padIndex, double seconds) { }
        /// <inheritdoc/>
        public void Resize(int newPadCount) { }
        /// <inheritdoc/>
        public void SetPadInsert(int padIndex, IAudioInsert? insert) { }
        /// <inheritdoc/>
        public IAudioInsert? GetPadInsert(int padIndex) => null;
        /// <inheritdoc/>
        public int PadSampleRate(int padIndex) => 48000;
        /// <inheritdoc/>
        public void Dispose() { }
    }

    /// <summary>A bus that never opens, so nothing is ever summed into it.</summary>
    /// <remarks>
    /// The tracker asks its engine for one while it is being built, and what it does with the
    /// answer is decided by <see cref="IsOpen"/>: false is the arrangement this suite wants,
    /// which is a stream that plays itself on a machine that has no card to play it on.
    /// </remarks>
    private sealed class Nowhere : IOutputBus
    {
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
        public bool Add(int source) => false;
        /// <inheritdoc/>
        public void Remove(int source) { }
        /// <inheritdoc/>
        public void HearOnly(IReadOnlyCollection<int> sources) { }
        /// <inheritdoc/>
        public bool Holds(int source) => false;
        /// <inheritdoc/>
        public void Close() { }
        /// <inheritdoc/>
        public void Dispose() { }
    }

    /// <summary>How long a test waits for the clock before it gives up on it.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>A song of one pattern of the length asked for, with nothing in it to sound.</summary>
    /// <remarks>
    /// No instruments on purpose: the point is the clock walking lines, and an instrument would
    /// want a voice and therefore a device. Fast, so a test is a moment rather than a wait.
    /// </remarks>
    private static Song Of(int lines)
    {
        var song = new Song { Bpm = 400, LinesPerBeat = 16 };

        song.Patterns.Add(new Pattern(lines, song.TrackCount) { Name = "P" + lines });
        song.Order.Add(0);
        song.Normalize();

        return song;
    }

    /// <summary>Waits for the clock to say something is true, or gives up and answers false.</summary>
    /// <remarks>
    /// The patience is handed in for the tests that wait for something not to happen, since
    /// those spend the whole of it and there is no reason for them to spend five seconds.
    /// </remarks>
    private static bool Until(Func<bool> said, TimeSpan? within = null)
    {
        var clock = Stopwatch.StartNew();
        var patience = within ?? Patience;

        while (clock.Elapsed < patience)
        {
            if (said()) return true;
            Thread.Sleep(5);
        }

        return said();
    }

    /// <summary>
    /// A pass follows the contents of the song it is on, so pouring the file in keeps it playing.
    /// </summary>
    /// <remarks>
    /// The pattern is four lines long and becomes sixteen while the transport runs. Reaching a
    /// line past the fourth is what says the running pass really is playing what came back off
    /// disc, rather than merely still being alive on what it started with.
    /// </remarks>
    [Fact]
    public void A_running_pass_follows_the_song_it_is_on_when_the_file_is_poured_into_it()
    {
        using var player = new TrackerPlayer(new Quiet()) { Loop = true };

        var open = Of(4);

        player.Play(open, TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => player.Position.Line > 0), "the clock never moved");

        open.TakeFrom(Of(16));

        Assert.True(Until(() => player.Position.Line >= 4), "the pass did not follow the poured song");
        Assert.True(player.IsPlaying);
    }

    /// <summary>Throwing the changes away does not stop the transport.</summary>
    /// <remarks>
    /// The plainest half of it, and the one the button was reported on: whatever the file holds,
    /// the transport is where it was and still running afterwards.
    /// </remarks>
    [Fact]
    public void Pouring_the_file_in_leaves_the_transport_running()
    {
        using var player = new TrackerPlayer(new Quiet()) { Loop = true };

        var open = Of(8);

        player.Play(open, TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => player.Position.Line > 0), "the clock never moved");

        open.TakeFrom(Of(8));

        var was = player.Position;

        Assert.True(player.IsPlaying);
        Assert.True(Until(() => player.Position != was), "the clock stopped when the song was poured into");
        Assert.Equal(TrackerTransportState.Playing, player.State);
    }

    /// <summary>
    /// A pass is bound to the object it was started on, which is why this has to be a pour.
    /// </summary>
    /// <remarks>
    /// The clock takes the song and the sequencer once at the top and keeps them, so handing the
    /// player a different object moves nothing until the next pass. That is the whole reason
    /// opening a song stops the transport, and the reason cancelling changes must not swap the
    /// object: eight lines here would become sixteen if a running pass followed it, and it does
    /// not.
    /// </remarks>
    [Fact]
    public void A_running_pass_does_not_follow_a_different_song_handed_over()
    {
        using var player = new TrackerPlayer(new Quiet()) { Loop = true };

        var open = Of(8);

        player.Play(open, TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => player.Position.Line > 0), "the clock never moved");

        player.Use(Of(16));

        Assert.False(
            Until(() => player.Position.Line >= 8, TimeSpan.FromMilliseconds(500)),
            "the pass followed the song it was handed");
        Assert.True(player.IsPlaying);
    }

    /// <summary>
    /// Cancel changes does not stop a song that is playing, which is the whole of the report.
    /// </summary>
    /// <remarks>
    /// The tracker's own <c>Restore</c>, over a real view model with the transport running.
    /// The dialog is not in it, since asking needs a window and what was wrong was never the
    /// asking; everything after the answer is here. Built with an engine that starts nothing
    /// and a song with nothing in it to sound, so what is left is the clock.
    ///
    /// It is the one test of the four that fails if the button goes back to opening the song
    /// as though it were somebody else's, which is what it did.
    /// </remarks>
    [Fact]
    public void Cancelling_the_changes_does_not_stop_the_transport()
    {
        var recordings = new ObservableCollection<Recording>();

        var tracker = new TrackerViewModel(
            new Quiet(),
            new SoundMachineRack(),
            recordings,
            new SoundMachineProjects());

        tracker.Song.Bpm = 400;
        tracker.Song.LinesPerBeat = 16;

        tracker.Player.Loop = true;
        tracker.Player.Play(tracker.Song, TrackerPosition.Start, TrackerPlayMode.Pattern);

        Assert.True(Until(() => tracker.Player.Position.Line > 0), "the clock never moved");

        tracker.Restore(Of(16));

        Assert.True(tracker.Player.IsPlaying, "cancelling the changes stopped the transport");

        var was = tracker.Player.Position;

        Assert.True(
            Until(() => tracker.Player.Position != was),
            "the clock stopped after the changes were cancelled");

        tracker.Player.Stop();
        tracker.Finished();
    }

    /// <summary>
    /// Pouring keeps the object, which is what everything holding the song depends on.
    /// </summary>
    /// <remarks>
    /// The player, the mixer, the panels and the tracker all hold the song they were opened on.
    /// Cancel changes brings the file's contents into that object rather than handing back a
    /// different one, so none of them has to be told.
    /// </remarks>
    [Fact]
    public void Pouring_keeps_the_song_that_everything_is_holding()
    {
        var open = Of(4);
        var held = open;

        open.Bpm = 90;

        open.TakeFrom(Of(16));

        Assert.Same(held, open);
        Assert.Equal(400, open.Bpm);
        Assert.Equal(16, open.Patterns[0].Lines);
    }
}
