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
    public PluginParameters()
    {
        InitializeComponent();

        AddHandler(PointerMovedEvent, Offers, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, Pressed, RoutingStrategies.Tunnel);

        // There is something to point at while this is on screen, which is what makes
        // Ctrl+Shift+M mean anything. See LinkKey.
        AttachedToVisualTree += (_, _) =>
        {
            LinkKey.Showing();

            if (Midi.ControlLink.Current is { } link) link.Changed += ShowLinks;

            ShowLinks();
        };

        DetachedFromVisualTree += (_, _) =>
        {
            LinkKey.Gone();

            if (Midi.ControlLink.Current is { } link) link.Changed -= ShowLinks;
        };
    }

    /// <summary>What the pointer last came to rest on, which is what the glow is around.</summary>
    private Knob? _offered;

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

    private void Offers(object? sender, PointerEventArgs e)
    {
        if (Midi.ControlLink.Current is not { IsLinking: true } link) return;

        var dial = DialAt(e.GetPosition(this));

        // Between knobs. What was offered is still offered, so you can look down at the desk.
        if (dial is null || ReferenceEquals(dial, _offered)) return;
        if (dial.DataContext is not PluginParameterViewModel parameter) return;
        if (Controls is not { } controls) return;

        _offered = dial;

        ShowLinks();

        link.Offer(new Midi.ControlMapping
        {
            Kind = Midi.ControlKind.Insert,
            Scope = Midi.ControlScope.Focused,
            Plugin = controls.Plugin.Info.Id,
            Parameter = parameter.Id,
            Name = controls.Plugin.Info.Name + " " + parameter.Name
        }, keep: true);
    }

    /// <summary>
    /// A press while the controller is being laid out takes a link off, and never turns a knob.
    /// </summary>
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
