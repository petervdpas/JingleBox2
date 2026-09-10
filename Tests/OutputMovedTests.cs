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

    /// <summary>The source is put back and the input is left pointed at nothing.</summary>
    /// <remarks>
    /// **Where the sound comes out is half of what taking a source aside means.** The source is
    /// off its own output on the promise that it is coming through this application instead, and
    /// what "here" is, is the output in SETTINGS: pick another and the arrangement stands over a
    /// device nobody is listening to, with the source still unplugged from its own. So it is put
    /// back rather than carried over, and the input is left with nothing chosen, which is the one
    /// state that cannot be quietly wrong.
    /// </remarks>
    [Fact]
    public void The_output_moving_puts_a_source_back()
    {
        var bench = Bench();

        bench.Page.SelectedRoute = RecorderBench.Firefox;

        int back = bench.Wiring.Back;

        Assert.True(bench.Wiring.Aside > 0, "the source was never taken off its own output");

        bench.Page.OutputMoved();

        Assert.Null(bench.Page.SelectedRoute);
        Assert.True(bench.Wiring.Back > back,
            "nothing was put back when the output moved; giving back was only ever the one that "
            + "happens on the way into choosing a source, so this passed while a browser stayed "
            + "unplugged");
    }

    /// <summary>And it says so, since a source put back silently reads as a fault.</summary>
    [Fact]
    public void It_says_why_the_source_went_back()
    {
        var bench = Bench();

        bench.Page.SelectedRoute = RecorderBench.Firefox;
        bench.Page.Status = string.Empty;

        bench.Page.OutputMoved();

        Assert.Contains("put back", bench.Page.Status, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Nothing happens where nothing was chosen, which is the ordinary run.</summary>
    [Fact]
    public void An_output_moving_with_nothing_chosen_touches_nothing()
    {
        var bench = Bench();

        bench.Page.Status = "still here";

        bench.Page.OutputMoved();

        Assert.Null(bench.Page.SelectedRoute);
        Assert.Equal(0, bench.Wiring.Back);
        Assert.Equal("still here", bench.Page.Status);
    }
}
