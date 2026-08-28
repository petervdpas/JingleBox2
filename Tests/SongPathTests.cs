using System.IO;
using System.Linq;
using JingleBox2.Tracker;
using Xunit;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tests;

/// <summary>
/// How a song names the recordings it plays, so it survives the application folder moving or
/// being on another machine.
/// </summary>
public class SongPathTests
{
    /// <summary>Recordings written so a song survives its folder moving.</summary>
    /// <remarks>One per test class, so nothing one test does reaches another.</remarks>
    private static readonly ISongPaths Portable = new SongPaths();

    /// <summary>
    /// A path under the application folder, wherever that folder is on this machine.
    /// </summary>
    private static string InsideTheAppFolder(params string[] parts) =>
        Path.Combine(new[] { new Files.AppFolder().Path() }.Concat(parts).ToArray());

    /// <summary>
    /// A recording the application owns is written down as the token and a relative path, never
    /// as wherever the folder sat on the machine that saved the song.
    /// </summary>
    [Fact]
    public void A_path_inside_the_application_folder_is_written_as_a_token()
    {
        string real = InsideTheAppFolder("recordings", "take.wav");

        Assert.Equal("{app}/recordings/take.wav", Portable.Pack(real));
    }

    /// <summary>
    /// The round trip is where the portability lands: the token resolves against the folder this
    /// machine has, so a song opened after the folder moved still finds its take.
    /// </summary>
    [Fact]
    public void And_read_back_as_wherever_that_folder_is_now()
    {
        string real = InsideTheAppFolder("recordings", "take.wav");

        Assert.Equal(real, Portable.Unpack(Portable.Pack(real)));
    }

    /// <summary>
    /// A file outside the application folder goes through both directions untouched.
    /// </summary>
    /// <remarks>
    /// Somebody's own file outside the folder is theirs, and rewriting it would point the song at
    /// something that is not there.
    /// </remarks>
    [Fact]
    public void A_path_somewhere_else_is_left_exactly_as_it_is()
    {
        string elsewhere = Path.Combine(Path.GetTempPath(), "borrowed.wav");

        Assert.Equal(elsewhere, Portable.Pack(elsewhere));
        Assert.Equal(elsewhere, Portable.Unpack(elsewhere));
    }

    /// <summary>
    /// An empty path and a null one both survive, since an instrument that generates its sound
    /// names no file and must not come back holding a token.
    /// </summary>
    [Fact]
    public void Nothing_at_all_stays_nothing()
    {
        Assert.Equal("", Portable.Pack(""));
        Assert.Equal("", Portable.Unpack(""));
        Assert.Equal("", Portable.Pack(null!));
    }

    /// <summary>
    /// Being inside the folder is not the same as beginning with its name, and comparing the text
    /// alone would swallow a sibling folder sitting next to it.
    /// </summary>
    [Fact]
    public void A_folder_whose_name_merely_starts_the_same_is_not_inside_it()
    {
        string inside = Path.Combine(new Files.AppFolder().Path() + "-elsewhere", "take.wav");

        Assert.Equal(inside, Portable.Pack(inside));
    }

    /// <summary>
    /// The whole instrument goes through rather than the path on its own, because that is what
    /// the song file writes and reads back.
    /// </summary>
    [Fact]
    public void An_instrument_is_packed_and_unpacked_whole()
    {
        string real = InsideTheAppFolder("recordings", "kick.wav");

        var instrument = new TrackerInstrument { Name = "Kick", FilePath = real };
        instrument.EnsureId();

        Portable.PackInto(instrument);
        Assert.StartsWith("{app}/", instrument.FilePath);

        Portable.UnpackInto(instrument);
        Assert.Equal(real, instrument.FilePath);
    }
}
