using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Config;
using JingleBox2.ViewModels;
using JingleBox2.Waveform;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Where the play cursor is, reading by reading, for a take played on its own and for a take
/// played on RECORD: off the shelf, off the scratchpad, just saved, and with the page moving
/// around it while it plays.
/// </summary>
/// <remarks>
/// The readings are taken by hand through <see cref="HandClock"/>, and the take says where it has
/// got to through the source double, so each step says exactly what the cursor was handed.
/// </remarks>
public class PlayheadScenarioTests
{
    /// <summary>A whole take, which is what RECORD plays.</summary>
    private const long Frames = 44100;

    /// <summary>A player over a four second take on a bus that takes it, driven by hand.</summary>
    private static (WaveformPlayer Player, TakePlaybackTests.Fake Source, HandClock Clock, List<double> Cursor, List<string> Stops) Player()
    {
        var told = new TakePlaybackTests.Told();
        var source = new TakePlaybackTests.Fake(told);
        var clock = new HandClock();
        var player = new WaveformPlayer(source, new TakePlaybackTests.Bus(told), clock);
        var cursor = new List<double>();
        var stops = new List<string>();

        player.PositionChanged += cursor.Add;
        player.Stopped += () => stops.Add("stopped");

        return (player, source, clock, cursor, stops);
    }

    /// <summary>Each reading hands the cursor where the take is, as a share of the whole.</summary>
    [Fact]
    public void Each_reading_moves_the_cursor()
    {
        var (player, source, clock, cursor, _) = Player();

        player.Play("take.wav", 0, 1, Frames);
        source.Position = 1;
        clock.Tick();
        source.Position = 2;
        clock.Tick();

        Assert.Equal([0, 0.25, 0.5], cursor);
    }

    /// <summary>A take the bus has not started pulling yet stays at its start and is not given up on.</summary>
    [Fact]
    public void A_take_not_yet_pulled_waits_at_its_start()
    {
        var (player, source, clock, cursor, stops) = Player();

        player.Play("take.wav", 0, 1, Frames);
        clock.Tick();
        clock.Tick();
        source.Position = 1;
        clock.Tick();

        Assert.Equal([0, 0, 0, 0.25], cursor);
        Assert.Empty(stops);
        Assert.True(player.IsPlaying);
    }

    /// <summary>The take running out stops the player and the reading, once.</summary>
    [Fact]
    public void Running_out_stops_once()
    {
        var (player, source, clock, _, stops) = Player();

        player.Play("take.wav", 0, 1, Frames);
        source.Over = true;
        clock.Tick();
        clock.Tick();

        Assert.Equal(["stopped"], stops);
        Assert.False(player.IsPlaying);
        Assert.False(clock.Running);
    }

    /// <summary>A region stops at its own end rather than the take's.</summary>
    [Fact]
    public void A_region_stops_at_its_end()
    {
        var (player, source, clock, cursor, stops) = Player();

        player.Play("take.wav", 0.25, 0.5, Frames);
        source.Position = 1.5;
        clock.Tick();
        source.Position = 2;
        clock.Tick();

        Assert.Equal([0.25, 0.375], cursor);
        Assert.Equal(["stopped"], stops);
    }

    /// <summary>Playing another take while one plays follows the new one, on one reading.</summary>
    [Fact]
    public void Another_take_takes_over_the_cursor()
    {
        var (player, source, clock, cursor, stops) = Player();

        player.Play("first.wav", 0, 1, Frames);
        source.Position = 3;
        player.Play("second.wav", 0, 1, Frames);
        source.Position = 1;
        clock.Tick();

        Assert.Equal([0, 0, 0.25], cursor);
        Assert.Equal(["stopped"], stops);
        Assert.True(clock.Running);
        Assert.Equal(2, clock.Starts);
    }

    /// <summary>Stopped and played again, the readings come back.</summary>
    [Fact]
    public void Played_again_the_readings_come_back()
    {
        var (player, source, clock, cursor, _) = Player();

        player.Play("take.wav", 0, 1, Frames);
        player.Stop();
        player.Play("take.wav", 0, 1, Frames);
        source.Position = 2;
        clock.Tick();

        Assert.Equal(0.5, cursor[^1]);
        Assert.True(player.IsPlaying);
    }

