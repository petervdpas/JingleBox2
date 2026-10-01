using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which buffer, top-up and cushion sizes are offered, and which of them is recommended, with and
/// without real-time audio.
/// </summary>
/// <remarks>
/// What the settings page lists and what a fresh installation runs at both come from here, so the
/// two cannot disagree: the recommended value is always one of the values offered, and real time
/// only ever adds smaller ones on top of what everybody gets.
/// </remarks>
public class AudioChoicesTests
{
    /// <summary>The rule under test.</summary>
    private readonly IAudioChoices _choices = new AudioChoices();

    /// <summary>Real time adds smaller buffers and takes none away.</summary>
    [Fact]
    public void Real_time_adds_smaller_buffers()
    {
        var plain = _choices.BufferFrames(realtime: false);
        var fast = _choices.BufferFrames(realtime: true);

        Assert.All(plain, frames => Assert.Contains(frames, fast));
        Assert.True(fast[0] < plain[0]);
    }

    /// <summary>Real time adds quicker top-ups and takes none away.</summary>
    [Fact]
    public void Real_time_adds_quicker_top_ups()
    {
        var plain = _choices.UpdatePeriods(realtime: false);
        var fast = _choices.UpdatePeriods(realtime: true);

        Assert.All(plain, period => Assert.Contains(period, fast));
        Assert.True(fast[0] < plain[0]);
    }

    /// <summary>Real time adds a smaller cushion and takes none away.</summary>
    [Fact]
    public void Real_time_adds_a_smaller_cushion()
    {
        var plain = _choices.Cushions(realtime: false);
        var fast = _choices.Cushions(realtime: true);

        Assert.All(plain, cushion => Assert.Contains(cushion, fast));
        Assert.True(fast.Count > plain.Count);
    }

    /// <summary>What is recommended is always something that is offered, either way.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_recommended_values_are_offered(bool realtime)
    {
        var recommended = _choices.Recommended(realtime, null, 44100);

        Assert.Contains(recommended.BufferFrames, _choices.BufferFrames(realtime));
        Assert.Contains(recommended.UpdatePeriodMs, _choices.UpdatePeriods(realtime));
        Assert.Contains(_choices.RecommendedCushion(realtime), _choices.Cushions(realtime));
    }

    /// <summary>
    /// With real time the recommended cushion is smaller and the recommended buffer is not, since
    /// the output is filled by threads that are not real time either way.
    /// </summary>
    /// <remarks>
    /// A 512 frame buffer with real time was measured running dry: the sound card took 409 blocks
    /// in five seconds where it plays 431. The same machine at 2048 frames with a 40 ms cushion
    /// played clean.
    /// </remarks>
    [Fact]
    public void Real_time_recommends_a_smaller_cushion_and_the_same_buffer()
    {
        Assert.Equal(_choices.Recommended(false, null, 44100), _choices.Recommended(true, null, 44100));
        Assert.True(_choices.RecommendedCushion(true) < _choices.RecommendedCushion(false));
    }

    /// <summary>
    /// Without real time the recommendation is the pair this application has been played at, so a
    /// machine that cannot have real time runs exactly as it always has.
    /// </summary>
    [Fact]
    public void Without_real_time_the_recommendation_is_what_has_been_played()
    {
        var plain = _choices.Recommended(false, null, 44100);

        Assert.Equal(2048, plain.BufferFrames);
        Assert.Equal(10, plain.UpdatePeriodMs);
        Assert.Equal(120, _choices.RecommendedCushion(false));
    }

    /// <summary>The lists run from small to large, which is how a slider and a picker read them.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_lists_run_from_small_to_large(bool realtime)
    {
        AssertRising(_choices.BufferFrames(realtime));
        AssertRising(_choices.UpdatePeriods(realtime));
        AssertRising(_choices.Cushions(realtime));
    }

    /// <summary>Says every entry is larger than the one before it.</summary>
    private static void AssertRising(System.Collections.Generic.IReadOnlyList<int> values)
    {
        for (int at = 1; at < values.Count; at++) Assert.True(values[at] > values[at - 1]);
    }

    /// <summary>
    /// The buffer is the smallest offered that holds two of the sound server's cycles, which is
    /// what a card running through a server needs so one cycle can be filled while the other plays.
    /// </summary>
    /// <remarks>
    /// The case that found it: PipeWire at 48 kHz with a quantum of 1024 is a cycle of 21.3 ms,
    /// and a buffer of 512 at 44.1 kHz is 11.6 ms, shorter than one cycle, so it ran dry.
    /// </remarks>
    [Theory]
    [InlineData(48000, 1024, 48000, 2048)]
    [InlineData(48000, 1024, 44100, 2048)]
    [InlineData(48000, 256, 48000, 512)]
    [InlineData(48000, 64, 48000, 128)]
    [InlineData(44100, 512, 44100, 1024)]
    [InlineData(48000, 8192, 48000, 8192)]
    public void The_buffer_holds_two_server_cycles(int serverRate, int quantum, int rate, int expected)
    {
        var sizes = _choices.Recommended(false, new ServerClock(serverRate, quantum), rate);

        Assert.Equal(expected, sizes.BufferFrames);
    }

    /// <summary>A server that cannot be read leaves the buffer at the size that has been played.</summary>
    [Fact]
    public void An_unknown_server_leaves_2048()
    {
        Assert.Equal(2048, _choices.Recommended(true, null, 48000).BufferFrames);
    }

    /// <summary>A cycle of nothing is not a cycle, and is treated as not knowing.</summary>
    [Fact]
    public void A_cycle_of_nothing_is_not_knowing()
    {
        Assert.Equal(2048, _choices.Recommended(false, new ServerClock(48000, 0), 48000).BufferFrames);
    }

    /// <summary>The rate recommended is the server's own, so nothing is converted on the way out.</summary>
    [Theory]
    [InlineData(48000, 48000)]
    [InlineData(44100, 44100)]
    [InlineData(96000, 96000)]
    [InlineData(22050, 44100)]
    public void The_rate_is_the_servers_own_where_it_is_offered(int serverRate, int expected)
    {
        Assert.Equal(expected, _choices.RecommendedRate(new ServerClock(serverRate, 1024)));
    }

    /// <summary>A server that cannot be read leaves the rate this application has always run at.</summary>
    [Fact]
    public void An_unknown_server_leaves_44100()
    {
        Assert.Equal(44100, _choices.RecommendedRate(null));
    }

    /// <summary>The recommended rate is always one that is offered.</summary>
    [Fact]
    public void The_recommended_rate_is_offered()
    {
        Assert.Contains(_choices.RecommendedRate(new ServerClock(48000, 1024)), _choices.Rates);
        Assert.Contains(_choices.RecommendedRate(null), _choices.Rates);
    }
}
