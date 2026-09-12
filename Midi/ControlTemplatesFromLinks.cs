using System;
using System.Collections.Generic;
using JingleBox2.Controllers.Interfaces;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <inheritdoc/>
public sealed class ControlTemplatesFromLinks : IControlTemplatesFromLinks
{
    /// <summary>The block this fills.</summary>
    private readonly IControlTemplateBlock _block;

    /// <summary>Where the links are, read afresh each time rather than held.</summary>
    private readonly Func<IEnumerable<ControlMapping>> _links;

    /// <summary>The one rule for cutting links into templates.</summary>
    private readonly IControlTemplates _templates;

    /// <summary>What is known about the controllers, for what a box and its controls are called.</summary>
    private readonly IControllerProfiles _profiles;

    /// <summary>Names what fills what.</summary>
    /// <param name="block">The block to fill.</param>
    /// <param name="links">Where the links are, asked each time.</param>
    /// <param name="profiles">
    /// What is known about the controllers plugged in. Handed in rather than made, since what a
    /// device has been seen doing is remembered in it and a second one answers for a device it
    /// has never heard speak.
    /// </param>
    /// <param name="templates">How links are cut into templates, defaulted to the real rule.</param>
    public ControlTemplatesFromLinks(
        IControlTemplateBlock block,
        Func<IEnumerable<ControlMapping>> links,
        IControllerProfiles profiles,
        IControlTemplates? templates = null)
    {
        _block = block;
        _links = links;
        _profiles = profiles;
        _templates = templates ?? new ControlTemplates();
    }

    /// <inheritdoc/>
    public void Fill(bool said)
    {
        var read = _templates.Cut(
            _links(),
            port => _profiles.Called(port),
            (port, channel, cc) => _profiles.Named(port, channel, cc));

        _block.Templates.Clear();
        _block.Templates.AddRange(read);

        if (said) _block.Moved();
    }
}
