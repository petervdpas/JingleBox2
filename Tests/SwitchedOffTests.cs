using System.Collections.Generic;
using JingleBox2.Audio.Plugins;
using JingleBox2.ViewModels;
using JingleBox2.ViewModels.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A device switched off on a chain lets go of its window, and switching it back on does not
/// open one.
/// </summary>
public class SwitchedOffTests
{
    /// <summary>A block on a chain of its own, over a plugin that does nothing.</summary>
    private static (PluginChainViewModel Chain, PluginSlotViewModel Block) Block()
    {
        var chain = new PluginChainViewModel(new PluginLibraryViewModel());
        var slot = new PluginChain().Add(new SongChainFollowsTests.Held());

        return (chain, new PluginSlotViewModel(chain, (SongChainFollowsTests.Held)slot.Insert, slot));
    }

    /// <summary>Switching a block off is said to whatever shows it, once, and on again is not.</summary>
    [Fact]
    public void Switching_off_lets_go_and_switching_on_does_not()
    {
        var (chain, block) = Block();
        var closing = new List<IChainSlot>();
        chain.DeviceClosing += closing.Add;

        block.IsBypassed = true;

        Assert.Equal([block], closing);

        block.IsBypassed = true;
        block.IsBypassed = false;

        Assert.Equal([block], closing);
    }
}
