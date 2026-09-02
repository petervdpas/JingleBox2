using System;
using System.IO;
using System.Text;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Writing a file whole, which is the one thing between a crash and somebody's afternoon.
/// </summary>
/// <remarks>
/// The half that can be tested here is the half that matters most: that the old file survives a
/// write that never happened, that the temporary file does not stay behind, and that nothing is
/// thrown at a caller who is usually in the middle of saving somebody's work.
/// </remarks>
public class SafeFileTests : IDisposable
{
    private readonly ISafeFile _files = new SafeFile();
    private readonly string _home;

    /// <summary>A folder of its own per test, since these all put files on a disc.</summary>
    public SafeFileTests()
    {
        _home = Path.Combine(Path.GetTempPath(), "jb-safefile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_home);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (Exception) { }
        GC.SuppressFinalize(this);
    }

    private string At(string name) => Path.Combine(_home, name);

    /// <summary>Text goes in, and comes back out as it went.</summary>
    [Fact]
    public void Text_is_written_whole()
    {
        string path = At("a.json");

        _files.Write(path, "{ \"a\": 1 }");

        Assert.Equal("{ \"a\": 1 }", File.ReadAllText(path));
    }

    /// <summary>The temporary file it writes through does not stay behind.</summary>
    [Fact]
    public void The_half_written_file_is_gone_afterwards()
    {
        string path = At("a.json");

        _files.Write(path, "one");
        _files.Write(path, "two");

        Assert.Equal(new[] { path }, Directory.GetFiles(_home));
    }

    /// <summary>A second write replaces the first outright rather than adding to it.</summary>
    [Fact]
    public void A_write_replaces_what_was_there()
    {
        string path = At("a.json");

        _files.Write(path, "a much longer first version");
        _files.Write(path, "short");

        Assert.Equal("short", File.ReadAllText(path));
    }

    /// <summary>The folder around it is made, however deep it is.</summary>
    [Fact]
    public void The_folder_is_made_on_the_way()
    {
        string path = Path.Combine(_home, "one", "two", "three", "a.json");

        _files.Write(path, "here");

        Assert.Equal("here", File.ReadAllText(path));
    }

    /// <summary>No byte order mark, since these are files people paste out of.</summary>
    [Fact]
    public void Nothing_is_written_in_front_of_the_text()
    {
        string path = At("a.json");

        _files.Write(path, "a");

        byte[] raw = File.ReadAllBytes(path);

        Assert.Equal(new byte[] { (byte)'a' }, raw);
    }

    /// <summary>Text that is not ASCII survives the round trip.</summary>
    [Fact]
    public void Text_beyond_ascii_survives()
    {
        string path = At("a.json");
        const string said = "kräftig — 日本語 🎹";

        _files.Write(path, said);

        Assert.Equal(said, File.ReadAllText(path, new UTF8Encoding(false)));
    }

    /// <summary>A path that says nothing writes nothing, and does not throw.</summary>
    /// <remarks>
    /// The caller is a save, and a save that throws on the way out of an application that is
    /// already closing helps nobody. Nothing written is the right answer to nowhere to write it.
    /// </remarks>
    [Fact]
    public void A_path_that_says_nothing_writes_nothing()
    {
        _files.Write("", "a");
        _files.Write("   ", "a");
        _files.Write(null!, "a");

        Assert.Empty(Directory.GetFiles(_home));
    }

    /// <summary>A stream write puts the whole stream down.</summary>
    [Fact]
    public void A_stream_is_written_whole()
    {
        string path = At("a.zip");

        _files.Write(path, stream => stream.Write(new byte[] { 1, 2, 3, 4 }));

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(path));
    }

    /// <summary>A stream write with nothing to write leaves an empty file, not no file.</summary>
    /// <remarks>
    /// An empty file is a real answer here: a song with nothing in it is still a song, and the
    /// caller asked for the write.
    /// </remarks>
    [Fact]
    public void A_stream_that_writes_nothing_leaves_an_empty_file()
    {
        string path = At("a.zip");

        _files.Write(path, _ => { });

        Assert.True(File.Exists(path));
        Assert.Empty(File.ReadAllBytes(path));
    }

    /// <summary>No writer at all writes nothing, and does not throw.</summary>
    [Fact]
    public void A_stream_write_with_no_writer_does_nothing()
    {
        _files.Write(At("a.zip"), (Action<Stream>)null!);

        Assert.Empty(Directory.GetFiles(_home));
    }

    /// <summary>
    /// Two threads writing one file at once leave one whole file and nothing else.
    /// </summary>
    /// <remarks>
    /// The settings are written from the drawing thread whenever anything on a page moves, and
    /// from the MIDI thread when a knob is learned or a control's own behaviour is worked out.
    /// Those are two threads at one path, and the half-written file was named after the path
    /// alone, so both were writing through the same temporary file: the second one to arrive
    /// could not create it, deleted it on its way out, and the first then had nothing left to
    /// move into place. What that looks like from outside is a settings file that occasionally
    /// loses whatever was last put in it, with nothing anywhere saying so.
    ///
    /// Which of the two contents wins is not the question and cannot be: they are two saves of
    /// the same object a moment apart. What has to be true is that the file holds one of them
    /// whole rather than a mixture or a crater.
    /// </remarks>
    [Fact]
    public void Two_writers_at_one_path_leave_one_whole_file()
    {
        string path = At("config.json");

        string one = new string('a', 20000);
        string two = new string('b', 20000);

        var faults = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        void Write(string what)
        {
            for (int again = 0; again < 40; again++)
            {
                try { _files.Write(path, what); }
                catch (Exception thrown) { faults.Add(thrown); }
            }
        }

        var first = new System.Threading.Thread(() => Write(one));
        var second = new System.Threading.Thread(() => Write(two));

        first.Start();
        second.Start();
        first.Join();
        second.Join();

        Assert.Empty(faults);

        Assert.Equal(new[] { path }, Directory.GetFiles(_home));

        string landed = File.ReadAllText(path);

        Assert.True(landed == one || landed == two, "the file holds neither write whole");
    }

    /// <summary>
    /// A writer that throws part way leaves the old file exactly as it was.
    /// </summary>
    /// <remarks>
    /// This is the whole reason the class exists, so it is the test that matters. The fallback
    /// path runs after the first attempt fails and throws again, since the writer is what is
    /// broken; what must not happen is that the old file has been emptied on the way.
    /// </remarks>
    [Fact]
    public void A_writer_that_throws_leaves_the_old_file_alone()
    {
        string path = At("song.jibx");
        File.WriteAllText(path, "the old song");

        Assert.ThrowsAny<Exception>(() => _files.Write(path, _ => throw new InvalidOperationException("no")));

        Assert.Equal("the old song", File.ReadAllText(path));
    }
}
