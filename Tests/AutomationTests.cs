using JingleBox2.Midi;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Lanes: what they say at a time, what a pattern does with them, and what the file keeps.
/// </summary>
/// <remarks>
/// The file is the half worth testing hardest. Everything else here can be rewritten next
/// month; a song written this month has to still open then.
/// </remarks>
public class AutomationTests
{
    private static AutomationLane Cutoff(int track = 0) => new()
    {
        Track = track,
        Kind = ControlKind.Instrument,
        Machine = "zampler",
        Key = "cutoff"
    };

    private static Song Made()
    {
        var song = new Song();
        song.Normalize();

        while (song.Mix.Count < song.TrackCount) song.Mix.Add(new TrackMix());

        return song;
    }

    [Fact]
    public void A_lane_with_nothing_in_it_says_nothing()
    {
        Assert.Null(Cutoff().ValueAt(0));
    }

    [Fact]
    public void One_point_covers_the_whole_pattern()
    {
        var lane = Cutoff();
        lane.Put(16, 0.25);

        Assert.Equal(0.25, lane.ValueAt(0));
        Assert.Equal(0.25, lane.ValueAt(16));
        Assert.Equal(0.25, lane.ValueAt(63));
    }

    [Fact]
    public void Lines_go_straight_between_the_points()
    {
        var lane = Cutoff();
        lane.Play = AutomationPlay.Lines;
        lane.Put(0, 0);
        lane.Put(10, 1);

        Assert.Equal(0.5, lane.ValueAt(5)!.Value, 6);
        Assert.Equal(0.2, lane.ValueAt(2)!.Value, 6);
    }

    [Fact]
    public void Points_hold_until_the_next_one()
    {
        var lane = Cutoff();
        lane.Play = AutomationPlay.Points;
        lane.Put(0, 0);
        lane.Put(10, 1);

        Assert.Equal(0, lane.ValueAt(5));
        Assert.Equal(0, lane.ValueAt(9));
        Assert.Equal(1, lane.ValueAt(10));
    }

    [Fact]
    public void A_point_at_a_time_that_has_one_replaces_it()
    {
        var lane = Cutoff();
        lane.Put(4, 0.1);
        lane.Put(4, 0.9);

        Assert.Single(lane.Points);
        Assert.Equal(0.9, lane.Points[0].Value);
    }

    [Fact]
    public void Points_are_kept_in_time_order_whatever_order_they_arrive_in()
    {
        var lane = Cutoff();
        lane.Put(32, 1);
        lane.Put(0, 0);
        lane.Put(16, 0.5);

        Assert.Equal(new double[] { 0, 16, 32 }, lane.Points.Select(one => one.Time));
    }

    [Fact]
    public void A_value_outside_nought_to_one_is_brought_inside_it()
    {
        var lane = Cutoff();
        lane.Put(0, 4);
        lane.Put(1, -2);

        Assert.Equal(1, lane.Points[0].Value);
        Assert.Equal(0, lane.Points[1].Value);
    }

    [Fact]
    public void A_pattern_gives_back_the_lane_it_already_has()
    {
        var pattern = new Pattern(64, 4);

        var first = pattern.Lane(Cutoff());
        var again = pattern.Lane(Cutoff());

        Assert.Same(first, again);
        Assert.Single(pattern.Lanes);
    }

    [Fact]
    public void Two_parameters_on_one_track_are_two_lanes()
    {
        var pattern = new Pattern(64, 4);

        pattern.Lane(Cutoff());
        pattern.Lane(new AutomationLane
        {
            Kind = ControlKind.Instrument, Machine = "zampler", Key = "resonance"
        });

        Assert.Equal(2, pattern.Lanes.Count);
    }

    [Fact]
    public void A_track_moved_takes_its_lanes_with_it()
    {
        var pattern = new Pattern(64, 4);
        pattern.Lane(Cutoff(3));

        pattern.MoveTrack(3, 0);

        Assert.Equal(0, pattern.Lanes[0].Track);
    }

    [Fact]
    public void The_tracks_a_move_slides_past_keep_their_own_lanes()
    {
        var pattern = new Pattern(64, 4);
        pattern.Lane(Cutoff(0));
        pattern.Lane(Cutoff(1));

        // Track 0 goes to the end, so what was track 1 becomes track 0.
        pattern.MoveTrack(0, 3);

        Assert.Equal(3, pattern.Lanes[0].Track);
        Assert.Equal(0, pattern.Lanes[1].Track);
    }

