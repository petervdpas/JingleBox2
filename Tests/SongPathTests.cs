using System.IO;
using System.Linq;
using JingleBox2.Tracker;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// How a song names the recordings it plays, so it survives the application folder moving or
/// being on another machine.
/// </summary>
public class SongPathTests
{
    private static string InsideTheAppFolder(params string[] parts) =>
        Path.Combine(new[] { Config.AppFolder.Path() }.Concat(parts).ToArray());

    [Fact]
    public void A_path_inside_the_application_folder_is_written_as_a_token()
    {
        string real = InsideTheAppFolder("recordings", "take.wav");

        Assert.Equal("{app}/recordings/take.wav", SongPaths.Pack(real));
    }

    [Fact]
    public void And_read_back_as_wherever_that_folder_is_now()
    {
        string real = InsideTheAppFolder("recordings", "take.wav");

        Assert.Equal(real, SongPaths.Unpack(SongPaths.Pack(real)));
    }

    [Fact]
    public void A_path_somewhere_else_is_left_exactly_as_it_is()
    {
        // Somebody's own file outside the folder is theirs, and rewriting it would point the
        // song at something that is not there.
        string elsewhere = Path.Combine(Path.GetTempPath(), "borrowed.wav");

        Assert.Equal(elsewhere, SongPaths.Pack(elsewhere));
        Assert.Equal(elsewhere, SongPaths.Unpack(elsewhere));
    }

    [Fact]
    public void Nothing_at_all_stays_nothing()
    {
        Assert.Equal("", SongPaths.Pack(""));
        Assert.Equal("", SongPaths.Unpack(""));
        Assert.Equal("", SongPaths.Pack(null!));
    }

    [Fact]
    public void A_folder_whose_name_merely_starts_the_same_is_not_inside_it()
    {
        string inside = Path.Combine(Config.AppFolder.Path() + "-elsewhere", "take.wav");

        Assert.Equal(inside, SongPaths.Pack(inside));
    }

    [Fact]
    public void An_instrument_is_packed_and_unpacked_whole()
    {
        string real = InsideTheAppFolder("recordings", "kick.wav");

        var instrument = new TrackerInstrument { Name = "Kick", FilePath = real };
        instrument.EnsureId();

        SongPaths.PackInto(instrument);
        Assert.StartsWith("{app}/", instrument.FilePath);

        SongPaths.UnpackInto(instrument);
        Assert.Equal(real, instrument.FilePath);
    }
}
