using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Controllers;
using JingleBox2.Controllers.Interfaces;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Midi.Records;

namespace JingleBox2.Midi;

/// <inheritdoc/>
/// <remarks>
/// One of these per thing a controller can be pointed at, holding nothing but which thing it is:
/// the templates are the block's, the links are the desk's and the ports are the machine's, and
/// all three are asked for each time rather than kept. So one made before the application had
/// finished starting still answers, and one made over a page that is later shown a different
/// device answers for whatever it is showing now.
/// </remarks>
public sealed class ControlExchange : IControlExchange
{
    /// <summary>Which one this is about, asked rather than held.</summary>
    /// <remarks>
    /// A face outlives what it draws: the rack shows whichever device is picked and a track's
    /// panel whichever machine the track plays, with the page and its objects untouched. Held,
    /// this would go on answering for the device before it.
    /// </remarks>
    private readonly Func<string> _which;

    /// <summary>Where the links live, and what carries the block and the ports.</summary>
    private readonly Func<ControlLink?> _desk;

    /// <summary>How a template becomes links again.</summary>
    private readonly IControlTemplates _templates;

    /// <summary>What is known about the controllers plugged in. Shared, since it remembers.</summary>
    private readonly IControllerProfiles _profiles;

    /// <summary>What a target is called, so this cuts the links as a card and a file do.</summary>
    private readonly ILinkTargets _naming;

    /// <param name="which">
    /// Which one, by the id songs and templates write down, or nothing for every one of this
    /// kind, which is what the mixer and the pads want.
    /// </param>
    /// <param name="kind">Which sort of thing, in the word the naming rule uses.</param>
    /// <param name="desk">Where the links live. Left out, the one the application set up.</param>
    /// <param name="templates">How a template becomes links again.</param>
    /// <param name="profiles">
    /// What is known about the controllers plugged in. Left out, one of its own; the application
    /// hands the same one to everything, since what a device is doing is remembered in it.
    /// </param>
    /// <param name="naming">What a target is called, shared with the page so the two agree.</param>
    public ControlExchange(
        Func<string> which,
        string kind = LinkTargets.SoundDevice,
        Func<ControlLink?>? desk = null,
        IControlTemplates? templates = null,
        IControllerProfiles? profiles = null,
        ILinkTargets? naming = null)
    {
        _which = which;
        Kind = kind;
        _desk = desk ?? (() => ControlLink.Current);
        _templates = templates ?? new ControlTemplates();
        _profiles = profiles ?? new ControllerProfiles();
        _naming = naming ?? new LinkTargets();
    }

    /// <inheritdoc/>
    public string Kind { get; }

    /// <inheritdoc/>
    public string Id => _which() ?? "";

    /// <summary>
    /// Whether this is about one particular thing rather than a whole kind.
    /// </summary>
    /// <remarks>
    /// Asked of the naming rule rather than answered here, which is why the pads worked the day
    /// they arrived: a second kind that names nothing turned up, and anything keeping its own
    /// list of which those are would have gone on offering an empty flyout while the page beside
    /// it drew the card.
    /// </remarks>
    private bool Names => !_naming.Whole(Kind);

    /// <inheritdoc/>
    public IReadOnlyList<ControlTemplate> Offered()
    {
        if (_desk() is not { Templates: { } block }) return Array.Empty<ControlTemplate>();

        string id = Id;

        if (Names && id.Length == 0) return Array.Empty<ControlTemplate>();

        return block.Templates.Where(one => Mine(one, id)).ToList();
    }

    /// <summary>Whether that template is one of this hook's.</summary>
    /// <remarks>
    /// Against what the template says it is about rather than against a link inside it, since
    /// that is what a template carries and what a file written by hand would say.
    /// </remarks>
    /// <param name="one">The template to place.</param>
    /// <param name="id">Which one this is about, or nothing for every one of its kind.</param>
    private bool Mine(ControlTemplate one, string id) =>
        one.Target is { } target
        && string.Equals(target.Kind, Kind, StringComparison.Ordinal)
        && (!Names || string.Equals(target.Id, id, StringComparison.Ordinal));

    /// <inheritdoc/>
    public ControlTemplateReading Take(ControlTemplate? template)
    {
        if (_desk() is not { } link)
            return new ControlTemplateReading(
                Array.Empty<ControlMapping>(), 0, template?.Controller ?? "", false);

        var reading = _templates.Take(template, link.Ports?.Invoke(), port => _profiles.Called(port));

        link.Take(reading.Links);

        return reading;
    }

    /// <inheritdoc/>
    public bool Wired(ControlMapping? control) => _desk()?.Holds(control) ?? false;
}