    [Fact]
    public void Clearing_a_track_takes_its_movement_as_well_as_its_notes()
    {
        var pattern = new Pattern(64, 4);
        pattern.Lane(Cutoff(2));

        pattern.ClearTrack(2);

        Assert.Empty(pattern.Lanes);
    }

    [Fact]
    public void A_pattern_made_shorter_drops_the_points_past_its_end()
    {
        var pattern = new Pattern(64, 4);
        var lane = pattern.Lane(Cutoff());
        lane.Put(0, 0);
        lane.Put(48, 1);

        pattern.Resize(16);

        Assert.Single(lane.Points);
        Assert.Equal(0, lane.Points[0].Time);
    }

    [Fact]
    public void A_track_taken_off_takes_its_lane_with_it()
    {
        var pattern = new Pattern(64, 8);
        pattern.Lane(Cutoff(6));
        pattern.Lane(Cutoff(1));

        pattern.SetTrackCount(4);

        Assert.Single(pattern.Lanes);
        Assert.Equal(1, pattern.Lanes[0].Track);
    }

    /// <remarks>
    /// The master is a strip without being a track, so a lane can be about it: it has a level, a
    /// pan and a chain, all of which move. Nothing else about it is a track, which is why the
    /// number is below nought rather than one past the end.
    /// </remarks>
    [Fact]
    public void A_lane_can_be_about_the_master()
    {
        var pattern = new Pattern(64, 4);

        var made = AutomationLane.For(
            new ControlMapping { Kind = ControlKind.Mix, Mix = MixControl.Volume },
            TrackerPlayer.MasterStrip);

        Assert.NotNull(made);

        var lane = pattern.Lane(made!);

        Assert.True(lane.IsMaster);
        Assert.Equal(TrackerPlayer.MasterStrip, lane.Track);
    }

    /// <remarks>
    /// And it stays when the tracks are taken away, because it is not one of them and no count
    /// of them reaches it.
    /// </remarks>
    [Fact]
    public void And_survives_the_tracks_being_cut_down()
    {
        var pattern = new Pattern(64, 8);

        pattern.Lane(AutomationLane.For(
            new ControlMapping { Kind = ControlKind.Mix, Mix = MixControl.Volume },
            TrackerPlayer.MasterStrip)!);

        pattern.Lane(Cutoff(6));

        pattern.SetTrackCount(4);

        var left = Assert.Single(pattern.Lanes);

        Assert.True(left.IsMaster);
    }

    [Fact]
    public void And_stays_put_when_the_tracks_are_moved()
    {
        var pattern = new Pattern(64, 4);

        pattern.Lane(AutomationLane.For(
            new ControlMapping { Kind = ControlKind.Mix, Mix = MixControl.Volume },
            TrackerPlayer.MasterStrip)!);

        pattern.MoveTrack(0, 3);

        Assert.Equal(TrackerPlayer.MasterStrip, pattern.Lanes[0].Track);
    }

    [Fact]
    public void A_master_lane_is_written_down_and_read_back_like_any_other()
    {
        var song = Made();

        var lane = song.Patterns[0].Lane(AutomationLane.For(
            new ControlMapping { Kind = ControlKind.Mix, Mix = MixControl.Pan },
            TrackerPlayer.MasterStrip)!);

        lane.Put(8, 0.75);

        var back = SongStore.Uncopy(SongStore.Copy(song))!.Patterns[0].Lanes[0];

        Assert.True(back.IsMaster);
        Assert.Equal(MixControl.Pan, back.Mix);
        Assert.Equal(0.75, back.Points[0].Value);
    }

    [Fact]
    public void A_lane_written_down_and_read_back_is_the_same_lane()
    {
        var song = Made();

        var lane = song.Patterns[0].Lane(Cutoff(1));
        lane.Play = AutomationPlay.Points;
        lane.Put(0, 0.125);
        lane.Put(31, 0.875);

        var was = SongStore.Uncopy(SongStore.Copy(song));

        Assert.NotNull(was);

        var back = Assert.Single(was!.Patterns[0].Lanes);

        Assert.Equal(1, back.Track);
        Assert.Equal(ControlKind.Instrument, back.Kind);
        Assert.Equal(AutomationPlay.Points, back.Play);
        Assert.Equal("zampler", back.Machine);
        Assert.Equal("cutoff", back.Key);
        Assert.Equal(2, back.Points.Count);
        Assert.Equal(0.125, back.Points[0].Value);
        Assert.Equal(31, back.Points[1].Time);
        Assert.Equal(0.875, back.Points[1].Value);
    }

