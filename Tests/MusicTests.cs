using System.Collections.Generic;
using JingleBox2.Music;
using JingleBox2.Music.Interfaces;
using JingleBox2.Tracker.Records;
using System;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The note and keyboard maths, which knows nothing about patterns and never did.
/// </summary>
/// <remarks>
/// These five lived under <c>Tracker/</c> because that is where the first thing to need them
/// happened to be, and they were static, so nothing could stand in front of them and nothing
/// ever tested them. They answer the questions everything else in the tracker rests on: which
/// key sounds which note, how fast to play a recording to reach a pitch, and where concert
/// pitch is. Every one of them is reached through its interface here, which is the point.
/// </remarks>
public class MusicTests
{
    private readonly IKeyRegions _regions = new KeyRegions();
    private readonly IPitchRatio _ratio = new PitchRatio();
    private readonly INoteFrequency _pitch = new NoteFrequency();
    private readonly IKeyboardNoteMap _keys = new KeyboardNoteMap();
    private readonly IMidiNoteInput _midi = new MidiNoteInput();

    /// <summary>A stretch shared out leaves no gap and no overlap, whatever it divides into.</summary>
    /// <remarks>
    /// The one that matters: a kit hands every pad a piece of the keyboard, so a gap is a key
    /// that plays nothing and an overlap is a key that plays two pads. Twelve into eighty eight
    /// does not divide, which is the case a naive division gets wrong at the top.
    /// </remarks>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(12)]
    [InlineData(16)]
    [InlineData(88)]
    public void ASharedOutKeyboardHasNoGapsAndNoOverlaps(int pieces)
    {
        var split = _regions.Split(_regions.PianoLow, _regions.PianoHigh, pieces);

        Assert.Equal(pieces, split.Count);
        Assert.Equal(_regions.PianoLow, split[0].Low);
        Assert.Equal(_regions.PianoHigh, split[^1].High);

        for (int i = 1; i < split.Count; i++)
        {
            Assert.Equal(split[i - 1].High + 1, split[i].Low);
        }
    }

    /// <summary>Asked for nothing, it hands back nothing rather than dividing by nought.</summary>
    [Fact]
    public void NoPiecesIsNoRegions()
    {
        Assert.Empty(_regions.Split(_regions.PianoLow, _regions.PianoHigh, 0));
        Assert.Empty(_regions.Split(_regions.PianoLow, _regions.PianoHigh, -3));
    }

    /// <summary>Ends outside the keyboard are pulled back onto it.</summary>
    [Fact]
    public void EndsOffTheKeyboardAreClamped()
    {
        var split = _regions.Split(-50, 400, 4);

        Assert.Equal(_regions.LowestKey, split[0].Low);
        Assert.Equal(_regions.HighestKey, split[^1].High);
    }

    /// <summary>More pieces than keys still gives every piece a key rather than an empty one.</summary>
    [Fact]
    public void MorePiecesThanKeysStillLeavesEachOneAKey()
    {
        var split = _regions.Split(60, 63, 12);

        Assert.Equal(12, split.Count);
        Assert.All(split, one => Assert.True(one.High >= one.Low));
    }

    /// <summary>The root of a piece sits in the middle of it.</summary>
    [Fact]
    public void TheRootSitsInTheMiddle()
    {
        Assert.Equal(5, _regions.Middle(0, 10));
        Assert.Equal(60, _regions.Middle(60, 60));
    }

    /// <summary>An octave up plays at twice the rate, and an octave down at half.</summary>
    [Fact]
    public void AnOctaveIsAFactorOfTwo()
    {
        Assert.Equal(1.0, _ratio.For(Note.C4, Note.C4), 10);
        Assert.Equal(2.0, _ratio.For(Note.C4.Transpose(12), Note.C4), 10);
        Assert.Equal(0.5, _ratio.For(Note.C4.Transpose(-12), Note.C4), 10);
    }

    /// <summary>A note nobody can play leaves the recording at its own speed.</summary>
    /// <remarks>
    /// Unity rather than nought or a throw: an empty cell means the instrument carries on, so
    /// the honest rate is the one it was recorded at.
    /// </remarks>
    [Fact]
    public void AnUnplayableNoteLeavesTheRateAlone()
    {
        Assert.Equal(1.0, _ratio.For(Note.Empty, Note.C4), 10);
        Assert.Equal(1.0, _ratio.For(Note.Off, Note.C4), 10);
        Assert.Equal(1.0, _ratio.For(Note.C4, Note.Empty), 10);
    }

    /// <summary>The shift stops at six octaves, so no note can ask for a rate nothing can render.</summary>
    [Fact]
    public void TheShiftStopsAtSixOctaves()
    {
        double highest = _ratio.For(new Note(Note.MaxSemitone), new Note(Note.MinSemitone));

        Assert.Equal(Math.Pow(2, _ratio.MaxSemitoneShift / 12.0), highest, 10);
    }

    /// <summary>The rate is what the sample rate is multiplied by.</summary>
    [Fact]
    public void TheFrequencyIsTheRateTimesTheSampleRate()
    {
        Assert.Equal(88200, _ratio.FrequencyFor(Note.C4.Transpose(12), Note.C4, 44100), 6);
    }

    /// <summary>Concert pitch is where it is supposed to be, and C-4 is middle C.</summary>
    [Fact]
    public void ConcertPitchLandsWhereItShould()
    {
        Assert.Equal(_pitch.A4Hz, _pitch.Hz(_pitch.A4Semitone), 10);
        Assert.Equal(261.6255653, _pitch.Hz(Note.C4), 6);
        Assert.Equal(880.0, _pitch.Hz(_pitch.A4Semitone + 12), 10);
    }