    /// <summary>A reading that arrives after the stop moves nothing and says nothing.</summary>
    [Fact]
    public void A_late_reading_after_a_stop_does_nothing()
    {
        var (player, _, clock, cursor, stops) = Player();

        player.Play("take.wav", 0, 1, Frames);
        player.Stop();
        int before = cursor.Count;
        clock.Tick();

        Assert.Equal(before, cursor.Count);
        Assert.Equal(["stopped"], stops);
    }

    /// <summary>Two players on one bus, as RECORD and the edit window are: stopping one leaves the other moving.</summary>
    [Fact]
    public void Two_players_on_one_bus_keep_their_own_cursors()
    {
        var told = new TakePlaybackTests.Told();
        var bus = new TakePlaybackTests.Bus(told);
        var pageSource = new TakePlaybackTests.Fake(told, channel: 7);
        var windowSource = new TakePlaybackTests.Fake(told, channel: 8);
        var pageClock = new HandClock();
        var windowClock = new HandClock();
        var page = new WaveformPlayer(pageSource, bus, pageClock);
        var window = new WaveformPlayer(windowSource, bus, windowClock);
        var seen = new List<double>();
        window.PositionChanged += seen.Add;

        page.Play("take.wav", 0, 1, Frames);
        window.Play("take.wav", 0, 1, Frames);
        page.Stop();
        windowSource.Position = 1;
        windowClock.Tick();

        Assert.Equal(0.25, seen[^1]);
        Assert.Contains(8, bus.On);
        Assert.DoesNotContain(7, bus.On);
    }

    /// <summary>Hands out a length for any file, so RECORD will play it.</summary>
    private sealed class Sized : IWaveformService
    {
        /// <inheritdoc/>
        public WaveformData AnalyzeFile(string filePath) => new() { PeakData = [0.5f, 0.5f] };

        /// <inheritdoc/>
        public TimeSpan GetDuration(string filePath) => TimeSpan.FromSeconds(4);

        /// <inheritdoc/>
        public long GetFrameCount(string filePath) => File.Exists(filePath) ? Frames : 0;

        /// <inheritdoc/>
        public void TrimFile(string filePath, long startFrame, long endFrame) { }

        /// <inheritdoc/>
        public void SilenceFile(string filePath, long startFrame, long endFrame) { }

        /// <inheritdoc/>
        public void ReverseFile(string filePath, long startFrame, long endFrame) { }

        /// <inheritdoc/>
        public void FadeFile(string filePath, long startFrame, long endFrame, bool rising) { }

        /// <inheritdoc/>
        public double NormalizeFile(string filePath, double targetDecibels) => 0;
    }

    /// <summary>The shelf RECORD reads, emptied, with the given takes on it.</summary>
    private static string Shelf(params string[] names)
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JingleBox2", "recordings");

        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        Directory.CreateDirectory(folder);

        foreach (string name in names) File.WriteAllBytes(Path.Combine(folder, name + ".wav"), [0, 0, 0, 0]);

