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
/// <para>
/// Four groups, in this order. What a lane answers at a time, which is the shape and the
/// clamping. What a pattern does with the lanes it holds, including tracks being moved, cleared,
/// resized and taken away. The master, which is a strip without being a track and so is the one
/// lane no count of tracks may reach. And the file, which is every kind of lane written down and
/// read back.
/// </para>
/// </remarks>
public class AutomationTests
{
    /// <summary>
    /// A lane on one track's Zampler cutoff, which is the plain case every test uses.
    /// </summary>
    private static AutomationLane Cutoff(int track = 0) => new()
    {
        Track = track,
        Kind = ControlKind.Instrument,
        Machine = "zampler",
        Key = "cutoff"
    };

    /// <summary>
    /// A normalised song with a strip per track, which is what the file tests write out.
    /// </summary>
    private static Song Made()
    {
        var song = new Song();
        song.Normalize();

        while (song.Mix.Count < song.TrackCount) song.Mix.Add(new TrackMix());

        return song;
    }

    /// <summary>
    /// An empty lane answers nothing rather than nought, so the parameter is left where the hand
    /// put it rather than being dragged to the floor.
    /// </summary>
    [Fact]
    public void A_lane_with_nothing_in_it_says_nothing()
    {
        Assert.Null(Cutoff().ValueAt(0));
    }

    /// <summary>
    /// One point is a flat line across the whole pattern, before it and after it, since a lane
    /// has no notion of a value that has not started yet.
    /// </summary>
    [Fact]
    public void One_point_covers_the_whole_pattern()
    {
        var lane = Cutoff();
        lane.Put(16, 0.25);

        Assert.Equal(0.25, lane.ValueAt(0));
        Assert.Equal(0.25, lane.ValueAt(16));
        Assert.Equal(0.25, lane.ValueAt(63));
    }

    /// <summary>Sweeping is a straight line between neighbouring points.</summary>
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

    /// <summary>
    /// Stepping holds a value until the next point takes over, which is what a switch or a wave
    /// choice needs and a sweep would ruin.
    /// </summary>
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

    /// <summary>
    /// A lane holds one point per time, so writing at an occupied time replaces rather than
    /// piling a second point on top of the first.
    /// </summary>
    [Fact]
    public void A_point_at_a_time_that_has_one_replaces_it()
    {
        var lane = Cutoff();
        lane.Put(4, 0.1);
        lane.Put(4, 0.9);

        Assert.Single(lane.Points);
        Assert.Equal(0.9, lane.Points[0].Value);
    }

    /// <summary>
    /// Recording and drawing both write points out of order, and every reader downstream walks
    /// the list expecting time to run forwards.
    /// </summary>
    [Fact]
    public void Points_are_kept_in_time_order_whatever_order_they_arrive_in()
    {
        var lane = Cutoff();
        lane.Put(32, 1);
        lane.Put(0, 0);
        lane.Put(16, 0.5);

        Assert.Equal(new double[] { 0, 16, 32 }, lane.Points.Select(one => one.Time));
    }

    /// <summary>
    /// Values are normalised nought to one, so anything outside is brought inside rather than
    /// converted through a target's range into a parameter that has no such setting.
    /// </summary>
    [Fact]
    public void A_value_outside_nought_to_one_is_brought_inside_it()
    {
        var lane = Cutoff();
        lane.Put(0, 4);
        lane.Put(1, -2);

        Assert.Equal(1, lane.Points[0].Value);
        Assert.Equal(0, lane.Points[1].Value);
    }

    /// <summary>
    /// One lane per parameter per track per pattern: asking twice gives the same lane back, or a
    /// parameter would end up with two lanes disagreeing about it.
    /// </summary>
    [Fact]
    public void A_pattern_gives_back_the_lane_it_already_has()
    {
        var pattern = new Pattern(64, 4);

        var first = pattern.Lane(Cutoff());
        var again = pattern.Lane(Cutoff());

        Assert.Same(first, again);
        Assert.Single(pattern.Lanes);
    }

    /// <summary>
    /// And the other half: two parameters on one track are told apart by their key.
    /// </summary>
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

