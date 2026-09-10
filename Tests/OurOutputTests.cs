using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Whether the capture goes on the recorder's bus, which turns on whose output a monitor is of.
/// </summary>
/// <remarks>
/// **The rule used to be the source's kind on its own and had to become a question for the
/// subsystem.** Capturing what an output is playing and hearing it through that same output sends
/// it round again; capturing what a *second* output is playing, while this application plays out
/// of the first, is the ordinary way anybody records another program and goes back to nowhere.
///
/// Refused on the kind alone the second case was silently not heard: the switch was on, the source
/// was chosen, the capture was left off the bus, and the line said it was a loop. That is the
/// report these are the answer to.
///
/// Which output a monitor is of is the subsystem's to say, since the names come from whatever
/// wired the machine up, and it has three answers rather than two. These pin what the page does
/// with each: the third is the one worth having, because cannot tell has to fall in with ours.
/// </remarks>
public class OurOutputTests
{
    /// <summary>An output's own playback, heard through that same output, is refused.</summary>
    [Fact]
    public void Our_own_output_is_not_heard()
    {
        var bench = new RecorderBench { Wiring = { Ours = true } };

        bench.Page.SelectedRoute = RecorderBench.Speakers;

        Assert.False(bench.Page.CanHear);
    }

    /// <summary>
    /// **And another output's is heard, which is the whole of what was wrong.**
    /// </summary>
    [Fact]
    public void Another_output_is_heard()
    {
        var bench = new RecorderBench { Wiring = { Ours = false } };

        bench.Page.SelectedRoute = RecorderBench.Speakers;

        Assert.True(bench.Page.CanHear);
    }

    /// <summary>
    /// A subsystem that cannot tell is read as though it were ours.
    /// </summary>
    /// <remarks>
    /// The one answer here that takes the safe side rather than the useful one, and it is worth
    /// its own test so nobody quietly turns it over: at the far end of a wrong yes is a switch
    /// that does nothing until a subsystem learns to tell its outputs apart, and at the far end
    /// of a wrong no is a room full of feedback at whatever the master is set to.
    /// </remarks>
    [Fact]
    public void Not_being_able_to_tell_is_refused()
    {
        var bench = new RecorderBench { Wiring = { Ours = null } };

        bench.Page.SelectedRoute = RecorderBench.Speakers;

        Assert.False(bench.Page.CanHear);
    }

    /// <summary>
    /// Anything that is not an output's playback is heard whatever the subsystem says.
    /// </summary>
    /// <remarks>
    /// A microphone, a line in and a program cannot come back round through this path, so the
    /// subsystem is not even asked. Answered here with the subsystem set to the one reply that
    /// would refuse a monitor, since what decides is the kind and not the reply.
    /// </remarks>
    [Fact]
    public void A_program_is_heard_whatever_the_subsystem_says()
    {
        var bench = new RecorderBench { Wiring = { Ours = true } };

        bench.Page.SelectedRoute = RecorderBench.Firefox;

        Assert.True(bench.Page.CanHear);
    }

    /// <summary>And nothing chosen is nothing to refuse.</summary>
    [Fact]
    public void Nothing_chosen_is_heard()
    {
        var bench = new RecorderBench { Wiring = { Ours = null } };

        bench.Page.SelectedRoute = null;

        Assert.True(bench.Page.CanHear);
    }

    /// <summary>
    /// **Only a capture device is watched for ringing.**
    /// </summary>
    /// <remarks>
    /// Feedback needs a microphone. What an output is playing and what a program is playing have
    /// never been near the air in the room, so there is nothing there that can come back round and
    /// the question is not asked at all.
    ///
    /// This is the gate the shape tests could not replace: every threshold that tried to tell a
    /// ring from music by its spectrum alone was wrong on somebody's material, and none of that
    /// judgement was ever needed on a source that cannot ring.
    /// </remarks>
    [Fact]
    public void Only_a_capture_device_is_watched_for_ringing()
    {
        var bench = new RecorderBench { Wiring = { Ours = false } };

        bench.Page.SelectedRoute = RecorderBench.Speakers;

        Assert.False(bench.Recorder.HearsTheRoom, "an output's own playback was watched for ringing");

        bench.Page.SelectedRoute = RecorderBench.Firefox;

        Assert.False(bench.Recorder.HearsTheRoom, "a program's audio was watched for ringing");

        bench.Page.SelectedRoute = RecorderBench.Microphone;

        Assert.True(bench.Recorder.HearsTheRoom, "a capture device was not watched for ringing");
    }
}