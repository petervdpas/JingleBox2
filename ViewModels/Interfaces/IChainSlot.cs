using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio.Plugins;
using JingleBox2.ViewModels.Records;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// One box on a chain, whichever world it came out of.
/// </summary>
/// <remarks>
/// A track's chain holds two kinds of thing that are the same thing to the person reading it: an
/// effect of ours and somebody else's plugin. Both have a name, a few readings on the front, a
/// power switch that leaves them where they are, and a window with their controls in it. So the
/// strip draws one block and the difference is behind it.
///
/// It is named in XAML, which is why it is a contract rather than a shared base class: a compiled
/// binding needs a type, one template cannot have two, and a block drawn twice would be two
/// templates drifting apart. The rack's rows are the same arrangement for the same reason.
/// </remarks>
public interface IChainSlot
{
    /// <summary>What it calls itself, which is the name on the block.</summary>
    string Name { get; }

    /// <summary>Which world it came out of: VST3, CLAP, or ours.</summary>
    /// <remarks>
    /// Worth printing because two plugins of the same name in two standards are two different
    /// pieces of software, and because an effect of ours is not somebody else's program and
    /// should not have to be opened to find that out.
    /// </remarks>
    string Format { get; }

    /// <summary>Who made it, when they said, shown where there are no readings.</summary>
    string Vendor { get; }

    /// <summary>The first few of its controls, printed on the block itself.</summary>
    /// <remarks>
    /// The point of a chain you can read: what a device is set to, without opening it. A chain of
    /// four boxes with names on them tells you the order of the effects and nothing about the
    /// sound.
    /// </remarks>
    IReadOnlyList<ControlReading> Summary { get; }

    /// <summary>True when there is anything to print, since some devices declare nothing.</summary>
    bool HasSummary { get; }

    /// <summary>Its place in the chain, which is what carries the bypass.</summary>
    PluginChain.Slot Device { get; }

    /// <summary>Stepped over while true, and left exactly where it is.</summary>
    bool IsBypassed { get; set; }

    /// <summary>True while its own window is up, which the block is drawn brighter for.</summary>
    bool IsOpen { get; set; }

    /// <summary>Takes it out of the chain.</summary>
    IRelayCommand RemoveCommand { get; }

    /// <summary>Moves it one place earlier in the chain, where there is one.</summary>
    IRelayCommand MoveLeftCommand { get; }

    /// <summary>And one place later.</summary>
    IRelayCommand MoveRightCommand { get; }

    /// <summary>Reads what it is set to again, after something moved a control.</summary>
    void Reread();
}