    /// <summary>
    /// A lane names its track by number, so moving the track has to renumber the lane or the
    /// movement would stay behind on whatever slid into the place.
    /// </summary>
    [Fact]
    public void A_track_moved_takes_its_lanes_with_it()
    {
        var pattern = new Pattern(64, 4);
        pattern.Lane(Cutoff(3));

        pattern.MoveTrack(3, 0);

        Assert.Equal(0, pattern.Lanes[0].Track);
    }

    /// <summary>
    /// Everything a move slides past is renumbered too, not only the track being moved.
    /// </summary>
    /// <remarks>
    /// Track 0 goes to the end, so what was track 1 becomes track 0.
    /// </remarks>
    [Fact]
    public void The_tracks_a_move_slides_past_keep_their_own_lanes()
    {
        var pattern = new Pattern(64, 4);
        pattern.Lane(Cutoff(0));
        pattern.Lane(Cutoff(1));

        pattern.MoveTrack(0, 3);

        Assert.Equal(3, pattern.Lanes[0].Track);
        Assert.Equal(0, pattern.Lanes[1].Track);
    }

    /// <summary>
    /// Clearing a track empties the whole track, and a lane left behind would go on moving a
    /// parameter on a column somebody has just emptied.
    /// </summary>
    [Fact]
    public void Clearing_a_track_takes_its_movement_as_well_as_its_notes()
    {
        var pattern = new Pattern(64, 4);
        pattern.Lane(Cutoff(2));

        pattern.ClearTrack(2);

        Assert.Empty(pattern.Lanes);
    }

    /// <summary>
    /// Shortening a pattern drops the points that fall off the end, the same as the notes there,
    /// so nothing is left holding a time the pattern no longer reaches.
    /// </summary>
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

    /// <summary>
    /// Cutting the track count throws away the lanes on the tracks that went, and leaves the ones
    /// below untouched at their own numbers.
    /// </summary>
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

    /// <summary>A lane names a strip rather than a track, so it can be about the master.</summary>
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

    /// <summary>A master lane outlives the tracks being cut down.</summary>
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

    /// <summary>
    /// Nor does it shift when tracks are reordered: strip -1 is not a place in the list, so no
    /// renumbering can land on it.
    /// </summary>
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

    /// <summary>
    /// The file keeps a master lane as a lane and not as a special case, so it comes back knowing
    /// which strip and which control it is about.
    /// </summary>
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

    /// <summary>
    /// Every field of an instrument lane survives the file: the track, the kind, the shape, the
    /// machine, the key and both points.
    /// </summary>
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

    /// <summary>
    /// A lane on a plugin in a track's chain names the plugin, its place in the chain and the
    /// parameter number, since none of those can be worked out from the others.
    /// </summary>
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

    /// <summary>And a lane on the strip keeps which of the strip's controls it moves.</summary>
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

    /// <summary>
    /// A song with no automation in it reads back with none rather than with an empty lane per
    /// pattern, since an empty lane lists as automated and moves nothing.
    /// </summary>
    [Fact]
    public void A_song_with_no_lanes_reads_back_with_none()
    {
        var was = SongStore.Uncopy(SongStore.Copy(Made()));

        Assert.NotNull(was);
        Assert.All(was!.Patterns, one => Assert.Empty(one.Lanes));
    }

    /// <summary>A time between two lines goes through the file unrounded.</summary>
    /// <remarks>
    /// The point of the double rather than an int. Nothing produces a fraction today, since
    /// there is no delay column for one to mean anything against, but the file has to carry one
    /// the day something does. Renoise quantises to 256 units per line and says what that unit
    /// is, "a time of 1.5 means line 1 with a note column delay of 128", so sub-line automation
    /// and a delay column are one grid there and this codebase has neither.
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

    /// <summary>
    /// A cloned pattern gets lanes of its own rather than a second view of the same ones, which
    /// is what a history step depends on.
    /// </summary>
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
/// <remarks>
/// The clock arriving at line 32 and a knob writing from CC 74 are one act, so the player reaches
/// a parameter through <see cref="IControlTarget"/> exactly as the control router does.
/// <para>
/// Two groups, in this order. Playback, which is a lane put onto a parameter and the rate at
/// which it is written. Then recording, which is a hand turning a knob while the song runs, and
/// is mostly about what counts as one thing a person did.
/// </para>
/// </remarks>
public class AutomationPlaybackTests
{
    /// <summary>A normalised song with a strip per track, ready to be given a lane.</summary>
    private static Song Made()
    {
        var song = new Song();
        song.Normalize();

        while (song.Mix.Count < song.TrackCount) song.Mix.Add(new TrackMix());

        return song;
    }

