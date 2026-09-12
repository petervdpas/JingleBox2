using System.Collections.Generic;
using JingleBox2.Config.Interfaces;
using JingleBox2.Midi;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The block the control templates live in, which is what everything else reads them from.
/// </summary>
/// <remarks>
/// What a block promises is small and every promise is load bearing somewhere else: it holds the
/// one list, it says when it moved, and it answers whether it survives the run. A writer walks
/// the blocks and asks; a page writes and says so; nothing asks a page when the disc should be
/// touched.
/// </remarks>
public sealed class ControlTemplateBlockTests
{
    /// <summary>One controller's layout for one machine, as a file holds it.</summary>
    private static ControlTemplate Template(string controller, string machine) => new()
    {
        Controller = controller,
        Target = new ControlTemplateTarget { Kind = "sounddevice", Id = machine, Name = machine },
        Controls = new List<ControlTemplateControl>
        {
            new() { Cc = 74, Channel = 1, Parameter = "cutoff", Name = "Cutoff" },
        },
    };

    /// <summary>A fresh installation has none, and that is a list rather than nothing.</summary>
    /// <remarks>
    /// Nothing downstream should have to ask whether there are any before it may look, since the
    /// first morning is exactly when a fault in that path would be invisible.
    /// </remarks>
    [Fact]
    public void A_fresh_installation_holds_an_empty_list()
    {
        var block = new ControlTemplateBlock();

        Assert.NotNull(block.Templates);
        Assert.Empty(block.Templates);
    }

    /// <summary>What was read at startup is what the block holds, as the same object.</summary>
    /// <remarks>
    /// The same object rather than a copy, which is the rule the settings block already keeps:
    /// two copies of one list is two answers to what a controller is doing.
    /// </remarks>
    [Fact]
    public void What_was_read_is_what_it_holds()
    {
        var read = new List<ControlTemplate> { Template("nanoKONTROL2", "machine.oddskilla") };

        var block = new ControlTemplateBlock(read);

        Assert.Same(read, block.Templates);
    }

    /// <summary>Saying it moved is what a writer hears.</summary>
    [Fact]
    public void Saying_it_moved_is_heard()
    {
        int heard = 0;

        var block = new ControlTemplateBlock();

        block.Changed += () => heard++;

        block.Templates.Add(Template("MiniLab3", "machine.ouroboros"));
        block.Moved();

        Assert.Equal(1, heard);
    }

    /// <summary>And nobody listening is not a fault, since the hint is not the whole truth.</summary>
    /// <remarks>
    /// A block is written to at startup before anything has subscribed, and a page may say it
    /// moved with no writer behind it at all, which is every test in this suite.
    /// </remarks>
    [Fact]
    public void Nobody_listening_is_not_a_fault()
    {
        var block = new ControlTemplateBlock();

        block.Moved();

        Assert.Empty(block.Templates);
    }

    /// <summary>It is one of the blocks, and says the two things a block says.</summary>
    [Fact]
    public void It_is_a_block()
    {
        IMemoryBlock block = new ControlTemplateBlock();

        Assert.Equal("Control templates", block.Name);
        Assert.True(block.Kept);
    }

    /// <summary>
    /// **Kept, unlike the input's block**, which is the answer that decides whether a writer
    /// ever sees it.
    /// </summary>
    /// <remarks>
    /// A template is a fact about your hardware and the thing it is pointed at, true of every
    /// song and worth still being there tomorrow. What the input is pointed at is true of this
    /// session alone, which is why that one answers no.
    /// </remarks>
    [Fact]
    public void A_template_survives_the_run() => Assert.True(new ControlTemplateBlock().Kept);
}