    [Fact]
    public void An_insert_lane_keeps_which_plugin_and_which_parameter()
    {
        var song = Made();

        song.Patterns[0].Lane(new AutomationLane
        {
            Track = 2,
            Kind = ControlKind.Insert,
            Plugin = "vst3:Serum",
            Slot = 1,
            Parameter = 74
        });

        var back = SongStore.Uncopy(SongStore.Copy(song))!.Patterns[0].Lanes[0];

        Assert.Equal(ControlKind.Insert, back.Kind);
        Assert.Equal("vst3:Serum", back.Plugin);
        Assert.Equal(1, back.Slot);
        Assert.Equal(74u, back.Parameter);
    }

    [Fact]
    public void A_mix_lane_keeps_which_control()
    {
        var song = Made();

        song.Patterns[0].Lane(new AutomationLane
        {
            Track = 0, Kind = ControlKind.Mix, Mix = MixControl.Pan
        });

        var back = SongStore.Uncopy(SongStore.Copy(song))!.Patterns[0].Lanes[0];

        Assert.Equal(ControlKind.Mix, back.Kind);
        Assert.Equal(MixControl.Pan, back.Mix);
    }

    [Fact]
    public void A_song_with_no_lanes_reads_back_with_none()
    {
        var was = SongStore.Uncopy(SongStore.Copy(Made()));

        Assert.NotNull(was);
        Assert.All(was!.Patterns, one => Assert.Empty(one.Lanes));
    }

    /// <remarks>
    /// The point of the double rather than an int. Nothing produces a fraction today, since
    /// there is no delay column for one to mean anything against, but the file has to carry one
    /// the day something does.
    /// </remarks>
    [Fact]
    public void A_point_between_two_lines_survives_the_file()
    {
        var song = Made();

        var lane = song.Patterns[0].Lane(Cutoff());
        lane.Put(4.5, 0.5);

        var back = SongStore.Uncopy(SongStore.Copy(song))!.Patterns[0].Lanes[0];

        Assert.Equal(4.5, back.Points[0].Time);
    }

    [Fact]
    public void A_copied_pattern_carries_its_movement()
    {
        var pattern = new Pattern(64, 4);
        pattern.Lane(Cutoff()).Put(8, 0.5);

        var copy = pattern.Clone();
        copy.Lanes[0].Put(8, 0.9);

        Assert.Equal(0.5, pattern.Lanes[0].Points[0].Value);
        Assert.Equal(0.9, copy.Lanes[0].Points[0].Value);
    }
}

/// <summary>
/// The clock writing a lane, and a hand writing one down. The two halves of the same door.
/// </summary>
public class AutomationPlaybackTests
{
    private static Song Made()
    {
        var song = new Song();
        song.Normalize();

        while (song.Mix.Count < song.TrackCount) song.Mix.Add(new TrackMix());

        return song;
    }

    private static AutomationLane Cutoff(int track = 0) => new()
    {
        Track = track,
        Kind = ControlKind.Instrument,
        Machine = "zampler",
        Key = "cutoff"
    };

    [Fact]
    public void A_lane_puts_the_parameter_where_it_says()
    {
        var song = Made();
        song.Patterns[0].Lane(Cutoff()).Put(0, 0.25);

        var knob = new Knob(0, 100, 200);
        var player = new AutomationPlayer(new OneTarget(knob));

        player.Play(song, new TrackerPosition(0, 0));

        // Nought to one, converted through the target's own range, which is the whole reason a
        // lane can be pointed at anything.
        Assert.Equal(125, knob.Value);
    }

    [Fact]
    public void A_value_that_has_not_moved_is_not_written_again()
    {
        var song = Made();
        song.Patterns[0].Lane(Cutoff()).Put(0, 0.5);

        var knob = new Knob(0, 0, 1);
        var player = new AutomationPlayer(new OneTarget(knob));

        player.Play(song, new TrackerPosition(0, 0));
        player.Play(song, new TrackerPosition(0, 1));
        player.Play(song, new TrackerPosition(0, 2));

        Assert.Equal(1, knob.Writes);
    }

