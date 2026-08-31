using System;
using System.IO;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The log as a file: adding to it, rolling it over, and throwing it away on purpose.
/// </summary>
/// <remarks>
/// It is kept between runs rather than cleared on start, because the run you most often want is
/// the one that already ended badly and a log cleared on start has thrown away the crash you
/// restarted because of. That makes clearing it a thing somebody has to be able to ask for, and
/// makes rolling it over the thing that stops it growing for ever.
///
/// Everything here fails in silence, which is the one property worth testing hardest: a log is
/// not worth an exception in the thing it is a log of, and the run where the disc is full is
/// exactly the run somebody is trying to get to the bottom of.
/// </remarks>
public class LogFileTests : IDisposable
{
    private readonly ILogFile _file = new LogFile();

    private readonly string _room =
        Path.Combine(Path.GetTempPath(), "jinglebox2-log-" + Guid.NewGuid().ToString("N"));

    /// <summary>The log this test writes to, inside a room of its own.</summary>
    private string Log => Path.Combine(_room, "jinglebox.log");

    /// <summary>And the one before it, which is the same name with .old on the end.</summary>
    private string Old => Log + ".old";

    /// <summary>Adding makes the folder it needs rather than refusing to write into nothing.</summary>
    [Fact]
    public void Adding_makes_the_folder_it_needs()
    {
        Assert.True(_file.Append(Log, "a line" + Environment.NewLine));
        Assert.True(File.Exists(Log));
    }

    /// <summary>And it adds rather than replacing, which is what keeps the last run readable.</summary>
    [Fact]
    public void Adding_keeps_what_was_there()
    {
        _file.Append(Log, "one" + Environment.NewLine);
        _file.Append(Log, "two" + Environment.NewLine);

        Assert.Contains("one", File.ReadAllText(Log));
        Assert.Contains("two", File.ReadAllText(Log));
    }

    /// <summary>A file under the limit is left alone.</summary>
    [Fact]
    public void A_small_file_is_not_rolled()
    {
        _file.Append(Log, "small" + Environment.NewLine);

        Assert.False(_file.Roll(Log, 4096));
        Assert.False(File.Exists(Old));
    }

    /// <summary>Past it, the old one is kept under .old and a new one starts.</summary>
    [Fact]
    public void A_big_file_is_rolled_over()
    {
        _file.Append(Log, new string('x', 200));

        Assert.True(_file.Roll(Log, 100));

        Assert.True(File.Exists(Old));
        Assert.False(File.Exists(Log));
    }

    /// <summary>And only ever one old one: the second roll writes over the first.</summary>
    [Fact]
    public void There_is_only_ever_one_old_file()
    {
        _file.Append(Log, new string('a', 200));
        _file.Roll(Log, 100);

        _file.Append(Log, new string('b', 200));
        _file.Roll(Log, 100);

        Assert.Contains("b", File.ReadAllText(Old));
        Assert.DoesNotContain("a", File.ReadAllText(Old));
    }

    /// <summary>
    /// Clearing takes both, since leaving the old one would clear a log and leave four megabytes
    /// of the same thing beside it.
    /// </summary>
    [Fact]
    public void Clearing_takes_the_old_one_too()
    {
        _file.Append(Log, new string('a', 200));
        _file.Roll(Log, 100);
        _file.Append(Log, "after" + Environment.NewLine);

        Assert.True(File.Exists(Log));
        Assert.True(File.Exists(Old));

        Assert.True(_file.Clear(Log));

        Assert.False(File.Exists(Log));
        Assert.False(File.Exists(Old));
    }

    /// <summary>Clearing nothing says so rather than pretending it did something.</summary>
    [Fact]
    public void Clearing_nothing_says_so()
    {
        Assert.False(_file.Clear(Log));
    }

    /// <summary>
    /// A path that cannot be written is false rather than an exception, everywhere. The run
    /// where the disc will not take it is the run somebody is trying to get to the bottom of.
    /// </summary>
    [Fact]
    public void A_path_that_will_not_work_is_survived()
    {
        string nowhere = Path.Combine(Log, "under", "a", "file.log");

        Assert.False(_file.Append("", "x"));
        Assert.False(_file.Roll("", 10));
        Assert.False(_file.Clear(""));

        _file.Append(Log, "a file, not a folder" + Environment.NewLine);

        Assert.False(_file.Append(nowhere, "x"));
    }

    /// <summary>Takes the room down, whatever the tests left in it.</summary>
    public void Dispose()
    {
        try { if (Directory.Exists(_room)) Directory.Delete(_room, recursive: true); }
        catch (IOException) { }
    }
}
