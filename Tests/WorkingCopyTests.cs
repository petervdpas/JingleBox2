using System;
using System.IO;
using JingleBox2.Audio;
using JingleBox2.Audio.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The copy the editor works on, and the promise that the take is not touched until Save.
/// </summary>
/// <remarks>
/// **The promise is the whole of what this is for.** An editor that writes as it goes has no
/// honest Save and no way back from a mistake but another edit, and the take on the shelf is
/// somebody's only copy of a performance. So what is pinned here is not that the copy works: it
/// is that the take on the shelf is byte for byte what it was after an edit, after a replay, and
/// after the window is closed without saving.
/// </remarks>
public sealed class WorkingCopyTests : IDisposable
{
    /// <summary>A folder standing in for the application's own.</summary>
    private sealed class Home : IAppFolder
    {
        /// <summary>Where it points.</summary>
        private readonly string _where;

        /// <summary>Names the folder.</summary>
        /// <param name="where">The folder.</param>
        public Home(string where) => _where = where;

        /// <inheritdoc/>
        public string Name => "JingleBox2";

        /// <inheritdoc/>
        public string Path() => _where;

        /// <inheritdoc/>
        public string Path(string under) => System.IO.Path.Combine(_where, under);
    }

    /// <summary>Where everything in this class lives.</summary>
    private readonly string _home;

    /// <summary>The shelf's own take.</summary>
    private readonly string _take;

    /// <summary>What is under test.</summary>
    private readonly IWorkingCopy _copy;

    /// <summary>Reading and writing takes, for the checks that read one.</summary>
    private readonly IWavFile _wav = new WavFile();

    /// <summary>The steps, so a real edit can be done to the copy.</summary>
    private readonly ITakeSteps _steps = new TakeSteps();

    /// <inheritdoc cref="WorkingCopyTests"/>
    public WorkingCopyTests()
    {
        _home = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jb-copy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_home);

        _take = System.IO.Path.Combine(_home, "take.wav");

        var samples = new short[100];

        for (int frame = 0; frame < samples.Length; frame++) samples[frame] = (short)(frame + 1);

        _wav.Write(_take, samples, 44100, 1);

        _copy = new WorkingCopy(new Home(_home), new SafeFile());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>The first sample of a take, which says which version of it this is.</summary>
    /// <param name="path">The take to read.</param>
    /// <returns>Its first sample.</returns>
    private short First(string path)
    {
        var (samples, _) = _wav.Read(path);

        return samples[0];
    }

    /// <summary>How many frames a take holds.</summary>
    /// <param name="path">The take to read.</param>
    /// <returns>Its length in frames.</returns>
    private long Frames(string path) => _wav.ReadInfo(path).FrameCount;

    /// <summary>Opening answers a copy that is somewhere else and holds the same audio.</summary>
    [Fact]
    public void Opening_takes_a_copy()
    {
        string? path = _copy.Open(_take);

        Assert.NotNull(path);
        Assert.NotEqual(_take, path);
        Assert.True(_copy.IsOpen);
        Assert.Equal(First(_take), First(path!));
    }

    /// <summary>A take that is not there is not opened, and nothing is left behind.</summary>
    [Fact]
    public void A_take_that_is_not_there_opens_nothing()
    {
        Assert.Null(_copy.Open(System.IO.Path.Combine(_home, "nothing.wav")));
        Assert.False(_copy.IsOpen);
    }

    /// <summary>
    /// **An edit to the copy leaves the take on the shelf exactly as it was.**
    /// </summary>
    [Fact]
    public void Editing_the_copy_does_not_touch_the_take()
    {
        string path = _copy.Open(_take)!;

        _steps.Run(new TakeStep(TakeEditKind.Reverse, 0, 100, -1), path);

        Assert.Equal(100, First(path));
        Assert.Equal(1, First(_take));
    }

    /// <summary>Taking it fresh throws the edit away and takes the shelf's copy again.</summary>
    [Fact]
    public void A_fresh_copy_is_the_take_again()
    {
        string path = _copy.Open(_take)!;

        _steps.Run(new TakeStep(TakeEditKind.Reverse, 0, 100, -1), path);
        _copy.Fresh();

        Assert.Equal(1, First(path));
    }

    /// <summary>Keeping puts the copy over the take, length and all.</summary>
    [Fact]
    public void Keeping_puts_the_copy_over_the_take()
    {
        string path = _copy.Open(_take)!;

        _steps.Run(new TakeStep(TakeEditKind.Trim, 0, 40, -1), path);

        Assert.True(_copy.Keep());

        Assert.Equal(40, Frames(_take));
        Assert.Equal(40, Frames(path));
    }

    /// <summary>And the copy is still there afterwards, since the window is still open on it.</summary>
    [Fact]
    public void The_copy_survives_being_kept()
    {
        _copy.Open(_take);
        _copy.Keep();

        Assert.True(_copy.IsOpen);
    }

    /// <summary>Closing throws the copy away and leaves the take.</summary>
    [Fact]
    public void Closing_throws_the_copy_away()
    {
        string path = _copy.Open(_take)!;

        _copy.Close();

        Assert.False(File.Exists(path));
        Assert.False(_copy.IsOpen);
        Assert.True(File.Exists(_take));
        Assert.Equal(100, Frames(_take));
    }

    /// <summary>Keeping with nothing open does nothing rather than throwing.</summary>
    [Fact]
    public void Keeping_nothing_answers_no()
    {
        Assert.False(_copy.Keep());
        Assert.False(_copy.Fresh());
    }

    /// <summary>Opening another take lets the first copy go rather than piling them up.</summary>
    [Fact]
    public void One_copy_at_a_time()
    {
        string first = _copy.Open(_take)!;

        string other = System.IO.Path.Combine(_home, "other.wav");
        _wav.Write(other, new short[10], 44100, 1);

        _copy.Open(other);

        Assert.Single(Directory.GetFiles(System.IO.Path.Combine(_home, WorkingCopy.FolderName)));
        Assert.Equal(10, Frames(_copy.Path!));
        Assert.True(first == _copy.Path || !File.Exists(first));
    }
}
