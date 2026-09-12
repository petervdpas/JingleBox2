using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The input gain, which is one value however many places show it.
/// </summary>
/// <remarks>
/// **It was two: a property on the page and a field in the settings, kept in step by hand.** Two
/// copies of one fact is the fault this codebase keeps naming, and it has the same shape every
/// time, which is that something re-applies one of them over the other and nothing anywhere says
/// so. The page reads the block and writes the block now, so there is nothing to keep in step.
///
/// The mixer's IN strip is the second place it is shown, and it was already reading through
/// rather than holding a copy. That is what these say out loud: what one of them writes, the
/// other one reads, and both of them are the settings.
/// </remarks>
public sealed class InputGainTests
{
    /// <summary>What the page is given is what the settings hold, with nothing in between.</summary>
    [Fact]
    public void The_page_shows_what_the_settings_hold()
    {
        var bench = new RecorderBench();

        bench.Settings.RecordGainDb = 6.5;

        Assert.Equal(6.5, bench.Page.RecordGainDb);
    }

    /// <summary>And what the page is moved to is what the settings hold.</summary>
    [Fact]
    public void Moving_it_on_the_page_moves_the_settings()
    {
        var bench = new RecorderBench();

        bench.Page.RecordGainDb = 6.5;

        Assert.Equal(6.5, bench.Settings.RecordGainDb);
    }

    /// <summary>The recorder is told at once, since that is what a take will hold.</summary>
    /// <remarks>
    /// The gain is applied to the audio coming in rather than to the file afterwards, which is
    /// the point of it: a take recorded too quietly cannot be repaired later without bringing the
    /// noise up with it.
    /// </remarks>
    [Fact]
    public void Moving_it_reaches_the_recorder()
    {
        var bench = new RecorderBench();

        bench.Page.RecordGainDb = 6.5;

        Assert.Equal(6.5, bench.Recorder.GainDb);
    }

    /// <summary>Setting it to what it already is moves nothing and says nothing.</summary>
    /// <remarks>
    /// A fader hands its value back whenever it is rebuilt, which is every time a page is drawn
    /// again, and a page switch is not somebody changing the gain.
    /// </remarks>
    [Fact]
    public void Setting_it_to_what_it_already_is_says_nothing()
    {
        var bench = new RecorderBench();

        bench.Page.RecordGainDb = 6.5;

        int said = 0;

        bench.Page.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(bench.Page.RecordGainDb)) said++;
        };

        bench.Page.RecordGainDb = 6.5;

        Assert.Equal(0, said);
        Assert.Equal(6.5, bench.Settings.RecordGainDb);
    }

    /// <summary>
    /// **The strip on the mixer and the fader on RECORD are one value**, not two that agree.
    /// </summary>
    [Fact]
    public void The_mixers_strip_and_the_page_are_the_same_gain()
    {
        var bench = new RecorderBench();

        bench.Page.RecordGainDb = -6;

        Assert.Equal(-6, bench.Settings.RecordGainDb);
        Assert.Equal(-6, bench.Page.RecordGainDb);
        Assert.Equal(-6, bench.Recorder.GainDb);
    }
}
