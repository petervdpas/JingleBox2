using System;
using System.IO;
using JingleBox2.Tracker;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A song saying which kind of machine wrote it, and what that is for.
/// </summary>
/// <remarks>
/// **A path is the one thing a song writes down that does not travel.** Until this there was no
/// way to know whether the paths in a song were worth looking at, so they were always looked at:
/// harmless where the answer is simply no, and wrong on the day a settings file is carried
/// between two computers, since the list of what was scanned would then hold the other machine's
/// paths and a match against one hands back a plugin that is not on this disc.
/// </remarks>
public class MachineWordTests
{
    private readonly MachineWord _word = new();

    /// <summary>This machine says one of the three words, and says the right one.</summary>
    [Fact]
    public void This_machine_has_a_word_for_itself()
    {
        string here = _word.Here;

        Assert.Contains(here, new[] { MachineWord.Windows, MachineWord.Mac, MachineWord.Linux });

        Assert.Equal(
            OperatingSystem.IsWindows() ? MachineWord.Windows
            : OperatingSystem.IsMacOS() ? MachineWord.Mac
            : MachineWord.Linux,
            here);
    }

    /// <summary>
    /// A song that does not say is read as having been made here.
    /// </summary>
    /// <remarks>
    /// Which is what every song already on anybody's disc means, and it has to behave exactly as
    /// before: those songs have been opened with their paths looked at all along, and a change
    /// that stopped looking would lose recordings that are found today.
    /// </remarks>
    [Fact]
    public void A_song_that_does_not_say_has_not_travelled()
    {
        Assert.False(_word.Travelled(null));
        Assert.False(_word.Travelled(""));
        Assert.False(_word.Travelled("   "));
        Assert.False(_word.Travelled(_word.Here));
    }

    /// <summary>A song from another kind of machine says so.</summary>
    [Fact]
    public void A_song_from_elsewhere_has_travelled()
    {
        string elsewhere = _word.Here == MachineWord.Windows ? MachineWord.Linux : MachineWord.Windows;

        Assert.True(_word.Travelled(elsewhere));
        Assert.False(_word.Travelled(elsewhere.ToUpperInvariant() == elsewhere ? elsewhere : _word.Here));
    }

    /// <summary>Every song saved here says so, and reads it back.</summary>
    /// <remarks>
    /// Written on every save rather than kept from where the song began, since what anybody wants
    /// to know is whether the paths in the file in front of them mean anything on this computer,
    /// and those were written by whoever saved it last.
    /// </remarks>
    [Fact]
    public void A_song_saved_here_says_where_it_was_made()
    {
        var store = new SongStore();
        string path = Path.Combine(Path.GetTempPath(), "jb-made-" + Guid.NewGuid().ToString("N") + ".jibx");

        try
        {
            store.Save(new Song { Name = "Made" }, path);

            var back = store.Load(path);

            Assert.NotNull(back);
            Assert.Equal(_word.Here, back!.WrittenOn);
            Assert.False(_word.Travelled(back.WrittenOn));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
