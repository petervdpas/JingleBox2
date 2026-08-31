using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using JingleBox2.Machines.Ui;
using JingleBox2.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace JingleBox2.Views;

/// <summary>
/// The knobs for one plugin. Nothing of its own except which of them a controller is pointed at.
/// </summary>
/// <remarks>
/// This is the panel the host draws for a plugin that has no window of its own, so these are
/// our controls and the pointer can be used on them the way it is used on a machine's. A plugin
/// that does draw its own window is the other case entirely: see
/// <see cref="PluginControlsViewModel"/>, where the knob you touched is the offer, because in
/// somebody else's window the pointer is not ours to read.
/// </remarks>
public partial class PluginParameters : UserControl
{
    /// <summary>
    /// Builds the panel, and says it is somewhere a hardware knob can be pointed.
    /// </summary>
    /// <remarks>
    /// Both pointer handlers are tunnelled, because a knob takes the move to be dragged and
    /// would swallow it before this page ever heard about it.
    ///
    /// <see cref="LinkKey"/>.Watch puts the panel in the tally of places worth entering the
    /// other mouse mode for, which is what makes Ctrl+Shift+M mean anything here.
    ///
    /// The link list is subscribed to while the panel is on screen and let go of when it
    /// leaves, so a panel nobody can see is not repainting rings.
    /// </remarks>
    public PluginParameters()
    {
        InitializeComponent();

        AddHandler(PointerMovedEvent, Offers, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, Pressed, RoutingStrategies.Tunnel);

        LinkKey.Watch(this);

        AttachedToVisualTree += (_, _) =>
        {
            if (Midi.ControlLink.Current is { } link) link.Changed += ShowLinks;

            ShowLinks();
        };

        DetachedFromVisualTree += (_, _) =>
        {
            if (Midi.ControlLink.Current is { } link) link.Changed -= ShowLinks;
        };
    }

    /// <summary>What the pointer last came to rest on, which is what the glow is around.</summary>
    private Knob? _offered;

    /// <summary>The plugin whose knobs these are, or nothing when the panel has been let go of.</summary>
    private PluginControlsViewModel? Controls => DataContext as PluginControlsViewModel;

    /// <summary>The knob under the pointer, or nothing when the pointer is between them.</summary>
    private Knob? DialAt(Point at)
    {
        foreach (var dial in Dials.GetVisualDescendants().OfType<Knob>())
        {
            if (dial.TranslatePoint(default, this) is not { } corner) continue;

            if (new Rect(corner, dial.Bounds.Size).Contains(at)) return dial;
        }

        return null;
    }

    /// <summary>
    /// Resting the pointer on a knob offers that parameter to whatever is touched next on the
    /// desk.
    /// </summary>
    /// <remarks>
    /// Between knobs, what was offered is still offered. That is deliberate: pointing a knob
    /// means looking down at the desk to find the control, and an offer that expired the moment
    /// the pointer left the dial would be an offer nobody could ever take up.
    /// </remarks>
    private void Offers(object? sender, PointerEventArgs e)
    {
        if (Midi.ControlLink.Current is not { IsLinking: true } link) return;

        var dial = DialAt(e.GetPosition(this));

        if (dial is null || ReferenceEquals(dial, _offered)) return;
        if (dial.DataContext is not PluginParameterViewModel parameter) return;
        if (Controls is not { } controls) return;

        _offered = dial;

        ShowLinks();

        link.Offer(new Midi.ControlMapping
        {
            Kind = Midi.Enums.ControlKind.Insert,
            Scope = Midi.Enums.ControlScope.Focused,
            Plugin = controls.Plugin.Info.Id,
            Parameter = parameter.Id,
            Owner = controls.Plugin.Info.Name,
            Name = controls.Plugin.Info.Name + " " + parameter.Name
        }, keep: true);
    }

    /// <summary>
    /// A press while the controller is being laid out takes a link off, and never turns a knob.
    /// </summary>
    /// <remarks>
    /// Handled before anything else sees it whether or not it landed on a knob, since a press
    /// in this mode is never about the value: a stray click that moved a parameter would be a
    /// change nobody meant and nobody would notice.
    /// </remarks>
    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (Midi.ControlLink.Current is not { IsLinking: true } link) return;

        e.Handled = true;

        if (DialAt(e.GetPosition(this)) is not { DataContext: PluginParameterViewModel parameter }) return;
        if (Controls is not { } controls) return;

        link.UnlinkPlugin(controls.Plugin.Info.Id, parameter.Id);
    }

    /// <summary>Puts the glow where the pointer is and a quiet ring on everything already taken.</summary>
    private void ShowLinks()
    {
        var link = Midi.ControlLink.Current;

        if (link is not { IsLinking: true } || Controls is not { } controls)
        {
            _offered = null;
            Glow.Showing(null, System.Array.Empty<Rect>());
            return;
        }

        var taken = link.ParametersOn(controls.Plugin.Info.Id);
        var rings = new List<Rect>();
        Rect? offered = null;

        foreach (var dial in Dials.GetVisualDescendants().OfType<Knob>())
        {
            if (dial.TranslatePoint(default, this) is not { } corner) continue;

            var area = new Rect(corner, dial.Bounds.Size);

            if (ReferenceEquals(dial, _offered)) { offered = area; continue; }

            if (dial.DataContext is PluginParameterViewModel one && taken.Contains(one.Id)) rings.Add(area);
        }

        Glow.Showing(offered, rings);
    }
}
