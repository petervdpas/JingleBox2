using System;
using System.IO;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Reading the WAV files people have, and writing the 16-bit ones this app keeps.
/// </summary>
/// <remarks>
/// Reading is generous and writing is not, so the tests are lopsided the same way: what matters
/// is that every width an editor writes comes back as the same sound, and that a file this app
/// cannot read says so out loud rather than becoming silence.
/// </remarks>
public class WavFileTests : IDisposable
{
    private readonly IWavFile _wav = new WavFile();
    private readonly string _home;

    /// <summary>A folder of its own, since every one of these puts a file on a disc.</summary>
    public WavFileTests()
    {
        _home = Path.Combine(Path.GetTempPath(), "jb-wav-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_home);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (Exception) { }
        GC.SuppressFinalize(this);
    }

    private string At(string name) => Path.Combine(_home, name);

    /// <summary>What goes out comes back, sample for sample.</summary>
    [Fact]
    public void What_is_written_is_what_is_read()
    {
        string path = At("a.wav");
        var samples = new short[] { 0, 1000, -1000, short.MaxValue, short.MinValue };

        _wav.Write(path, samples, 44100, 1);

        var (back, info) = _wav.Read(path);

        Assert.Equal(samples, back);
        Assert.Equal(44100, info.SampleRate);
        Assert.Equal(1, info.Channels);
        Assert.Equal(samples.Length, info.FrameCount);
    }

    /// <summary>A stereo file counts frames rather than samples.</summary>
    /// <remarks>
    /// The distinction is the one that goes wrong quietly: a length read as samples on a stereo
    /// file is twice what it should be, which is a loop point in the wrong place rather than an
    /// error anybody sees.
    /// </remarks>
    [Fact]
    public void A_frame_is_not_a_sample()
    {
        string path = At("a.wav");

        _wav.Write(path, new short[] { 1, 2, 3, 4, 5, 6 }, 48000, 2);

        var info = _wav.ReadInfo(path);

        Assert.Equal(2, info.Channels);
        Assert.Equal(3, info.FrameCount);
    }

    /// <summary>The headers can be read without the audio being pulled into memory.</summary>
    [Fact]
    public void The_headers_read_without_the_audio()
    {
        string path = At("a.wav");

        _wav.Write(path, new short[8000], 22050, 1);

        var info = _wav.ReadInfo(path);
        var (samples, whole) = _wav.Read(path);

        Assert.Equal(whole, info);
        Assert.Equal(8000, samples.Length);
    }

    /// <summary>An empty recording is a file, and reads back as one.</summary>
    [Fact]
    public void An_empty_recording_is_still_a_file()
    {
        string path = At("a.wav");

        _wav.Write(path, Array.Empty<short>(), 44100, 1);

        var (samples, info) = _wav.Read(path);

        Assert.Empty(samples);
        Assert.Equal(0, info.FrameCount);
        Assert.Equal(44100, info.SampleRate);
    }

    /// <summary>A file this app wrote says it is already what this app keeps.</summary>
    [Fact]
    public void What_we_write_is_already_ours()
    {
        string path = At("a.wav");

        _wav.Write(path, new short[] { 1, 2 }, 44100, 1);

        var stored = _wav.StoredAs(path);

        Assert.True(stored.IsOurs);
        Assert.True(stored.Known);
        Assert.Equal(WavStored.Pcm, stored.Format);
        Assert.Equal(16, stored.Bits);
        Assert.Equal(2, stored.Bytes);
    }

    /// <summary>Bytes that are already a 16-bit payload go down without being widened.</summary>
    [Fact]
    public void Raw_bytes_can_be_written_as_they_are()
    {
        string path = At("a.wav");
        var pcm = new byte[] { 0x00, 0x00, 0xE8, 0x03, 0x18, 0xFC };

        _wav.Write(path, pcm, 44100, 1);

        var (samples, info) = _wav.Read(path);

        Assert.Equal(new short[] { 0, 1000, -1000 }, samples);
        Assert.Equal(3, info.FrameCount);
    }

    /// <summary>A file that is not a WAV at all says so rather than becoming silence.</summary>
    [Fact]
    public void Something_that_is_not_a_wav_says_so()
    {
        string path = At("a.wav");
        File.WriteAllText(path, "this is not a wav file at all, it is a note");

        Assert.Throws<InvalidOperationException>(() => _wav.ReadInfo(path));
        Assert.Throws<InvalidOperationException>(() => _wav.Read(path));
        Assert.Throws<InvalidOperationException>(() => _wav.StoredAs(path));
    }

    /// <summary>A file with nothing in it says so too.</summary>
    [Fact]
    public void An_empty_file_says_so()
    {
        string path = At("a.wav");
        File.WriteAllBytes(path, Array.Empty<byte>());

        Assert.ThrowsAny<Exception>(() => _wav.ReadInfo(path));
    }

    /// <summary>
    /// A WAV cut off part way through its audio plays what is there.
    /// </summary>
    /// <remarks>
    /// The length is taken from what the file really holds rather than from what its header
    /// claims, so a take that was being written when the machine went down plays whatever got
    /// as far as the disc. That is worth more than a refusal: the recording is somebody's, and
    /// most of it is fine.
    /// </remarks>
    [Fact]
    public void A_wav_cut_off_part_way_plays_what_is_there()
    {
        string whole = At("whole.wav");
        _wav.Write(whole, new short[4000], 44100, 1);

        byte[] raw = File.ReadAllBytes(whole);
        string cut = At("cut.wav");

        File.WriteAllBytes(cut, raw[..1044]);

        var (samples, info) = _wav.Read(cut);

        Assert.Equal(500, samples.Length);
        Assert.Equal(500, info.FrameCount);
        Assert.Equal(44100, info.SampleRate);
    }

    /// <summary>
    /// A WAV cut off before its audio even begins says so, in the file's own words.
    /// </summary>
    /// <remarks>
    /// There is a line between salvaging and inventing, and it is here: a file with no audio at
    /// all is not a short recording, it is not a recording. Every cut short of the audio is
    /// tried, since the failure was a read walking off the end of a chunk and the cut that does
    /// it is whichever one lands mid field. What it must never be is the runtime's own
    /// complaint about a stream, which is what reaches RECORD and means nothing to anybody.
    /// </remarks>
    [Fact]
    public void A_wav_with_no_audio_says_so()
    {
        string whole = At("whole.wav");
        _wav.Write(whole, new short[4000], 44100, 1);

        byte[] raw = File.ReadAllBytes(whole);

        for (int cut = 0; cut < 44; cut++)
        {
            string path = At("cut" + cut + ".wav");
            File.WriteAllBytes(path, raw[..cut]);

            Assert.Throws<InvalidOperationException>(() => _wav.Read(path));
        }
    }

    /// <summary>A file that is not there says so rather than answering with silence.</summary>
    [Fact]
    public void A_file_that_is_not_there_says_so()
    {
        Assert.ThrowsAny<Exception>(() => _wav.ReadInfo(At("nothing.wav")));
    }

    /// <summary>Every rate this app is likely to meet survives the round trip.</summary>
    [Fact]
    public void Every_ordinary_rate_survives()
    {
        foreach (int rate in new[] { 8000, 22050, 44100, 48000, 88200, 96000, 192000 })
        {
            string path = At("r" + rate + ".wav");

            _wav.Write(path, new short[] { 1, 2, 3, 4 }, rate, 2);

            var info = _wav.ReadInfo(path);

            Assert.Equal(rate, info.SampleRate);
            Assert.Equal(2, info.FrameCount);
        }
    }

    /// <summary>A layout says how it reads, for the message a panel shows beside a file.</summary>
    [Fact]
    public void A_layout_says_how_it_reads()
    {
        Assert.Equal("16-bit", new WavStored(WavStored.Pcm, 16).ToString());
        Assert.Equal("24-bit", new WavStored(WavStored.Pcm, 24).ToString());
        Assert.Equal("32-bit float", new WavStored(WavStored.Float, 32).ToString());
        Assert.Equal("64-bit float", new WavStored(WavStored.Float, 64).ToString());
    }

    /// <summary>Every width an editor writes is one this app can turn into shorts.</summary>
    [Fact]
    public void Every_width_an_editor_writes_is_known()
    {
        foreach (int bits in new[] { 8, 16, 24, 32 })
            Assert.True(new WavStored(WavStored.Pcm, bits).Known, bits + "-bit is not known");

        foreach (int bits in new[] { 32, 64 })
            Assert.True(new WavStored(WavStored.Float, bits).Known, bits + "-bit float is not known");
    }

    /// <summary>Widths nobody writes are not claimed, so a strange file is refused rather than misread.</summary>
    [Fact]
    public void A_width_nobody_writes_is_not_claimed()
    {
        Assert.False(new WavStored(WavStored.Pcm, 4).Known);
        Assert.False(new WavStored(WavStored.Pcm, 12).Known);
        Assert.False(new WavStored(WavStored.Pcm, 64).Known);
        Assert.False(new WavStored(WavStored.Float, 16).Known);
        Assert.False(new WavStored(WavStored.Extensible, 16).Known);
    }

    /// <summary>Only a 16-bit whole-number file is already what this app keeps.</summary>
    [Fact]
    public void Only_sixteen_bit_whole_numbers_are_ours()
    {
        Assert.True(new WavStored(WavStored.Pcm, 16).IsOurs);

        Assert.False(new WavStored(WavStored.Pcm, 24).IsOurs);
        Assert.False(new WavStored(WavStored.Float, 32).IsOurs);
        Assert.False(new WavStored(WavStored.Extensible, 16).IsOurs);
    }
}
