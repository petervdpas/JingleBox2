using System;
using System.IO;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The edits that work on a stretch of a take and leave it its length.
/// </summary>
/// <remarks>
/// Every one of these goes wrong in a way nobody hears as a fault. A reversal done a sample at a
/// time is the right audio with the two channels swapped, which reads as a stereo image that has
/// quietly turned round; a fade that is one frame out at its head never reaches silence, which
/// is the click somebody put the fade on to be rid of; and an edit that reached one frame past
/// the region is audible on nothing at all.
///
/// The samples are asked directly, because that is the only place any of it can be seen: a
/// picture of a hundred and sixty thousand peaks says nothing about the frame at either end.
/// </remarks>
public sealed class RecordingEditTests : IDisposable
{
    /// <summary>The rules under test.</summary>
    private readonly IRecordingEdit _edit = new RecordingEdit();

    /// <summary>Reading and writing WAV files, for the two that go through a file.</summary>
    private readonly IWavFile _wav = new WavFile();

    /// <summary>The service, which is what the page really calls.</summary>
    private readonly IWaveformService _waveforms = new WaveformService();

    /// <summary>A folder of its own, since two of these put a file on a disc.</summary>
    private readonly string _home;

    /// <inheritdoc cref="RecordingEditTests"/>
    public RecordingEditTests()
    {
        _home = Path.Combine(Path.GetTempPath(), "jb-edit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_home);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>A mono take whose every frame says which frame it is.</summary>
    /// <param name="frames">How many of them.</param>
    /// <returns>The samples.</returns>
    private static short[] Counting(int frames = 10)
    {
        var samples = new short[frames];

        for (int frame = 0; frame < frames; frame++) samples[frame] = (short)(frame + 1);

        return samples;
    }

    /// <summary>A stereo take whose two channels say different things at every frame.</summary>
    /// <remarks>
    /// The left is positive and the right is negative, so a channel that has moved to the other
    /// side shows up as a sign rather than as a number somebody has to check against a list.
    /// </remarks>
    /// <param name="frames">How many frames.</param>
    /// <returns>The samples, interleaved.</returns>
    private static short[] TwoSided(int frames = 4)
    {
        var samples = new short[frames * 2];

        for (int frame = 0; frame < frames; frame++)
        {
            samples[frame * 2] = (short)(frame + 1);
            samples[frame * 2 + 1] = (short)-(frame + 1);
        }

        return samples;
    }

    /// <summary>What is inside the region is nought afterwards and the rest is untouched.</summary>
    [Fact]
    public void Silence_empties_the_region_and_nothing_else()
    {
        var samples = Counting();

        _edit.Silence(samples, 1, 3, 6);

        Assert.Equal(new short[] { 1, 2, 3, 0, 0, 0, 7, 8, 9, 10 }, samples);
    }

    /// <summary>The region comes back the other way round.</summary>
    [Fact]
    public void Reverse_turns_the_region_round()
    {
        var samples = Counting();

        _edit.Reverse(samples, 1, 2, 6);

        Assert.Equal(new short[] { 1, 2, 6, 5, 4, 3, 7, 8, 9, 10 }, samples);
    }

    /// <summary>
    /// **A stereo take comes back with its channels the way round they went in.**
    /// </summary>
    /// <remarks>
    /// The one that is silent when it is wrong: reversed a sample at a time the audio is right
    /// and the left and right have swapped, which sounds like a recording that was always that
    /// way about. The left channel is positive here and the right negative, so a swap is a sign.
    /// </remarks>
    [Fact]
    public void Reverse_keeps_the_channels_the_way_round_they_were()
    {
        var samples = TwoSided();

        _edit.Reverse(samples, 2, 0, 4);

        Assert.Equal(new short[] { 4, -4, 3, -3, 2, -2, 1, -1 }, samples);
    }

    /// <summary>Reversing the same stretch again is what was there before.</summary>
    [Fact]
    public void Reverse_twice_is_where_it_started()
    {
        var samples = Counting();
        var before = (short[])samples.Clone();

        _edit.Reverse(samples, 1, 1, 8);
        _edit.Reverse(samples, 1, 1, 8);

        Assert.Equal(before, samples);
    }

    /// <summary>A fade in is silent on the region's first frame.</summary>
    [Fact]
    public void A_fade_in_starts_at_silence()
    {
        var samples = Counting();

        _edit.Fade(samples, 1, 2, 8, rising: true);

        Assert.Equal(0, samples[2]);
    }

    /// <summary>And leaves the last frame of the region exactly as it was.</summary>
    /// <remarks>
    /// The off-by-one this is here for: a ramp worked out over the frames rather than over the
    /// steps between them never quite reaches full, so a fade in is followed by a step.
    /// </remarks>
    [Fact]
    public void A_fade_in_reaches_full_at_the_end_of_the_region()
    {
        var samples = Counting();

        _edit.Fade(samples, 1, 2, 8, rising: true);

        Assert.Equal(8, samples[7]);
    }

    /// <summary>A fade out is the same the other way about.</summary>
    [Fact]
    public void A_fade_out_starts_full_and_ends_at_silence()
    {
        var samples = Counting();

        _edit.Fade(samples, 1, 2, 8, rising: false);

        Assert.Equal(3, samples[2]);
        Assert.Equal(0, samples[7]);
    }

    /// <summary>Halfway along a fade is halfway down, which is what makes it a ramp.</summary>
    [Fact]
    public void A_fade_is_even_along_its_length()
    {
        var samples = new short[5];

        for (int frame = 0; frame < samples.Length; frame++) samples[frame] = 1000;

        _edit.Fade(samples, 1, 0, 5, rising: true);

        Assert.Equal(new short[] { 0, 250, 500, 750, 1000 }, samples);
    }

    /// <summary>A fade touches nothing outside the region it was given.</summary>
    [Fact]
    public void A_fade_leaves_the_rest_of_the_take_alone()
    {
        var samples = Counting();

        _edit.Fade(samples, 1, 4, 7, rising: true);

        Assert.Equal(4, samples[3]);
        Assert.Equal(8, samples[7]);
    }

    /// <summary>A region of one frame is not a ramp and is left where it is.</summary>
    [Fact]
    public void A_fade_over_one_frame_changes_nothing()
    {
        var samples = Counting();

        _edit.Fade(samples, 1, 5, 6, rising: true);

        Assert.Equal(6, samples[5]);
    }

    /// <summary>A region with nothing in it changes nothing, whichever edit it is.</summary>
    [Fact]
    public void An_empty_region_changes_nothing()
    {
        var samples = Counting();
        var before = (short[])samples.Clone();

        _edit.Silence(samples, 1, 4, 4);
        _edit.Reverse(samples, 1, 4, 4);
        _edit.Fade(samples, 1, 4, 4, rising: true);

        Assert.Equal(before, samples);
    }

    /// <summary>A region hanging off the end of the take is brought inside it.</summary>
    /// <remarks>
    /// A handle is dragged against the edge of a picture and lands a hair outside, which is the
    /// ordinary case rather than a corner. What may not happen is walking off the array.
    /// </remarks>
    [Fact]
    public void A_region_past_the_end_is_brought_inside_the_take()
    {
        var samples = Counting();

        _edit.Silence(samples, 1, 8, 400);

        Assert.Equal(new short[] { 1, 2, 3, 4, 5, 6, 7, 8, 0, 0 }, samples);
    }

    /// <summary>And one with nothing to work on is answered rather than thrown at.</summary>
    [Fact]
    public void Nothing_to_work_on_is_answered()
    {
        _edit.Silence(null, 1, 0, 4);
        _edit.Reverse(null, 2, 0, 4);
        _edit.Fade(null, 2, 0, 4, rising: true);

        _edit.Reverse(Counting(), 0, 0, 4);
    }

    /// <summary>The reversal really reaches the file, which is what the page asks for.</summary>
    [Fact]
    public void A_take_is_reversed_on_the_disc()
    {
        string path = Path.Combine(_home, "take.wav");
        _wav.Write(path, Counting(6), 44100, 1);

        _waveforms.ReverseFile(path, 0, 6);

        var (samples, _) = _wav.Read(path);

        Assert.Equal(new short[] { 6, 5, 4, 3, 2, 1 }, samples);
    }

    /// <summary>And so does the fade, without the take changing length.</summary>
    [Fact]
    public void A_take_is_faded_on_the_disc()
    {
        string path = Path.Combine(_home, "take.wav");
        var loud = new short[5];

        for (int frame = 0; frame < loud.Length; frame++) loud[frame] = 1000;

        _wav.Write(path, loud, 44100, 1);

        _waveforms.FadeFile(path, 0, 5, rising: false);

        var (samples, info) = _wav.Read(path);

        Assert.Equal(5, info.FrameCount);
        Assert.Equal(1000, samples[0]);
        Assert.Equal(0, samples[4]);
    }

    /// <summary>A region nobody marked is refused with a sentence rather than doing nothing.</summary>
    [Fact]
    public void An_empty_region_on_the_disc_is_refused()
    {
        string path = Path.Combine(_home, "take.wav");
        _wav.Write(path, Counting(6), 44100, 1);

        Assert.Throws<InvalidOperationException>(() => _waveforms.ReverseFile(path, 3, 3));
    }
}
