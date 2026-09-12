using System.Collections.Generic;
using JingleBox2.Controllers;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The write path: what is pointed at what, read into the templates block.
/// </summary>
/// <remarks>
/// **A block nothing writes is a snapshot taken at startup.** The gesture writes a link, this is
/// what the links come to, and the block says it moved so that whoever puts it on disc hears
/// about it and nobody else has to know. Asked here with no window, no controller and no settings
/// file, which is the whole reason it is a seam rather than two lines inside the window.
/// </remarks>
public sealed class ControlTemplatesFromLinksTests
{
    /// <summary>A knob on a machine, learned on a port.</summary>
    private static ControlMapping Knob(string port, string machine, int cc) => new()
    {
        Device = port,
        Channel = 1,
        Cc = cc,
        Kind = ControlKind.SoundDevice,
        Machine = machine,
        Key = "knob" + cc,
        Owner = machine,
        Name = machine + " knob " + cc,
    };

    /// <summary>A block, the links under it, and the thing that keeps the two together.</summary>
    private sealed class Bench
    {
        /// <summary>What is pointed at what.</summary>
        public List<ControlMapping> Links { get; } = new();

        /// <summary>The block being filled.</summary>
        public ControlTemplateBlock Block { get; } = new();

        /// <summary>How many times the block said it moved.</summary>
        public int Said { get; private set; }

        /// <summary>The write path itself.</summary>
        public ControlTemplatesFromLinks Filling { get; }

        /// <summary>Wires them together over profiles that know nothing, which is a bare machine.</summary>
        public Bench()
        {
            Block.Changed += () => Said++;

            Filling = new ControlTemplatesFromLinks(Block, () => Links, new ControllerProfiles());
        }
    }

    /// <summary>The first fill is what was already on disc, so it says nothing.</summary>
    /// <remarks>
    /// A writer hearing a hint before anybody has touched anything writes the file back at
    /// startup, which is a disc write for a change nobody made.
    /// </remarks>
    [Fact]
    public void The_first_fill_says_nothing()
    {
        var bench = new Bench();

        bench.Links.Add(Knob("nanoKONTROL2", "machine.oddskilla", 16));

        bench.Filling.Fill(said: false);

        Assert.Single(bench.Block.Templates);
        Assert.Equal(0, bench.Said);
    }

    /// <summary>And every fill after it does.</summary>
    [Fact]
    public void Every_fill_after_it_says_so()
    {
        var bench = new Bench();

        bench.Links.Add(Knob("nanoKONTROL2", "machine.oddskilla", 16));

        bench.Filling.Fill(said: true);

        Assert.Equal(1, bench.Said);
    }

    /// <summary>A link learned reaches the block.</summary>
    [Fact]
    public void A_link_learned_reaches_the_block()
    {
        var bench = new Bench();

        bench.Filling.Fill(said: false);

        Assert.Empty(bench.Block.Templates);

        bench.Links.Add(Knob("nanoKONTROL2", "machine.oddskilla", 16));
        bench.Filling.Fill(said: true);

        Assert.Single(bench.Block.Templates);
        Assert.Single(bench.Block.Templates[0].Controls);
    }

    /// <summary>A link taken off leaves with it.</summary>
    [Fact]
    public void A_link_taken_off_leaves_the_block()
    {
        var bench = new Bench();

        bench.Links.Add(Knob("nanoKONTROL2", "machine.oddskilla", 16));
        bench.Filling.Fill(said: false);

        bench.Links.Clear();
        bench.Filling.Fill(said: true);

        Assert.Empty(bench.Block.Templates);
    }

    /// <summary>
    /// **The block is the links said again and never a second copy that drifts.**
    /// </summary>
    /// <remarks>
    /// Filled whole rather than by the one template that moved, so there is no arrangement of
    /// events that leaves the block holding something the links do not say. The list is the same
    /// object throughout, so anything holding the block still holds it.
    /// </remarks>
    [Fact]
    public void The_block_is_the_links_said_again()
    {
        var bench = new Bench();

        var held = bench.Block.Templates;

        bench.Links.Add(Knob("nanoKONTROL2", "machine.oddskilla", 16));
        bench.Filling.Fill(said: true);

        bench.Links.Add(Knob("MiniLab3", "machine.ouroboros", 74));
        bench.Filling.Fill(said: true);

        Assert.Same(held, bench.Block.Templates);
        Assert.Equal(2, bench.Block.Templates.Count);
        Assert.Equal(2, bench.Said);
    }

    /// <summary>Nothing pointed anywhere fills nothing and still says so, since something moved.</summary>
    [Fact]
    public void Nothing_pointed_anywhere_still_says_it_moved()
    {
        var bench = new Bench();

        bench.Filling.Fill(said: true);

        Assert.Empty(bench.Block.Templates);
        Assert.Equal(1, bench.Said);
    }
}
