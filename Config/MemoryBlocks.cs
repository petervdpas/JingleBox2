using System.Collections.Generic;
using JingleBox2.Audio.Routing;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Config.Interfaces;

namespace JingleBox2.Config;

/// <inheritdoc/>
public sealed class MemoryBlocks : IMemoryBlocks
{
    /// <summary>The settings, wrapped so they can say they are kept and say when they move.</summary>
    private readonly SettingsBlock _settings;

    /// <summary>And the input, wrapped so it can say it is not.</summary>
    private readonly InputBlock _input;

    /// <summary>
    /// Holds what this run knows, over the settings that were read at startup.
    /// </summary>
    /// <remarks>
    /// The input's own setting is made here rather than handed in, since nothing before this
    /// moment has one: it starts empty at every start, which is the point of it.
    /// </remarks>
    /// <param name="settings">The document read from the settings file.</param>
    /// <param name="input">What the input is set to, defaulted to a fresh one.</param>
    public MemoryBlocks(AppConfig settings, IInputSetting? input = null)
    {
        _settings = new SettingsBlock(settings);
        _input = new InputBlock(input ?? new InputSetting());
    }

    /// <inheritdoc/>
    public ISettingsBlock Settings => _settings;

    /// <inheritdoc/>
    public IInputSetting Input => _input.Input;

    /// <inheritdoc/>
    public IReadOnlyList<IMemoryBlock> Blocks => new IMemoryBlock[] { _settings, _input };
}
