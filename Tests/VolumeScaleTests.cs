using System.Linq;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The volume column's scale, and what a song written on the old one means.
/// </summary>
/// <remarks>
/// The column ran 0 to 64 for as long as this had a pattern, which is half of MIDI's 128
/// velocities: every second velocity landed on the number below it. Widening it to 0x80 makes
/// the two the same size, and makes every song already written wrong by a factor of two unless
/// it is brought across on the way in. That conversion is the only thing standing between a
/// song saved last week and a song that plays at half its level, so it is tested here rather
/// than believed.
/// </remarks>
public class VolumeScaleTests
{
    private readonly IVolumeScale _scale = new VolumeScale();

    /// <summary>Full on the old scale is full on this one, which is the whole of the rule.</summary>
    [Fact]
    public void FullStaysFull()
    {
        Assert.Equal(TrackerCell.MaxVolume, _scale.Widen(VolumeScale.OldMaxVolume));
        Assert.Equal(0, _scale.Widen(0));
    }

    /// <summary>
    /// And everything between, exactly. The old scale is precisely half of this one, so no
    /// reading is rounded and no two old readings collapse onto one.
    /// </summary>
    [Fact]
    public void EveryOldReadingHasItsOwnPlace()
    {
        var seen = new System.Collections.Generic.HashSet<int>();

        for (int volume = 0; volume <= VolumeScale.OldMaxVolume; volume++)
        {
            int widened = _scale.Widen(volume);

            Assert.Equal(volume * 2, widened);
            Assert.InRange(widened, 0, TrackerCell.MaxVolume);
            Assert.True(seen.Add(widened));
        }
    }

    /// <summary>A blank column stays blank rather than becoming a loud one.</summary>
    [Fact]
    public void ABlankColumnStaysBlank()
    {
        Assert.Equal(TrackerCell.NoVolume, _scale.Widen(TrackerCell.NoVolume));
        Assert.Equal(TrackerCell.NoVolume, _scale.Widen(TrackerCell.Empty).Volume);
    }

    /// <summary>
    /// A reading past the old full is held at this one's full. It already played at full, since
    /// the old reading clamped its gain to one, so doubling it past the top would be inventing
    /// a level the song never had.
    /// </summary>
    [Fact]
    public void PastTheOldFullIsStillFull()
    {
        Assert.Equal(TrackerCell.MaxVolume, _scale.Widen(VolumeScale.OldMaxVolume + 20));
        Assert.Equal(TrackerCell.MaxVolume, _scale.Widen(255));
    }

    /// <summary>
    /// The V command goes with the column. The two set the same thing and the effect wins where
    /// both are written, so leaving one on each scale would mean 40 being full in one column of
    /// a cell and half in the next.
    /// </summary>
    [Fact]
    public void TheVolumeEffectIsOnTheSameScale()
    {
        var cell = new TrackerCell(new Note(60), 0, 32, new TrackerCommand(TrackerCommand.SetVolume, 32));
        var wide = _scale.Widen(cell);

        Assert.Equal(64, wide.Volume);
        Assert.Equal(64, wide.Effect.Parameter);
    }

    /// <summary>And no other command is touched, since none of them is a level.</summary>
    [Fact]
    public void NoOtherEffectIsTouched()
    {
        var cell = new TrackerCell(new Note(60), 0, 32, new TrackerCommand(TrackerCommand.SetPan, 64));
        var wide = _scale.Widen(cell);

        Assert.Equal(64, wide.Volume);
        Assert.Equal(64, wide.Effect.Parameter);
        Assert.Equal(TrackerCommand.SetPan, wide.Effect.Command);
    }

    /// <summary>
    /// A song written on the old scale reads back at the level it was written at, through the
    /// real reader and writer rather than through a second copier written for the test.
    /// </summary>
    [Fact]
    public void AnOldSongComesBackAtItsOwnLevel()
    {
        var song = Song.CreateDefault();
        song.Patterns[0][0, 0] = new TrackerCell(new Note(60), 0, 32, TrackerCommand.None);

        string said = SongStore.Copy(song);

        Assert.Contains("\"Version\": 4", said);

        var back = SongStore.Uncopy(Aged(said));

        Assert.NotNull(back);
        Assert.Equal(64, back!.Patterns[0][0, 0].Volume);
    }

    /// <summary>
    /// And a song written by this build is not doubled a second time, which is the way a
    /// conversion like this usually goes wrong: quietly, and only on the second open.
    /// </summary>
    [Fact]
    public void ASongOfThisBuildIsLeftAlone()
    {
        var song = Song.CreateDefault();
        song.Patterns[0][0, 0] = new TrackerCell(new Note(60), 0, 100, TrackerCommand.None);

        var once = SongStore.Uncopy(SongStore.Copy(song));
        var twice = SongStore.Uncopy(SongStore.Copy(once!));

        Assert.Equal(100, once!.Patterns[0][0, 0].Volume);
        Assert.Equal(100, twice!.Patterns[0][0, 0].Volume);
    }

    /// <summary>
    /// A song with no version in it at all is older than the field and is converted, which is
    /// what the document's default of 1 buys.
    /// </summary>
    [Fact]
    public void ASongWithNoVersionIsOld()
    {
        var song = Song.CreateDefault();
        song.Patterns[0][0, 0] = new TrackerCell(new Note(60), 0, 32, TrackerCommand.None);

        string said = string.Join('\n',
            SongStore.Copy(song).Split('\n').Where(line => !line.Contains("ersion")));

        var back = SongStore.Uncopy(said);

        Assert.NotNull(back);
        Assert.Equal(64, back!.Patterns[0][0, 0].Volume);
    }

    /// <summary>The document as a build before the widening wrote it.</summary>
    private static string Aged(string said) => said.Replace("\"Version\": 4", "\"Version\": 2");
}