    [Fact]
    public void A_sweep_is_written_on_every_line_it_moves()
    {
        var song = Made();
        var lane = song.Patterns[0].Lane(Cutoff());
        lane.Play = AutomationPlay.Lines;
        lane.Put(0, 0);
        lane.Put(4, 1);

        var knob = new Knob(0, 0, 1);
        var player = new AutomationPlayer(new OneTarget(knob));

        for (int line = 0; line <= 4; line++) player.Play(song, new TrackerPosition(0, line));

        Assert.Equal(1, knob.Value);

        // Five and not four: the first line is written whatever the parameter happens to hold,
        // because where a hand left it is not something a lane is entitled to assume.
        Assert.Equal(5, knob.Writes);
    }

    [Fact]
    public void A_pattern_with_no_lanes_writes_nothing()
    {
        var knob = new Knob(0.5, 0, 1);
        var player = new AutomationPlayer(new OneTarget(knob));

        player.Play(Made(), new TrackerPosition(0, 0));

        Assert.Equal(0, knob.Writes);
    }

    /// <remarks>
    /// The parameters have been moved by hand between one pass and the next, so what was written
    /// last time is not what they hold, and a lane that trusted its own memory would decline to
    /// write the first line of the next take.
    /// </remarks>
    [Fact]
    public void Starting_again_writes_the_first_line_again()
    {
        var song = Made();
        song.Patterns[0].Lane(Cutoff()).Put(0, 0.5);

        var knob = new Knob(0, 0, 1);
        var player = new AutomationPlayer(new OneTarget(knob));

        player.Play(song, new TrackerPosition(0, 0));
        player.Reset();
        player.Play(song, new TrackerPosition(0, 0));

        Assert.Equal(2, knob.Writes);
    }

    private static (AutomationRecorder Recorder, Song Song, int Steps) Recording(
        bool armed = true, bool running = true)
    {
        var song = Made();
        int steps = 0;

        var recorder = new AutomationRecorder(
            () => song,
            () => running,
            () => new TrackerPosition(0, 8),
            () => 0)
        {
            Armed = armed,
            Taking = (_, _) => steps++
        };

        return (recorder, song, steps);
    }

    private static ControlMapping Link() => new()
    {
        Kind = ControlKind.Instrument,
        Scope = ControlScope.Focused,
        Machine = "zampler",
        Key = "cutoff",
        Cc = 74
    };

    [Fact]
    public void A_knob_turned_while_armed_makes_a_lane_and_a_point()
    {
        var song = Made();

        var recorder = new AutomationRecorder(
            () => song, () => true, () => new TrackerPosition(0, 8), () => 0) { Armed = true };

        Assert.True(recorder.Moved(Link(), new Knob(150, 100, 200), 150));

        var lane = Assert.Single(song.Patterns[0].Lanes);

        Assert.Equal(0, lane.Track);
        Assert.Equal("cutoff", lane.Key);

        var point = Assert.Single(lane.Points);

        Assert.Equal(8, point.Time);

        // Halfway up a range of 100 to 200, which is a half however the parameter is measured.
        Assert.Equal(0.5, point.Value);
    }

    [Fact]
    public void Nothing_is_written_when_it_is_not_armed()
    {
        var song = Made();

        var recorder = new AutomationRecorder(
            () => song, () => true, () => new TrackerPosition(0, 8), () => 0) { Armed = false };

        Assert.False(recorder.Moved(Link(), new Knob(), 0.5));
        Assert.Empty(song.Patterns[0].Lanes);
    }

    [Fact]
    public void Nothing_is_written_when_the_song_is_not_playing()
    {
        var song = Made();

        var recorder = new AutomationRecorder(
            () => song, () => false, () => new TrackerPosition(0, 8), () => 0) { Armed = true };

        Assert.False(recorder.Moved(Link(), new Knob(), 0.5));
        Assert.Empty(song.Patterns[0].Lanes);
    }

