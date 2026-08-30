using System;
using System.IO;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Emptying part of a take and leaving the rest of it alone.
/// </summary>
/// <remarks>
/// The thing worth pinning is what does **not** change. A trim moves everything after the cut,
/// so anything holding a position in the file is wrong afterwards; a silence must not, or a pad,
/// an instrument or a slice pointing into this take would quietly point at something else.
/// </remarks>
public class SilenceTests : IDisposable
{
    private readonly IWavFile _wav = new WavFile();
    private readonly IWaveformService _waveforms = new WaveformService();
    private readonly string _home;

    /// <summary>A folder of its own, since these all put a file on a disc.</summary>
    public SilenceTests()
    {
        _home = Path.Combine(Path.GetTempPath(), "jb-silence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_home);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>A take of a hundred frames, every one of them the same loud sample.</summary>
    private string Take(int frames = 100, int channels = 1)
    {
        string path = Path.Combine(_home, "take.wav");
        var samples = new short[frames * channels];

        for (int at = 0; at < samples.Length; at++) samples[at] = 8000;

        _wav.Write(path, samples, 44100, channels);

        return path;
    }

    /// <summary>What is inside the region is nought afterwards.</summary>
    [Fact]
    public void The_region_is_emptied()
    {
        string path = Take();

        _waveforms.SilenceFile(path, 20, 40);

        var (samples, _) = _wav.Read(path);

        for (int frame = 20; frame < 40; frame++) Assert.Equal(0, samples[frame]);
    }

    /// <summary>And what is outside it is untouched.</summary>
    [Fact]
    public void Everything_else_is_left_alone()
    {
        string path = Take();

        _waveforms.SilenceFile(path, 20, 40);

        var (samples, _) = _wav.Read(path);

        Assert.Equal(8000, samples[19]);
        Assert.Equal(8000, samples[40]);
        Assert.Equal(8000, samples[99]);
    }

    /// <summary>
    /// The take is the same length, which is the whole reason this is not a cut.
    /// </summary>
    [Fact]
    public void The_length_does_not_change()
    {
        string path = Take();

        _waveforms.SilenceFile(path, 20, 40);

        var (samples, info) = _wav.Read(path);

        Assert.Equal(100, info.FrameCount);
        Assert.Equal(100, samples.Length);
    }

    /// <summary>Both sides of a stereo frame go quiet, not one.</summary>
    [Fact]
    public void Stereo_is_silenced_on_both_sides()
    {
        string path = Take(frames: 50, channels: 2);

        _waveforms.SilenceFile(path, 10, 20);

        var (samples, _) = _wav.Read(path);

        for (int at = 10 * 2; at < 20 * 2; at++) Assert.Equal(0, samples[at]);

        Assert.Equal(8000, samples[9 * 2]);
        Assert.Equal(8000, samples[9 * 2 + 1]);
        Assert.Equal(8000, samples[20 * 2]);
    }

    /// <summary>A region with nothing in it is refused rather than rewriting the file for nothing.</summary>
    [Fact]
    public void An_empty_region_is_refused()
    {
        string path = Take();

        Assert.Throws<InvalidOperationException>(() => _waveforms.SilenceFile(path, 30, 30));
    }

    /// <summary>A region past the end is held to the end rather than throwing.</summary>
    [Fact]
    public void A_region_past_the_end_is_clamped()
    {
        string path = Take();

        _waveforms.SilenceFile(path, 90, 5000);

        var (samples, info) = _wav.Read(path);

        Assert.Equal(100, info.FrameCount);
        Assert.Equal(0, samples[99]);
        Assert.Equal(8000, samples[89]);
    }
}
