using System;
using System.IO;
using JingleBox2.Audio;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Where a take lives before it has a name, and what it takes to keep one.
/// </summary>
/// <remarks>
/// The rule this is really about is that **an unnamed take is disposable and a named one is
/// not**. Everything here is one half of that: sweeping throws away what nobody kept, keeping
/// moves a take out of reach of the sweep, and nothing on the shelf is ever written over on the
/// way past. The last of those is the one that would cost somebody a recording, so it is the one
/// with the most said about it.
/// </remarks>
public class TakeScratchTests : IDisposable
{
    /// <summary>A folder of this test's own, for the shelf half of a move.</summary>
    private readonly string _shelf =
        Path.Combine(Path.GetTempPath(), "jinglebox2-shelf-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>The scratchpad under the sandboxed application folder.</summary>
    private readonly TakeScratch _scratch = new();

    /// <summary>Writes a file into the scratchpad and answers where it is.</summary>
    private string Scratched(string name, string holding = "audio")
    {
        string path = Path.Combine(_scratch.Folder, name + ".wav");
        File.WriteAllText(path, holding);
        return path;
    }

    /// <summary>The folder is there to be written into, whether or not anybody made it.</summary>
    [Fact]
    public void The_folder_is_there_to_be_written_into()
    {
        Assert.True(Directory.Exists(_scratch.Folder));

        Directory.Delete(_scratch.Folder, recursive: true);

        Assert.True(Directory.Exists(_scratch.Folder));
    }

    /// <summary>Sweeping throws away everything nobody kept.</summary>
    /// <remarks>
    /// Run on the way in as well as on the way out, so a run that ended badly does not leave a
    /// folder filling up with takes nobody asked for.
    /// </remarks>
    [Fact]
    public void Sweeping_throws_away_what_nobody_kept()
    {
        Scratched("take");
        Scratched("take (clean)");

        _scratch.Sweep();

        Assert.Empty(Directory.GetFiles(_scratch.Folder));
        Assert.True(Directory.Exists(_scratch.Folder));
    }

    /// <summary>A take that was kept is out of the sweep's reach.</summary>
    /// <remarks>
    /// The whole point of the thing said as one test: naming a take is what makes it survive.
    /// </remarks>
    [Fact]
    public void A_take_that_was_kept_survives_the_sweep()
    {
        string from = Scratched("take", "the performance");

        string? kept = _scratch.Keep(from, _shelf, "Saxophone");

        _scratch.Sweep();

        Assert.NotNull(kept);
        Assert.True(File.Exists(kept));
        Assert.Equal("the performance", File.ReadAllText(kept!));
        Assert.False(File.Exists(from));
    }

    /// <summary>Keeping is a move, so a long take costs nothing to name.</summary>
    [Fact]
    public void Keeping_moves_rather_than_copies()
    {
        string from = Scratched("take");

        _scratch.Keep(from, _shelf, "Named");

        Assert.False(File.Exists(from));
        Assert.Single(Directory.GetFiles(_shelf));
    }

    /// <summary>
    /// A name already on the shelf is refused, and what was there is untouched.
    /// </summary>
    /// <remarks>
    /// **The one that would cost somebody a recording.** A take saved over last week's under the
    /// same name is not recoverable, and the answer here is to refuse rather than to number,
    /// because the name has already been through the box's own check by the time it arrives:
    /// two rules disagreeing about one name is worse than one rule saying no.
    /// </remarks>
    [Fact]
    public void A_name_already_on_the_shelf_is_refused_and_left_alone()
    {
        Directory.CreateDirectory(_shelf);
        File.WriteAllText(Path.Combine(_shelf, "Saxophone.wav"), "last week");

        string from = Scratched("take", "this week");

        string? kept = _scratch.Keep(from, _shelf, "Saxophone");

        Assert.Null(kept);
        Assert.Equal("last week", File.ReadAllText(Path.Combine(_shelf, "Saxophone.wav")));
        Assert.True(File.Exists(from));
    }

    /// <summary>Dropping one take leaves the other where it is.</summary>
    /// <remarks>
    /// A take with a chain on it is two files, and throwing one away is not throwing both away:
    /// the pair is only ever dropped together by whoever holds both.
    /// </remarks>
    [Fact]
    public void Dropping_one_leaves_the_other()
    {
        string one = Scratched("take");
        string two = Scratched("take (clean)");

        _scratch.Drop(one);

        Assert.False(File.Exists(one));
        Assert.True(File.Exists(two));
    }

    /// <summary>Nothing to keep and nothing to drop are answers rather than throws.</summary>
    /// <remarks>
    /// Every one of these is reachable from the page: a take already moved, a name emptied, a
    /// file somebody deleted underneath. This runs where somebody has just made a recording, so
    /// the one thing it may never do is fail in a way that loses the take.
    /// </remarks>
    [Fact]
    public void Nothing_to_work_on_is_an_answer_rather_than_a_throw()
    {
        Assert.Null(_scratch.Keep(Path.Combine(_scratch.Folder, "gone.wav"), _shelf, "Named"));
        Assert.Null(_scratch.Keep("", _shelf, "Named"));
        Assert.Null(_scratch.Keep(Scratched("take"), _shelf, "   "));
        Assert.Null(_scratch.Keep(Scratched("other"), "", "Named"));

        _scratch.Drop(null);
        _scratch.Drop("");
        _scratch.Drop(Path.Combine(_scratch.Folder, "never-existed.wav"));
    }

    /// <summary>Sweeping a folder that is not there does nothing rather than throwing.</summary>
    [Fact]
    public void Sweeping_nothing_does_nothing()
    {
        Directory.Delete(_scratch.Folder, recursive: true);

        _scratch.Sweep();
    }

    /// <summary>Takes the test's own shelf away, whatever happened.</summary>
    public void Dispose()
    {
        _scratch.Sweep();

        try { if (Directory.Exists(_shelf)) Directory.Delete(_shelf, recursive: true); }
        catch (Exception) { }
    }
}
