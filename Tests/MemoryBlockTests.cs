using System.Linq;
using JingleBox2.Audio.Routing;
using JingleBox2.Audio.Routing.Records;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Config;
using JingleBox2.Config.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What this run knows, in one place, and which of it survives the run.
/// </summary>
/// <remarks>
/// The first step of <c>docs/memory-blocks.md</c>: one manager, built where the settings are
/// read and handed to whoever needs a block, so that no page owns a fact about the machine.
///
/// **What is asked here is the one rule that must not bend**, which is that a block says for
/// itself whether it is written down. A writer walks the blocks and never sees the input's, so
/// the source somebody chose and the monitor they switched on cannot be written by accident
/// however the document is reshaped later.
/// </remarks>
public sealed class MemoryBlockTests
{
    /// <summary>A source of the kind somebody lines up before a show.</summary>
    private static readonly AudioRoute Browser = new("Firefox", "Firefox", AudioRouteKind.Application);

    /// <summary>The settings are what is written down.</summary>
    [Fact]
    public void The_settings_are_kept()
    {
        IMemoryBlocks blocks = new MemoryBlocks(new AppConfig());

        Assert.True(blocks.Blocks.Single(block => block.Name == "Settings").Kept);
    }

    /// <summary>
    /// **And what the input is set to never is.**
    /// </summary>
    /// <remarks>
    /// An application that started with somebody's browser already unplugged from its own
    /// speakers, or a microphone already open into the mix, would be doing something that nobody
    /// had asked for that morning.
    /// </remarks>
    [Fact]
    public void What_the_input_is_set_to_is_not_kept()
    {
        IMemoryBlocks blocks = new MemoryBlocks(new AppConfig());

        Assert.False(blocks.Blocks.Single(block => block.Name == "Input").Kept);
    }

    /// <summary>Everything it holds is a block, so a writer that walks them misses nothing.</summary>
    [Fact]
    public void Everything_it_holds_says_what_it_is()
    {
        IMemoryBlocks blocks = new MemoryBlocks(new AppConfig());

        Assert.Equal(2, blocks.Blocks.Count);
        Assert.DoesNotContain(blocks.Blocks, block => string.IsNullOrWhiteSpace(block.Name));
    }

    /// <summary>The settings it hands out are the document that was read, not a copy of it.</summary>
    /// <remarks>
    /// Everything is holding one object by reference, which is what makes a setting changed on
    /// one page true on every other.
    /// </remarks>
    [Fact]
    public void The_settings_are_the_document_that_was_read()
    {
        var settings = new AppConfig();

        IMemoryBlocks blocks = new MemoryBlocks(settings);

        Assert.Same(settings, blocks.Settings.Config);
    }

    /// <summary>The input starts empty at every start, which is the point of not keeping it.</summary>
    [Fact]
    public void The_input_starts_with_nothing_chosen()
    {
        IMemoryBlocks blocks = new MemoryBlocks(new AppConfig());

        Assert.Null(blocks.Input.Source);
        Assert.False(blocks.Input.Heard);
    }

    /// <summary>And what one holder writes, every holder reads.</summary>
    /// <remarks>
    /// The whole of what handing one down buys: RECORD and the mixer's IN strip are two views of
    /// one fact rather than two facts that have to be kept in step.
    /// </remarks>
    [Fact]
    public void What_one_writes_every_holder_reads()
    {
        IMemoryBlocks blocks = new MemoryBlocks(new AppConfig());

        var said = 0;

        blocks.Input.Changed += () => said++;

        blocks.Input.Say(Browser, heard: false, playingOut: "Speakers");

        Assert.Equal(1, said);
        Assert.Equal(Browser.Node, blocks.Input.Source?.Node);
    }

    /// <summary>A setting handed in is the one it works over, for a run that already has one.</summary>
    [Fact]
    public void An_input_handed_in_is_the_one_it_holds()
    {
        var input = new InputSetting();

        IMemoryBlocks blocks = new MemoryBlocks(new AppConfig(), input);

        Assert.Same(input, blocks.Input);
    }
}