    /// <summary>Middle C on a keyboard is C-4 in the pattern.</summary>
    /// <remarks>
    /// The offset exists because MIDI counts from a C two octaves below where a tracker starts.
    /// It is the one number that, wrong by twelve, makes every recording play an octave out and
    /// nothing report an error.
    /// </remarks>
    [Fact]
    public void MiddleCOnTheWireIsMiddleCInThePattern()
    {
        Assert.True(_midi.TryNote(60, out var note));
        Assert.Equal(Note.C4, note);
        Assert.Equal(261.6255653, _pitch.Hz(note), 6);
    }

    /// <summary>A number off the ends of the wire is refused rather than clamped.</summary>
    [Fact]
    public void ANoteOffTheWireIsRefused()
    {
        Assert.False(_midi.TryNote(-1, out _));
        Assert.False(_midi.TryNote(128, out _));
        Assert.False(_midi.TryNote(0, out _));
    }

    /// <summary>
    /// Velocity is written into the volume column unchanged, which is what the column being
    /// 128 wide bought. Every one of the 128 velocities has a number of its own, so no two
    /// hits can be told apart by the hand and not by the pattern.
    /// </summary>
    [Fact]
    public void VelocityFillsTheVolumeColumn()
    {
        Assert.Equal(0, _midi.VolumeFor(0));
        Assert.Equal(0, _midi.VolumeFor(-5));
        Assert.Equal(_midi.MaxVelocity, _midi.VolumeFor(_midi.MaxVelocity));
        Assert.Equal(_midi.MaxVelocity, _midi.VolumeFor(200));

        var seen = new HashSet<int>();

        for (int velocity = 0; velocity <= _midi.MaxVelocity; velocity++)
        {
            Assert.Equal(velocity, _midi.VolumeFor(velocity));
            Assert.InRange(_midi.VolumeFor(velocity), 0, TrackerCell.MaxVolume);
            Assert.True(seen.Add(_midi.VolumeFor(velocity)));
        }
    }

    /// <summary>
    /// And the one level above them, which a key cannot reach and a person can type.
    /// </summary>
    [Fact]
    public void FullIsAboveAnythingAKeyCanPlay()
    {
        Assert.Equal(128, TrackerCell.MaxVolume);
        Assert.True(_midi.VolumeFor(_midi.MaxVelocity) < TrackerCell.MaxVolume);
        Assert.Equal("80", new TrackerCell(new Note(60), 0, TrackerCell.MaxVolume, TrackerCommand.None).VolumeText);
    }

    /// <summary>The two letter rows are exactly one octave apart, which is the whole layout.</summary>
    [Fact]
    public void TheTwoRowsAreAnOctaveApart()
    {
        var lower = _keys.NoteFor("Z", 4);
        var upper = _keys.NoteFor("Q", 4);

        Assert.NotNull(lower);
        Assert.NotNull(upper);
        Assert.Equal(12, upper!.Value.Semitone - lower!.Value.Semitone);
    }

    /// <summary>The letter row agrees with the wire: Z at octave 4 is what MIDI 60 sends.</summary>
    [Fact]
    public void TheLetterRowAgreesWithTheWire()
    {
        _midi.TryNote(60, out var played);

        Assert.Equal(played, _keys.NoteFor("Z", 4));
    }

    /// <summary>The black keys sit where they look on a piano.</summary>
    [Theory]
    [InlineData("Z", 0)]
    [InlineData("S", 1)]
    [InlineData("X", 2)]
    [InlineData("D", 3)]
    [InlineData("C", 4)]
    [InlineData("V", 5)]
    [InlineData("M", 11)]
    public void TheBlackKeysSitWhereTheyLook(string key, int offset)
    {
        Assert.Equal(new Note(48 + offset), _keys.NoteFor(key, 4));
    }

    /// <summary>A key that is not on the keyboard is nothing, not a wrong note.</summary>
    [Fact]
    public void AKeyThatIsNotANoteIsNothing()
    {
        Assert.Null(_keys.NoteFor("F1", 4));
        Assert.Null(_keys.NoteFor("Space", 4));
        Assert.False(_keys.IsNoteKey("F1"));
        Assert.True(_keys.IsNoteKey("Z"));
    }

    /// <summary>An octave that would run off the top of the pattern gives nothing.</summary>
    [Fact]
    public void AnOctaveOffTheTopGivesNothing()
    {
        Assert.Null(_keys.NoteFor("P", 9));
        Assert.Null(_keys.NoteFor("Z", -1));
    }

    /// <summary>Note off is two keys everywhere and three while typing notes.</summary>
    /// <remarks>
    /// The digit is a note off only in the note column, because everywhere else it is a digit
    /// somebody is trying to type.
    /// </remarks>
    [Fact]
    public void NoteOffIsTwoKeysOrThreeDependingOnWhereYouAre()
    {
        Assert.True(_keys.IsNoteOff(_keys.NoteOffKey));
        Assert.True(_keys.IsNoteOff(_keys.NoteOffCapsLock));
        Assert.False(_keys.IsNoteOff(_keys.NoteOffDigit));

        Assert.True(_keys.IsNoteOffInNotes(_keys.NoteOffDigit));
        Assert.True(_keys.IsNoteOffInNotes(_keys.NoteOffKey));
    }
}