    /// <summary>
    /// A lane on one track's Zampler cutoff, which is the plain case every test uses.
    /// </summary>
    private static AutomationLane Cutoff(int track = 0) => new()
    {
        Track = track,
        Kind = ControlKind.Instrument,
        Machine = "zampler",
        Key = "cutoff"
    };

    /// <summary>
    /// A lane's value reaches the parameter through the target's own range, which is what lets
    /// one lane drive a filter, a pan or a plugin knob without knowing what any of them mean.
    /// </summary>
    /// <remarks>
    /// Nought to one, converted through the target's own range, which is the whole reason a lane
    /// can be pointed at anything.
    /// </remarks>
    [Fact]
    public void A_lane_puts_the_parameter_where_it_says()
    {
        var song = Made();
        song.Patterns[0].Lane(Cutoff()).Put(0, 0.25);

        var knob = new Knob(0, 100, 200);
        var player = new AutomationPlayer(new OneTarget(knob));

        player.Play(song, new TrackerPosition(0, 0));

        Assert.Equal(125, knob.Value);
    }

    /// <summary>
    /// A flat lane writes once and then stops, since writing a parameter is a round trip to
    /// another process for a plugin and this runs on every line of every pass.
    /// </summary>
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

    /// <summary>
    /// A moving lane is written on every line it moves, which is the other side of the rule
    /// above.
    /// </summary>
    /// <remarks>
    /// Five and not four: the first line is written whatever the parameter happens to hold,
    /// because where a hand left it is not something a lane is entitled to assume.
    /// </remarks>
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

        Assert.Equal(5, knob.Writes);
    }

    /// <summary>
    /// A song with no automation touches no parameter at all, so a knob moved by hand during
    /// playback stays where it was put.
    /// </summary>
    [Fact]
    public void A_pattern_with_no_lanes_writes_nothing()
    {
        var knob = new Knob(0.5, 0, 1);
        var player = new AutomationPlayer(new OneTarget(knob));

        player.Play(Made(), new TrackerPosition(0, 0));

        Assert.Equal(0, knob.Writes);
    }

    /// <summary>
    /// A reset forgets what was written, so the next pass starts by writing again.
    /// </summary>
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

    /// <summary>
    /// A recorder over a fresh song, parked at line 8 of the first pattern, counting the undo
    /// steps it asks for.
    /// </summary>
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

    /// <summary>
    /// A link from CC 74 to Zampler's cutoff on whichever track is in front, which is the shape
    /// a link learned by hand actually has.
    /// </summary>
    private static ControlMapping Link() => new()
    {
        Kind = ControlKind.Instrument,
        Scope = ControlScope.Focused,
        Machine = "zampler",
        Key = "cutoff",
        Cc = 74
    };

    /// <summary>
    /// Turning a knob while armed and running makes the lane if there is none and writes a point
    /// at the line the clock is on.
    /// </summary>
    /// <remarks>
    /// Halfway up a range of 100 to 200, which is a half however the parameter is measured.
    /// </remarks>
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

