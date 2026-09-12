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

    /// <summary>And the templates, which are what a controller does to everything.</summary>
    private readonly Midi.ControlTemplateBlock _templates;

    /// <summary>And the links, which are what the templates are made of and what is stored.</summary>
    private readonly Midi.ControlLinkBlock _links;

    /// <summary>
    /// Holds what this run knows, over the settings that were read at startup.
    /// </summary>
    /// <remarks>
    /// The input's own setting is made here rather than handed in, since nothing before this
    /// moment has one: it starts empty at every start, which is the point of it.
    /// </remarks>
    /// <param name="settings">The document read from the settings file.</param>
    /// <param name="input">What the input is set to, defaulted to a fresh one.</param>
    /// <param name="templates">
    /// The templates read at startup, or nothing for an installation that has none yet. Almost
    /// always nothing, since they are worked out from the links once both they and the controller
    /// profiles exist.
    /// </param>
    /// <param name="links">
    /// The links read at startup, or nothing for an installation with nothing pointed at
    /// anything.
    /// </param>
    public MemoryBlocks(
        AppConfig settings,
        IInputSetting? input = null,
        List<Midi.ControlTemplate>? templates = null,
        List<Midi.ControlMapping>? links = null)
    {
        _settings = new SettingsBlock(settings);
        _input = new InputBlock(input ?? new InputSetting());
        _templates = new Midi.ControlTemplateBlock(templates);
        _links = new Midi.ControlLinkBlock(links);
    }

    /// <inheritdoc/>
    public ISettingsBlock Settings => _settings;

    /// <inheritdoc/>
    public IInputSetting Input => _input.Input;

    /// <inheritdoc/>
    public Midi.Interfaces.IControlTemplateBlock Templates => _templates;

    /// <inheritdoc/>
    public Midi.Interfaces.IControlLinkBlock Links => _links;

    /// <inheritdoc/>
    public IReadOnlyList<IMemoryBlock> Blocks =>
        new IMemoryBlock[] { _settings, _input, _templates, _links };
}
