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

    /// <summary>
    /// What an output is playing is left off the recorder's bus, since hearing that through the
    /// desk is the output hearing itself.
    /// </summary>
    /// <remarks>
    /// **The switch is not refused, the capture is.** Hear it says whether the recorder is heard,
    /// which is a property of the recorder; whether the capture is one of the things it carries is
    /// a property of the source. They were one switch, and the loop took the whole recorder with
    /// it.
    /// </remarks>
    [Fact]
    public void What_an_output_is_playing_is_left_off_the_recorder()
    {
        var bench = new RecorderBench();

        bench.Page.SelectedRoute = RecorderBench.Speakers;

        Assert.False(bench.Page.CanHear);

        bench.Page.Hearing = true;

        Assert.True(bench.Page.Hearing, "the switch was refused over a source it is not about");
        Assert.False(bench.Recorder.HearsCapture, "the capture was left on the bus and the loop stands");
    }

    /// <summary>
    /// And choosing one while it is already on takes the capture off the bus rather than turning
    /// the recorder off.
    /// </summary>
    /// <remarks>
    /// The source can change under a switch that is already on, and by then the audio would
    /// already be going round, so the answer cannot wait for anybody to press anything. What it
    /// answers with is the capture leaving the recorder's bus, which is where the loop is, and a
    /// line saying so.
    /// </remarks>
    [Fact]
    public void Choosing_it_while_listening_takes_the_capture_off()
    {
        var bench = new RecorderBench();

        bench.Page.SelectedRoute = RecorderBench.Firefox;
        bench.Page.Hearing = true;

        Assert.True(bench.Page.Hearing);
        Assert.True(bench.Recorder.HearsCapture);

        bench.Page.SelectedRoute = RecorderBench.Speakers;

        Assert.True(bench.Page.Hearing, "the recorder was silenced over one thing on its bus");
        Assert.False(bench.Recorder.HearsCapture, "the loop was left standing");
        Assert.Contains("loop", bench.Page.Status);
    }

    /// <summary>
    /// Moving back to a source that can be heard puts the capture back on the recorder's bus,
    /// since nothing was ever switched off.
    /// </summary>
    /// <remarks>
    /// The switch is what somebody set and it stays where they set it. What came off was the
    /// capture, and it comes back when the reason it left does, which is the source: nothing here
    /// turns itself on, because nothing turned itself off.
    /// </remarks>
    [Fact]
    public void The_capture_comes_back_when_the_source_can_be_heard_again()
    {
        var bench = new RecorderBench();

        bench.Page.SelectedRoute = RecorderBench.Firefox;
        bench.Page.Hearing = true;

        bench.Page.SelectedRoute = RecorderBench.Speakers;

        Assert.False(bench.Recorder.HearsCapture);

        bench.Page.SelectedRoute = RecorderBench.Firefox;

        Assert.True(bench.Page.CanHear);
        Assert.True(bench.Page.Hearing, "the switch somebody set did not stay where they set it");
        Assert.True(bench.Recorder.HearsCapture);
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