        Assert.Equal(0.5, point.Value);
    }

    /// <summary>
    /// Unarmed, a knob moves the parameter and writes nothing down, which is the ordinary way of
    /// working a song.
    /// </summary>
    [Fact]
    public void Nothing_is_written_when_it_is_not_armed()
    {
        var song = Made();

        var recorder = new AutomationRecorder(
            () => song, () => true, () => new TrackerPosition(0, 8), () => 0) { Armed = false };

        Assert.False(recorder.Moved(Link(), new Knob(), 0.5));
        Assert.Empty(song.Patterns[0].Lanes);
    }

    /// <summary>
    /// Stopped, there is no line to write against, so an armed recorder still writes nothing
    /// rather than piling every movement onto whatever line the cursor was left on.
    /// </summary>
    [Fact]
    public void Nothing_is_written_when_the_song_is_not_playing()
    {
        var song = Made();

        var recorder = new AutomationRecorder(
            () => song, () => false, () => new TrackerPosition(0, 8), () => 0) { Armed = true };

        Assert.False(recorder.Moved(Link(), new Knob(), 0.5));
        Assert.Empty(song.Patterns[0].Lanes);
    }

    /// <summary>Thirty two points recorded in one pass leave one undo step behind them.</summary>
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

    /// <summary>
    /// Stopping ends the gathering, so the next pass is its own step and undo takes back one take
    /// at a time.
    /// </summary>
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

    /// <summary>
    /// Gathering is per lane and not per pass: two parameters moved in one take are two things a
    /// person did and come back one at a time.
    /// </summary>
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

    /// <summary>
    /// A default layout link names a place rather than a parameter, and records nothing.
    /// </summary>
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

    /// <summary>
    /// An action is a press and not a value, so there is no position for a point to hold and no
    /// lane is made.
    /// </summary>
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
/// <remarks>
/// The strip shows whichever view model it is given rather than reaching through to the tracker
/// for one, which is what lets the same strip serve a track and the master, and is also what
/// lets it be put a question to here without a window.
/// </remarks>
public class AutomationListTests
{
    /// <summary>
    /// A stand-in for the machines and the mixer: two instrument parameters and the strip's own
    /// level, answered in panel order, with a knob behind each so a value can be read back.
    /// </summary>
    private sealed class Rack : IControlTargets
    {
        /// <summary>The knobs handed out by <see cref="On"/>, kept so <see cref="Find"/> can
        /// answer with the same one.</summary>
        private readonly Dictionary<string, Knob> _knobs = new();

        /// <inheritdoc/>
        public IControlTarget? Find(ControlMapping mapping) =>
            _knobs.TryGetValue(Key(mapping), out var knob) ? knob : null;

        /// <inheritdoc/>
        /// <remarks>
        /// The order is the order the list has to show: the machine's parameters in panel order,
        /// and then the strip.
        /// </remarks>
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

        /// <summary>
        /// What tells one mapping from another here, since a mapping is not a key.
        /// </summary>
        private static string Key(ControlMapping one) =>
            one.Kind + ":" + one.Track + ":" + one.Machine + ":" + one.Key + ":" + one.Mix;
    }

    /// <summary>
    /// A strip over a fresh song, opened on one track, with the stand-in rack behind it and no
    /// history hooked up.
    /// </summary>
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

    /// <summary>
    /// The list is the machine's parameters in panel order and then the strip's, and each row
    /// says its device as well as its name.
    /// </summary>
    /// <remarks>
    /// The device is named beside the parameter rather than appended to it, because a target's
    /// own name is written for a status line and ends in the track it is on, and forty rows
    /// ending in the same three words is a list nobody can scan.
    /// </remarks>
    [Fact]
    public void Every_parameter_on_the_track_is_offered_in_panel_order()
    {
        var (strip, _) = Made();

        Assert.Equal(new[] { "Cutoff", "Resonance", "Level" }, strip.Parameters.Select(one => one.Name));
        Assert.Equal("Zampler  \u00b7  Cutoff", strip.Parameters[0].Said);
    }

    /// <summary>The strip opens with the first parameter already chosen.</summary>
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

    /// <summary>
    /// The strip knows which track it is showing and says so, since the parameters of two tracks
    /// look identical and only the number tells them apart.
    /// </summary>
    [Fact]
    public void It_is_about_the_track_whose_button_was_pressed()
    {
        var (strip, _) = Made(2);

        Assert.Equal(2, strip.Track);
        Assert.StartsWith("TR-03", strip.About);
    }

    /// <summary>
    /// The search box matches the device as well as the parameter, which is how a plugin with
    /// two thousand parameters is got through at all.
    /// </summary>
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

    /// <summary>
    /// A new lane arrives with one point holding where the parameter stands, since an empty lane
    /// would list as automated and move nothing.
    /// </summary>
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

    /// <summary>Adding a lane leaves the same parameter chosen, now with a lane on it.</summary>
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

    /// <summary>
    /// The other direction: forgetting a lane removes it from the pattern entirely.
    /// </summary>
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

    /// <summary>
    /// The shape button cycles the lane between sweeping and stepping, and the word on it follows
    /// the lane rather than a count of presses.
    /// </summary>
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

    /// <summary>
    /// Adding and forgetting a lane each announce themselves, so a lane cleared by mistake is one
    /// press of undo away rather than gone.
    /// </summary>
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
