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

    /// <summary>A source taken aside is put back and the switch goes off.</summary>
    [Fact]
    public void The_output_moving_puts_a_source_back()
    {
        var bench = Bench();

        bench.Page.TakeAside = true;

        Assert.True(bench.Page.TakeAside);

        bench.Page.OutputMoved();

        Assert.False(bench.Page.TakeAside, "the switch stayed on over an output it was never set up for");
        Assert.True(bench.Wiring.Back > 0, "the machine was never put back");
    }

    /// <summary>And it says so, since a switch that turns itself off silently reads as a fault.</summary>
    [Fact]
    public void It_says_why_the_switch_went_off()
    {
        var bench = Bench();

        bench.Page.TakeAside = true;
        bench.Page.Status = string.Empty;

        bench.Page.OutputMoved();

        Assert.Contains("put back", bench.Page.Status, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Nothing whatever happens where nothing was taken aside, which is the ordinary run.</summary>
    [Fact]
    public void An_output_moving_with_nothing_aside_touches_nothing()
    {
        var bench = Bench();

        bench.Page.Status = "still here";

        bench.Page.OutputMoved();

        Assert.False(bench.Page.TakeAside);
        Assert.Equal(0, bench.Wiring.Back);
        Assert.Equal("still here", bench.Page.Status);
    }
}