    /// <remarks>
    /// A hand sweeping a filter is one thing a person did and a hundred points. A step apiece
    /// would be a hundred presses of Ctrl+Z to get back to where they started.
    /// </remarks>
    [Fact]
    public void A_whole_sweep_is_one_step()
    {
        var song = Made();
        int steps = 0;
        int line = 0;

        var recorder = new AutomationRecorder(
            () => song, () => true, () => new TrackerPosition(0, line), () => 0)
        {
            Armed = true,
            Taking = (_, _) => steps++
        };

        var knob = new Knob(0.5, 0, 1);

        for (line = 0; line < 32; line++) recorder.Moved(Link(), knob, line / 32.0);

        Assert.Equal(1, steps);
        Assert.Equal(32, song.Patterns[0].Lanes[0].Points.Count);
    }

    [Fact]
    public void Stopping_and_going_again_is_a_second_step()
    {
        var song = Made();
        int steps = 0;

        var recorder = new AutomationRecorder(
            () => song, () => true, () => new TrackerPosition(0, 4), () => 0)
        {
            Armed = true,
            Taking = (_, _) => steps++
        };

        recorder.Moved(Link(), new Knob(), 0.2);
        recorder.Stopped();
        recorder.Moved(Link(), new Knob(), 0.8);

        Assert.Equal(2, steps);
    }

    [Fact]
    public void A_second_parameter_in_one_pass_is_its_own_step_and_its_own_lane()
    {
        var song = Made();
        int steps = 0;

        var recorder = new AutomationRecorder(
            () => song, () => true, () => new TrackerPosition(0, 4), () => 0)
        {
            Armed = true,
            Taking = (_, _) => steps++
        };

        var other = Link();
        other.Key = "resonance";

        recorder.Moved(Link(), new Knob(), 0.2);
        recorder.Moved(other, new Knob(), 0.8);

        Assert.Equal(2, steps);
        Assert.Equal(2, song.Patterns[0].Lanes.Count);
    }

    /// <remarks>
    /// A link that names no parameter means the third knob on whatever face is in front of you,
    /// which is a fact about a hand rather than about a song. There is nothing to write down.
    /// </remarks>
    [Fact]
    public void A_link_that_names_no_parameter_records_nothing()
    {
        var song = Made();

        var recorder = new AutomationRecorder(
            () => song, () => true, () => new TrackerPosition(0, 4), () => 0) { Armed = true };

        var layout = new ControlMapping
        {
            Kind = ControlKind.Instrument, Machine = "zampler", Key = "", Ordinal = 2
        };

        Assert.False(recorder.Moved(layout, new Knob(), 0.5));
        Assert.Empty(song.Patterns[0].Lanes);
    }

    [Fact]
    public void A_button_cannot_be_recorded()
    {
        var song = Made();

        var recorder = new AutomationRecorder(
            () => song, () => true, () => new TrackerPosition(0, 4), () => 0) { Armed = true };

        var press = new ControlMapping
        {
            Kind = ControlKind.Action, Machine = "zampler", Key = "retrigger"
        };

        Assert.False(recorder.Moved(press, new Knob(), 1));
        Assert.Empty(song.Patterns[0].Lanes);
    }
}

/// <summary>The strip under the mixer: what it offers, what it narrows to, and what it makes.</summary>
public class AutomationListTests
{
    private sealed class Rack : IControlTargets
    {
        private readonly Dictionary<string, Knob> _knobs = new();

        public IControlTarget? Find(ControlMapping mapping) =>
            _knobs.TryGetValue(Key(mapping), out var knob) ? knob : null;

        public IEnumerable<ControlChoice> On(int track)
        {
            foreach (var key in new[] { "cutoff", "resonance" })
            {
                var mapping = new ControlMapping
                {
                    Kind = ControlKind.Instrument,
                    Scope = ControlScope.Fixed,
                    Track = track,
                    Machine = "zampler",
                    Key = key
                };

                _knobs[Key(mapping)] = new Knob(0.25, 0, 1);

                yield return new ControlChoice(mapping, "Zampler", key == "cutoff" ? "Cutoff" : "Resonance");
            }

            var level = new ControlMapping
            {
                Kind = ControlKind.Mix, Scope = ControlScope.Fixed, Track = track, Mix = MixControl.Volume
            };

            _knobs[Key(level)] = new Knob(0.8, 0, 1);

            yield return new ControlChoice(level, "Mixer", "Level");
        }

