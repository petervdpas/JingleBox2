using System;
using System.IO;
using JingleBox2.Tracker;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A song file from somewhere else being made one of yours.
/// </summary>
/// <remarks>
/// **Pack could write a song out and nothing could read one back**, so a song could leave an
/// installation and could not arrive at one, and a packed song carried to another machine was a
/// file nothing would open. This is the other half, and what it has to get right is not the
/// copying but the two ways it could cost somebody work: overwriting a song of theirs that
/// happened to share a name, and letting something that is not a song into the list.
/// </remarks>
public class SongImportTests
{
    private static SongStore Store() => new();

    private static string Written(Song song, string name)
    {
        string path = Path.Combine(Path.GetTempPath(), name + "-" + Guid.NewGuid().ToString("N") + ".jibx");

        Store().Save(song, path);

        return path;
    }

    /// <summary>A song from anywhere lands in the songs folder and reads back.</summary>
    [Fact]
    public void A_song_from_elsewhere_becomes_one_of_yours()
    {
        var store = Store();
        var song = new Song { Name = "Arrived", Bpm = 133 };

        string outside = Written(song, "Arrived");

        try
        {
            string? landed = store.Import(outside);

            Assert.NotNull(landed);
            Assert.Equal(store.SongsDirectory, Path.GetDirectoryName(landed));

            var back = store.Load(landed!);

            Assert.NotNull(back);
            Assert.Equal(133, back!.Bpm);
        }
        finally
        {
            File.Delete(outside);
        }
    }

    /// <summary>
    /// A name that is taken gets a number, and the song already there is untouched.
    /// </summary>
    /// <remarks>
    /// The case this exists for: a song arriving from another machine under a name you already
    /// use is the ordinary situation rather than the strange one, and losing the one you had to
    /// it would be unforgivable. Checked by reading the old one back afterwards rather than by
    /// looking at the file names, since what matters is that the work is still there.
    /// </remarks>
    [Fact]
    public void A_name_that_is_taken_does_not_cost_the_song_that_has_it()
    {
        var store = Store();

        string mine = store.PathFor("Twice");
        store.Save(new Song { Name = "Twice", Bpm = 90 }, mine);

        string outside = Written(new Song { Name = "Twice", Bpm = 175 }, "Twice");

        try
        {
            string? landed = store.Import(outside);

            Assert.NotNull(landed);
            Assert.NotEqual(mine, landed);

            Assert.Equal(90, store.Load(mine)!.Bpm);
            Assert.Equal(175, store.Load(landed!)!.Bpm);
        }
        finally
        {
            File.Delete(outside);
            File.Delete(mine);
            if (File.Exists(store.PathFor("Twice (2)"))) File.Delete(store.PathFor("Twice (2)"));
        }
    }

    /// <summary>
    /// Something that is not a song is refused rather than copied in.
    /// </summary>
    /// <remarks>
    /// Read before it is copied, so a file that will not open never reaches the folder. The
    /// alternative is a row in the list that cannot be opened and that somebody then has to
    /// delete, which is a worse answer than being told at the moment of asking.
    /// </remarks>
    [Fact]
    public void Something_that_is_not_a_song_is_refused()
    {
        var store = Store();

        string rubbish = Path.Combine(Path.GetTempPath(), "not-a-song-" + Guid.NewGuid().ToString("N") + ".jibx");

        File.WriteAllText(rubbish, "this is not a zip and never was");

        try
        {
            Assert.Null(store.Import(rubbish));
            Assert.False(File.Exists(store.PathFor(Path.GetFileNameWithoutExtension(rubbish))));
        }
        finally
        {
            File.Delete(rubbish);
        }
    }

    /// <summary>A file that is not there at all is refused without throwing.</summary>
    [Fact]
    public void A_file_that_is_not_there_is_refused()
    {
        var store = Store();

        Assert.Null(store.Import(Path.Combine(Path.GetTempPath(), "nothing-here.jibx")));
        Assert.Null(store.Import(""));
    }
}
