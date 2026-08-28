using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using JingleBox2.Machines.Ui;
using JingleBox2.Midi;

namespace JingleBox2.Views;

/// <summary>
/// Makes any control on any page something a hardware knob can be pointed at.
/// </summary>
/// <remarks>
/// Pointing was built into the machine panel, which is right for a panel: it is drawn rather
/// than built, so only it knows what element is under the pointer. Everything else on the screen
/// is ordinary controls, and each place that wanted the gesture wrote the same handful of lines
/// again. This is that handful, once, as an attached property: hang a mapping on a control and
/// the pointer resting on it offers that mapping, wherever the control happens to live.
///
/// What is hung on the control is a template rather than the mapping itself, and it is copied
/// before it is offered. <see cref="ControlLink.Handle"/> fills the controller's half into the
/// object it was given and then keeps it, so handing out one shared instance would have every
/// link overwriting the last.
///
/// Tunnelled rather than bubbled. A knob or a fader takes hold of the pointer to be dragged, and
/// a control that has already handled the move would never let it reach here.
/// </remarks>
public static class Pointable
{
    /// <summary>
    /// What this control offers when the pointer rests on it, or nothing to offer nothing.
    /// </summary>
    public static readonly AttachedProperty<ControlMapping?> OffersProperty =
        AvaloniaProperty.RegisterAttached<Control, Control, ControlMapping?>("Offers");

    /// <summary>
    /// Whether the link belongs to the song being worked on rather than to the desk.
    /// </summary>
    /// <remarks>
    /// The same distinction the panels make. An instrument on a track is this song's, so what is
    /// pointed at it travels in the file; a machine on the rack is the machine, and belongs to
    /// the desk. A mixer strip is a track, which only a song has, and so is the song's.
    /// </remarks>
    public static readonly AttachedProperty<bool> InSongProperty =
        AvaloniaProperty.RegisterAttached<Control, Control, bool>("InSong");

    static Pointable()
    {
        OffersProperty.Changed.AddClassHandler<Control>(Hung);
    }

    public static ControlMapping? GetOffers(Control control) => control.GetValue(OffersProperty);

    public static void SetOffers(Control control, ControlMapping? value) =>
        control.SetValue(OffersProperty, value);

    public static bool GetInSong(Control control) => control.GetValue(InSongProperty);

    public static void SetInSong(Control control, bool value) =>
        control.SetValue(InSongProperty, value);

    private static void Hung(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        control.RemoveHandler(InputElement.PointerMovedEvent, Rested);
        control.RemoveHandler(InputElement.PointerEnteredEvent, Rested);

        if (e.NewValue is not ControlMapping) return;

        // Tunnel and bubble both. A knob takes the move to be dragged and a button takes it to
        // light up, so coming down is the only way to be sure of hearing it; entering is a
        // direct event and reaches the control it is about however it is asked for.
        control.AddHandler(InputElement.PointerMovedEvent, Rested,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        control.AddHandler(InputElement.PointerEnteredEvent, Rested,
            RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
    }

    /// <summary>
    /// Puts the glow on what is being offered and takes it off what was.
    /// </summary>
    /// <remarks>
    /// On the control itself, not on anything wrapped round it. A knob and a fader paint
    /// themselves and so paint their own ring, which is the same ring the machine panel draws
    /// and drawn from the same file; a control made of a template wears a class instead and its
    /// style does the rest. Either way it is the thing you pointed at that lights, because that
    /// is the thing the link is about.
    /// </remarks>
    private static void Light(Control? control)
    {
        if (ReferenceEquals(_lit, control)) return;

        if (_lit is { } was)
        {
            LinkGlow.SetLit(was, false);
            was.Classes.Remove(Glow);
        }

        _lit = control;

        if (_lit is { } now)
        {
            LinkGlow.SetLit(now, true);
            now.Classes.Add(Glow);
        }
    }

    /// <summary>
    /// Watches the link so the glow goes out when the offer does.
    /// </summary>
    /// <remarks>
    /// An offer ends in three ways and only one of them is the pointer moving on: the mode is
    /// switched off, and a link is made, which clears the offer so the next wiggle of the same
    /// knob does not make a second one. Left to the pointer alone the glow would sit there on a
    /// control that is no longer being offered anything.
    /// </remarks>
    private static void Watch(ControlLink link)
    {
        if (ReferenceEquals(_watching, link)) return;

        if (_watching is { } was) was.Changed -= Looked;

        _watching = link;
        link.Changed += Looked;
    }

    private static void Looked()
    {
        if (_watching is { IsLinking: true, Offered: not null }) return;

        _last = null;

        Light(null);
    }

    private const string Glow = "offered";

    private static ControlLink? _watching;

    private static Control? _lit;

    /// <summary>
    /// The pointer is on it. Offered, and nothing is handled: the control still works.
    /// </summary>
    /// <remarks>
    /// Working while it is being pointed at is the whole confirmation. Turn the knob on the desk
    /// and the one on the screen moves, which says the link took better than any light could.
    /// </remarks>
    /// <summary>The control last offered, so resting still offers once rather than per move.</summary>
    private static Control? _last;

    private static void Rested(object? sender, PointerEventArgs e)
    {
        if (sender is not Control control) return;
        if (ControlLink.Current is not { IsLinking: true } link) return;
        if (control.GetValue(OffersProperty) is not { } template) return;

        // Offered once per control rather than once per move. What is offered has to be a fresh
        // copy, since a link keeps the object it is given, so the link cannot tell two offers of
        // the same thing apart the way the panel can. It is remembered here instead. An offer
        // that has been taken clears itself, and resting on the same control again offers it
        // again, which is what a hand that has just made a link and wants another expects.
        if (ReferenceEquals(_last, control) && link.Offered is not null) return;

        _last = control;

        Watch(link);

        link.Offer(ControlMapping.Copy(template), control.GetValue(InSongProperty));

        Light(control);
    }
}
