using System;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Config.Interfaces;

namespace JingleBox2.Config;

/// <summary>
/// What the input is set to this run: the source, whether it is heard, and what we play out of.
/// </summary>
/// <remarks>
/// **Not kept, and that is the whole reason it is a block of its own.** The two facts in it are
/// about this session: a source chosen is somebody's browser unplugged from its own speakers, and
/// Hear it is a microphone let into the mix. Either of them coming back on its own at the next
/// start would be the application doing something that morning that nobody had asked for.
///
/// A wrapper for the reason the settings are one: what a block says about itself is not a thing
/// to write into the document it is about.
/// </remarks>
public sealed class InputBlock : IMemoryBlock
{
    /// <summary>What the pages write and the routing watches.</summary>
    public IInputSetting Input { get; }

    /// <summary>Names the setting this run is working over.</summary>
    /// <param name="input">The setting, which starts empty at every start.</param>
    public InputBlock(IInputSetting input) => Input = input;

    /// <inheritdoc/>
    public string Name => "Input";

    /// <inheritdoc/>
    public bool Kept => false;

    /// <inheritdoc/>
    /// <remarks>
    /// The setting's own, passed straight through: there is one fact in this block and the thing
    /// holding it already says when it moves. A second event beside it would be a second thing to
    /// raise and a second thing to forget.
    /// </remarks>
    public event Action? Changed
    {
        add => Input.Changed += value;
        remove => Input.Changed -= value;
    }
}