        return folder;
    }

    /// <summary>RECORD over a shelf of takes, with a recorder that writes a take when one is stopped.</summary>
    private static (RecordViewModel Page, TakePlaybackTests.Fake Source, HandClock Clock) Page(params string[] shelf)
    {
        Shelf(shelf);

        var told = new TakePlaybackTests.Told();
        var source = new TakePlaybackTests.Fake(told);
        var clock = new HandClock();
        var recorder = new RecorderBench.Deaf
        {
            Writes = (folder, name, _) =>
            {
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, name + ".wav");
                File.WriteAllBytes(path, [0, 0, 0, 0]);
                return new SavedTake(path, null);
            }
        };

        var page = new RecordViewModel(recorder, new RecorderBench.Flat(), new Sized(), new SettingsBlock(new AppConfig()),
            new RecorderBench.Rewiring(), takes: new TakePlaybackTests.Bus(told), recordings: source, playhead: clock);

        return (page, source, clock);
    }

    /// <summary>Records a take onto the scratchpad.</summary>
    private static void Record(RecordViewModel page)
    {
        page.StopRecordingCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        Assert.NotNull(page.ScratchTake);
    }

    /// <summary>A take off the shelf moves the take's cursor and leaves the scratchpad's alone.</summary>
    [Fact]
    public void A_take_off_the_shelf_moves_its_cursor()
    {
        var (page, source, clock) = Page("Old take");
        var take = page.Recordings.Single();
        page.SelectedRecording = take;

        page.PlayRecordingCommand.Execute(take);
        source.Position = 2;
        clock.Tick();

        Assert.Equal(0.5, page.Playhead);
        Assert.Equal(-1, page.ScratchPlayhead);
    }

    /// <summary>A take on the scratchpad moves the scratchpad's cursor.</summary>
    [Fact]
    public void A_take_on_the_scratchpad_moves_its_own_cursor()
    {
        var (page, source, clock) = Page();
        Record(page);

        page.PlayRecordingCommand.Execute(page.ScratchShown);
        source.Position = 1;
        clock.Tick();

        Assert.Equal(0.25, page.ScratchPlayhead);
    }

    /// <summary>A take just recorded and saved plays with its cursor moving, which is the new recording case.</summary>
    [Fact]
    public void A_take_just_saved_moves_its_cursor()
    {
        var (page, source, clock) = Page("Old take");
        Record(page);
        page.RecordingName = "Brand new";
        page.SaveTakeCommand.Execute(null);

        var saved = page.SelectedRecording;
        Assert.Equal("Brand new", saved?.Name);

        page.PlayRecordingCommand.Execute(saved);
        source.Position = 3;
        clock.Tick();

        Assert.True(saved!.IsPlaying);
        Assert.Equal(0.75, page.Playhead);
    }

    /// <summary>A take off the shelf played while another waits on the scratchpad moves the take's cursor.</summary>
    [Fact]
    public void A_shelf_take_beside_a_waiting_scratch_moves_the_takes_cursor()
    {
        var (page, source, clock) = Page("Old take");
        Record(page);
        var take = page.Recordings.Single(one => one.Name == "Old take");
        page.SelectedRecording = take;

        page.PlayRecordingCommand.Execute(take);
        source.Position = 2;
        clock.Tick();

        Assert.Equal(0.5, page.Playhead);
        Assert.Equal(-1, page.ScratchPlayhead);
    }

    /// <summary>Filing the take that is playing under a category leaves it playing.</summary>
    [Fact]
    public void Filing_the_playing_take_leaves_it_playing()
    {
        var (page, source, clock) = Page("Old take");
        var take = page.Recordings.Single();
        page.SelectedRecording = take;
        page.PlayRecordingCommand.Execute(take);

        page.FileTakeUnder("Human voices");
        source.Position = 2;
        clock.Tick();

        Assert.True(page.IsPreviewing);
        Assert.Equal(0.5, page.Playhead);
    }

    /// <summary>The shelf read again while a take plays leaves it playing and its cursor moving.</summary>
    [Fact]
    public void Reading_the_shelf_again_leaves_the_take_playing()
    {
        var (page, source, clock) = Page("Old take", "Other take");
        var take = page.Recordings.Single(one => one.Name == "Old take");
        page.SelectedRecording = take;
        page.PlayRecordingCommand.Execute(take);

        page.Rescan();
        source.Position = 2;
        clock.Tick();

        Assert.True(page.IsPreviewing);
        Assert.Equal(0.5, page.Playhead);
        Assert.Equal("Old take", page.SelectedRecording?.Name);
        Assert.Contains(page.SelectedRecording, page.Recordings);
    }

    /// <summary>The take running out puts the cursor away and the row back to idle.</summary>
    [Fact]
    public void Running_out_puts_the_cursor_away()
    {
        var (page, source, clock) = Page("Old take");
        var take = page.Recordings.Single();
        page.SelectedRecording = take;
        page.PlayRecordingCommand.Execute(take);

        source.Over = true;
        clock.Tick();

        Assert.Equal(-1, page.Playhead);
        Assert.False(take.IsPlaying);
        Assert.False(page.IsPreviewing);
    }
}
