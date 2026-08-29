using JingleBox2.Tracker.Synth;
using Xunit;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tests;

/// <summary>
/// Whether one track's audio is one track's, which is the question a mixer exists to answer.
/// </summary>
/// <remarks>
/// The engine is built per track: a buffer, a level, a pan, an insert, a ducker and a plugin
/// slot apiece, all indexed by track number. Indexed things are exactly what goes wrong quietly,
/// so this plays a note on one track and asks what every other track is sounding.
///
/// No hardware and no window: the mixer takes a sample rate and fills a buffer.
/// </remarks>
public class MixerIsolationTests
{
    /// <summary>The sample rate everything here is rendered at.</summary>
    private const int Rate = 44100;

    /// <summary>One block, in frames. Long enough for a level to be worth reading off.</summary>
    private const int Frames = 512;

    /// <summary>A patch that certainly makes a noise: on at once and staying on.</summary>
    private static SynthPatch Loud() => new()
    {
        AttackMs = 0,
        DecayMs = 0,
        Sustain = 1,
        ReleaseMs = 500
    };

    /// <summary>
    /// A mixer with one note sounding on one track, one block in, so the meters have something
    /// to say. Every test that asks about isolation starts from this and then asks about the
    /// other tracks.
    /// </summary>
    private static TrackMixer Playing(int track)
    {
        var mixer = new TrackMixer(Rate);

        mixer.NoteOn(track, 0, Loud(), new Note(60), 1f, 0f);
        mixer.Render(new float[Frames * 2], Frames);

        return mixer;
    }

    /// <summary>
    /// A note lands on the strip it was aimed at, which is the easy half of the question.
    /// </summary>
    [Fact]
    public void A_note_on_one_track_sounds_on_that_track()
    {
        var mixer = Playing(0);

        Assert.True(mixer.LevelFor(0).Left > 0);
    }

    /// <summary>And on no other strip, which is the half that goes wrong quietly.</summary>
    /// <remarks>
    /// The whole question. A voice carries the track it belongs to, and every buffer, level and
    /// ducker is indexed by that number; one off anywhere and a note would be heard on the wrong
    /// strip, which is the kind of fault that reads as the mixer being haunted.
    /// </remarks>
    [Fact]
    public void And_on_no_other_track()
    {
        var mixer = Playing(0);

        for (int track = 1; track < 8; track++)
        {
            Assert.Equal(0, mixer.LevelFor(track).Left);
            Assert.Equal(0, mixer.LevelFor(track).Right);
        }
    }

    /// <summary>A fader on one strip does not reach the audio of another.</summary>
    /// <remarks>
    /// A level belongs to its own track. Turning another one down used to be the cheapest way to
    /// find out it did not, and now it is a test instead.
    /// </remarks>
    [Fact]
    public void Turning_another_track_down_leaves_this_one_alone()
    {
        var mixer = Playing(2);

        mixer.SetLevels(0, 0, 0f, null);
        mixer.SetLevels(1, 0, 0f, null);
        mixer.Render(new float[Frames * 2], Frames);

        Assert.True(mixer.LevelFor(2).Left > 0);
    }

    /// <summary>
    /// And a fader pulled to nothing does silence its own track, so the test above it is not
    /// passing by accident.
    /// </summary>
    [Fact]
    public void And_turning_this_one_down_silences_it()
    {
        var mixer = Playing(2);

        mixer.SetLevels(2, 0, 0f, null);

        var buffer = new float[Frames * 2];
        mixer.Render(buffer, Frames);

        float loudest = 0;
        foreach (var sample in buffer) loudest = System.Math.Max(loudest, System.Math.Abs(sample));

        Assert.Equal(0, loudest, 4);
    }

    /// <summary>A preview that names no track moves no strip's meter.</summary>
    /// <remarks>
    /// A note played by hand belongs to nobody's track. It carries no track number at all, so a
    /// panel's keyboard cannot be heard on a strip, and a strip's fader cannot turn it down.
    /// </remarks>
    [Fact]
    public void An_audition_sounds_on_no_track_at_all()
    {
        var mixer = new TrackMixer(Rate);

        mixer.Preview(Loud(), new Note(60), 1f, 1.0, "test");
        mixer.Render(new float[Frames * 2], Frames);

        for (int track = 0; track < 8; track++)
            Assert.Equal(0, mixer.LevelFor(track).Left);
    }

    /// <summary>The master fader is heard in what leaves the mixer.</summary>
    /// <remarks>
    /// The master is the last thing between the mix and the card, so what it is set to has to be
    /// audible in what leaves rather than in what any one track is doing.
    /// </remarks>
    [Fact]
    public void The_master_fader_turns_the_whole_mix_down()
    {
        var mixer = Playing(0);

        mixer.SetMaster(0f, null);

        var buffer = new float[Frames * 2];
        mixer.Render(buffer, Frames);

        float loudest = 0;
        foreach (var sample in buffer) loudest = System.Math.Max(loudest, System.Math.Abs(sample));

        Assert.Equal(0, loudest, 4);
    }

