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

    /// <summary>What is carrying audio, or nothing where nobody can say.</summary>
    private readonly IAudioFlowing? _flowing;

    /// <summary>Which cables that makes live.</summary>
    private readonly IPatchFlow _flow;

    /// <summary>Where the blocks were left, or nothing where nobody is keeping that.</summary>
    private readonly IPatchPlaces? _places;

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
    /// <param name="places">Where the blocks were left last time, or nothing to use the graph's own.</param>
    /// <param name="flowing">What is carrying audio, for the cables that are drawn solid.</param>
    /// <param name="graph">What blocks and cables those make.</param>
    /// <param name="flow">Which cables that makes live.</param>
    public PatchbayViewModel(
        IInputSource input,
        IOutputChosen? output = null,
        IPatchPlaces? places = null,
        IAudioFlowing? flowing = null,
        IPatchGraph? graph = null,
        IPatchFlow? flow = null)
    {
        _input = input;
        _output = output;
        _places = places;
        _flowing = flowing;
        _graph = graph ?? new PatchGraph();
        _flow = flow ?? new PatchFlow();

        _input.Routes.CollectionChanged += Changed;

        if (_input is INotifyPropertyChanged told) told.PropertyChanged += Told;
        if (_output is INotifyPropertyChanged said) said.PropertyChanged += Heard;

        Read();
    }

    /// <summary>The blocks, swapped whole whenever the machine is read again.</summary>
    [ObservableProperty] private IReadOnlyList<PatchNode> nodes = Array.Empty<PatchNode>();

    /// <summary>The cables, swapped with them.</summary>
    [ObservableProperty] private IReadOnlyList<PatchLink> links = Array.Empty<PatchLink>();

    /// <summary>The cables that are carrying audio right now, out of the ones drawn.</summary>
    /// <remarks>
    /// Its own list rather than a mark on each cable, because this moves many times a second and
    /// the cables do not: swapped whole, the picture redraws its wires and nothing else.
    /// </remarks>
    [ObservableProperty] private IReadOnlyList<PatchLink> live = Array.Empty<PatchLink>();

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

        Nodes = Laid(scene.Nodes);
        Links = scene.Links;

        Pulse();

        if (Selected is not { } picked) return;

        Selected = Find(picked.Id);
    }

    /// <summary>Asks the routing to read the graph again, for a page that has just been opened.</summary>
    public void Refresh() => _input.RefreshRoutes();

    /// <summary>
    /// Works out which cables are carrying audio, for the page's own meter clock to call.
    /// </summary>
    /// <remarks>
    /// Told rather than keeping a clock of its own: the mixer already runs one at the rate its
    /// meters want, and a second timer on a page nobody is looking at is exactly what this
    /// codebase took off the master's meter once already. With nothing able to say what is
    /// sounding, every cable is drawn as it always was.
    /// </remarks>
    public void Pulse()
    {
        if (_flowing == null) return;

        Live = _flow.Live(Links, _flowing.Signals);
    }

    /// <summary>
    /// Puts each block where it was left, where anybody has moved it.
    /// </summary>
    /// <remarks>
    /// Over the graph's own arrangement rather than instead of it, so a block that has never
    /// been touched opens where it was meant to and one that has opens where you put it. A
    /// block added to this application later needs no entry and no migration.
    /// </remarks>
    /// <param name="read">The blocks as the graph laid them out.</param>
    private IReadOnlyList<PatchNode> Laid(IReadOnlyList<PatchNode> read)
    {
        if (_places == null) return read;

        var laid = new List<PatchNode>(read.Count);

        foreach (var node in read)
        {
            laid.Add(_places.Placed(node.Id, out double x, out double y)
                ? node with { X = x, Y = y }
                : node);
        }

        return laid;
    }

    /// <summary>Writes down where a block has been left, so it is there again tomorrow.</summary>
    /// <remarks>
    /// Told by the surface once the hand has let go, so a drag is one line written. The picture
    /// is not read again afterwards: the block is already where it was put, and rebuilding it
    /// under the pointer would be a page that jumps as you let go of it.
    /// </remarks>
    /// <param name="node">Which block, by its id.</param>
    /// <param name="x">How far across it was left.</param>
    /// <param name="y">How far down.</param>
    public void Place(string node, double x, double y) => _places?.Place(node, x, y);

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
