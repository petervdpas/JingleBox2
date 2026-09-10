using System;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A source taken aside is put back when the output moves.
/// </summary>
/// <remarks>
/// **What "only here" means depends on where here comes out.** The switch unplugs somebody
/// else's program from its own output on the promise that it is heard through this application
/// instead, and the output in SETTINGS is the whole of that second half. Picked another one and
/// the promise is over a device nobody is listening to, with the source still unplugged.
///
/// So the switch goes off and the machine is put back rather than the arrangement being carried
/// over to a device nobody asked it to be carried to. The unhappy path is the one that matters
/// as much: a device picked while nothing was taken aside must touch nothing at all, since that
/// is every ordinary run of this application.
/// </remarks>
public sealed class OutputMovedTests
{
    /// <summary>The page over doubles, since nothing here is about audio.</summary>
    private static RecorderBench Bench() => new();

    /// <summary>
    /// The output moving takes the source off its new own output again, and keeps it.
    /// </summary>
    /// <remarks>
    /// **The arrangement is made again rather than thrown away.** It used to clear the source
    /// outright, which was defensible while taking one aside was a switch somebody set: the
    /// promise had been made about an output nobody was listening to any more. It is not now that
    /// choosing the source is the arrangement, and what it came to was the input silently emptying
    /// itself whenever the output picker was touched, with the source handed back to its own
    /// speakers, which is heard as the sound coming back.
    /// </remarks>
    [Fact]
    public void The_output_moving_takes_the_source_aside_again()
    {
        var bench = Bench();

        bench.Page.SelectedRoute = RecorderBench.Firefox;

        int aside = bench.Wiring.Aside;

        bench.Page.PlayingOut = "Some other output";
        bench.Page.OutputMoved();

        Assert.Equal(RecorderBench.Firefox, bench.Page.SelectedRoute);
        Assert.True(bench.Wiring.Aside > aside, "the source was not taken off its own output again");
    }

    /// <summary>
    /// Unless the output has moved onto the source, which is hearing an output through itself.
    /// </summary>
    /// <remarks>
    /// The one case that really does end the arrangement, and it is answered against the new
    /// output rather than the old, since that is what has just moved. Said on the status line,
    /// because a source that goes back to its own speakers with nothing saying why reads as this
    /// application having dropped it.
    /// </remarks>
    [Fact]
    public void It_says_so_when_the_output_moves_onto_the_source()
    {
        var bench = Bench();

        bench.Wiring.Ours = true;
        bench.Page.SelectedRoute = RecorderBench.Speakers;
        bench.Page.Status = string.Empty;

        bench.Page.PlayingOut = RecorderBench.Speakers.Name;
        bench.Page.OutputMoved();

        Assert.Contains("loop", bench.Page.Status, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Nothing happens where nothing was chosen, which is the ordinary run.</summary>
    [Fact]
    public void An_output_moving_with_nothing_chosen_touches_nothing()
    {
        var bench = Bench();

        bench.Page.Status = "still here";

        bench.Page.OutputMoved();

        Assert.Null(bench.Page.SelectedRoute);
        Assert.Equal(0, bench.Wiring.Aside);
        Assert.Equal("still here", bench.Page.Status);
    }
}