    /// <summary>And it is not a strip: a track's own meter is untouched by it.</summary>
    /// <remarks>
    /// And it is not a track: turning the master down does not turn a track down, which is what
    /// the strip's own meter would show if the two were the same thing.
    /// </remarks>
    [Fact]
    public void And_leaves_the_track_reading_what_the_track_is_doing()
    {
        var mixer = Playing(0);

        mixer.SetMaster(0f, null);
        mixer.Render(new float[Frames * 2], Frames);

        Assert.True(mixer.LevelFor(0).Left > 0);
    }

    /// <summary>
    /// The master's meter follows the master's fader, since it measures the mix after it.
    /// </summary>
    [Fact]
    public void The_master_meter_reads_what_is_leaving()
    {
        var mixer = Playing(0);

        mixer.SetMaster(1f, null);
        mixer.Render(new float[Frames * 2], Frames);

        Assert.True(mixer.MasterLevel.Left > 0);

        mixer.SetMaster(0f, null);
        mixer.Render(new float[Frames * 2], Frames);

        Assert.Equal(0, mixer.MasterLevel.Left, 4);
    }

    /// <summary>
    /// An insert on the master is handed the summed mix, and is the one the mixer holds.
    /// </summary>
    /// <remarks>
    /// The effect goes before the fader, because a limiter across the mix is put there to catch
    /// what the music does rather than what the hand on the fader does.
    /// </remarks>
    [Fact]
    public void An_effect_across_the_master_hears_the_whole_mix()
    {
        var mixer = Playing(0);
        var heard = new Listener();

        mixer.SetMasterInsert(heard);
        mixer.Render(new float[Frames * 2], Frames);

        Assert.True(heard.Loudest > 0);
        Assert.Same(heard, mixer.MasterInsert);
    }

    /// <summary>The master's meter goes out when the music does.</summary>
    /// <remarks>
    /// The meter falls when there is nothing left to render, which is the one path that skips
    /// the render altogether. A track's own meter falls by itself, since it is worked out from
    /// the voices that are sounding; the master's is a peak measured off the last buffer, so
    /// without this it holds whatever the last thing to play was and the mixer goes on looking
    /// as though the song were still going.
    ///
    /// Two blocks: the first lets the cut fade out, the second finds nothing to render.
    /// </remarks>
    [Fact]
    public void The_master_meter_falls_when_there_is_nothing_left_to_play()
    {
        var mixer = new TrackMixer(Rate);

        mixer.NoteOn(0, 0, Loud(), new Note(60), 1f, 0f);
        mixer.Render(new float[Frames * 2], Frames);

        Assert.True(mixer.MasterLevel.Left > 0);

        mixer.StopAll();

        mixer.Render(new float[Frames * 2], Frames);
        mixer.Render(new float[Frames * 2], Frames);

        Assert.Equal(0, mixer.MasterLevel.Left, 4);
        Assert.Equal(0, mixer.MasterLevel.Right, 4);
    }

    /// <summary>A preview that names a track is that track playing, and only that track.</summary>
    /// <remarks>
    /// A note played by hand on a track is that track playing: it moves the track's own meter
    /// and the master's, and it goes through the track's fader on the way. Without that the
    /// keyboard told you nothing about what the part would sound like, because it was not going
    /// anywhere near the strip the part will play through.
    /// </remarks>
    [Fact]
    public void A_note_played_by_hand_on_a_track_moves_that_tracks_meter()
    {
        var mixer = new TrackMixer(Rate);

        mixer.Preview(Loud(), new Note(60), 1f, 1.0, "by hand", 2);
        mixer.Render(new float[Frames * 2], Frames);

        Assert.True(mixer.LevelFor(2).Left > 0);
        Assert.Equal(0, mixer.LevelFor(0).Left);
    }

    /// <summary>
    /// And it reaches the master too, since it goes through the mix rather than round it.
    /// </summary>
    [Fact]
    public void And_the_masters()
    {
        var mixer = new TrackMixer(Rate);

        mixer.Preview(Loud(), new Note(60), 1f, 1.0, "by hand", 2);
        mixer.Render(new float[Frames * 2], Frames);

        Assert.True(mixer.MasterLevel.Left > 0);
    }

    /// <summary>
    /// A preview with no track is still heard at the output without touching a strip.
    /// </summary>
    /// <remarks>
    /// And the rack's keyboard still belongs to nobody's track, because the instrument it is
    /// playing may not be in any song.
    /// </remarks>
    [Fact]
    public void While_the_racks_keyboard_still_belongs_to_no_track()
    {
        var mixer = new TrackMixer(Rate);

        mixer.Preview(Loud(), new Note(60), 1f, 1.0, "on the rack");
        mixer.Render(new float[Frames * 2], Frames);

        for (int track = 0; track < 8; track++)
            Assert.Equal(0, mixer.LevelFor(track).Left);

        Assert.True(mixer.MasterLevel.Left > 0);
    }