        private static string Key(ControlMapping one) =>
            one.Kind + ":" + one.Track + ":" + one.Machine + ":" + one.Key + ":" + one.Mix;
    }

    private static (AutomationViewModel Strip, Song Song) Made(int track = 0)
    {
        var song = new Song();
        song.Normalize();

        while (song.Mix.Count < song.TrackCount) song.Mix.Add(new TrackMix());

        var strip = new AutomationViewModel(
            new Rack(), () => song, () => song.Patterns[0], () => 4, () => -1);
        strip.Show(track);

        return (strip, song);
    }

    [Fact]
    public void Every_parameter_on_the_track_is_offered_in_panel_order()
    {
        var (strip, _) = Made();

        Assert.Equal(new[] { "Cutoff", "Resonance", "Level" }, strip.Parameters.Select(one => one.Name));
        Assert.Equal("Zampler  \u00b7  Cutoff", strip.Parameters[0].Said);
    }

    /// <remarks>
    /// The head block is about one parameter, so there is always one being worked on. Opening a
    /// strip that offered nothing to choose from would be a strip with nothing under it either.
    /// </remarks>
    [Fact]
    public void One_is_being_worked_on_from_the_moment_it_opens()
    {
        var (strip, _) = Made();

        Assert.True(strip.HasChosen);
        Assert.Equal("Cutoff", strip.Chosen!.Name);
    }

    [Fact]
    public void It_is_about_the_track_whose_button_was_pressed()
    {
        var (strip, _) = Made(2);

        Assert.Equal(2, strip.Track);
        Assert.StartsWith("TR-03", strip.About);
    }

    [Fact]
    public void Searching_narrows_by_the_parameter_and_by_the_device()
    {
        var (strip, _) = Made();

        strip.Search = "reso";
        Assert.Single(strip.Parameters);
        Assert.Equal("Resonance", strip.Parameters[0].Name);

        strip.Search = "mixer";
        Assert.Single(strip.Parameters);
        Assert.Equal("Level", strip.Parameters[0].Name);
    }

    [Fact]
    public void Automating_a_parameter_gives_it_a_lane_holding_where_it_stands()
    {
        var (strip, song) = Made();

        var row = strip.Chosen!;
        Assert.False(row.HasLane);

        row.AddCommand.Execute(null);

        var lane = Assert.Single(song.Patterns[0].Lanes);

        Assert.Equal("cutoff", lane.Key);

        var point = Assert.Single(lane.Points);

        Assert.Equal(0, point.Time);
        Assert.Equal(0.25, point.Value);
    }

    /// <remarks>
    /// The rows are made again after every edit, so what was being worked on has to be found
    /// again by what it names. Thrown back to the first parameter after every click, the strip
    /// would be unusable.
    /// </remarks>
    [Fact]
    public void What_is_being_worked_on_survives_an_edit_to_it()
    {
        var (strip, _) = Made();

        strip.Chosen = strip.Parameters[1];
        strip.Chosen!.AddCommand.Execute(null);

        Assert.Equal("Resonance", strip.Chosen!.Name);
        Assert.True(strip.Chosen!.HasLane);
    }

    [Fact]
    public void Clearing_takes_the_lane_off_again()
    {
        var (strip, song) = Made();

        strip.Chosen!.AddCommand.Execute(null);
        Assert.Single(song.Patterns[0].Lanes);

        strip.Chosen!.ForgetCommand.Execute(null);

        Assert.Empty(song.Patterns[0].Lanes);
        Assert.False(strip.Chosen!.HasLane);
    }

    [Fact]
    public void The_shape_can_be_switched_between_sweeping_and_stepping()
    {
        var (strip, song) = Made();

        strip.Chosen!.AddCommand.Execute(null);

        Assert.Equal("Sweeps", strip.Chosen!.How);

        strip.Chosen!.NextCommand.Execute(null);

        Assert.Equal(AutomationPlay.Points, song.Patterns[0].Lanes[0].Play);
        Assert.Equal("Steps", strip.Chosen!.How);
    }

    [Fact]
    public void A_lane_is_added_and_taken_off_through_the_history()
    {
        var (strip, _) = Made();

        int steps = 0;
        strip.Taking = (_, _) => steps++;

        strip.Chosen!.AddCommand.Execute(null);
        strip.Chosen!.ForgetCommand.Execute(null);

        Assert.Equal(2, steps);
    }
}
