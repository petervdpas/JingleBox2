using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Audio.Routing.Records;
using JingleBox2.UI;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;
using JingleBox2.ViewModels.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// The patchbay under the mixer: what can feed this application, what is feeding it, and the
/// cables between.
/// </summary>
/// <remarks>
/// **It holds a picture and hands decisions on.** What a cable means is the routing's business,
/// so plugging one in is <see cref="IInputSource.SelectedRoute"/> being set and nothing else:
/// the same act as picking the source at the foot of the IN strip, since they are one choice and
/// this codebase has already paid for having two ways to make one.
///
/// Read again rather than edited in place. The list of sources changes under it constantly, a
/// program appearing the moment it plays a sound, so what the picture holds is swapped whole
/// each time: a scene half old and half new is a cable naming a block that is no longer there.
/// </remarks>
public sealed partial class PatchbayViewModel : ObservableObject
{
    /// <summary>Where the sources come from and where a choice is made.</summary>
    private readonly IInputSource _input;

    /// <summary>What turns those into blocks and cables.</summary>
    private readonly IPatchGraph _graph;

    /// <summary>Where the mix leaves, or nothing where nobody has said.</summary>
    /// <remarks>
    /// Optional, so a patchbay can be built and put a question to without an engine: what it
    /// costs is the last block being called Output rather than by its name.
    /// </remarks>
    private readonly IOutputChosen? _output;

    /// <summary>
    /// Takes what feeds the recorder, and what draws it.
    /// </summary>
    /// <remarks>
    /// The graph is defaulted so a page can be built without one and a test can hand in its own,
    /// which is the rule every seam here follows.
    /// </remarks>
    /// <param name="input">The sources, and the one being taken.</param>
    /// <param name="output">Where the mix leaves through, for the block at the end of the path.</param>
    /// <param name="graph">What blocks and cables those make.</param>
    public PatchbayViewModel(IInputSource input, IOutputChosen? output = null, IPatchGraph? graph = null)
    {
        _input = input;
        _output = output;
        _graph = graph ?? new PatchGraph();

        _input.Routes.CollectionChanged += Changed;

        if (_input is INotifyPropertyChanged told) told.PropertyChanged += Told;
        if (_output is INotifyPropertyChanged said) said.PropertyChanged += Heard;

        Read();
    }

    /// <summary>The blocks, swapped whole whenever the machine is read again.</summary>
    [ObservableProperty] private IReadOnlyList<PatchNode> nodes = Array.Empty<PatchNode>();

    /// <summary>The cables, swapped with them.</summary>
    [ObservableProperty] private IReadOnlyList<PatchLink> links = Array.Empty<PatchLink>();

    /// <summary>Which block the sidebar is about, or nothing while none is picked.</summary>
    [ObservableProperty] private PatchNode? selected;

    /// <summary>What the last gesture did, for the line under the sidebar.</summary>
    [ObservableProperty] private string says = "";

    /// <summary>Reads the machine again and swaps the picture for what it says.</summary>
    /// <remarks>
    /// The picked block is looked up again by id rather than kept, since the block object is new
    /// on every reading: kept, the sidebar would go on describing a block nobody can see.
    /// </remarks>
    public void Read()
    {
        var scene = _graph.Read(_input.Routes, _input.SelectedRoute, _output?.SelectedOutputDevice?.Name);

        Nodes = scene.Nodes;
        Links = scene.Links;

        if (Selected is not { } picked) return;

        Selected = Find(picked.Id);
    }

    /// <summary>Asks the routing to read the graph again, for a page that has just been opened.</summary>
    public void Refresh() => _input.RefreshRoutes();

    /// <summary>
    /// A cable was dropped on our own input, so that source is what the recorder takes.
    /// </summary>
    /// <remarks>
    /// Only a cable landing on us means anything, since every block that is not ours belongs to
    /// somebody else's program: joining two of those would be rewiring the machine around us,
    /// which is the one thing a patchbay drawn from our own point of view deliberately cannot do.
    /// </remarks>
    /// <param name="link">The cable, as the surface made it.</param>
    public void Plug(PatchLink link)
    {
        if (!string.Equals(link.To.Node, _graph.OwnNode, StringComparison.Ordinal))
        {
            Says = "Only a cable into JingleBox2 changes anything here.";
            return;
        }

        var route = Route(link.From.Node);

        if (route == null)
        {
            Says = "That source is not there any more.";
            Read();

            return;
        }

        _input.SelectedRoute = route;
        Says = $"Taking audio from {route.Display}.";
    }

    /// <summary>
    /// A cable was pulled out, which cannot be done yet.
    /// </summary>
    /// <remarks>
    /// **Said rather than silently ignored.** Choosing a source is one call and taking the input
    /// off everything is another, and the routing has no such call today: the sound server wires
    /// a capture to its own default and nothing here undoes that. So the gesture is answered with
    /// what it would take rather than with a cable that springs back for no visible reason.
    /// </remarks>
    /// <param name="link">The cable that was pulled out.</param>
    public void Unplug(PatchLink link)
    {
        Says = "Nothing is taken off yet: pick another source instead.";

        Read();
    }

    /// <summary>The route with that address, or nothing where it has stopped playing.</summary>
    private AudioRoute? Route(string node)
    {
        foreach (var route in _input.Routes)
            if (string.Equals(route.Node, node, StringComparison.Ordinal)) return route;

        return null;
    }

    /// <summary>The block with that id in the picture as it now is.</summary>
    private PatchNode? Find(string id)
    {
        foreach (var node in Nodes)
            if (string.Equals(node.Id, id, StringComparison.Ordinal)) return node;

        return null;
    }

    /// <summary>The list of sources moved, so the picture is read again.</summary>
    private void Changed(object? sender, NotifyCollectionChangedEventArgs e) => Read();

    /// <summary>The chosen source moved, which is the one property here that draws a cable.</summary>
    private void Told(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IInputSource.SelectedRoute)) Read();
    }

    /// <summary>The output moved, which renames the block at the end of our own path.</summary>
    private void Heard(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IOutputChosen.SelectedOutputDevice)) Read();
    }
}
