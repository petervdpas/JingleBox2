using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which source may be heard through the desk, and what picking another one does to it.
/// </summary>
/// <remarks>
/// **The one that has to be right is the loop.** The picker's default is what an output is
/// playing, which is that output's own monitor, so hearing it through the output feeds it back
/// into itself: at full scale, through whatever the chain is doing to it, in a room with a
/// person in it. It is the first thing anybody would press, and the guard has to hold from both
/// directions, since the source can be chosen after the switch as easily as before it.
///
/// The audio itself is not here and cannot be: the path is a BASS stream on a bus and this suite
/// runs with no card. What is asked here is the rule about it.
/// </remarks>
public sealed class HearInputTests
{
    /// <summary>A program can be heard, which is the case the whole thing is for.</summary>
    [Fact]
    public void A_program_can_be_heard()
    {
        var bench = new RecorderBench();

        bench.Page.SelectedRoute = RecorderBench.Firefox;

        Assert.True(bench.Page.CanHear);

        bench.Page.Hearing = true;

        Assert.True(bench.Page.Hearing);
        Assert.True(bench.Recorder.Hearing, "the recorder was never told to push what it captures");
    }

    /// <summary>What an output is playing cannot, since that is the output hearing itself.</summary>
    [Fact]
    public void What_an_output_is_playing_cannot_be_heard()
    {
        var bench = new RecorderBench();

        bench.Page.SelectedRoute = RecorderBench.Speakers;

        Assert.False(bench.Page.CanHear);

        bench.Page.Hearing = true;

        Assert.False(bench.Page.Hearing, "a loop was made out of the source the picker defaults to");
        Assert.False(bench.Recorder.Hearing);
    }

    /// <summary>And choosing one while it is already on turns it off rather than leaving it.</summary>
    /// <remarks>
    /// The switch being grey cannot cover this on its own: it goes grey at the moment the source
    /// changes, and by then the audio is already going round.
    /// </remarks>
    [Fact]
    public void Choosing_it_while_listening_stops_the_listening()
    {
        var bench = new RecorderBench();

        bench.Page.SelectedRoute = RecorderBench.Firefox;
        bench.Page.Hearing = true;

        Assert.True(bench.Page.Hearing);

        bench.Page.SelectedRoute = RecorderBench.Speakers;

        Assert.False(bench.Page.Hearing, "the loop was made by changing the source rather than the switch");
        Assert.False(bench.Recorder.Hearing);
        Assert.Contains("loop", bench.Page.Status);
    }

    /// <summary>Moving back to a source that can be heard does not start listening again.</summary>
    /// <remarks>
    /// Turning it off is answering a danger, and answering it does not leave a promise to turn it
    /// back on later: a switch that came on by itself is one nobody set.
    /// </remarks>
    [Fact]
    public void It_does_not_come_back_on_by_itself()
    {
        var bench = new RecorderBench();

        bench.Page.SelectedRoute = RecorderBench.Firefox;
        bench.Page.Hearing = true;

        bench.Page.SelectedRoute = RecorderBench.Speakers;
        bench.Page.SelectedRoute = RecorderBench.Firefox;

        Assert.True(bench.Page.CanHear);
        Assert.False(bench.Page.Hearing);
    }

    /// <summary>Turning it off is not refused, whatever the source is.</summary>
    /// <remarks>
    /// The guard is on going on rather than on the switch, so somebody left holding a source that
    /// cannot be listened to can still put the switch down.
    /// </remarks>
    [Fact]
    public void It_can_always_be_turned_off()
    {
        var bench = new RecorderBench();

        bench.Page.SelectedRoute = RecorderBench.Firefox;
        bench.Page.Hearing = true;

        bench.Page.Hearing = false;

        Assert.False(bench.Page.Hearing);
        Assert.False(bench.Recorder.Hearing);
    }

    /// <summary>Nothing chosen at all is not a loop and is not refused.</summary>
    [Fact]
    public void Nothing_chosen_can_still_be_heard()
    {
        var bench = new RecorderBench();

        Assert.Null(bench.Page.SelectedRoute);
        Assert.True(bench.Page.CanHear);
    }
}
