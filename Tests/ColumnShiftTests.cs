using System;
using System.Linq;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Synth;
using JingleBox2.Tracker.Synth.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The two things the mixer does for the pattern commands: holding one note column off its pitch,
/// which is how an arpeggio steps, and cutting one column dead, which is a note cut.
/// </summary>
/// <remarks>
/// Measured off the rendered block rather than read off a voice, since a pitch held on a voice that
/// never reaches the audio would pass every test that only asked the voice.
/// </remarks>
public class ColumnShiftTests
{
    /// <summary>The rate everything renders at.</summary>
    private const int Rate = 44100;

    /// <summary>A quarter of a second, long enough to count the cycles of middle C.</summary>
    private const int Frames = Rate / 4;

    /// <summary>A saw that is on at once and stays on, so its crossings are its pitch.</summary>
    private static SynthPatch Loud() => new()
    {
        Wave = SynthWave.Saw,
        AttackMs = 0,
        DecayMs = 0,
        Sustain = 1,
        ReleaseMs = 500
    };

    /// <summary>Upward crossings a second on the left channel, which for a saw is its pitch.</summary>
    private static double Pitch(float[] buffer)
    {
        int crossings = 0;

        for (int frame = 1; frame < Frames; frame++)
        {
            if (buffer[(frame - 1) * 2] < 0 && buffer[frame * 2] >= 0) crossings++;
        }

        return crossings / (Frames / (double)Rate);
    }

    /// <summary>One block from the mixer.</summary>
    private static float[] Render(TrackMixer mixer)
    {
        var buffer = new float[Frames * 2];
        mixer.Render(buffer, Frames);
        return buffer;
    }

    /// <summary>A mixer sounding middle C on track nought in each of the columns given.</summary>
    private static TrackMixer Playing(params int[] columns)
    {
        var mixer = new TrackMixer(Rate);

        foreach (int column in columns) mixer.NoteOn(0, column, Loud(), new Note(60), 1f, 0f);

        return mixer;
    }

    /// <summary>A shift of an octave doubles the pitch of what the column sounds.</summary>
    [Fact]
    public void A_shift_of_an_octave_doubles_the_pitch()
    {
        double plain = Pitch(Render(Playing(0)));

        var mixer = Playing(0);
        mixer.SetShift(0, 0, 12);

        Assert.Equal(2, Pitch(Render(mixer)) / plain, 1);
    }

    /// <summary>A shift on one column leaves the other columns of the chord where they were.</summary>
    [Fact]
    public void A_shift_on_one_column_leaves_the_others()
    {
        var mixer = new TrackMixer(Rate);
        mixer.NoteOn(0, 1, Loud(), new Note(60), 1f, 0f);
        mixer.SetShift(0, 0, 12);

        Assert.Equal(Render(Playing(1)), Render(mixer));
    }

    /// <summary>A shift adds to the wheel rather than replacing it.</summary>
    [Fact]
    public void A_shift_adds_to_the_wheel()
    {
        double plain = Pitch(Render(Playing(0)));

        var mixer = Playing(0);
        mixer.SetBend(0, 12);
        mixer.SetShift(0, 0, 12);

        Assert.Equal(4, Pitch(Render(mixer)) / plain, 1);

        mixer.SetBend(0, 0);

        Assert.Equal(2, Pitch(Render(mixer)) / plain, 1);
    }

    /// <summary>A new note on the column starts at its own pitch.</summary>
    [Fact]
    public void A_new_note_starts_at_its_own_pitch()
    {
        var mixer = new TrackMixer(Rate);
        mixer.SetShift(0, 0, 12);
        mixer.NoteOn(0, 0, Loud(), new Note(60), 1f, 0f);

        Assert.Equal(Render(Playing(0)), Render(mixer));
    }

    /// <summary>A track or column that is not there is answered with nothing rather than thrown at.</summary>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 99)]
    [InlineData(9999, 0)]
    public void Nonsense_is_ignored(int track, int column)
    {
        var mixer = Playing(0);

        mixer.SetShift(track, column, 12);
        mixer.Cut(track, column);

        Assert.Equal(Render(Playing(0)), Render(mixer));
    }

    /// <summary>A cut silences the column within a few milliseconds, where a release would ring on.</summary>
    [Fact]
    public void A_cut_silences_the_column_and_a_release_does_not()
    {
        var cut = Playing(0);
        Render(cut);
        cut.Cut(0, 0);

        var released = Playing(0);
        Render(released);
        released.NoteOff(0, 0);

        Assert.Equal(0f, Render(cut).Skip(Rate / 10 * 2).Max(Math.Abs));
        Assert.True(Render(released).Skip(Rate / 10 * 2).Max(Math.Abs) > 0.01f);
    }

    /// <summary>A cut takes one column and leaves the rest of the chord.</summary>
    [Fact]
    public void A_cut_takes_one_column()
    {
        var mixer = Playing(0, 1);
        Render(mixer);
        mixer.Cut(0, 0);

        Assert.True(Render(mixer).Skip(Rate / 10 * 2).Max(Math.Abs) > 0.01f);
    }
}