    /// <summary>
    /// A master reading is stamped when it is taken and stops being true on its own.
    /// </summary>
    /// <remarks>
    /// The master's meter is a peak off the last buffer, so it is only true while buffers are
    /// being asked for. It went on showing the last thing that played after the stream stopped,
    /// because nothing was left to notice: clearing it where the rendering stops only helps on
    /// the one path that goes through this class, and there are several. A reading that ages is
    /// gone whichever way the music stopped.
    /// </remarks>
    [Fact]
    public void A_master_reading_says_nothing_once_it_is_old()
    {
        Assert.True(TrackMixer.Fresh(0));
        Assert.True(TrackMixer.Fresh(TrackMixer.MeterHoldMs));
        Assert.False(TrackMixer.Fresh(TrackMixer.MeterHoldMs + 1));
    }

    /// <summary>What decides whether the meters are worth reading at all.</summary>
    /// <remarks>
    /// And the meters are polled for as long as something is sounding rather than for as long
    /// as the transport is running. Both faults the user found came from the second rule: the
    /// master sat lit after a pass ended, because the last reading taken was true and no further
    /// one was ever taken, and a note played by hand with the transport stopped moved nothing at
    /// all, because nothing was reading.
    ///
    /// A pass between two notes is silent and is not over. A note played by hand needs no pass.
    /// And the third case is the only one where there is nothing to read.
    /// </remarks>
    [Fact]
    public void Meters_are_read_for_as_long_as_something_is_sounding()
    {
        Assert.True(TrackMixer.Sounding(playing: true, loudest: 0f));

        Assert.True(TrackMixer.Sounding(playing: false, loudest: 0.2f));

        Assert.False(TrackMixer.Sounding(playing: false, loudest: 0f));
    }

    /// <summary>A strip's meter answers before the transport has ever run.</summary>
    /// <remarks>
    /// A track's meter was bounded by the volume column's memory, which is only made when a pass
    /// starts, so before anybody pressed play there were nought tracks to report on and every
    /// strip read silent. The master does not go through that door, which is why a note played
    /// by hand moved the master's meter and no track's.
    ///
    /// The mixer answers for every track a song can have, played or not.
    /// </remarks>
    [Fact]
    public void A_tracks_meter_reads_before_anything_has_been_played()
    {
        var mixer = new TrackMixer(Rate);

        mixer.Preview(Loud(), new Note(60), 1f, 1.0, "by hand", 1);
        mixer.Render(new float[Frames * 2], Frames);

        Assert.True(mixer.LevelFor(1).Left > 0);
        Assert.Equal(0, mixer.LevelFor(JingleBox2.Tracker.Song.MaxTrackCount - 1).Left);
        Assert.Equal(0, mixer.LevelFor(JingleBox2.Tracker.Song.MaxTrackCount).Left);
    }

    /// <summary>An effect that listens and passes the audio through untouched.</summary>
    private sealed class Listener : JingleBox2.Audio.Plugins.Interfaces.IAudioInsert
    {
        /// <summary>The loudest sample it has ever been handed, over every block.</summary>
        public float Loudest { get; private set; }

        /// <inheritdoc/>
        public void Process(float[] buffer, int frames)
        {
            for (int i = 0; i < frames * 2; i++)
                Loudest = System.Math.Max(Loudest, System.Math.Abs(buffer[i]));
        }
    }

    /// <summary>An effect that does nothing, so it can be recognised rather than heard.</summary>
    private sealed class Marker : JingleBox2.Audio.Plugins.Interfaces.IAudioInsert
    {
        /// <inheritdoc/>
        public void Process(float[] buffer, int frames) { }
    }

    /// <summary>Reordering the tracks carries each track's insert along with it.</summary>
    /// <remarks>
    /// Everything a track sounds through moves with it. The engine keeps seven things keyed by
    /// track number and a reorder has to carry all seven; one left behind and a track would play
    /// through somebody else's effect.
    /// </remarks>
    [Fact]
    public void A_track_moved_takes_what_it_sounds_through_with_it()
    {
        var mixer = new TrackMixer(Rate);
        var mine = new Marker();

        mixer.SetInsert(0, mine);

        mixer.MoveTrack(0, 3);

        Assert.Same(mine, mixer.InsertOn(3));
        Assert.Null(mixer.InsertOn(0));
    }

    /// <summary>And a track a move shifted past keeps its own.</summary>
    /// <remarks>
    /// And the tracks it passed over keep theirs, which is the half a shift gets wrong.
    /// </remarks>
    [Fact]
    public void And_the_tracks_it_passed_over_keep_theirs()
    {
        var mixer = new TrackMixer(Rate);
        var mine = new Marker();
        var yours = new Marker();

        mixer.SetInsert(0, mine);
        mixer.SetInsert(1, yours);

        mixer.MoveTrack(0, 3);

        Assert.Same(yours, mixer.InsertOn(0));
        Assert.Same(mine, mixer.InsertOn(3));
    }
}
