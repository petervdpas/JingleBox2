using JingleBox2.Tracker;
using JingleBox2.Tracker.Synth;
using Xunit;

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
    private const int Rate = 44100;

    private const int Frames = 512;

    /// <summary>A patch that certainly makes a noise: on at once and staying on.</summary>
    private static SynthPatch Loud() => new()
    {
        AttackMs = 0,
        DecayMs = 0,
        Sustain = 1,
        ReleaseMs = 500
    };

    private static TrackMixer Playing(int track)
    {
        var mixer = new TrackMixer(Rate);

        mixer.NoteOn(track, Loud(), new Note(60), 1f, 0f);
        mixer.Render(new float[Frames * 2], Frames);

        return mixer;
    }

    [Fact]
    public void A_note_on_one_track_sounds_on_that_track()
    {
        var mixer = Playing(0);

        Assert.True(mixer.LevelFor(0).Left > 0);
    }

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

    /// <remarks>
    /// A level belongs to its own track. Turning another one down used to be the cheapest way to
    /// find out it did not, and now it is a test instead.
    /// </remarks>
    [Fact]
    public void Turning_another_track_down_leaves_this_one_alone()
    {
        var mixer = Playing(2);

        mixer.SetLevels(0, 0f, null);
        mixer.SetLevels(1, 0f, null);
        mixer.Render(new float[Frames * 2], Frames);

        Assert.True(mixer.LevelFor(2).Left > 0);
    }

    [Fact]
    public void And_turning_this_one_down_silences_it()
    {
        var mixer = Playing(2);

        mixer.SetLevels(2, 0f, null);

        var buffer = new float[Frames * 2];
        mixer.Render(buffer, Frames);

        float loudest = 0;
        foreach (var sample in buffer) loudest = System.Math.Max(loudest, System.Math.Abs(sample));

        Assert.Equal(0, loudest, 4);
    }

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

    /// <summary>An effect that does nothing, so it can be recognised rather than heard.</summary>
    private sealed class Marker : JingleBox2.Audio.Plugins.IAudioInsert
    {
        public void Process(float[] buffer, int frames) { }
    }

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
